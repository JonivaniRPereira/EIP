namespace EIP.Data.Warehouse.Domain;

/// <summary>docs/09-Data-Warehouse.md §5.2: "código e atributos monetários — dimensão de
/// referência". Mínima nesta fase (só código + nome, `docs/roadmap/fase-1-backlog.md` E5.1) — sem
/// taxa de câmbio própria (fora do escopo da Fase 1). Dado de referência compartilhado, sem RLS
/// (mesmo raciocínio de <see cref="DimDate"/>).</summary>
public sealed class DimCurrency
{
    public int CurrencyKey { get; private set; }

    public string Code { get; private set; }

    public string Name { get; private set; }

    private DimCurrency()
    {
        Code = string.Empty;
        Name = string.Empty;
    }

    private DimCurrency(string code, string name)
    {
        Code = code;
        Name = name;
    }

    public static DimCurrency Create(string code, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new DimCurrency(code, name);
    }
}
