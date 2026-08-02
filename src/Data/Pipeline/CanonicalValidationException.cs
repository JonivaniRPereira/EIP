namespace EIP.Data.Pipeline;

/// <summary>Sinaliza que um registro específico falhou uma regra de validação/mapeamento
/// (docs/04-Modelo-Canonico.md §8.1) — capturada por registro, nunca aborta o lote inteiro. O
/// registro correspondente vai para quarentena com <see cref="Rule"/> como o nome da regra que
/// falhou.</summary>
public sealed class CanonicalValidationException : Exception
{
    public string Rule { get; }

    public CanonicalValidationException(string rule, string message)
        : base(message)
    {
        Rule = rule;
    }
}
