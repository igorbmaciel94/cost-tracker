using CostTracker.Application.Contracts;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;

namespace CostTracker.Application.Projections;

public class PortfolioProjectionService
{
    public InvestmentPortfolioDto ToPortfolioDto(InvestmentPortfolio portfolio)
    {
        var targets = portfolio.AllocationTargets
            .OrderBy(target => target.AssetClass)
            .Select(target => new AllocationTargetDto(target.AssetClass.ToCode(), target.Weight))
            .ToList();

        return new InvestmentPortfolioDto(
            portfolio.Id,
            portfolio.BaseCurrency.Value,
            portfolio.Version,
            portfolio.UpdatedAt,
            targets.Count == Enum.GetValues<AssetClass>().Length &&
            targets.Select(target => target.AssetClass).Distinct(StringComparer.Ordinal).Count() == targets.Count &&
            targets.All(target => target.Weight * 100m == decimal.Truncate(target.Weight * 100m)) &&
            targets.Sum(target => target.Weight) == 1m,
            targets);
    }

    public InvestmentInstrumentDto ToInstrumentDto(InvestmentInstrument instrument)
    {
        var position = CalculatePosition(instrument);
        var latestManualValuation = instrument.ManualValuations
            .OrderByDescending(item => item.AsOf)
            .ThenByDescending(item => item.RecordedAt)
            .Select(ToManualValuationDto)
            .FirstOrDefault();

        return new InvestmentInstrumentDto(
            instrument.Id,
            instrument.PortfolioId,
            instrument.AssetClass.ToCode(),
            instrument.Kind.ToCode(),
            instrument.Name,
            instrument.PublicIdentifier,
            instrument.Ticker,
            instrument.Mic,
            instrument.Isin,
            instrument.NativeCurrency.Value,
            instrument.ValuationMode.ToCode(),
            instrument.AllocationScore,
            instrument.QuantityStep,
            instrument.IsArchived,
            instrument.Version,
            instrument.CreatedAt,
            instrument.UpdatedAt,
            position,
            latestManualValuation);
    }

    public InvestmentInstrumentDetailDto ToInstrumentDetailDto(InvestmentInstrument instrument)
        => new(
            ToInstrumentDto(instrument),
            instrument.Transactions
                .OrderByDescending(item => item.TransactionDate)
                .ThenByDescending(item => item.CreatedAt)
                .Select(ToTransactionDto)
                .ToList(),
            instrument.ManualValuations
                .OrderByDescending(item => item.AsOf)
                .ThenByDescending(item => item.RecordedAt)
                .Select(ToManualValuationDto)
                .ToList());

    public InvestmentTransactionDto ToTransactionDto(InvestmentTransaction transaction)
        => new(
            transaction.Id,
            transaction.InstrumentId,
            transaction.Type.ToCode(),
            transaction.TransactionDate,
            transaction.Quantity,
            transaction.UnitPrice,
            transaction.Amount,
            transaction.Currency.Value,
            transaction.FeeAmount,
            transaction.CurrencyPerEurRate,
            transaction.Notes,
            transaction.IdempotencyKey,
            transaction.CreatedAt);

    public ManualValuationDto ToManualValuationDto(ManualValuation valuation)
        => new(
            valuation.Id,
            valuation.InstrumentId,
            valuation.Amount,
            valuation.Currency.Value,
            valuation.AsOf,
            valuation.RecordedAt,
            valuation.IdempotencyKey);

