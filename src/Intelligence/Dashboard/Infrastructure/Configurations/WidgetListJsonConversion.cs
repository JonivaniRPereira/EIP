using System.Text.Json;
using EIP.Intelligence.Dashboard.Domain;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EIP.Intelligence.Dashboard.Infrastructure.Configurations;

/// <summary>
/// Conversão JSON compartilhada para toda propriedade <c>IReadOnlyList&lt;Widget&gt;</c> mapeada como
/// coluna única (<see cref="Dashboard.Widgets"/> e <see cref="DashboardVersion.Widgets"/>) — sem
/// precedente de <c>OwnsMany</c>/<c>ToJson</c> no resto do código-base (que exigiria remodelar
/// <c>AnalyticsQueryDefinition</c> para tipos amigáveis ao mapeamento de dono do EF), então a conversão
/// manual via <c>System.Text.Json</c> segue o mesmo idioma já usado para os enums
/// (<c>HasConversion&lt;string&gt;()</c>), só que para a lista inteira. <see cref="Comparer"/> é
/// obrigatório para propriedades de coleção convertidas — sem ele o EF Core avisa e o change tracking
/// pode comparar por referência em vez de por valor.
/// </summary>
internal static class WidgetListJsonConversion
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static ValueConverter<IReadOnlyList<Widget>, string> Converter { get; } = new(
        widgets => JsonSerializer.Serialize(widgets, SerializerOptions),
        json => JsonSerializer.Deserialize<List<Widget>>(json, SerializerOptions) ?? new List<Widget>());

    public static ValueComparer<IReadOnlyList<Widget>> Comparer { get; } = new(
        (a, b) => (a ?? Array.Empty<Widget>()).SequenceEqual(b ?? Array.Empty<Widget>()),
        widgets => widgets.Aggregate(0, (hash, widget) => HashCode.Combine(hash, widget.GetHashCode())),
        widgets => widgets.ToList());
}
