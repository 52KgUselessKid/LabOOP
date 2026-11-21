using LogiCore.Domain;
using Xunit;

namespace LogiCore.Tests;

public class VehicleTests
{
    [Theory]
    [InlineData("truck", 460)]
    [InlineData("refrigerator", 660)]
    [InlineData("plane", 2620)]
    [InlineData("ship", 150)]
    [InlineData("drone", 130)]
    public void EachVehicleUsesItsOwnFormula(string kind, decimal expected)
    {
        var vehicle = Create(kind);
        Assert.Equal(expected, vehicle.CalculateDeliveryCost(TestData.Route(), [TestData.Box()]));
    }
    [Theory]
    [InlineData("truck", 0)]
    [InlineData("refrigerator", 0)]
    [InlineData("plane", 120)]
    [InlineData("ship", 0)]
    [InlineData("drone", 50)]
    public void ZeroDistanceKeepsOnlyMassCharges(string kind, decimal expected)
    {
        Assert.Equal(expected, Create(kind).CalculateDeliveryCost(TestData.Route(0), [TestData.Box()]));
    }
    [Theory]
    [InlineData("truck", 460)]
    [InlineData("refrigerator", 660)]
    [InlineData("plane", 482500)]
    [InlineData("ship", 150)]
    [InlineData("drone", 105)]
    public void CapacityBoundaryIsInclusive(string kind, decimal expectedCost)
    {
        var vehicle = Create(kind);
        var load = TestData.Box(vehicle.MaxLoadKg, vehicle.MaxVolumeM3);
        Assert.True(vehicle.CanCarry(load));
        Assert.False(vehicle.CanCarry(TestData.Box(vehicle.MaxLoadKg + 0.01m, vehicle.MaxVolumeM3)));
        Assert.False(vehicle.CanCarry(TestData.Box(vehicle.MaxLoadKg, vehicle.MaxVolumeM3 + 0.001m)));
        Assert.Equal(expectedCost, vehicle.CalculateDeliveryCost(TestData.Route(), [load]));
    }
    [Fact]
    public void ShipAddsFeeForEachOversizedItemEvenAtZeroDistance()
    {
        Cargo[] cargo = [new OversizedCargo("Рама", 100, 2, 1000), new OversizedCargo("Труба", 20, 1, 500)];
        Assert.Equal(7000m, new CargoShip("SH-100").CalculateDeliveryCost(TestData.Route(0), cargo));
    }
    [Fact]
    public void PlaneRejectsRestrictedHazardClasses()
    {
        var plane = new CargoPlane("AP-100");
        Assert.False(plane.CanCarry(new DangerousCargo("Взрывчатое", 1, 1, 1, 1)));
        Assert.False(plane.CanCarry(new DangerousCargo("Радиоактивное", 1, 1, 1, 7)));
        Assert.True(plane.CanCarry(new DangerousCargo("Коррозионное", 1, 1, 1, 8)));
    }
    [Fact]
    public void DroneChecksRangeIncludingBoundary()
    {
        var drone = new DroneCourier("DR-100");
        Assert.True(drone.CanTravel(TestData.Route(25)));
        Assert.False(drone.CanTravel(TestData.Route(25.01m)));
    }
    [Fact]
    public void RegistrationAndIdentityAreValidated()
    {
        Assert.Throws<ArgumentException>(() => new Truck("ошибка"));
        var id = Guid.NewGuid(); var first = new Truck("TR-100", id); var second = new Truck("TR-101", id);
        Assert.Equal(first, second); Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Single(new HashSet<Vehicle> { first, second });
    }
    [Fact]
    public void MaintenanceBlocksReservation()
    {
        var (service, customer, truck) = TestData.Setup(); truck.BeginMaintenance();
        Assert.Throws<RouteNotFoundException>(() => service.AssignCheapest(TestData.NewOrder(service, customer), new StandardTariff()));
        truck.EndMaintenance(); Assert.Equal(VehicleState.Free, truck.State);
    }
    static Vehicle Create(string kind) => kind switch
    {
        "truck" => new Truck("TR-100"), "refrigerator" => new RefrigeratorTruck("RF-100"),
        "plane" => new CargoPlane("AP-100"), "ship" => new CargoShip("SH-100"), _ => new DroneCourier("DR-100")
    };
}