    public InvestmentPositionDto CalculatePosition(InvestmentInstrument instrument)
    {
        var quantity = 0m;
        var costBasis = 0m;
        var isCostKnown = true;
        decimal? netInvestedNative = 0m;
        decimal? netInvestedEur = 0m;

        foreach (var transaction in instrument.Transactions
                     .OrderBy(item => item.TransactionDate)
                     .ThenBy(item => item.CreatedAt))
        {
            if (instrument.ValuationMode == ValuationMode.MarketQuote)
            {
                ApplyMarketTransaction(
                    transaction,
                    ref quantity,
                    ref costBasis,
                    ref isCostKnown,
                    ref netInvestedNative,
                    ref netInvestedEur);
            }
            else
            {
                ApplyManualCashFlow(transaction, ref netInvestedNative, ref netInvestedEur);
            }
        }

        if (instrument.ValuationMode == ValuationMode.Manual &&
            !instrument.Transactions.Any(item => item.Type == InvestmentTransactionType.OpeningBalance))
        {
            // A balance snapshot says what the asset is worth, not how much was contributed.
            netInvestedNative = null;
            netInvestedEur = null;
        }

        if (quantity == 0m)
        {
            costBasis = 0m;
            isCostKnown = true;
        }

        var latestValuation = instrument.ManualValuations
            .OrderByDescending(item => item.AsOf)
            .ThenByDescending(item => item.RecordedAt)
            .FirstOrDefault();

        decimal? currentManualValue = null;
        var isManualValueEstimated = false;
        if (latestValuation is not null)
        {
            currentManualValue = latestValuation.Amount;
            foreach (var transaction in instrument.Transactions.Where(item => item.TransactionDate > latestValuation.AsOf))
            {
                var effect = transaction.Type switch
                {
                    InvestmentTransactionType.Deposit => transaction.Amount,
                    InvestmentTransactionType.Withdrawal => -transaction.Amount,
                    InvestmentTransactionType.Adjustment => transaction.Amount,
                    _ => null
                };

                if (effect.HasValue)
                {
                    currentManualValue += effect.Value;
                    isManualValueEstimated = true;
                }
            }
        }

        return new InvestmentPositionDto(
            quantity,
            isCostKnown,
            isCostKnown ? costBasis : null,
            isCostKnown && quantity > 0m ? costBasis / quantity : null,
            netInvestedNative,
            netInvestedEur,
            currentManualValue,
            latestValuation?.AsOf,
            isManualValueEstimated);
    }

    private static void ApplyMarketTransaction(
        InvestmentTransaction transaction,
        ref decimal quantity,
        ref decimal costBasis,
        ref bool isCostKnown,
        ref decimal? netInvestedNative,
        ref decimal? netInvestedEur)
    {
        var transactionQuantity = transaction.Quantity ?? 0m;
        var isIncrease = transaction.Type is InvestmentTransactionType.OpeningBalance or InvestmentTransactionType.Buy ||
                         transaction.Type == InvestmentTransactionType.Adjustment && transactionQuantity > 0m;
        var isDecrease = transaction.Type == InvestmentTransactionType.Sell ||
                         transaction.Type == InvestmentTransactionType.Adjustment && transactionQuantity < 0m;

        if (isIncrease)
        {
            quantity += transactionQuantity;
            if (transaction.UnitPrice.HasValue && isCostKnown)
                costBasis += transactionQuantity * transaction.UnitPrice.Value + transaction.FeeAmount;
            else
                isCostKnown = false;
        }
        else if (isDecrease)
        {
            var decrease = decimal.Abs(transactionQuantity);
            if (isCostKnown && quantity > 0m)
                costBasis -= costBasis / quantity * decrease;

            quantity -= decrease;
        }

        if (transaction.Type is not (InvestmentTransactionType.OpeningBalance or InvestmentTransactionType.Buy or InvestmentTransactionType.Sell))
            return;

        if (!transaction.UnitPrice.HasValue || !transaction.Quantity.HasValue)
        {
            netInvestedNative = null;
            netInvestedEur = null;
            return;
        }

        var gross = transaction.Quantity.Value * transaction.UnitPrice.Value;
        var nativeEffect = transaction.Type == InvestmentTransactionType.Sell
            ? -(gross - transaction.FeeAmount)
            : gross + transaction.FeeAmount;

        if (netInvestedNative.HasValue)
            netInvestedNative += nativeEffect;

        AddEurEffect(transaction, nativeEffect, ref netInvestedEur);
    }

    private static void ApplyManualCashFlow(
        InvestmentTransaction transaction,
        ref decimal? netInvestedNative,
        ref decimal? netInvestedEur)
    {
        if (transaction.Type is not (InvestmentTransactionType.OpeningBalance or InvestmentTransactionType.Deposit or InvestmentTransactionType.Withdrawal))
            return;

        if (!transaction.Amount.HasValue)
        {
            netInvestedNative = null;
            netInvestedEur = null;
            return;
        }

        var nativeEffect = transaction.Type == InvestmentTransactionType.Withdrawal
            ? -transaction.Amount.Value
            : transaction.Amount.Value;

        if (netInvestedNative.HasValue)
            netInvestedNative += nativeEffect;

        AddEurEffect(transaction, nativeEffect, ref netInvestedEur);
    }

    private static void AddEurEffect(
        InvestmentTransaction transaction,
        decimal nativeEffect,
        ref decimal? netInvestedEur)
    {
        if (!netInvestedEur.HasValue)
            return;

        var rate = transaction.Currency.Value == "EUR" ? 1m : transaction.CurrencyPerEurRate;
        if (!rate.HasValue || rate.Value <= 0m)
        {
            netInvestedEur = null;
            return;
        }

        netInvestedEur += nativeEffect / rate.Value;
    }
}
