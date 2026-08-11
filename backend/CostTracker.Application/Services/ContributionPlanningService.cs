using System.Globalization;
using CostTracker.Application.Contracts;
using CostTracker.Application.Exceptions;
using CostTracker.Application.Investments.Contributions;
using CostTracker.Application.Options;
using CostTracker.Application.Projections;
using CostTracker.Domain.Entities;
using CostTracker.Domain.Enums;
using CostTracker.Domain.Investments.Calculations;
using CostTracker.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersistedContributionPlan = CostTracker.Domain.Entities.ContributionPlan;
using PersistedContributionPlanLine = CostTracker.Domain.Entities.ContributionPlanLine;

namespace CostTracker.Application.Services;

public sealed class ContributionPlanningService(
    IContributionPlanningDbContext dbContext,
    PortfolioProjectionService projectionService,
    TimeProvider timeProvider,
    IOptions<MarketDataOptions> marketDataOptions)
{
    private static readonly AssetClass[] StableClassOrder =
    [
        AssetClass.Stocks,
        AssetClass.Reits,
        AssetClass.BrazilFixedIncome,
        AssetClass.InternationalFixedIncome,
        AssetClass.Cryptocurrencies
    ];

    private readonly MarketDataOptions _options = marketDataOptions.Value;

    public async Task<IReadOnlyList<ContributionPlanDto>> GetPlansAsync(CancellationToken cancellationToken)
    {
        await ExpireDraftPlansAsync(cancellationToken);

        var plans = await dbContext.ContributionPlans
            .AsNoTracking()
            .Include(item => item.Lines)
            .OrderByDescending(item => item.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return plans.Select(ToDto).ToList();
    }

    public async Task<ContributionPlanDto> GetPlanAsync(Guid id, CancellationToken cancellationToken)
    {
        await ExpireDraftPlansAsync(cancellationToken);

        var plan = await dbContext.ContributionPlans
            .AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Contribution plan not found.");

        return ToDto(plan);
    }

    public async Task<ContributionPlanDto> CreatePlanAsync(
        CreateContributionPlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ContributionAmountEur <= 0m)
            throw new DomainValidationException("contributionAmountEur must be greater than zero.");

        var contributionAmount = decimal.Round(request.ContributionAmountEur, 8, MidpointRounding.ToEven);
        if (contributionAmount <= 0m)
            throw new DomainValidationException("contributionAmountEur is too small at the supported precision.");

        var now = UtcNow();
        var today = LocalDate(now);
        var portfolio = await dbContext.InvestmentPortfolios
            .AsNoTracking()
            .Include(item => item.AllocationTargets)
            .Include(item => item.Instruments)
                .ThenInclude(item => item.Transactions.Where(transaction => transaction.TransactionDate <= today))
            .Include(item => item.Instruments)
                .ThenInclude(item => item.ManualValuations.Where(valuation => valuation.AsOf <= today))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ConflictException("Configure the investment portfolio before creating a contribution plan.");

        EnsurePortfolioConfigured(portfolio);

        var instruments = portfolio.Instruments
            .Where(item => !item.IsArchived)
            .OrderBy(item => item.AssetClass)
            .ThenBy(item => item.Mic)
            .ThenBy(item => item.Ticker)
            .ThenBy(item => item.Name)
            .ToArray();
        if (instruments.Length == 0)
            throw new ConflictException("Register at least one active investment instrument before creating a contribution plan.");
        var instrumentIds = instruments.Select(item => item.Id).ToArray();

        var quoteSnapshots = instrumentIds.Length == 0
            ? []
            : await dbContext.MarketQuoteSnapshots
                .AsNoTracking()
                .Where(item => instrumentIds.Contains(item.InstrumentId) && item.AsOf <= today)
                .ToListAsync(cancellationToken);
        var fxSnapshots = await dbContext.FxRateSnapshots
            .AsNoTracking()
            .Where(item => item.AsOf <= today)
            .ToListAsync(cancellationToken);

        var latestQuotes = quoteSnapshots
            .GroupBy(item => item.InstrumentId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.AsOf)
                    .ThenByDescending(item => item.FetchedAt)
                    .First());
        var latestFx = fxSnapshots
            .Where(item => item.BaseCurrency.Value == "EUR")
            .GroupBy(item => item.QuoteCurrency.Value, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.AsOf)
                    .ThenByDescending(item => item.FetchedAt)
                    .First(),
                StringComparer.Ordinal);

        var valuedInstruments = instruments
            .Select(instrument => ValueInstrument(instrument, latestQuotes, latestFx, today))
            .ToDictionary(item => item.Instrument.Id);

        var worstFreshness = valuedInstruments.Count == 0
            ? ContributionDataFreshness.Fresh
            : valuedInstruments.Values.Select(item => item.Freshness).MaxBy(FreshnessRank);

        if (worstFreshness is ContributionDataFreshness.Missing)
            throw new ConflictException("Market data or a manual valuation is missing. Refresh or record it before creating a plan.");
        if (worstFreshness is ContributionDataFreshness.Blocked)
            throw new ConflictException("Investment valuation data is too old. Refresh it before creating a plan.");
        if (worstFreshness is ContributionDataFreshness.Stale && !request.AllowStaleData)
            throw new ConflictException("Investment valuation data is stale. Refresh it or explicitly allow stale data.");

        var targets = portfolio.AllocationTargets.ToDictionary(item => item.AssetClass, item => item.Weight);
        var classSnapshots = StableClassOrder.Select(assetClass =>
        {
            var classValues = valuedInstruments.Values
                .Where(item => item.Instrument.AssetClass == assetClass)
                .ToArray();
            var engineInstruments = IsMarketClass(assetClass)
                ? classValues.Select(item => new InstrumentSnapshot(
                    item.Instrument.Id.ToString("D", CultureInfo.InvariantCulture),
                    item.Instrument.Mic ?? "UNSPECIFIED",
                    item.Instrument.Ticker ?? item.Instrument.PublicIdentifier ?? item.Instrument.Isin ?? item.Instrument.Name,
                    item.ValueEur,
                    item.Quote?.Price ?? 1m,
                    item.NativeCurrencyPerEur)).ToArray()
                : [];

            return new PortfolioClassSnapshot(
                assetClass,
                classValues.Sum(item => item.ValueEur),
                engineInstruments);
        }).ToArray();

        var portfolioVersion = portfolio.Version.ToString(CultureInfo.InvariantCulture);
        var calculated = ContributionAllocator.Calculate(
            new PortfolioSnapshot(portfolioVersion, classSnapshots),
            new ContributionAmount(contributionAmount),
            new AllocationPolicy(
                portfolioVersion,
                StableClassOrder.Select(assetClass => new ClassAllocationTarget(assetClass, targets[assetClass])).ToArray(),
                instruments
                    .Where(item => IsMarketClass(item.AssetClass))
                    .Select(item => new InstrumentAllocationScore(
                        item.Id.ToString("D", CultureInfo.InvariantCulture),
                        item.AllocationScore))
                    .ToArray()),
            new ExecutionConstraints(
                0.000001m,
                instruments
                    .Where(item => IsMarketClass(item.AssetClass) && item.QuantityStep.HasValue)
                    .Select(item => new InstrumentExecutionConstraint(
                        item.Id.ToString("D", CultureInfo.InvariantCulture),
                        item.QuantityStep!.Value))
                    .ToArray()));

        AssetClass? fixedIncomeWithoutDestination = calculated.ClassLines
            .Where(item => IsFixedIncomeClass(item.AssetClass) && item.RecommendedContributionEur > 0m)
            .Select(item => (AssetClass?)item.AssetClass)
            .FirstOrDefault(assetClass => instruments.All(instrument => instrument.AssetClass != assetClass));
        if (fixedIncomeWithoutDestination.HasValue)
        {
            throw new ConflictException(
                $"Register an active {fixedIncomeWithoutDestination.Value.ToCode()} instrument before allocating to that class.");
        }

        var plan = new PersistedContributionPlan
        {
            Id = Guid.NewGuid(),
            PortfolioId = portfolio.Id,
            PortfolioVersion = portfolio.Version,
            PolicyVersion = calculated.PolicyVersion,
            StrategyVersion = calculated.AlgorithmVersion,
            Status = ContributionPlanStatus.Draft,
            ContributionAmountEur = contributionAmount,
            AllowedStaleData = request.AllowStaleData,
            CreatedAt = now,
            ExpiresAt = now.AddHours(24)
        };

        AddMarketPlanLines(plan, calculated, targets, valuedInstruments);
        AddFixedIncomePlanLines(plan, calculated, targets, valuedInstruments);
        ConservePersistedTotal(plan);

        await dbContext.ContributionPlans.AddAsync(plan, cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return ToDto(plan);
    }

    public async Task<ContributionPlanDto> ConfirmPlanAsync(
        Guid id,
        ConfirmContributionPlanRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var idempotencyKey = ValidateIdempotencyKey(request.IdempotencyKey);
        var now = UtcNow();

        var priorConfirmation = await dbContext.ContributionPlans
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(
                item => item.ConfirmationIdempotencyKey == idempotencyKey,
                cancellationToken);
        if (priorConfirmation is not null)
        {
            if (priorConfirmation.Id != id)
                throw new ConflictException("This idempotencyKey was already used for another contribution plan.");
            return ToDto(priorConfirmation);
        }

        var plan = await dbContext.ContributionPlans
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new NotFoundException("Contribution plan not found.");

        if (plan.Status == ContributionPlanStatus.Confirmed)
            throw new ConflictException("The contribution plan was already confirmed with another idempotencyKey.");
        if (plan.Status is ContributionPlanStatus.Cancelled or ContributionPlanStatus.Expired || plan.ExpiresAt <= now)
        {
            if (plan.Status == ContributionPlanStatus.Draft)
            {
                plan.Status = ContributionPlanStatus.Expired;
                await SaveChangesAsync(cancellationToken);
            }

            throw new ConflictException("The contribution plan has expired. Create a new preview.");
        }

        var portfolio = await dbContext.InvestmentPortfolios
            .Include(item => item.Instruments)
                .ThenInclude(item => item.Transactions)
            .Include(item => item.Instruments)
                .ThenInclude(item => item.ManualValuations)
            .SingleOrDefaultAsync(item => item.Id == plan.PortfolioId, cancellationToken)
            ?? throw new ConflictException("The contribution plan portfolio no longer exists.");

        if (portfolio.Version != plan.PortfolioVersion)
            throw new ConflictException("The portfolio changed after this preview. Create a new contribution plan.");

        var executions = request.Executions ?? [];
        if (executions.Count != plan.Lines.Count || executions.Count == 0)
            throw new DomainValidationException("executions must contain exactly one item for each contribution plan line.");
        if (executions.Any(item => item.PlanLineId == Guid.Empty) ||
            executions.GroupBy(item => item.PlanLineId).Any(group => group.Count() > 1))
        {
            throw new DomainValidationException("Each execution must reference a unique planLineId.");
        }

        var linesById = plan.Lines.ToDictionary(item => item.Id);
        if (executions.Any(item => !linesById.ContainsKey(item.PlanLineId)))
            throw new DomainValidationException("An execution references a line outside this contribution plan.");

        foreach (var execution in executions)
            ValidateExecution(execution, LocalDate(now));

        var actualTotal = executions.Sum(item => item.ActualAmountEur);
        if (actualTotal <= 0m)
            throw new DomainValidationException("At least one execution must have actualAmountEur greater than zero.");
        if (actualTotal > plan.ContributionAmountEur)
            throw new DomainValidationException("The executed EUR total cannot exceed the contribution amount.");

        var activeInstruments = portfolio.Instruments
            .Where(item => !item.IsArchived)
            .ToDictionary(item => item.Id);
        var touchedInstruments = new HashSet<Guid>();
        var manualExecutions = new Dictionary<Guid, ManualExecutionAccumulator>();

        foreach (var execution in executions.OrderBy(item => item.PlanLineId))
        {
            if (execution.ActualAmountEur == 0m)
                continue;

            var line = linesById[execution.PlanLineId];
            var instrumentId = execution.InstrumentId ?? line.InstrumentId
                ?? throw new DomainValidationException(
                    $"instrumentId is required for the {line.AssetClass.ToCode()} contribution line.");
            if (!activeInstruments.TryGetValue(instrumentId, out var instrument))
                throw new DomainValidationException("An execution destination is missing, archived or outside this portfolio.");
            if (instrument.AssetClass != line.AssetClass)
                throw new DomainValidationException("An execution destination must belong to the plan line asset class.");

            var currency = ParseCurrency(execution.Currency ?? instrument.NativeCurrency.Value);
            if (currency != instrument.NativeCurrency)
                throw new DomainValidationException("Execution currency must match the instrument native currency.");
            var fees = decimal.Round(execution.Fees ?? 0m, 8, MidpointRounding.ToEven);
            var transaction = IsMarketClass(line.AssetClass)
                ? BuildMarketTransaction(plan, line, instrument, execution, currency, fees, now)
                : BuildManualTransaction(plan, line, instrument, execution, currency, fees, now, manualExecutions);

            instrument.Transactions.Add(transaction);
            dbContext.InvestmentTransactions.Add(transaction);
            touchedInstruments.Add(instrument.Id);
        }

        AddManualBalanceSnapshots(plan, activeInstruments, manualExecutions, now);

        foreach (var instrumentId in touchedInstruments)
            activeInstruments[instrumentId].Touch(now);
        portfolio.Touch(now);
        plan.Status = ContributionPlanStatus.Confirmed;
        plan.ConfirmedAt = now;
        plan.ConfirmationIdempotencyKey = idempotencyKey;

        await SaveChangesAsync(cancellationToken);
        return ToDto(plan);
    }

    private ValuedInstrument ValueInstrument(
        InvestmentInstrument instrument,
        IReadOnlyDictionary<Guid, MarketQuoteSnapshot> latestQuotes,
        IReadOnlyDictionary<string, FxRateSnapshot> latestFx,
        DateOnly today)
    {
        var position = projectionService.CalculatePosition(instrument);
        if (instrument.ValuationMode == ValuationMode.MarketQuote &&
            position.Quantity == 0m &&
            instrument.AllocationScore == 0)
        {
            // A disabled, empty market position is worth exactly zero and cannot
            // receive this contribution, so external quote/FX data is irrelevant.
            return new ValuedInstrument(
                instrument,
                0m,
                0m,
                null,
                null,
                1m,
                ContributionDataFreshness.Fresh,
                null);
        }

        var rate = 1m;
        FxRateSnapshot? fx = null;
        var fxFreshness = ContributionDataFreshness.Fresh;
        if (instrument.NativeCurrency.Value != "EUR")
        {
            if (!latestFx.TryGetValue(instrument.NativeCurrency.Value, out fx))
            {
                return ValuedInstrument.Missing(instrument, "Missing EUR FX rate.");
            }

            rate = fx.Rate;
            fxFreshness = QuoteFreshness(fx.AsOf, today);
        }

        if (instrument.ValuationMode == ValuationMode.MarketQuote)
        {
            if (!latestQuotes.TryGetValue(instrument.Id, out var quote))
                return ValuedInstrument.Missing(instrument, "Missing market quote.");

            var freshness = Worst(QuoteFreshness(quote.AsOf, today), fxFreshness);
            return new ValuedInstrument(
                instrument,
                decimal.Round(position.Quantity * quote.Price / rate, 8, MidpointRounding.ToEven),
                position.Quantity,
                quote,
                fx,
                rate,
                freshness,
                null);
        }

        var latestManual = instrument.ManualValuations
            .OrderByDescending(item => item.AsOf)
            .ThenByDescending(item => item.RecordedAt)
            .FirstOrDefault();
        if (latestManual is null)
            return ValuedInstrument.Missing(instrument, "Missing manual valuation.");

        var manualFreshness = ManualFreshness(latestManual.AsOf, today);
        var currentNativeValue = position.CurrentManualValueNative ?? latestManual.Amount;
        return new ValuedInstrument(
            instrument,
            decimal.Round(currentNativeValue / rate, 8, MidpointRounding.ToEven),
            0m,
            null,
            fx,
            rate,
            Worst(manualFreshness, fxFreshness),
            null);
    }

    private static void AddMarketPlanLines(
        PersistedContributionPlan plan,
        CostTracker.Domain.Investments.Calculations.ContributionPlan calculated,
        IReadOnlyDictionary<AssetClass, decimal> targets,
        IReadOnlyDictionary<Guid, ValuedInstrument> valuedInstruments)
    {
        foreach (var calculatedLine in calculated.InstrumentLines.Where(item => item.RecommendedContributionEur > 0m))
        {
            var instrumentId = Guid.Parse(calculatedLine.InstrumentId);
            var valued = valuedInstruments[instrumentId];
            plan.Lines.Add(new PersistedContributionPlanLine
            {
                Id = Guid.NewGuid(),
                ContributionPlanId = plan.Id,
                AssetClass = calculatedLine.AssetClass,
                InstrumentId = instrumentId,
                InstrumentName = valued.Instrument.Name,
                Ticker = valued.Instrument.Ticker,
                NativeCurrency = valued.Instrument.NativeCurrency.Value,
                CurrentValueEur = RoundMoney(calculatedLine.ValueBeforeEur),
                TargetWeight = targets[calculatedLine.AssetClass],
                RecommendedAmountEur = RoundMoney(calculatedLine.RecommendedContributionEur),
                RecommendedNativeAmount = RoundMoney(calculatedLine.RecommendedAmountNative),
                SuggestedQuantity = decimal.Round(calculatedLine.SuggestedQuantity, 12, MidpointRounding.ToEven),
                UnitPrice = decimal.Round(calculatedLine.RecommendedAmountNative / calculatedLine.SuggestedQuantity, 12, MidpointRounding.ToEven),
                AllocationScore = calculatedLine.Score,
                Explanation = JoinExplanations(calculatedLine.Explanations),
                QuoteSnapshotId = valued.Quote?.Id,
                QuoteAsOf = valued.Quote?.AsOf,
                FxSnapshotId = valued.Fx?.Id,
                FxAsOf = valued.Fx?.AsOf,
                NativeCurrencyPerEur = valued.NativeCurrencyPerEur,
                Freshness = valued.Freshness
            });
        }
    }

    private static void AddFixedIncomePlanLines(
        PersistedContributionPlan plan,
        CostTracker.Domain.Investments.Calculations.ContributionPlan calculated,
        IReadOnlyDictionary<AssetClass, decimal> targets,
        IReadOnlyDictionary<Guid, ValuedInstrument> valuedInstruments)
    {
        foreach (var classLine in calculated.ClassLines.Where(item =>
                     IsFixedIncomeClass(item.AssetClass) && item.RecommendedContributionEur > 0m))
        {
            var candidates = valuedInstruments.Values
                .Where(item => item.Instrument.AssetClass == classLine.AssetClass)
                .OrderBy(item => item.Instrument.Name)
                .ToArray();
            var destination = candidates.Length == 1 ? candidates[0] : null;
            var freshness = candidates.Length == 0
                ? ContributionDataFreshness.Fresh
                : candidates.Select(item => item.Freshness).MaxBy(FreshnessRank);
            var recommendedEur = RoundMoney(classLine.RecommendedContributionEur);

            plan.Lines.Add(new PersistedContributionPlanLine
            {
                Id = Guid.NewGuid(),
                ContributionPlanId = plan.Id,
                AssetClass = classLine.AssetClass,
                InstrumentId = destination?.Instrument.Id,
                InstrumentName = destination?.Instrument.Name,
                Ticker = destination?.Instrument.Ticker,
                NativeCurrency = destination?.Instrument.NativeCurrency.Value,
                CurrentValueEur = RoundMoney(classLine.ValueBeforeEur),
                TargetWeight = targets[classLine.AssetClass],
                RecommendedAmountEur = recommendedEur,
                RecommendedNativeAmount = destination is null
                    ? null
                    : RoundMoney(recommendedEur * destination.NativeCurrencyPerEur),
                AllocationScore = null,
                Explanation = JoinExplanations(classLine.Explanations),
                FxSnapshotId = destination?.Fx?.Id,
                FxAsOf = destination?.Fx?.AsOf,
                NativeCurrencyPerEur = destination?.NativeCurrencyPerEur,
                Freshness = freshness
            });
        }
    }

    private static void ConservePersistedTotal(PersistedContributionPlan plan)
    {
        var total = plan.Lines.Sum(item => item.RecommendedAmountEur);
        if (total > plan.ContributionAmountEur)
        {
            var finalLine = plan.Lines.OrderBy(item => item.Id).Last();
            finalLine.RecommendedAmountEur -= total - plan.ContributionAmountEur;
            total = plan.ContributionAmountEur;
        }

        plan.TotalSuggestedEur = total;
        plan.ResidualAmountEur = plan.ContributionAmountEur - total;
    }

    private InvestmentTransaction BuildMarketTransaction(
        PersistedContributionPlan plan,
        PersistedContributionPlanLine line,
        InvestmentInstrument instrument,
        ContributionExecutionLineRequest execution,
        CurrencyCode currency,
        decimal fees,
        DateTimeOffset now)
    {
        if (instrument.ValuationMode != ValuationMode.MarketQuote)
            throw new DomainValidationException("STOCKS and REITS executions require a MARKET_QUOTE instrument.");
        if (execution.ActualQuantity is null or <= 0m || execution.ActualUnitPrice is null or <= 0m)
            throw new DomainValidationException("Market executions require positive actualQuantity and actualUnitPrice.");

        var quantity = decimal.Round(execution.ActualQuantity.Value, 12, MidpointRounding.ToEven);
        var unitPrice = decimal.Round(execution.ActualUnitPrice.Value, 8, MidpointRounding.ToEven);
        var nativeSpent = quantity * unitPrice + fees;
        var rate = currency.Value == "EUR" ? 1m : nativeSpent / execution.ActualAmountEur;
        if (currency.Value == "EUR" && decimal.Abs(nativeSpent - execution.ActualAmountEur) > 0.01m)
            throw new DomainValidationException("For EUR market executions, actualAmountEur must match quantity, price and fees.");

        return new InvestmentTransaction
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrument.Id,
            Type = InvestmentTransactionType.Buy,
            TransactionDate = execution.OccurredOn,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Currency = currency,
            FeeAmount = fees,
            CurrencyPerEurRate = decimal.Round(rate, 10, MidpointRounding.ToEven),
            Notes = $"Confirmed contribution plan {plan.Id:D}, line {line.Id:D}.",
            IdempotencyKey = $"contribution:{plan.Id:N}:{line.Id:N}",
            CreatedAt = now
        };
    }

    private InvestmentTransaction BuildManualTransaction(
        PersistedContributionPlan plan,
        PersistedContributionPlanLine line,
        InvestmentInstrument instrument,
        ContributionExecutionLineRequest execution,
        CurrencyCode currency,
        decimal fees,
        DateTimeOffset now,
        IDictionary<Guid, ManualExecutionAccumulator> manualExecutions)
    {
        if (instrument.ValuationMode != ValuationMode.Manual)
            throw new DomainValidationException("Fixed-income executions require a MANUAL instrument.");
        var latestValuation = instrument.ManualValuations
            .OrderByDescending(item => item.AsOf)
            .ThenByDescending(item => item.RecordedAt)
            .FirstOrDefault();
        if (latestValuation is not null && execution.OccurredOn < latestValuation.AsOf)
            throw new DomainValidationException("A fixed-income execution cannot predate its latest manual valuation.");

        var nativeAmount = execution.ActualNativeAmount;
        if (nativeAmount is null && currency.Value == "EUR")
            nativeAmount = execution.ActualAmountEur;
        if (nativeAmount is null && line.NativeCurrencyPerEur is > 0m)
            nativeAmount = execution.ActualAmountEur * line.NativeCurrencyPerEur.Value;
        if (nativeAmount is null or <= 0m)
            throw new DomainValidationException("Non-EUR fixed-income executions require actualNativeAmount.");

        var roundedNativeAmount = decimal.Round(nativeAmount.Value, 8, MidpointRounding.ToEven);
        var rate = currency.Value == "EUR" ? 1m : roundedNativeAmount / execution.ActualAmountEur;
        if (currency.Value == "EUR" && decimal.Abs(roundedNativeAmount - execution.ActualAmountEur) > 0.01m)
            throw new DomainValidationException("For EUR fixed-income executions, actualNativeAmount must match actualAmountEur.");

        if (!manualExecutions.TryGetValue(instrument.Id, out var accumulator))
        {
            accumulator = new ManualExecutionAccumulator(
                projectionService.CalculatePosition(instrument).CurrentManualValueNative ?? 0m,
                0m,
                execution.OccurredOn);
        }

        manualExecutions[instrument.Id] = accumulator with
        {
            AddedNativeAmount = accumulator.AddedNativeAmount + roundedNativeAmount,
            LatestDate = execution.OccurredOn > accumulator.LatestDate ? execution.OccurredOn : accumulator.LatestDate
        };

        return new InvestmentTransaction
        {
            Id = Guid.NewGuid(),
            InstrumentId = instrument.Id,
            Type = InvestmentTransactionType.Deposit,
            TransactionDate = execution.OccurredOn,
            Amount = roundedNativeAmount,
            Currency = currency,
            FeeAmount = fees,
            CurrencyPerEurRate = decimal.Round(rate, 10, MidpointRounding.ToEven),
            Notes = $"Confirmed contribution plan {plan.Id:D}, line {line.Id:D}.",
            IdempotencyKey = $"contribution:{plan.Id:N}:{line.Id:N}",
            CreatedAt = now
        };
    }

    private void AddManualBalanceSnapshots(
        PersistedContributionPlan plan,
        IReadOnlyDictionary<Guid, InvestmentInstrument> instruments,
        IReadOnlyDictionary<Guid, ManualExecutionAccumulator> manualExecutions,
        DateTimeOffset now)
    {
        foreach (var (instrumentId, execution) in manualExecutions)
        {
            var instrument = instruments[instrumentId];
            var valuation = new ManualValuation
            {
                Id = Guid.NewGuid(),
                InstrumentId = instrumentId,
                Amount = decimal.Round(
                    execution.CurrentNativeValue + execution.AddedNativeAmount,
                    8,
                    MidpointRounding.ToEven),
                Currency = instrument.NativeCurrency,
                AsOf = execution.LatestDate,
                RecordedAt = now,
                IdempotencyKey = $"contribution-value:{plan.Id:N}:{instrumentId:N}"
            };

            instrument.ManualValuations.Add(valuation);
            dbContext.ManualValuations.Add(valuation);
        }
    }

    private static void ValidateExecution(ContributionExecutionLineRequest execution, DateOnly today)
    {
        if (execution.OccurredOn == default)
            throw new DomainValidationException("occurredOn is required for every execution.");
        if (execution.OccurredOn > today)
            throw new DomainValidationException("occurredOn cannot be in the future.");
        if (execution.ActualAmountEur < 0m)
            throw new DomainValidationException("actualAmountEur cannot be negative.");
        if (execution.ActualNativeAmount is < 0m || execution.ActualQuantity is < 0m ||
            execution.ActualUnitPrice is < 0m || execution.Fees is < 0m)
        {
            throw new DomainValidationException("Execution amounts, quantity, price and fees cannot be negative.");
        }
    }

    private async Task ExpireDraftPlansAsync(CancellationToken cancellationToken)
    {
        var now = UtcNow();
        var expired = await dbContext.ContributionPlans
            .Where(item => item.Status == ContributionPlanStatus.Draft && item.ExpiresAt <= now)
            .ToListAsync(cancellationToken);
        if (expired.Count == 0)
            return;

        foreach (var plan in expired)
            plan.Status = ContributionPlanStatus.Expired;
        await SaveChangesAsync(cancellationToken);
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
        catch (DbUpdateException exception)
        {
            throw new ConflictException(
                $"The contribution change conflicts with existing data: {exception.GetBaseException().Message}");
        }
    }

    private ContributionDataFreshness QuoteFreshness(DateOnly asOf, DateOnly today)
    {
        var sessions = BusinessSessionsElapsed(asOf, today);
        if (sessions > _options.QuoteBlockingSessions)
            return ContributionDataFreshness.Blocked;
        if (sessions > _options.QuoteWarningSessions)
            return ContributionDataFreshness.Stale;
        return ContributionDataFreshness.Fresh;
    }

    private ContributionDataFreshness ManualFreshness(DateOnly asOf, DateOnly today)
    {
        var days = Math.Max(0, today.DayNumber - asOf.DayNumber);
        if (days > _options.ManualValuationBlockingDays)
            return ContributionDataFreshness.Blocked;
        if (days > _options.ManualValuationWarningDays)
            return ContributionDataFreshness.Stale;
        return ContributionDataFreshness.Fresh;
    }

    private static int BusinessSessionsElapsed(DateOnly from, DateOnly to)
    {
        var sessions = 0;
        for (var date = from.AddDays(1); date <= to; date = date.AddDays(1))
        {
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
                sessions++;
        }

        return sessions;
    }

    private DateOnly LocalDate(DateTimeOffset value)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(_options.RefreshTimeZone);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, zone).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(value.UtcDateTime);
        }
        catch (InvalidTimeZoneException)
        {
            return DateOnly.FromDateTime(value.UtcDateTime);
        }
    }

    private static ContributionPlanDto ToDto(PersistedContributionPlan plan)
        => new(
            plan.Id,
            StatusCode(plan.Status),
            plan.ContributionAmountEur,
            plan.TotalSuggestedEur,
            plan.ResidualAmountEur,
            plan.PortfolioVersion,
            plan.StrategyVersion,
            plan.CreatedAt,
            plan.ExpiresAt,
            plan.Lines
                .OrderBy(item => Array.IndexOf(StableClassOrder, item.AssetClass))
                .ThenBy(item => item.Ticker)
                .ThenBy(item => item.InstrumentName)
                .ThenBy(item => item.Id)
                .Select(item => new ContributionPlanLineDto(
                    item.Id,
                    item.AssetClass.ToCode(),
                    item.InstrumentId,
                    item.InstrumentName,
                    item.Ticker,
                    item.NativeCurrency,
                    item.CurrentValueEur,
                    item.TargetWeight,
                    item.RecommendedAmountEur,
                    item.RecommendedNativeAmount,
                    item.SuggestedQuantity,
                    item.UnitPrice,
                    item.AllocationScore,
                    item.Explanation,
                    item.QuoteAsOf,
                    item.FxAsOf,
                    FreshnessCode(item.Freshness)))
                .ToList());

    private static string StatusCode(ContributionPlanStatus status) => status switch
    {
        ContributionPlanStatus.Draft => "DRAFT",
        ContributionPlanStatus.Confirmed => "CONFIRMED",
        ContributionPlanStatus.Expired => "EXPIRED",
        ContributionPlanStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static string FreshnessCode(ContributionDataFreshness freshness) => freshness switch
    {
        ContributionDataFreshness.Fresh => "FRESH",
        ContributionDataFreshness.Stale => "STALE",
        ContributionDataFreshness.Blocked => "BLOCKED",
        ContributionDataFreshness.Missing => "MISSING",
        _ => throw new ArgumentOutOfRangeException(nameof(freshness))
    };

    private static string JoinExplanations(IEnumerable<ContributionExplanation> explanations)
        => string.Join(" ", explanations.Select(item => item.Message).Distinct(StringComparer.Ordinal));

    private static ContributionDataFreshness Worst(
        ContributionDataFreshness left,
        ContributionDataFreshness right)
        => FreshnessRank(left) >= FreshnessRank(right) ? left : right;

    private static int FreshnessRank(ContributionDataFreshness freshness) => freshness switch
    {
        ContributionDataFreshness.Fresh => 0,
        ContributionDataFreshness.Stale => 1,
        ContributionDataFreshness.Blocked => 2,
        ContributionDataFreshness.Missing => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(freshness))
    };

    private static bool IsMarketClass(AssetClass assetClass)
        => assetClass is AssetClass.Stocks or AssetClass.Reits;

    private static bool IsFixedIncomeClass(AssetClass assetClass)
        => assetClass is AssetClass.BrazilFixedIncome or AssetClass.InternationalFixedIncome;

    private static decimal RoundMoney(decimal value)
        => decimal.Round(value, 8, MidpointRounding.ToEven);

    private static string ValidateIdempotencyKey(string? value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is < 1 or > 128)
            throw new DomainValidationException("idempotencyKey is required and cannot exceed 128 characters.");
        return normalized;
    }

    private static CurrencyCode ParseCurrency(string value)
    {
        try
        {
            return new CurrencyCode(value);
        }
        catch (ArgumentException exception)
        {
            throw new DomainValidationException(exception.Message);
        }
    }

    private static void EnsurePortfolioConfigured(InvestmentPortfolio portfolio)
    {
        if (portfolio.AllocationTargets.Count != StableClassOrder.Length ||
            StableClassOrder.Any(assetClass => portfolio.AllocationTargets.Count(item => item.AssetClass == assetClass) != 1) ||
            portfolio.AllocationTargets.Any(item => item.Weight * 100m != decimal.Truncate(item.Weight * 100m)) ||
            portfolio.AllocationTargets.Sum(item => item.Weight) != 1m)
        {
            throw new ConflictException("Configure all five allocation targets before creating a contribution plan.");
        }
    }

    private DateTimeOffset UtcNow() => timeProvider.GetUtcNow();

    private sealed record ValuedInstrument(
        InvestmentInstrument Instrument,
        decimal ValueEur,
        decimal Quantity,
        MarketQuoteSnapshot? Quote,
        FxRateSnapshot? Fx,
        decimal NativeCurrencyPerEur,
        ContributionDataFreshness Freshness,
        string? MissingReason)
    {
        public static ValuedInstrument Missing(InvestmentInstrument instrument, string reason)
            => new(instrument, 0m, 0m, null, null, 0m, ContributionDataFreshness.Missing, reason);
    }

    private sealed record ManualExecutionAccumulator(
        decimal CurrentNativeValue,
        decimal AddedNativeAmount,
        DateOnly LatestDate);
}
