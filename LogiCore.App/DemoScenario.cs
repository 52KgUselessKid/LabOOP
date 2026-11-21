using LogiCore.Domain;
using Route = LogiCore.Domain.Route;

namespace LogiCore.App;

internal sealed class DemoScenario
{
    internal void Run(DeliveryService service)
    {
        Console.WriteLine("logiCore - сборные отправки мастерских и небольших производств\n");
        VehicleFactory[] factories = [new TruckFactory(), new TruckFactory(), new RefrigeratorFactory(),
            new PlaneFactory(), new ShipFactory(), new DroneFactory()];
        string[] registrations = ["TR-101", "TR-102", "RF-201", "AP-301", "SH-401", "DR-501"];
        for (var i = 0; i < factories.Length; i++) service.RegisterVehicle(factories[i].Create(registrations[i]));
        var workshop = new Customer("мастерская «Медный лист»", "dispatch@medlist.example");
        var dairy = new Customer("сыроварня «Три луга»", "orders@triluga.example");
        var laboratory = new Customer("лаборатория покрытий", "lab@coating.example");
        foreach (var customer in new[] { workshop, dairy, laboratory }) service.RegisterCustomer(customer);
        var tomorrow = DateOnly.FromDateTime(DateTime.Today).AddDays(7);
        Cargo[] cargo = [
            new CargoFactory<StandardCargo>(() => new("латунные заготовки", 180, 0.7m, 25_000)).Create(),
            new CargoFactory<FragileCargo>(() => new("витраж для кафе", 24, 1.1m, 60_000, 1.8m)).Create(),
            new CargoFactory<PerishableCargo>(() => new("молодой сыр", 90, 0.4m, 18_000, tomorrow, 4)).Create(),
            new CargoFactory<PerishableCargo>(() => new("закваска", 8, 0.05m, 8_000, tomorrow, 4)).Create(),
            new CargoFactory<DangerousCargo>(() => new("растворитель", 75, 0.3m, 9_000, 3)).Create(),
            new CargoFactory<DangerousCargo>(() => new("реактив для покрытия", 40, 0.2m, 12_000, 8)).Create(),
            new CargoFactory<OversizedCargo>(() => new("рама сушильной камеры", 4500, 90, 400_000)).Create(),
            new CargoFactory<OversizedCargo>(() => new("воздуховод", 800, 20, 70_000)).Create(),
            new CargoFactory<StandardCargo>(() => new("фурнитура", 30, 0.2m, 6_000)).Create(),
            new CargoFactory<FragileCargo>(() => new("керамические ручки", 12, 0.15m, 15_000)).Create(),
            new CargoFactory<StandardCargo>(() => new("пробники ткани", 1, 0.01m, 900)).Create(),
            new CargoFactory<StandardCargo>(() => new("чертежи", 0.5m, 0.01m, 500)).Create()
        ];
        Console.WriteLine($"фабрики создали {service.Vehicles.GetAll().Count()} ТС и {cargo.Length} грузов всех пяти видов");
        var road = new Route([new(0, 0), new(90, 120), new(150, 120)]);
        Console.WriteLine($"маршрут: {road.DistanceKm} км, первая точка {(string)road.Points[0]}");
        var refrigerator = service.Vehicles.GetAll().OfType<RefrigeratorTruck>().Single();
        var flags = refrigerator.Conditions | TransportConditions.LongRange;
        Console.WriteLine($"flags: {flags}; холод: {(flags & TransportConditions.Refrigerated) != 0}; без LongRange: {flags & ~TransportConditions.LongRange}");
        IReadOnlyRepository<Vehicle> fleet = service.Vehicles;
        IReadOnlyRepository<IEntity> entities = fleet;
        IValidator<Cargo> generalValidator = new CargoCompatibilityValidator();
        IValidator<PerishableCargo> foodValidator = generalValidator;
        foodValidator.Validate((PerishableCargo)cargo[2]);
        Console.WriteLine($"ковариантный репозиторий: {entities.GetAll().Count()} объектов; контравариантный валидатор проверил сыр");
        try { service.CreateOrder(dairy, [cargo[2], cargo[4]], road); }
        catch (IncompatibleCargoException ex) { Console.WriteLine($"ожидаемый отказ: {ex.Message}"); }

        var orders = new[] {
            service.CreateOrder(workshop, [cargo[0], cargo[1]], road),
            service.CreateOrder(dairy, [cargo[2], cargo[3]], road),
            service.CreateOrder(laboratory, [cargo[4], cargo[5]], road),
            service.CreateOrder(workshop, [cargo[6], cargo[7]], road),
            service.CreateOrder(workshop, [cargo[8], cargo[9]], road),
            service.CreateOrder(workshop, [cargo[10], cargo[11]], new Route([new(0, 0), new(6, 8)]))
        };
        var drone = service.Vehicles.GetAll().OfType<DroneCourier>().Single();
        try { service.AssignTo(orders[0], drone, new StandardTariff()); }
        catch (VehicleOverloadException ex) when (orders[0].Status == OrderStatus.Created)
        { Console.WriteLine($"попытка отклонена, заказ остался Created: {ex.Message}"); }

        for (var i = 0; i < orders.Length; i++)
        {
            ITariffStrategy tariff = i == 3 ? new HeavyCargoTariff() : i == 0 ? new ExpressTariff() : new StandardTariff();
            service.AssignCheapest(orders[i], tariff, new ServiceOptions { Insurance = true, Urgent = i == 0, Packaging = true });
            Console.WriteLine($"\nзаказ {i + 1}: {orders[i].AssignedVehicle}, {orders[i].Route.EstimateTime(orders[i].AssignedVehicle!)} в движении");
            Console.WriteLine(string.Join(Environment.NewLine, orders[i].CostSteps));
            if (i == 4) { service.Cancel(orders[i]); continue; }
            service.StartDelivery(orders[i]);
            if (i < 4) service.Complete(orders[i]); // шестой заказ завершим отдельно ниже
        }
        try { service.Cancel(orders[0]); }
        catch (InvalidOrderStateException ex) { Console.WriteLine($"отмена отклонена: {ex.Message}"); }

        var count = 0;
        LogisticsEventHandler<OrderStatusChangedEventArgs> temporary = (_, _) => count++;
        service.OrderStatusChanged += temporary;
        service.Complete(orders[5]);
        service.OrderStatusChanged -= temporary;
        // проверяем счётчик после отписки
        var extra = service.CreateOrder(laboratory,
            [new CargoFactory<StandardCargo>(() => new("контрольный образец", 2, 0.01m, 1200)).Create()], road);
        service.AssignCheapest(extra, new StandardTariff()); service.StartDelivery(extra);
        Console.WriteLine($"временный подписчик получил {count} событие; после отписки счётчик остался {count}");
    }
}
