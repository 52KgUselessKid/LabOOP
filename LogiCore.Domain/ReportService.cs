namespace LogiCore.Domain;

/// <summary>строка отчёта с количеством и суммой</summary>
public sealed record ReportRow(string Name, int Count, decimal Value);
/// <summary>строка отчёта о грузе, его заказе и клиенте</summary>
public sealed record CargoOwnerRow(string Cargo, Guid OrderId, string Customer);
/// <summary>шесть отчётов по данным диспетчера</summary>
public class ReportService(DeliveryService service)
{
    /// <summary>три транспорта с самой большой выручкой от доставленных заказов</summary>
    public IEnumerable<ReportRow> TopVehicles() => service.Orders.GetAll()
        .Where(o => o.Status == OrderStatus.Delivered)
        .GroupBy(o => o.AssignedVehicle!)
        .Select(g => new ReportRow(g.Key.RegistrationNumber, g.Count(), g.Sum(o => o.TotalCost)))
        .OrderByDescending(r => r.Value).Take(3);
    /// <summary>количество и стоимость заказов по статусам</summary>
    public IEnumerable<ReportRow> OrdersByStatus() => service.Orders.GetAll().GroupBy(o => o.Status)
        .Select(g => new ReportRow(g.Key.ToString(), g.Count(), g.Sum(o => o.TotalCost))).OrderBy(r => r.Name);
    /// <summary>средняя загрузка по типам транспорта, без отменённых заказов</summary>
    public IEnumerable<ReportRow> AverageLoads() => service.Orders.GetAll().Where(o => o.AssignedVehicle != null && o.Status != OrderStatus.Cancelled)
        .GroupBy(o => o.AssignedVehicle!.GetType().Name)
        .Select(g => new ReportRow(g.Key, g.Count(), g.Average(o => o.Cargo.Sum(c => c.WeightKg) / o.AssignedVehicle!.MaxLoadKg * 100)));
    /// <summary>клиенты с суммой заказов выше порога, здесь используется from и select</summary>
    public IEnumerable<ReportRow> CustomersAbove(decimal threshold) =>
        from customer in service.Customers.GetAll()
        let active = customer.Orders.Where(o => o.Status != OrderStatus.Cancelled).ToArray()
        let total = active.Sum(o => o.TotalCost)
        where total > threshold
        orderby total descending
        select new ReportRow(customer.Name, active.Length, total);
    /// <summary>соединяет грузы, заказы и клиентов через Join</summary>
    public IEnumerable<CargoOwnerRow> CargoOwners() => service.Orders.GetAll()
        .SelectMany(o => o.Cargo.Select(c => new { Cargo = c, OrderId = o.Id }))
        .Join(service.Orders.GetAll(), item => item.OrderId, o => o.Id, (item, o) => new { item.Cargo, Order = o })
        .Join(service.Customers.GetAll(), item => item.Order.Customer.Id, c => c.Id,
            (item, c) => new CargoOwnerRow(item.Cargo.Description, item.Order.Id, c.Name));
    /// <summary>количество опасных грузов каждого класса</summary>
    public IReadOnlyDictionary<int, int> HazardCounts() => service.Orders.GetAll().SelectMany(o => o.Cargo)
        .OfType<DangerousCargo>().GroupBy(c => c.HazardClass).ToDictionary(g => g.Key, g => g.Count());
}
