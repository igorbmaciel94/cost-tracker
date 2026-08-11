using CostTracker.Domain.Enums;

namespace CostTracker.Domain.ValueObjects;

public readonly record struct AllocationWeight(AssetClass AssetClass, decimal Weight);

public sealed class AllocationTargetSet
{
    private static readonly AssetClass[] RequiredClasses = Enum.GetValues<AssetClass>();

    private AllocationTargetSet(IReadOnlyDictionary<AssetClass, decimal> weights)
    {
        Weights = weights;
    }

    public IReadOnlyDictionary<AssetClass, decimal> Weights { get; }

    public static AllocationTargetSet Create(IEnumerable<AllocationWeight> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var materialized = items.ToList();
        if (materialized.Count != RequiredClasses.Length)
            throw new ArgumentException("Allocation must contain each of the four asset classes exactly once.", nameof(items));

        if (materialized.Select(item => item.AssetClass).Distinct().Count() != RequiredClasses.Length ||
            RequiredClasses.Any(required => materialized.All(item => item.AssetClass != required)))
        {
            throw new ArgumentException("Allocation must contain each of the four asset classes exactly once.", nameof(items));
        }

        if (materialized.Any(item => item.Weight is < 0m or > 1m))
            throw new ArgumentException("Every allocation weight must be between zero and one.", nameof(items));

        var normalized = materialized.ToDictionary(
            item => item.AssetClass,
            item => decimal.Round(item.Weight, 8, MidpointRounding.ToEven));

        if (normalized.Values.Sum() != 1.00000000m)
            throw new ArgumentException("Allocation weights must total exactly 1.00000000.", nameof(items));

        return new AllocationTargetSet(normalized);
    }
}
