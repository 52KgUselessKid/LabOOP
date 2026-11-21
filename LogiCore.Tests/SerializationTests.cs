using System.Text.Json;
using System.Text.Json.Nodes;
using LogiCore.Domain;
using Xunit;

namespace LogiCore.Tests;

public sealed class SerializationTests : IDisposable
{
    readonly string directory = Path.Combine(Path.GetTempPath(), "logicore-tests-" + Guid.NewGuid().ToString("N"));
    string PathToFile => Path.Combine(directory, "state.json");
    public SerializationTests() => Directory.CreateDirectory(directory);
    [Fact]
    public void RoundTripRestoresExactTypesValuesIdsAndSharedReferences()
    {
        var state = CreateMixedState(); var store = new JsonStateStore(); store.Save(PathToFile, state);
        var json = File.ReadAllText(PathToFile);
        Assert.Contains("$vehicle", json); Assert.Contains("$cargo", json);
        var originalOrders = state.Orders.GetAll().ToArray();
        var restored = store.Load(PathToFile);
        Assert.Equal(state.Revenue, restored.Revenue);
        Assert.Equal(6, restored.Vehicles.GetAll().Count()); Assert.Single(restored.Customers.GetAll());
        foreach (var old in state.Vehicles.GetAll())
        {
            var copy = restored.Vehicles.GetById(old.Id)!;
            Assert.Equal(old.GetType(), copy.GetType()); Assert.Equal(old.State, copy.State);
            Assert.Equal(old.RegistrationNumber, copy.RegistrationNumber);
            if (old is RefrigeratorTruck fridge)
            {
                var copiedFridge = Assert.IsType<RefrigeratorTruck>(copy);
                Assert.Equal(fridge.MinTemperatureC, copiedFridge.MinTemperatureC);
                Assert.Equal(fridge.MaxTemperatureC, copiedFridge.MaxTemperatureC);
            }
        }
        foreach (var old in originalOrders)
        {
            var copy = restored.Orders.GetById(old.Id)!;
            Assert.Equal(old.Status, copy.Status); Assert.Equal(old.TotalCost, copy.TotalCost);
            Assert.Equal(old.CostSteps.ToArray(), copy.CostSteps.ToArray()); Assert.Equal(old.Route.Points.ToArray(), copy.Route.Points.ToArray());
            Assert.Same(restored.Customers.GetById(old.Customer.Id), copy.Customer);
            Assert.Contains(copy, copy.Customer.Orders);
            if (copy.AssignedVehicle is { } v) Assert.Same(restored.Vehicles.GetById(v.Id), v);
            foreach (var (cargo, other) in old.Cargo.Zip(copy.Cargo))
            {
                Assert.Equal(cargo.GetType(), other.GetType());
                Assert.Equal(JsonSerializer.Serialize<Cargo>(cargo), JsonSerializer.Serialize<Cargo>(other));
            }
        }
        var active = restored.Orders.GetAll().Single(o => o.Status == OrderStatus.InTransit);
        restored.Complete(active);
        Assert.Equal(state.Revenue + active.TotalCost, restored.Revenue);
        Assert.Equal(VehicleState.Free, active.AssignedVehicle!.State);
    }
    [Fact]
    public void ClearReallyRemovesAllStateBeforeReload()
    {
        var state = CreateMixedState(); var store = new JsonStateStore(); store.Save(PathToFile, state);
        var oldCustomer = state.Customers.GetAll().Single(); state.Clear();
        Assert.Empty(state.Vehicles.GetAll()); Assert.Empty(state.Customers.GetAll()); Assert.Empty(state.Orders.GetAll());
        Assert.Empty(oldCustomer.Orders); Assert.Equal(0, state.Revenue);
        var restored = store.Load(PathToFile); Assert.Equal(6, restored.Orders.GetAll().Count());
    }
    [Fact]
    public void MissingAndBrokenFilesBecomeStorageErrors()
    {
        var store = new JsonStateStore();
        Assert.Throws<StateStorageException>(() => store.Load(PathToFile));
        File.WriteAllText(PathToFile, "{broken"); Assert.Throws<StateStorageException>(() => store.Load(PathToFile));
        File.WriteAllText(PathToFile, "null"); Assert.Throws<StateStorageException>(() => store.Load(PathToFile));
    }
    [Fact]
    public void UnknownDiscriminatorIsRejected()
    {
        var store = new JsonStateStore(); store.Save(PathToFile, CreateMixedState());
        File.WriteAllText(PathToFile, File.ReadAllText(PathToFile).Replace("\"$cargo\": \"fragile\"", "\"$cargo\": \"future-cargo\""));
        var error = Assert.Throws<StateStorageException>(() => store.Load(PathToFile)); Assert.IsType<JsonException>(error.InnerException);
    }
    [Fact]
    public void BrokenReferencesAndContradictoryVehicleStatesAreRejected()
    {
        var store = new JsonStateStore(); store.Save(PathToFile, CreateMixedState());
        var node = JsonNode.Parse(File.ReadAllText(PathToFile))!;
        node["Orders"]![0]!["CustomerId"] = Guid.NewGuid();
        File.WriteAllText(PathToFile, node.ToJsonString()); Assert.Throws<StateStorageException>(() => store.Load(PathToFile));
        store.Save(PathToFile, CreateMixedState()); node = JsonNode.Parse(File.ReadAllText(PathToFile))!;
        node["Vehicles"]![0]!["State"] = "InTransit";
        File.WriteAllText(PathToFile, node.ToJsonString()); Assert.Throws<StateStorageException>(() => store.Load(PathToFile));
    }
    [Fact]
    public void SnapshotCanBeReplacedAndLeavesNoTemporaryFile()
    {
        var state = CreateMixedState(); var store = new JsonStateStore();
        store.Save(PathToFile, state); state.Clear(); store.Save(PathToFile, state);
        Assert.Empty(store.Load(PathToFile).Orders.GetAll());
        Assert.Single(Directory.GetFiles(directory));
    }
    static DeliveryService CreateMixedState()
    {
        var service = new DeliveryService(); var customer = new Customer("Цех", "warehouse@example.org");
        service.RegisterCustomer(customer);
        Vehicle[] fleet = [new Truck("TR-100"), new RefrigeratorTruck("RF-100", -12, 6), new CargoPlane("AP-100"),
            new CargoShip("SH-100"), new DroneCourier("DR-100"), new Truck("TR-101")];
        foreach (var vehicle in fleet) service.RegisterVehicle(vehicle);
        fleet[5].BeginMaintenance();
        Cargo[] cargo = [new FragileCargo("Стекло", 2, 0.01m, 1500, 1.7m),
            new PerishableCargo("Сыр", 4, 0.01m, 900, DateOnly.MaxValue, 3),
            new DangerousCargo("Краска", 2, 0.01m, 600, 3), new OversizedCargo("Рама", 200, 10, 15000), TestData.Box(1, 0.01m)];
        for (var i = 0; i < 5; i++)
        {
            var order = service.CreateOrder(customer, [cargo[i]], TestData.Route());
            service.AssignTo(order, fleet[i], new ExpressTariff(), new() { Insurance = true, Urgent = true, Packaging = true });
            if (i == 3) { service.Cancel(order); continue; }
            if (i != 2) service.StartDelivery(order);
            if (i is 0 or 4) service.Complete(order);
        }
        service.CreateOrder(customer, [TestData.Box()], TestData.Route());
        return service;
    }
    public void Dispose() => Directory.Delete(directory, recursive: true);
}
