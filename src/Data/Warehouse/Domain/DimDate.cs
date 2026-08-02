namespace EIP.Data.Warehouse.Domain;

/// <summary>
/// docs/09-Data-Warehouse.md §5.1/§5.2 — dimensão de referência pré-gerada e compartilhada
/// logicamente entre tenants (não é dado de negócio de nenhum tenant específico); por isso nunca tem
/// RLS (docs/09 §5.1: "chave inteira YYYYMMDD"). <see cref="DateKey"/> não é identity — é
/// determinístico a partir de <see cref="Date"/>, para que fatos referenciem uma data sem depender
/// da ordem em que o calendário foi gerado.
/// </summary>
public sealed class DimDate
{
    public int DateKey { get; private set; }

    public DateOnly Date { get; private set; }

    public int Year { get; private set; }

    public int Quarter { get; private set; }

    public int Month { get; private set; }

    public int Day { get; private set; }

    public int DayOfWeek { get; private set; }

    public bool IsWeekend { get; private set; }

    private DimDate()
    {
    }

    private DimDate(int dateKey, DateOnly date, int year, int quarter, int month, int day, int dayOfWeek, bool isWeekend)
    {
        DateKey = dateKey;
        Date = date;
        Year = year;
        Quarter = quarter;
        Month = month;
        Day = day;
        DayOfWeek = dayOfWeek;
        IsWeekend = isWeekend;
    }

    public static DimDate FromDate(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        return new DimDate(
            ToDateKey(date),
            date,
            date.Year,
            (date.Month - 1) / 3 + 1,
            date.Month,
            date.Day,
            dayOfWeek,
            dayOfWeek is 0 or 6);
    }

    public static int ToDateKey(DateOnly date) => (date.Year * 10000) + (date.Month * 100) + date.Day;
}
