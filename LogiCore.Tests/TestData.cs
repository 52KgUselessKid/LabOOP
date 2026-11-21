using LogiCore.Domain;
using Route = LogiCore.Domain.Route;

namespace LogiCore.Tests;

internal static class TestData
{
    internal static Route Route(decimal distance = 10) => new([new(0, 0), new((double)distance, 0)]);
    internal static StandardCargo Box(decimal weight = 10, decimal volume = 1) => new("Коробка", weight, volume, 1000);
    internal static (DeliveryService Service, Customer Customer, Truck Truck) Setup()
    {
        var service = new DeliveryService();
        var customer = new Customer("Тестовая мастерская", "test@example.org");
        var truck = new Truck("TR-100");
        service.RegisterCustomer(customer); service.RegisterVehicle(truck);
        return (service, customer, truck);
    }
    internal static Order NewOrder(DeliveryService service, Customer customer) => service.CreateOrder(customer, [Box()], Route());
}
