using System.Text.Json;
using EIP.Intelligence.Analytics.Application;

namespace EIP.Intelligence.AI.Application;

public sealed record AiAnalyticsResult(Guid TenantId, string Tool, string Source,
    AnalyticsQueryDefinition Query, IReadOnlyList<IReadOnlyDictionary<string, object?>> Data,
    AnalyticsQueryMetadata Metadata);

/// <summary>Contrato fechado: contexto autenticado e SQL nunca são argumentos da ferramenta.</summary>
public static class AiAnalyticsQuery
{
    public static AnalyticsQueryDefinition Parse(JsonElement input)
    {
        Object(input, ["dataset", "metrics", "dimensions", "filters", "orderBy", "limit"]);
        var dataset = Text(input.GetProperty("dataset"));
        var metrics = Strings(input.GetProperty("metrics"));
        if (metrics.Distinct(StringComparer.Ordinal).Count() != metrics.Length)
        {
            throw new JsonException();
        }
        var dimensions = Strings(input.GetProperty("dimensions"));
        var filters = new List<AnalyticsFilterClause>();
        if (input.TryGetProperty("filters", out var filterArray))
        {
            foreach (var filter in filterArray.EnumerateArray())
            {
                Object(filter, ["field", "operator", "values"]);
                filters.Add(new(Text(filter.GetProperty("field")), Text(filter.GetProperty("operator")),
                    Strings(filter.GetProperty("values"))));
            }
        }

        var orderBy = new List<AnalyticsOrderBy>();
        if (input.TryGetProperty("orderBy", out var orderArray))
        {
            foreach (var order in orderArray.EnumerateArray())
            {
                Object(order, ["field", "direction"]);
                orderBy.Add(new(Text(order.GetProperty("field")), Text(order.GetProperty("direction"))));
            }
        }

        int? limit = input.TryGetProperty("limit", out var limitElement) ? limitElement.GetInt32() : null;
        return new(dataset, metrics, dimensions, filters, orderBy, limit);
    }

    public static AiAnalyticsResult Evidence(Guid tenantId, AnalyticsQueryDefinition query, AnalyticsQueryResult result) =>
        new(tenantId, AiToolRegistry.QueryAnalytics, "analytics", query,
            result.Data.Select(row => (IReadOnlyDictionary<string, object?>)row.DimensionValues
                .Select(pair => new KeyValuePair<string, object?>(pair.Key, pair.Value))
                .Concat(row.MetricValues.Select(pair => new KeyValuePair<string, object?>(pair.Key, pair.Value)))
                .ToDictionary(pair => pair.Key, pair => pair.Value)).ToList(), result.Metadata);

    private static void Object(JsonElement value, string[] allowed)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException();
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
            {
                throw new JsonException();
            }
        }
    }

    private static string Text(JsonElement value) => value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()! : throw new JsonException();

    private static string[] Strings(JsonElement value) => value.EnumerateArray().Select(Text).ToArray();
}
