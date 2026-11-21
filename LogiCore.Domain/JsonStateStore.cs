using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogiCore.Domain;

/// <summary>сохраняет и загружает состояние системы в JSON</summary>
public class JsonStateStore
{
    readonly JsonSerializerOptions options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    /// <summary>сначала пишет во временный файл, затем заменяет сохранение</summary>
    public void Save(string path, DeliveryService service)
    {
        var snapshot = new StateSnapshot(1,
            service.Vehicles.GetAll().Select(v => new VehicleSnapshot(v, v.State)).ToArray(),
            service.Customers.GetAll().Select(c => new CustomerSnapshot(c.Id, c.Name, c.Contact)).ToArray(),
            service.Orders.GetAll().Select(o => new OrderSnapshot(o.Id, o.Customer.Id, o.Cargo.ToArray(),
                o.Route.Points.ToArray(), o.AssignedVehicle?.Id, o.Status, o.TotalCost, o.CostSteps.ToArray())).ToArray());
        var fullPath = Path.GetFullPath(path);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using (var stream = File.Create(temporary)) JsonSerializer.Serialize(stream, snapshot, options);
            File.Move(temporary, fullPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new StateStorageException("не удалось сохранить состояние", ex); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    /// <summary>загружает данные в новый диспетчер, при ошибке текущее состояние остаётся на месте</summary>
    public DeliveryService Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var snapshot = JsonSerializer.Deserialize<StateSnapshot>(stream, options)
                ?? throw new StateStorageException("пустой снимок");
            return Restore(snapshot);
        }
        catch (StateStorageException) { throw; } // throw без переменной сохраняет место, где возникла ошибка
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or
            NotSupportedException or ArgumentException or InvalidOperationException or NullReferenceException or LogisticsException)
        { throw new StateStorageException("снимок отсутствует, повреждён или содержит неизвестный тип", ex); }
    }
    static DeliveryService Restore(StateSnapshot snapshot)
    {
        if (snapshot.Version != 1) throw new StateStorageException("неизвестная версия снимка");
        var service = new DeliveryService();
        foreach (var row in snapshot.Vehicles)
        {
            if (row.Vehicle.Id == Guid.Empty || !Enum.IsDefined(row.State)) throw new StateStorageException("неверный транспорт в снимке");
            service.RegisterVehicle(row.Vehicle);
        }
        foreach (var row in snapshot.Customers)
        {
            if (row.Id == Guid.Empty) throw new StateStorageException("пустой Id клиента");
            service.RegisterCustomer(new(row.Name, row.Contact, row.Id));
        }
        var cargoIds = new HashSet<Guid>();
        foreach (var row in snapshot.Orders)
        {
            if (row.Id == Guid.Empty || row.Cargo.Length == 0 || row.Cargo.Any(c => !cargoIds.Add(c.Id)))
                throw new StateStorageException("пустая партия или повторные Id грузов");
            var customer = service.Customers.GetById(row.CustomerId) ?? throw new StateStorageException("клиент не найден");
            var vehicle = row.VehicleId is { } id
                ? service.Vehicles.GetById(id) ?? throw new StateStorageException("транспорт не найден") : null;
            var order = new Order(customer, row.Cargo, new Route(row.Points), row.Id);
            order.Restore(vehicle, row.Status, row.TotalCost, row.CostSteps);
            service.RestoreOrder(order);
        }
        foreach (var row in snapshot.Vehicles)
        {
            var vehicle = service.Vehicles.GetById(row.Vehicle.Id)!;
            if (row.State == VehicleState.UnderMaintenance) vehicle.BeginMaintenance();
            if (vehicle.State != row.State) throw new StateStorageException("состояние ТС противоречит заказам");
        }
        return service;
    }
    sealed record StateSnapshot(int Version, VehicleSnapshot[] Vehicles, CustomerSnapshot[] Customers, OrderSnapshot[] Orders);
    sealed record VehicleSnapshot(Vehicle Vehicle, VehicleState State);
    sealed record CustomerSnapshot(Guid Id, string Name, string Contact);
    sealed record OrderSnapshot(Guid Id, Guid CustomerId, Cargo[] Cargo, RoutePoint[] Points,
        Guid? VehicleId, OrderStatus Status, decimal TotalCost, string[] CostSteps);
}
