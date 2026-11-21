using LogiCore.Domain;

namespace LogiCore.App;

internal sealed class ReportPrinter
{
    internal void Print(DeliveryService service)
    {
        var reports = new ReportService(service);
        Console.WriteLine("\n1 топ транспорта по выручке"); PrintRows(reports.TopVehicles(), "руб");
        Console.WriteLine("\n2 заказы по статусам"); PrintRows(reports.OrdersByStatus(), "руб");
        Console.WriteLine("\n3 средняя загрузка по типам транспорта"); PrintRows(reports.AverageLoads(), "%");
        Console.WriteLine("\n4 клиенты с суммой заказов более 1000 руб"); PrintRows(reports.CustomersAbove(1000), "руб");
        Console.WriteLine("\n5 груз → заказ → клиент");
        Console.WriteLine(reports.CargoOwners().ToReportTable(r => $"{r.Cargo} | {r.OrderId.ToString()[..8]} | {r.Customer}"));
        Console.WriteLine("\n6 класс опасности → число грузов");
        Console.WriteLine(reports.HazardCounts().OrderBy(p => p.Key).ToReportTable(p => $"класс {p.Key}: {p.Value}"));
        Console.WriteLine($"выручка: {service.Revenue:F2} руб");
    }
    static void PrintRows(IEnumerable<ReportRow> rows, string unit)
    {
        var text = rows.ToReportTable(r => $"{r.Name,-20} | {r.Count,3} | {r.Value,12:F2} {unit}");
        Console.WriteLine(text.Length == 0 ? "нет данных" : text);
    }
}
