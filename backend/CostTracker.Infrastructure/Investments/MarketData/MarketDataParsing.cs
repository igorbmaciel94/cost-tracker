using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CostTracker.Infrastructure.Investments.MarketData;

internal static class MarketDataParsing
{
    public static string Sha256(string payload)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = 0m;
        if (!element.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.Number)
            return property.TryGetDecimal(out value);

        return property.ValueKind == JsonValueKind.String &&
               decimal.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    public static string? GetString(JsonElement element, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
                return property.GetString();
        }

        return null;
    }

    public static string? NormalizeProviderCurrency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();

        // Yahoo and some market-data vendors use the case-sensitive ISO 4217
        // convention "GBp" for prices quoted in pence. Upper-casing first would
        // silently turn it into GBP and overstate London positions by 100x.
        return string.Equals(trimmed, "GBp", StringComparison.Ordinal)
            ? "GBX"
            : trimmed.ToUpperInvariant();
    }

    public static bool TryParseDate(string? value, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            date = DateOnly.FromDateTime(timestamp.UtcDateTime);
            return true;
        }

        return false;
    }
}
