using CostTracker.Application.Contracts;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Interfaces;
using CostTracker.Application.Options;
using CostTracker.Application.Projections;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using CostTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CostTracker.Application.Services;

public class PortfolioManagementService(
    ICostTrackerDbContext dbContext,
    PortfolioProjectionService projectionService,
    TimeProvider timeProvider,
    IOptions<MarketDataOptions> marketDataOptions)
{
    public async Task<InvestmentPortfolioDto> GetPortfolioAsync(CancellationToken cancellationToken)
    {
        var portfolio = await GetOrCreatePortfolioAsync(cancellationToken);
        return projectionService.ToPortfolioDto(portfolio);
    }

    public async Task<InvestmentPortfolioDto> UpdateAllocationAsync(
        UpdateInvestmentAllocationRequest request,
        CancellationToken cancellationToken)
    {
        var portfolio = await GetOrCreatePortfolioAsync(cancellationToken);
        EnsureExpectedVersion(request.ExpectedVersion, portfolio.Version, "portfolio");

        AllocationTargetSet targetSet;
        try
        {
            targetSet = AllocationTargetSet.Create(request.Items.Select(item =>
                new AllocationWeight(InvestmentContractCodes.ParseAssetClass(item.AssetClass), item.Weight)));
        }
        catch (ArgumentException exception)
        {
            throw new DomainValidationException(exception.Message);
        }

        var existingClasses = portfolio.AllocationTargets.Select(item => item.AssetClass).ToHashSet();
        portfolio.ConfigureTargets(targetSet, UtcNow());
        foreach (var newTarget in portfolio.AllocationTargets.Where(item => !existingClasses.Contains(item.AssetClass)))
            dbContext.InvestmentAllocationTargets.Add(newTarget);
        await SaveChangesAsync(cancellationToken);

        return projectionService.ToPortfolioDto(portfolio);
    }

    public async Task<IReadOnlyList<InvestmentInstrumentDto>> GetInstrumentsAsync(
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        var query = dbContext.InvestmentInstruments
            .AsNoTracking()
            .Include(item => item.Transactions)
            .Include(item => item.ManualValuations)
            .AsQueryable();

        if (!includeArchived)
            query = query.Where(item => !item.IsArchived);

        var instruments = await query
            .OrderBy(item => item.AssetClass)
            .ThenBy(item => item.Mic)
            .ThenBy(item => item.Ticker)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return instruments.Select(projectionService.ToInstrumentDto).ToList();
    }

    public async Task<InvestmentInstrumentDetailDto> GetInstrumentAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var instrument = await LoadInstrumentAsync(id, false, cancellationToken);
        return projectionService.ToInstrumentDetailDto(instrument);
    }

    public async Task<InvestmentInstrumentDetailDto> CreateInstrumentAsync(
        CreateInvestmentInstrumentRequest request,
        CancellationToken cancellationToken)
    {
        var portfolio = await GetOrCreatePortfolioAsync(cancellationToken);
        EnsurePortfolioConfigured(portfolio);

        var values = ValidateInstrumentInput(
            request.AssetClass,
            request.Kind,
            request.Name,
            request.Identifier,
            request.Ticker,
            request.Mic,
            request.Isin,
            request.NativeCurrency,
            request.ValuationMode,
            request.AllocationScore,
            request.QuantityStep);

        await EnsureUniqueIdentityAsync(portfolio.Id, values.IdentityKey, null, cancellationToken);

        var now = UtcNow();
        var instrument = new InvestmentInstrument
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolio.Id,
            AssetClass = values.AssetClass,
            Kind = values.Kind,
            Name = values.Name,
            PublicIdentifier = values.Identifier,
            Ticker = values.Ticker,
            Mic = values.Mic,
            Isin = values.Isin,
            IdentityKey = values.IdentityKey,
            NativeCurrency = values.NativeCurrency,
            ValuationMode = values.ValuationMode,
            AllocationScore = values.AllocationScore,
            QuantityStep = values.QuantityStep,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (request.OpeningTransaction is not null)
        {
            if (InvestmentContractCodes.ParseTransactionType(request.OpeningTransaction.TransactionType) != InvestmentTransactionType.OpeningBalance)
                throw new DomainValidationException("openingTransaction.transactionType must be OPENING_BALANCE.");

            instrument.Transactions.Add(BuildTransaction(instrument, request.OpeningTransaction, now));
        }

        if (request.ManualValuation is not null)
            instrument.ManualValuations.Add(BuildManualValuation(instrument, request.ManualValuation, now));

        EnsureNonNegativeQuantityHistory(instrument);
        await dbContext.InvestmentInstruments.AddAsync(instrument, cancellationToken);
        portfolio.Touch(now);

        await SaveChangesAsync(cancellationToken);
        return projectionService.ToInstrumentDetailDto(instrument);
    }

    public async Task<InvestmentInstrumentDetailDto> UpdateInstrumentAsync(
        Guid id,
        UpdateInvestmentInstrumentRequest request,
        CancellationToken cancellationToken)
    {
        var instrument = await LoadInstrumentAsync(id, true, cancellationToken);
        EnsureNotArchived(instrument);
        EnsureExpectedVersion(request.ExpectedVersion, instrument.Version, "instrument");

        var values = ValidateInstrumentInput(
            request.AssetClass,
            request.Kind,
            request.Name,
            request.Identifier,
            request.Ticker,
            request.Mic,
            request.Isin,
            request.NativeCurrency,
            request.ValuationMode,
            request.AllocationScore,
            request.QuantityStep);

        if ((instrument.Transactions.Count > 0 || instrument.ManualValuations.Count > 0) &&
            (instrument.ValuationMode != values.ValuationMode || instrument.NativeCurrency != values.NativeCurrency))
        {
            throw new ConflictException("valuationMode and nativeCurrency cannot change after history has been recorded.");
        }

        await EnsureUniqueIdentityAsync(instrument.PortfolioId, values.IdentityKey, instrument.Id, cancellationToken);

        instrument.AssetClass = values.AssetClass;
        instrument.Kind = values.Kind;
        instrument.Name = values.Name;
        instrument.PublicIdentifier = values.Identifier;
        instrument.Ticker = values.Ticker;
        instrument.Mic = values.Mic;
        instrument.Isin = values.Isin;
        instrument.IdentityKey = values.IdentityKey;
        instrument.NativeCurrency = values.NativeCurrency;
        instrument.ValuationMode = values.ValuationMode;
        instrument.AllocationScore = values.AllocationScore;
        instrument.QuantityStep = values.QuantityStep;

        var now = UtcNow();
        instrument.Touch(now);
        instrument.Portfolio.Touch(now);

        await SaveChangesAsync(cancellationToken);
        return projectionService.ToInstrumentDetailDto(instrument);
    }

    public async Task<InvestmentInstrumentDetailDto> ArchiveInstrumentAsync(
        Guid id,
        long? expectedVersion,
        CancellationToken cancellationToken)
    {
        var instrument = await LoadInstrumentAsync(id, true, cancellationToken);
        EnsureExpectedVersion(expectedVersion, instrument.Version, "instrument");

        if (!instrument.IsArchived)
        {
            var now = UtcNow();
            instrument.Archive(now);
            instrument.Portfolio.Touch(now);
            await SaveChangesAsync(cancellationToken);
        }

        return projectionService.ToInstrumentDetailDto(instrument);
    }

    public async Task<IReadOnlyList<InvestmentTransactionDto>> GetTransactionsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        _ = await dbContext.InvestmentInstruments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == instrumentId, cancellationToken)
            ?? throw new NotFoundException("Investment instrument not found.");

        var transactions = await dbContext.InvestmentTransactions
            .AsNoTracking()
            .Where(item => item.InstrumentId == instrumentId)
            .OrderByDescending(item => item.TransactionDate)
            .ThenByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        return transactions.Select(projectionService.ToTransactionDto).ToList();
    }

    public async Task<InvestmentInstrumentDetailDto> CreateTransactionAsync(
        Guid instrumentId,
        CreateInvestmentTransactionRequest request,
        CancellationToken cancellationToken)
    {
        ValidateIdempotencyKey(request.IdempotencyKey);
        var instrument = await LoadInstrumentAsync(instrumentId, true, cancellationToken);

        var existing = instrument.Transactions.SingleOrDefault(item => item.IdempotencyKey == request.IdempotencyKey.Trim());
        if (existing is not null)
            return projectionService.ToInstrumentDetailDto(instrument);

        EnsureNotArchived(instrument);
        var now = UtcNow();
        var transaction = BuildTransaction(instrument, request, now);
        instrument.Transactions.Add(transaction);
        dbContext.InvestmentTransactions.Add(transaction);
        EnsureNonNegativeQuantityHistory(instrument);
        instrument.Touch(now);
        instrument.Portfolio.Touch(now);

        await SaveChangesAsync(cancellationToken);
        return projectionService.ToInstrumentDetailDto(instrument);
    }

    public async Task<IReadOnlyList<ManualValuationDto>> GetManualValuationsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        _ = await dbContext.InvestmentInstruments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == instrumentId, cancellationToken)
            ?? throw new NotFoundException("Investment instrument not found.");

        var valuations = await dbContext.ManualValuations
            .AsNoTracking()
            .Where(item => item.InstrumentId == instrumentId)
            .OrderByDescending(item => item.AsOf)
            .ThenByDescending(item => item.RecordedAt)
            .ToListAsync(cancellationToken);

        return valuations.Select(projectionService.ToManualValuationDto).ToList();
    }

    public async Task<InvestmentInstrumentDetailDto> CreateManualValuationAsync(
        Guid instrumentId,
        CreateManualValuationRequest request,
        CancellationToken cancellationToken)
    {
        ValidateIdempotencyKey(request.IdempotencyKey);
        var instrument = await LoadInstrumentAsync(instrumentId, true, cancellationToken);

        var existing = instrument.ManualValuations.SingleOrDefault(item => item.IdempotencyKey == request.IdempotencyKey.Trim());
        if (existing is not null)
            return projectionService.ToInstrumentDetailDto(instrument);

        EnsureNotArchived(instrument);
        var now = UtcNow();
        var valuation = BuildManualValuation(instrument, request, now);
        instrument.ManualValuations.Add(valuation);
        dbContext.ManualValuations.Add(valuation);
        instrument.Touch(now);
        instrument.Portfolio.Touch(now);

        await SaveChangesAsync(cancellationToken);
        return projectionService.ToInstrumentDetailDto(instrument);
    }

    private async Task<InvestmentPortfolio> GetOrCreatePortfolioAsync(CancellationToken cancellationToken)
    {
        var existing = await dbContext.InvestmentPortfolios
            .Include(item => item.AllocationTargets)
            .SingleOrDefaultAsync(cancellationToken);

        if (existing is not null)
            return existing;

        var portfolio = InvestmentPortfolio.Create(UtcNow());
        await dbContext.InvestmentPortfolios.AddAsync(portfolio, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return portfolio;
    }

    private async Task<InvestmentInstrument> LoadInstrumentAsync(
        Guid id,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = dbContext.InvestmentInstruments
            .Include(item => item.Portfolio)
            .Include(item => item.Transactions)
            .Include(item => item.ManualValuations)
            .AsQueryable();

        if (!tracking)
            query = query.AsNoTracking();

        return await query.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
               ?? throw new NotFoundException("Investment instrument not found.");
    }

    private InvestmentTransaction BuildTransaction(
        InvestmentInstrument instrument,
        CreateInvestmentTransactionRequest request,
        DateTimeOffset now)
    {
        ValidateIdempotencyKey(request.IdempotencyKey);
        if (instrument.Transactions.Any(item => item.IdempotencyKey == request.IdempotencyKey.Trim()))
            throw new ConflictException("A transaction with this idempotencyKey already exists.");

        var type = InvestmentContractCodes.ParseTransactionType(request.TransactionType);
        if (request.TransactionDate == default)
            throw new DomainValidationException("transactionDate is required.");
        if (request.TransactionDate > LocalDate(now))
            throw new DomainValidationException("transactionDate cannot be in the future.");
        if (request.FeeAmount < 0m)
            throw new DomainValidationException("feeAmount must be greater than or equal to zero.");
        if (request.CurrencyPerEurRate is <= 0m)
            throw new DomainValidationException("currencyPerEurRate must be greater than zero when provided.");
        if (request.Notes?.Length > 512)
            throw new DomainValidationException("notes cannot exceed 512 characters.");

        ValidateTransactionShape(instrument, type, request);

        var currency = instrument.NativeCurrency;
        var carriesMonetaryValue = request.UnitPrice.HasValue || request.Amount.HasValue || request.FeeAmount > 0m;
        if (carriesMonetaryValue || !string.IsNullOrWhiteSpace(request.Currency))
        {
            currency = ParseCurrency(string.IsNullOrWhiteSpace(request.Currency)
                ? instrument.NativeCurrency.Value
                : request.Currency!);
            if (currency != instrument.NativeCurrency)
                throw new DomainValidationException("currency must match the instrument nativeCurrency.");
        }

        var rate = request.CurrencyPerEurRate;
        if (currency.Value == "EUR")
        {
            if (rate.HasValue && rate.Value != 1m)
                throw new DomainValidationException("currencyPerEurRate must be 1 for EUR transactions.");
            rate = 1m;
        }

        return new InvestmentTransaction
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrument.Id,
            Type = type,
            TransactionDate = request.TransactionDate,
            Quantity = RoundNullable(request.Quantity, 12),
            UnitPrice = RoundNullable(request.UnitPrice, 8),
            Amount = RoundNullable(request.Amount, 8),
            Currency = currency,
            FeeAmount = decimal.Round(request.FeeAmount, 8, MidpointRounding.ToEven),
            CurrencyPerEurRate = RoundNullable(rate, 10),
            Notes = TrimOptional(request.Notes),
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CreatedAt = now
        };
    }

    private ManualValuation BuildManualValuation(
        InvestmentInstrument instrument,
        CreateManualValuationRequest request,
        DateTimeOffset now)
    {
        ValidateIdempotencyKey(request.IdempotencyKey);
        if (instrument.ValuationMode != ValuationMode.Manual)
            throw new DomainValidationException("Manual valuations are only allowed for MANUAL instruments.");
        if (request.Amount < 0m)
            throw new DomainValidationException("amount must be greater than or equal to zero.");
        if (request.AsOf == default)
            throw new DomainValidationException("asOf is required.");
        if (request.AsOf > LocalDate(now))
            throw new DomainValidationException("asOf cannot be in the future.");

        var currency = ParseCurrency(string.IsNullOrWhiteSpace(request.Currency)
            ? instrument.NativeCurrency.Value
            : request.Currency);
        if (currency != instrument.NativeCurrency)
            throw new DomainValidationException("currency must match the instrument nativeCurrency.");

        return new ManualValuation
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrument.Id,
            Amount = decimal.Round(request.Amount, 8, MidpointRounding.ToEven),
            Currency = currency,
            AsOf = request.AsOf,
            RecordedAt = now,
            IdempotencyKey = request.IdempotencyKey.Trim()
        };
    }

    private static void ValidateTransactionShape(
        InvestmentInstrument instrument,
        InvestmentTransactionType type,
        CreateInvestmentTransactionRequest request)
    {
        if (instrument.ValuationMode == ValuationMode.MarketQuote)
        {
            if (type is InvestmentTransactionType.Deposit or InvestmentTransactionType.Withdrawal)
                throw new DomainValidationException("MARKET_QUOTE instruments accept OPENING_BALANCE, BUY, SELL or ADJUSTMENT transactions.");

            if (request.Quantity is null or 0m)
                throw new DomainValidationException("quantity must be non-zero for a market transaction.");
            if (type is not InvestmentTransactionType.Adjustment && request.Quantity <= 0m)
                throw new DomainValidationException("quantity must be greater than zero for this transactionType.");
            if (type is InvestmentTransactionType.Buy or InvestmentTransactionType.Sell && request.UnitPrice is null or <= 0m)
                throw new DomainValidationException("unitPrice must be greater than zero for BUY and SELL.");
            if (type == InvestmentTransactionType.OpeningBalance && request.UnitPrice is <= 0m)
                throw new DomainValidationException("unitPrice must be greater than zero when provided.");
            if (type == InvestmentTransactionType.Adjustment && request.UnitPrice is <= 0m)
                throw new DomainValidationException("unitPrice must be greater than zero when provided.");
            if (request.Amount.HasValue)
                throw new DomainValidationException("amount is not used for MARKET_QUOTE transactions.");
        }
        else
        {
            if (type is InvestmentTransactionType.Buy or InvestmentTransactionType.Sell)
                throw new DomainValidationException("MANUAL instruments accept OPENING_BALANCE, DEPOSIT, WITHDRAWAL or ADJUSTMENT transactions.");
            if (request.Amount is null or 0m)
                throw new DomainValidationException("amount must be non-zero for a manual instrument transaction.");
            if (type is not InvestmentTransactionType.Adjustment && request.Amount <= 0m)
                throw new DomainValidationException("amount must be greater than zero for this transactionType.");
            if (request.Quantity.HasValue || request.UnitPrice.HasValue)
                throw new DomainValidationException("quantity and unitPrice are not used for MANUAL instruments.");
        }

        if (type == InvestmentTransactionType.OpeningBalance &&
            instrument.Transactions.Any(item => item.Type == InvestmentTransactionType.OpeningBalance))
        {
            throw new ConflictException("An OPENING_BALANCE transaction already exists for this instrument.");
        }
    }

    private static InstrumentInput ValidateInstrumentInput(
        string assetClassValue,
        string kindValue,
        string nameValue,
        string? identifierValue,
        string? tickerValue,
        string? micValue,
        string? isinValue,
        string nativeCurrencyValue,
        string valuationModeValue,
        int allocationScore,
        decimal? quantityStepValue)
    {
        var assetClass = InvestmentContractCodes.ParseAssetClass(assetClassValue);
        if (assetClass == AssetClass.Cryptocurrencies)
        {
            throw new DomainValidationException(
                "CRYPTOCURRENCIES is allocation-target-only and cannot contain instruments.");
        }

        var kind = InvestmentContractCodes.ParseInstrumentKind(kindValue);
        var valuationMode = InvestmentContractCodes.ParseValuationMode(valuationModeValue);
        var currency = ParseCurrency(nativeCurrencyValue);
        var name = nameValue?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 160)
            throw new DomainValidationException("name is required and cannot exceed 160 characters.");
        if (allocationScore < 0)
            throw new DomainValidationException("allocationScore must be greater than or equal to zero.");

        var isMarketClass = assetClass is AssetClass.Stocks or AssetClass.Reits;
        if (isMarketClass != (valuationMode == ValuationMode.MarketQuote))
            throw new DomainValidationException("STOCKS and REITS require MARKET_QUOTE; fixed-income classes require MANUAL.");

        var kindMatchesClass = assetClass switch
        {
            AssetClass.Stocks => kind is InstrumentKind.Stock or InstrumentKind.Etf or InstrumentKind.Adr,
            AssetClass.Reits => kind == InstrumentKind.Reit,
            AssetClass.BrazilFixedIncome or AssetClass.InternationalFixedIncome => kind is InstrumentKind.Bond or InstrumentKind.Account,
            _ => false
        };
        if (!kindMatchesClass)
            throw new DomainValidationException("kind is not valid for the selected assetClass.");
        if (!isMarketClass && allocationScore != 0)
            throw new DomainValidationException("allocationScore is only used by STOCKS and REITS and must be zero for fixed income.");

        var identifier = NormalizeOptional(identifierValue, 128, "identifier");
        var ticker = NormalizeUpperOptional(tickerValue, 32, "ticker");
        var mic = NormalizeUpperOptional(micValue, 16, "mic");
        var isin = NormalizeUpperOptional(isinValue, 16, "isin");

        if (isin is not null && (isin.Length != 12 || !isin.All(char.IsLetterOrDigit)))
            throw new DomainValidationException("isin must contain exactly 12 letters or digits.");
        if (isMarketClass && identifier is null && ticker is null && isin is null)
            throw new DomainValidationException("A market instrument requires identifier, ticker or isin.");

        decimal? quantityStep;
        if (isMarketClass)
        {
            quantityStep = decimal.Round(quantityStepValue ?? 0.000001m, 12, MidpointRounding.ToEven);
            if (quantityStep <= 0m)
                throw new DomainValidationException("quantityStep must be greater than zero.");
        }
        else
        {
            if (quantityStepValue.HasValue)
                throw new DomainValidationException("quantityStep is only used by MARKET_QUOTE instruments.");
            quantityStep = null;
        }

        var identityKey = BuildIdentityKey(assetClass, name, identifier, ticker, mic, isin, currency);
        return new InstrumentInput(
            assetClass,
            kind,
            name,
            identifier,
            ticker,
            mic,
            isin,
            identityKey,
            currency,
            valuationMode,
            allocationScore,
            quantityStep);
    }

    private async Task EnsureUniqueIdentityAsync(
        Guid portfolioId,
        string identityKey,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        var duplicate = await dbContext.InvestmentInstruments.AnyAsync(
            item => item.PortfolioId == portfolioId &&
                    item.IdentityKey == identityKey &&
                    !item.IsArchived &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

        if (duplicate)
            throw new ConflictException("An active instrument with the same identity already exists.");
    }

    private static string BuildIdentityKey(
        AssetClass assetClass,
        string name,
        string? identifier,
        string? ticker,
        string? mic,
        string? isin,
        CurrencyCode currency)
    {
        if (isin is not null)
            return $"ISIN:{isin}";
        if (ticker is not null)
            return $"TICKER:{ticker}@{mic ?? "UNSPECIFIED"}";
        if (identifier is not null)
            return $"IDENTIFIER:{identifier.ToUpperInvariant()}";

        return $"MANUAL:{assetClass}:{currency.Value}:{name.ToUpperInvariant()}";
    }

    private static void EnsurePortfolioConfigured(InvestmentPortfolio portfolio)
    {
        if (portfolio.AllocationTargets.Count != Enum.GetValues<AssetClass>().Length ||
            portfolio.AllocationTargets.Select(item => item.AssetClass).Distinct().Count() != portfolio.AllocationTargets.Count ||
            portfolio.AllocationTargets.Any(item => item.Weight * 100m != decimal.Truncate(item.Weight * 100m)) ||
            portfolio.AllocationTargets.Sum(item => item.Weight) != 1m)
        {
            throw new ConflictException("Configure all five allocation targets before adding instruments.");
        }
    }

    private static void EnsureNotArchived(InvestmentInstrument instrument)
    {
        if (instrument.IsArchived)
            throw new ConflictException("Archived instruments cannot be changed.");
    }

    private static void EnsureExpectedVersion(long? expectedVersion, long currentVersion, string resourceName)
    {
        if (expectedVersion.HasValue && expectedVersion.Value != currentVersion)
            throw new ConflictException($"The {resourceName} changed. Reload it and retry.");
    }

    private static void EnsureNonNegativeQuantityHistory(InvestmentInstrument instrument)
    {
        if (instrument.ValuationMode != ValuationMode.MarketQuote)
            return;

        var runningQuantity = 0m;
        foreach (var transaction in instrument.Transactions
                     .OrderBy(item => item.TransactionDate)
                     .ThenBy(item => item.CreatedAt))
        {
            runningQuantity += transaction.Type switch
            {
                InvestmentTransactionType.OpeningBalance or InvestmentTransactionType.Buy => transaction.Quantity ?? 0m,
                InvestmentTransactionType.Sell => -(transaction.Quantity ?? 0m),
                InvestmentTransactionType.Adjustment => transaction.Quantity ?? 0m,
                _ => 0m
            };

            if (runningQuantity < 0m)
                throw new ConflictException("The transaction would make the historical quantity negative.");
        }
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The investment portfolio changed. Reload it and retry.");
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("The investment change conflicts with existing data.");
        }
    }

    private DateTimeOffset UtcNow() => timeProvider.GetUtcNow();

    private DateOnly LocalDate(DateTimeOffset instant)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(marketDataOptions.Value.RefreshTimeZone);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(instant.UtcDateTime);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(instant.UtcDateTime);
        }
    }

    private static CurrencyCode ParseCurrency(string? value)
    {
        try
        {
            return new CurrencyCode(value ?? string.Empty);
        }
        catch (ArgumentException exception)
        {
            throw new DomainValidationException(exception.Message);
        }
    }

    private static void ValidateIdempotencyKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 128)
            throw new DomainValidationException("idempotencyKey is required and cannot exceed 128 characters.");
    }

    private static decimal? RoundNullable(decimal? value, int decimals)
        => value.HasValue ? decimal.Round(value.Value, decimals, MidpointRounding.ToEven) : null;

    private static string? TrimOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = TrimOptional(value);
        if (normalized?.Length > maxLength)
            throw new DomainValidationException($"{fieldName} cannot exceed {maxLength} characters.");
        return normalized;
    }

    private static string? NormalizeUpperOptional(string? value, int maxLength, string fieldName)
        => NormalizeOptional(value, maxLength, fieldName)?.ToUpperInvariant();

    private sealed record InstrumentInput(
        AssetClass AssetClass,
        InstrumentKind Kind,
        string Name,
        string? Identifier,
        string? Ticker,
        string? Mic,
        string? Isin,
        string IdentityKey,
        CurrencyCode NativeCurrency,
        ValuationMode ValuationMode,
        int AllocationScore,
        decimal? QuantityStep);
}
