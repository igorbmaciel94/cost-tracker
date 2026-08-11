using CostTracker.Domain.Enums;

namespace CostTracker.Domain.Investments.Calculations;

/// <summary>
/// Pure contribution-planning module. It projects class and market-instrument gaps onto
/// simplexes, applies executable quantity steps, and returns every unspent amount explicitly.
/// </summary>
public static class ContributionAllocator
{
    public const string CurrentAlgorithmVersion = "simplex-score-target-only-v2";

    private static readonly AssetClass[] StableClassOrder =
    [
        AssetClass.Stocks,
        AssetClass.Reits,
        AssetClass.BrazilFixedIncome,
        AssetClass.InternationalFixedIncome,
        AssetClass.Cryptocurrencies
    ];

    public static ContributionPlan Calculate(
        PortfolioSnapshot portfolio,
        ContributionAmount contribution,
        AllocationPolicy policy,
        ExecutionConstraints constraints)
    {
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(constraints);

        var inputs = ValidateAndNormalize(portfolio, contribution, policy, constraints);
        var totalBefore = StableClassOrder.Sum(x => inputs.Classes[x].CurrentValueEur);
        var totalAfterContribution = totalBefore + contribution.Eur;

        var macroCandidates = StableClassOrder
            .Select(assetClass =>
            {
                var classSnapshot = inputs.Classes[assetClass];
                var targetWeight = inputs.Targets[assetClass];
                var targetValue = targetWeight * totalAfterContribution;

                return new ProjectionCandidate<AssetClass>(
                    assetClass,
                    targetValue - classSnapshot.CurrentValueEur,
                    StableClassIndex(assetClass));
            })
            .ToArray();

        var macroAllocations = ProjectOntoSimplex(macroCandidates, contribution.Eur);
        var classLines = new List<ClassContributionPlanLine>(StableClassOrder.Length);
        var instrumentLines = new List<InstrumentContributionPlanLine>();

        foreach (var assetClass in StableClassOrder)
        {
            var classSnapshot = inputs.Classes[assetClass];
            var targetWeight = inputs.Targets[assetClass];
            var targetValue = targetWeight * totalAfterContribution;
            var planned = macroAllocations[assetClass];
            var classExplanations = new List<ContributionExplanation>();
            decimal recommended;

            if (IsMarketClass(assetClass))
            {
                var marketResult = AllocateMarketClass(
                    classSnapshot,
                    planned,
                    inputs.Scores,
                    inputs.QuantitySteps,
                    constraints.DefaultQuantityStep);

                recommended = marketResult.RecommendedEur;
                instrumentLines.AddRange(marketResult.Lines);
                classExplanations.AddRange(marketResult.Explanations);
            }
            else if (IsFixedIncomeClass(assetClass))
            {
                recommended = planned;
                classExplanations.Add(new ContributionExplanation(
                    ContributionExplanationCode.FixedIncomeRequiresManualSelection,
                    "O valor foi calculado para a classe; o destino de renda fixa deve ser escolhido manualmente."));
            }
            else
            {
                recommended = 0m;
                classExplanations.Add(new ContributionExplanation(
                    ContributionExplanationCode.TargetOnlyClass,
                    "Criptomoedas são apenas uma meta percentual; a parcela calculada permanece como residual, sem ordem de compra."));
            }

            if (recommended > 0m)
            {
                classExplanations.Insert(0, new ContributionExplanation(
                    ContributionExplanationCode.MovesClassTowardTarget,
                    "O aporte reduz o desvio desta classe sem recomendar venda."));
            }
            else
            {
                classExplanations.Insert(0, new ContributionExplanation(
                    ContributionExplanationCode.ClassReceivesNoContribution,
                    "Esta classe não recebe dinheiro novo na projeção ótima sem vendas."));
            }

            var projectedValue = classSnapshot.CurrentValueEur + recommended;
            classLines.Add(new ClassContributionPlanLine(
                assetClass,
                classSnapshot.CurrentValueEur,
                targetWeight,
                targetValue,
                targetValue - classSnapshot.CurrentValueEur,
                planned,
                recommended,
                projectedValue,
                targetValue - projectedValue,
                planned - recommended,
                classExplanations.ToArray()));
        }

        var totalRecommended = classLines.Sum(x => x.RecommendedContributionEur);
        if (totalRecommended > contribution.Eur)
        {
            throw new InvalidOperationException("The contribution plan exceeded the available amount.");
        }

        var residual = contribution.Eur - totalRecommended;
        var planExplanations = new List<ContributionExplanation>
        {
            new(
                ContributionExplanationCode.FeesAndTaxesNotIncluded,
                "O preview não estima comissões, spread, impostos ou taxas.")
        };

        var targetOnlyResidual = classLines
            .Where(line => line.AssetClass == AssetClass.Cryptocurrencies)
            .Sum(line => line.ResidualEur);
        if (targetOnlyResidual > 0m)
        {
            planExplanations.Add(new ContributionExplanation(
                ContributionExplanationCode.TargetOnlyClass,
                "A parcela de criptomoedas permanece como residual porque esta versão registra apenas a meta percentual da classe."));
        }

        if (residual > targetOnlyResidual)
        {
            planExplanations.Add(new ContributionExplanation(
                ContributionExplanationCode.ResidualCouldNotBuyFullStep,
                "O residual permanece em caixa porque não pôde ser alocado sem violar as restrições de execução."));
        }

        return new ContributionPlan(
            CurrentAlgorithmVersion,
            portfolio.Version,
            policy.Version,
            contribution.Eur,
            totalRecommended,
            residual,
            classLines.ToArray(),
            instrumentLines.ToArray(),
            planExplanations.ToArray());
    }

