using LogiCore.Domain;
using Xunit;

namespace LogiCore.Tests;

public class OrderAndServiceTests
{
    [Fact]
    public void FullLifecycleFreesVehicleAndRecognizesRevenueOnce()
    {
        var (service, customer, truck) = TestData.Setup(); var order = TestData.NewOrder(service, customer);
        service.AssignCheapest(order, new StandardTariff());
        Assert.Equal(OrderStatus.Assigned, order.Status); Assert.Equal(VehicleState.InTransit, truck.State);
        Assert.Equal(0, service.Revenue);
        service.StartDelivery(order); Assert.Equal(OrderStatus.InTransit, order.Status);
        service.Complete(order);
        Assert.Equal(OrderStatus.Delivered, order.Status); Assert.Equal(VehicleState.Free, truck.State);
        Assert.Equal(order.TotalCost, service.Revenue);
        Assert.Throws<InvalidOrderStateException>(() => service.Complete(order));
        Assert.Equal(order.TotalCost, service.Revenue);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationBeforeDepartureReleasesReservation(bool assign)
    {
        var (service, customer, truck) = TestData.Setup(); var order = TestData.NewOrder(service, customer);
        if (assign) service.AssignCheapest(order, new StandardTariff());
        service.Cancel(order); Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Equal(VehicleState.Free, truck.State); Assert.Equal(0, service.Revenue);
        Assert.Throws<InvalidOrderStateException>(() => service.Cancel(order));
    }
    [Fact]
    public void IllegalTransitionsAreRejected()
    {
        var (service, customer, _) = TestData.Setup(); var order = TestData.NewOrder(service, customer);
        Assert.Throws<InvalidOrderStateException>(() => service.StartDelivery(order));
        Assert.Throws<InvalidOrderStateException>(() => service.Complete(order));
        service.AssignCheapest(order, new StandardTariff());
        Assert.Throws<InvalidOrderStateException>(() => service.AssignCheapest(order, new StandardTariff()));
        service.StartDelivery(order);
        Assert.Throws<InvalidOrderStateException>(() => service.Cancel(order));
        Assert.Throws<InvalidOrderStateException>(() => service.StartDelivery(order));
        service.Complete(order);
        Assert.Throws<InvalidOrderStateException>(() => service.Cancel(order));
    }
    [Fact]
    public void AllEventsAndUnsubscriptionWork()
    {
        var (service, customer, truck) = TestData.Setup();
        var created = 0; var statuses = 0; var completed = 0; var overload = 0;
        LogisticsEventHandler<OrderCreatedEventArgs> handler = (_, _) => created++;
        service.OrderCreated += handler;
        service.OrderStatusChanged += (_, _) => statuses++;
        service.DeliveryCompleted += (_, e) => { completed++; Assert.Equal(e.Order.TotalCost, e.Revenue); };
        service.VehicleOverloadAttempt += (_, e) => { overload++; Assert.Equal(10m, e.WeightKg); };
        var order = TestData.NewOrder(service, customer);
        var drone = new DroneCourier("DR-100"); service.RegisterVehicle(drone);
        Assert.Throws<VehicleOverloadException>(() => service.AssignTo(order, drone, new StandardTariff()));
        Assert.Equal(OrderStatus.Created, order.Status); Assert.Equal(VehicleState.Free, drone.State);
        service.AssignTo(order, truck, new StandardTariff()); service.StartDelivery(order); service.Complete(order);
        service.OrderCreated -= handler; TestData.NewOrder(service, customer);
        Assert.Equal(1, created); Assert.Equal(3, statuses); Assert.Equal(1, completed); Assert.Equal(1, overload);
    }
    [Fact]
    public void DispatcherSelectsCheapestSuitableFreeVehicle()
    {
        var (service, customer, _) = TestData.Setup(); var ship = new CargoShip("SH-100");
        service.RegisterVehicle(ship); service.RegisterVehicle(new CargoPlane("AP-100"));
        var order = TestData.NewOrder(service, customer); service.AssignCheapest(order, new StandardTariff());
        Assert.Same(ship, order.AssignedVehicle);
        var next = TestData.NewOrder(service, customer); service.AssignCheapest(next, new StandardTariff());
        Assert.IsType<Truck>(next.AssignedVehicle);
    }
    [Fact]
    public void OnePhysicalCargoCannotBelongToTwoOrders()
    {
        var (service, customer, _) = TestData.Setup(); var cargo = TestData.Box();
        service.CreateOrder(customer, [cargo], TestData.Route());
        Assert.Throws<CargoValidationException>(() => service.CreateOrder(customer, [cargo], TestData.Route()));
        Assert.Single(customer.Orders);
    }
    [Fact]
    public void ClientHistoryAndCargoCollectionsCannotBeModifiedExternally()
    {
        var (service, customer, _) = TestData.Setup(); var cargo = new List<Cargo> { TestData.Box() };
        var order = service.CreateOrder(customer, cargo, TestData.Route()); cargo.Clear();
        Assert.Single(order.Cargo); Assert.Single(customer.Orders);
        Assert.Throws<NotSupportedException>(() => ((ICollection<Order>)customer.Orders).Clear());
    }
}
