using CostTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CostTracker.Api.Services;

public static class MonthQueryExtensions
{
    public static IQueryable<Month> WithDetails(this IQueryable<Month> query)
    {
        return query
            .Include(x => x.CategoryBudgets)
            .Include(x => x.Entries)
            .Include(x => x.GroupTargets);
    }
}