    private static NormalizedInputs ValidateAndNormalize(
        PortfolioSnapshot portfolio,
        ContributionAmount contribution,
        AllocationPolicy policy,
        ExecutionConstraints constraints)
    {
        if (string.IsNullOrWhiteSpace(portfolio.Version))
        {
            throw new ArgumentException("Portfolio version is required.", nameof(portfolio));
        }

        if (string.IsNullOrWhiteSpace(policy.Version))
        {
            throw new ArgumentException("Policy version is required.", nameof(policy));
        }

        if (contribution.Eur <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(contribution), "Contribution must be positive.");
        }

        if (constraints.DefaultQuantityStep <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(constraints), "The default quantity step must be positive.");
        }

        if (portfolio.Classes is null)
        {
            throw new ArgumentException("Portfolio classes are required.", nameof(portfolio));
        }

        if (policy.ClassTargets is null || policy.InstrumentScores is null)
        {
            throw new ArgumentException("Allocation targets and scores are required.", nameof(policy));
        }

        if (constraints.InstrumentOverrides is null)
        {
            throw new ArgumentException("Instrument overrides are required.", nameof(constraints));
        }

        var classes = ToUniqueDictionary(
            portfolio.Classes,
            x => x.AssetClass,
            "Portfolio contains duplicate asset classes.",
            nameof(portfolio));

        var targets = ToUniqueDictionary(
            policy.ClassTargets,
            x => x.AssetClass,
            "Allocation policy contains duplicate asset classes.",
            nameof(policy));

        if (classes.Count != StableClassOrder.Length || StableClassOrder.Any(x => !classes.ContainsKey(x)))
        {
            throw new ArgumentException("Portfolio must contain each of the five asset classes exactly once.", nameof(portfolio));
        }

        if (targets.Count != StableClassOrder.Length || StableClassOrder.Any(x => !targets.ContainsKey(x)))
        {
            throw new ArgumentException("Policy must contain each of the five asset classes exactly once.", nameof(policy));
        }

        if (classes.Values.Any(x => x.CurrentValueEur < 0m))
        {
            throw new ArgumentOutOfRangeException(nameof(portfolio), "Class values cannot be negative.");
        }

        if (targets.Values.Any(x => x.TargetWeight < 0m || x.TargetWeight > 1m) ||
            targets.Values.Sum(x => x.TargetWeight) != 1m)
        {
            throw new ArgumentException("Class targets must be between zero and one and sum exactly to one.", nameof(policy));
        }

        if (targets.Values.Any(x => x.TargetWeight * 100m != decimal.Truncate(x.TargetWeight * 100m)))
        {
            throw new ArgumentException("Class targets must use whole percentages.", nameof(policy));
        }

        var allInstruments = classes.Values
            .Where(x => IsMarketClass(x.AssetClass))
            .SelectMany(x => x.Instruments ?? throw new ArgumentException(
                "Instrument collections are required for market classes.", nameof(portfolio)))
            .ToArray();

        var instrumentsById = ToUniqueDictionary(
            allInstruments,
            x => x.InstrumentId,
            "Portfolio contains duplicate instrument IDs.",
            nameof(portfolio),
            StringComparer.Ordinal);

        foreach (var instrument in allInstruments)
        {
            if (string.IsNullOrWhiteSpace(instrument.InstrumentId) ||
                string.IsNullOrWhiteSpace(instrument.Mic) ||
                string.IsNullOrWhiteSpace(instrument.Symbol))
            {
                throw new ArgumentException("Instrument ID, MIC and symbol are required.", nameof(portfolio));
            }

            if (instrument.CurrentValueEur < 0m)
            {
                throw new ArgumentOutOfRangeException(nameof(portfolio), "Instrument values cannot be negative.");
            }
        }

        var scores = ToUniqueDictionary(
            policy.InstrumentScores,
            x => x.InstrumentId,
            "Allocation policy contains duplicate instrument scores.",
            nameof(policy),
            StringComparer.Ordinal);

        foreach (var score in scores.Values)
        {
            if (string.IsNullOrWhiteSpace(score.InstrumentId) || score.Score < 0)
            {
                throw new ArgumentException("Instrument scores require an ID and cannot be negative.", nameof(policy));
            }

            if (!instrumentsById.ContainsKey(score.InstrumentId))
            {
                throw new ArgumentException($"Score references unknown instrument '{score.InstrumentId}'.", nameof(policy));
            }
        }

        foreach (var instrument in allInstruments.Where(x => scores.GetValueOrDefault(x.InstrumentId)?.Score > 0))
        {
            if (instrument.UnitPriceNative <= 0m || instrument.NativeCurrencyPerEur <= 0m)
            {
                throw new ArgumentException(
                    $"Eligible instrument '{instrument.InstrumentId}' requires a positive price and FX rate.",
                    nameof(portfolio));
            }
        }

        var quantitySteps = ToUniqueDictionary(
            constraints.InstrumentOverrides,
            x => x.InstrumentId,
            "Execution constraints contain duplicate instrument overrides.",
            nameof(constraints),
            StringComparer.Ordinal);

        foreach (var item in quantitySteps.Values)
        {
            if (string.IsNullOrWhiteSpace(item.InstrumentId) || item.QuantityStep <= 0m)
            {
                throw new ArgumentException("Quantity step overrides require an ID and a positive step.", nameof(constraints));
            }

            if (!instrumentsById.ContainsKey(item.InstrumentId))
            {
                throw new ArgumentException(
                    $"Quantity step references unknown instrument '{item.InstrumentId}'.",
                    nameof(constraints));
            }
        }

        return new NormalizedInputs(
            classes,
            targets.ToDictionary(x => x.Key, x => x.Value.TargetWeight),
            scores.ToDictionary(x => x.Key, x => x.Value.Score, StringComparer.Ordinal),
            quantitySteps.ToDictionary(x => x.Key, x => x.Value.QuantityStep, StringComparer.Ordinal));
    }

    private static MarketAllocationResult AllocateMarketClass(
        PortfolioClassSnapshot classSnapshot,
        decimal classContribution,
        IReadOnlyDictionary<string, int> scores,
        IReadOnlyDictionary<string, decimal> quantitySteps,
        decimal defaultQuantityStep)
    {
        var orderedInstruments = classSnapshot.Instruments
            .OrderBy(x => x.Mic, StringComparer.Ordinal)
            .ThenBy(x => x.Symbol, StringComparer.Ordinal)
            .ThenBy(x => x.InstrumentId, StringComparer.Ordinal)
            .ToArray();

        var eligible = orderedInstruments
            .Where(x => scores.GetValueOrDefault(x.InstrumentId) > 0)
            .ToArray();

        if (eligible.Length == 0)
        {
            var excludedLines = orderedInstruments
                .Select(instrument => CreateExcludedInstrumentLine(
                    classSnapshot.AssetClass,
                    instrument,
                    scores.GetValueOrDefault(instrument.InstrumentId),
                    quantitySteps.GetValueOrDefault(instrument.InstrumentId, defaultQuantityStep)))
                .ToArray();

            return new MarketAllocationResult(
                0m,
                excludedLines,
                [new ContributionExplanation(
                    ContributionExplanationCode.NoEligibleInstrument,
                    "A classe não possui instrumento com nota positiva; seu valor permanece como residual.")]);
        }

        var scoreTotal = eligible.Sum(x => (decimal)scores[x.InstrumentId]);
        var classValueAfterPlannedContribution = classSnapshot.CurrentValueEur + classContribution;
        var projectionCandidates = eligible
            .Select((instrument, stableIndex) =>
            {
                var targetWeight = (decimal)scores[instrument.InstrumentId] / scoreTotal;
                var targetValue = targetWeight * classValueAfterPlannedContribution;

                return new ProjectionCandidate<string>(
                    instrument.InstrumentId,
                    targetValue - instrument.CurrentValueEur,
                    stableIndex);
            })
            .ToArray();

        var plannedAllocations = ProjectOntoSimplex(projectionCandidates, classContribution);
        var workItems = new List<InstrumentWorkItem>(orderedInstruments.Length);

        foreach (var instrument in orderedInstruments)
        {
            var score = scores.GetValueOrDefault(instrument.InstrumentId);
            var step = quantitySteps.GetValueOrDefault(instrument.InstrumentId, defaultQuantityStep);

            if (score == 0)
            {
                workItems.Add(InstrumentWorkItem.Excluded(classSnapshot.AssetClass, instrument, step));
                continue;
            }

            var targetWeight = (decimal)score / scoreTotal;
            var targetValue = targetWeight * classValueAfterPlannedContribution;
            var planned = plannedAllocations[instrument.InstrumentId];
            var quantity = FloorToStep(planned * instrument.NativeCurrencyPerEur / instrument.UnitPriceNative, step);
            var recommendedNative = quantity * instrument.UnitPriceNative;
            var recommendedEur = recommendedNative / instrument.NativeCurrencyPerEur;

            while (recommendedEur > planned && quantity >= step)
            {
                quantity -= step;
                recommendedNative = quantity * instrument.UnitPriceNative;
                recommendedEur = recommendedNative / instrument.NativeCurrencyPerEur;
            }

            var explanations = new List<ContributionExplanation>
            {
                new(
                    ContributionExplanationCode.DistributedByScoreAndGap,
                    "A nota define o peso interno e o desvio atual define a prioridade do aporte.")
            };

            if (recommendedEur < planned)
            {
                explanations.Add(new ContributionExplanation(
                    ContributionExplanationCode.RoundedDownToQuantityStep,
                    "A quantidade foi arredondada para baixo conforme o passo executável."));
            }

            workItems.Add(new InstrumentWorkItem(
                classSnapshot.AssetClass,
                instrument,
                score,
                targetWeight,
                targetValue,
                planned,
                step,
                quantity,
                recommendedEur,
                explanations));
        }

        RedistributeExecutableResidual(classContribution, workItems);

        var recommended = workItems.Sum(x => x.RecommendedEur);
        var classExplanations = new List<ContributionExplanation>
        {
            new(
                ContributionExplanationCode.DistributedByScoreAndGap,
                "O aporte da classe foi distribuído por nota e distância ao peso desejado.")
        };

        if (workItems.Any(x => x.ResidualWasReinvested))
        {
            classExplanations.Add(new ContributionExplanation(
                ContributionExplanationCode.ResidualReinvested,
                "O residual executável foi aplicado ao maior gap remanescente."));
        }

        if (recommended < classContribution)
        {
            classExplanations.Add(new ContributionExplanation(
                ContributionExplanationCode.ResidualCouldNotBuyFullStep,
                "Parte do valor não compra um incremento completo e permanece como residual."));
        }

        return new MarketAllocationResult(
            recommended,
            workItems.Select(x => x.ToPlanLine()).ToArray(),
            classExplanations.ToArray());
    }

    private static void RedistributeExecutableResidual(
        decimal classContribution,
        IReadOnlyList<InstrumentWorkItem> workItems)
    {
        var residual = classContribution - workItems.Sum(x => x.RecommendedEur);

        while (residual > 0m)
        {
            var candidate = workItems
                .Where(x => x.Score > 0 && x.UnitCostEur <= residual)
                .OrderByDescending(x => x.RemainingGapEur)
                .ThenBy(x => x.Instrument.Mic, StringComparer.Ordinal)
                .ThenBy(x => x.Instrument.Symbol, StringComparer.Ordinal)
                .ThenBy(x => x.Instrument.InstrumentId, StringComparer.Ordinal)
                .FirstOrDefault();

            if (candidate is null)
            {
                break;
            }

            var otherCandidate = workItems
                .Where(x => x.Score > 0 &&
                            !ReferenceEquals(x, candidate) &&
                            x.UnitCostEur <= residual)
                .OrderByDescending(x => x.RemainingGapEur)
                .ThenBy(x => x.Instrument.Mic, StringComparer.Ordinal)
                .ThenBy(x => x.Instrument.Symbol, StringComparer.Ordinal)
                .ThenBy(x => x.Instrument.InstrumentId, StringComparer.Ordinal)
                .FirstOrDefault();

            var affordableUnits = decimal.Floor(residual / candidate.UnitCostEur);
            var units = affordableUnits;

            if (otherCandidate is not null)
            {
                var gapLead = candidate.RemainingGapEur - otherCandidate.RemainingGapEur;
                if (gapLead <= 0m)
                {
                    units = 1m;
                }
                else
                {
                    units = Math.Min(
                        affordableUnits,
                        Math.Max(1m, decimal.Ceiling(gapLead / candidate.UnitCostEur)));
                }
            }

            candidate.AddSteps(units);
            residual = classContribution - workItems.Sum(x => x.RecommendedEur);
        }
    }

    private static InstrumentContributionPlanLine CreateExcludedInstrumentLine(
        AssetClass assetClass,
        InstrumentSnapshot instrument,
        int score,
        decimal quantityStep)
    {
        return new InstrumentContributionPlanLine(
            assetClass,
            instrument.InstrumentId,
            instrument.Mic,
            instrument.Symbol,
            instrument.CurrentValueEur,
            score,
            0m,
            0m,
            -instrument.CurrentValueEur,
            0m,
            0m,
            0m,
            0m,
            quantityStep,
            instrument.CurrentValueEur,
            -instrument.CurrentValueEur,
            [new ContributionExplanation(
                ContributionExplanationCode.ScoreZeroExcluded,
                "Nota zero exclui o instrumento deste aporte.")]);
    }

    private static decimal FloorToStep(decimal quantity, decimal step)
    {
        return decimal.Floor(quantity / step) * step;
    }

    private static IReadOnlyDictionary<TKey, decimal> ProjectOntoSimplex<TKey>(
        IReadOnlyList<ProjectionCandidate<TKey>> candidates,
        decimal total)
        where TKey : notnull
    {
        if (total < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(total), "Simplex total cannot be negative.");
        }

        if (candidates.Count == 0)
        {
            if (total == 0m)
            {
                return new Dictionary<TKey, decimal>();
            }

            throw new ArgumentException("A positive simplex total requires at least one candidate.", nameof(candidates));
        }

        if (total == 0m)
        {
            return candidates.ToDictionary(x => x.Key, _ => 0m);
        }

        var sorted = candidates
            .OrderByDescending(x => x.Gap)
            .ThenBy(x => x.StableIndex)
            .ToArray();

        var prefix = 0m;
        var tau = 0m;
        var activeCount = 0;

        for (var index = 0; index < sorted.Length; index++)
        {
            prefix += sorted[index].Gap;
            var candidateTau = (prefix - total) / (index + 1);

            if (sorted[index].Gap - candidateTau > 0m)
            {
                activeCount = index + 1;
                tau = candidateTau;
            }
        }

        if (activeCount == 0)
        {
            throw new InvalidOperationException("Simplex projection did not find an active candidate.");
        }

        var result = candidates.ToDictionary(
            x => x.Key,
            x => Math.Max(0m, x.Gap - tau));

        // Decimal division may leave a tiny remainder. Assign it to the last positive
        // stable candidate so every projection conserves the exact input amount.
        var correction = total - result.Values.Sum();
        if (correction != 0m)
        {
            var correctionTarget = candidates
                .Where(x => result[x.Key] > 0m)
                .OrderByDescending(x => x.StableIndex)
                .First();

            result[correctionTarget.Key] += correction;
        }

        if (result.Values.Any(x => x < 0m) || result.Values.Sum() != total)
        {
            throw new InvalidOperationException("Simplex projection failed to conserve its total.");
        }

        return result;
    }

    private static bool IsMarketClass(AssetClass assetClass)
    {
        return assetClass is AssetClass.Stocks or AssetClass.Reits;
    }

    private static bool IsFixedIncomeClass(AssetClass assetClass)
    {
        return assetClass is AssetClass.BrazilFixedIncome or AssetClass.InternationalFixedIncome;
    }

    private static int StableClassIndex(AssetClass assetClass)
    {
        return Array.IndexOf(StableClassOrder, assetClass);
    }

    private static Dictionary<TKey, TValue> ToUniqueDictionary<TValue, TKey>(
        IEnumerable<TValue> items,
        Func<TValue, TKey> keySelector,
        string duplicateMessage,
        string parameterName,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        var dictionary = new Dictionary<TKey, TValue>(comparer);
        foreach (var item in items)
        {
            if (!dictionary.TryAdd(keySelector(item), item))
            {
                throw new ArgumentException(duplicateMessage, parameterName);
            }
        }

        return dictionary;
    }

    private sealed record ProjectionCandidate<TKey>(
        TKey Key,
        decimal Gap,
        int StableIndex)
        where TKey : notnull;

    private sealed record NormalizedInputs(
        IReadOnlyDictionary<AssetClass, PortfolioClassSnapshot> Classes,
        IReadOnlyDictionary<AssetClass, decimal> Targets,
        IReadOnlyDictionary<string, int> Scores,
        IReadOnlyDictionary<string, decimal> QuantitySteps);

    private sealed record MarketAllocationResult(
        decimal RecommendedEur,
        IReadOnlyList<InstrumentContributionPlanLine> Lines,
        IReadOnlyList<ContributionExplanation> Explanations);

    private sealed class InstrumentWorkItem
    {
        private readonly List<ContributionExplanation> _explanations;

        public InstrumentWorkItem(
            AssetClass assetClass,
            InstrumentSnapshot instrument,
            int score,
            decimal targetWeight,
            decimal targetValueEur,
            decimal plannedEur,
            decimal quantityStep,
            decimal quantity,
            decimal recommendedEur,
            List<ContributionExplanation> explanations)
        {
            AssetClass = assetClass;
            Instrument = instrument;
            Score = score;
            TargetWeight = targetWeight;
            TargetValueEur = targetValueEur;
            PlannedEur = plannedEur;
            QuantityStep = quantityStep;
            Quantity = quantity;
            RecommendedEur = recommendedEur;
            _explanations = explanations;
        }

        public AssetClass AssetClass { get; }
        public InstrumentSnapshot Instrument { get; }
        public int Score { get; }
        public decimal TargetWeight { get; }
        public decimal TargetValueEur { get; }
        public decimal PlannedEur { get; }
        public decimal QuantityStep { get; }
        public decimal Quantity { get; private set; }
        public decimal RecommendedEur { get; private set; }
        public bool ResidualWasReinvested { get; private set; }
        public decimal UnitCostEur => QuantityStep * Instrument.UnitPriceNative / Instrument.NativeCurrencyPerEur;
        public decimal RemainingGapEur => TargetValueEur - Instrument.CurrentValueEur - RecommendedEur;

        public static InstrumentWorkItem Excluded(
            AssetClass assetClass,
            InstrumentSnapshot instrument,
            decimal quantityStep)
        {
            return new InstrumentWorkItem(
                assetClass,
                instrument,
                0,
                0m,
                0m,
                0m,
                quantityStep,
                0m,
                0m,
                [new ContributionExplanation(
                    ContributionExplanationCode.ScoreZeroExcluded,
                    "Nota zero exclui o instrumento deste aporte.")]);
        }

        public void AddSteps(decimal numberOfSteps)
        {
            Quantity += numberOfSteps * QuantityStep;
            RecommendedEur = Quantity * Instrument.UnitPriceNative / Instrument.NativeCurrencyPerEur;
            ResidualWasReinvested = true;

            if (_explanations.All(x => x.Code != ContributionExplanationCode.ResidualReinvested))
            {
                _explanations.Add(new ContributionExplanation(
                    ContributionExplanationCode.ResidualReinvested,
                    "Foi aplicado residual executável a este gap remanescente."));
            }
        }

        public InstrumentContributionPlanLine ToPlanLine()
        {
            var projectedValue = Instrument.CurrentValueEur + RecommendedEur;

            return new InstrumentContributionPlanLine(
                AssetClass,
                Instrument.InstrumentId,
                Instrument.Mic,
                Instrument.Symbol,
                Instrument.CurrentValueEur,
                Score,
                TargetWeight,
                TargetValueEur,
                TargetValueEur - Instrument.CurrentValueEur,
                PlannedEur,
                RecommendedEur,
                Quantity * Instrument.UnitPriceNative,
                Quantity,
                QuantityStep,
                projectedValue,
                TargetValueEur - projectedValue,
                _explanations.ToArray());
        }
    }
}
