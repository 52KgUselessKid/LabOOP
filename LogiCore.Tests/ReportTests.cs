using LogiCore.Domain;
using Xunit;

namespace LogiCore.Tests;

public class ReportTests
{
    [Fact]
    public void SixReportsHaveExpectedTotalsAndRelationships()
    {
        var (service, customer, truck) = TestData.Setup();
        var order = service.CreateOrder(customer, [new DangerousCargo("Реактив", 100, 1, 1000, 8)], TestData.Route(100));
        service.AssignTo(order, truck, new StandardTariff()); service.StartDelivery(order); service.Complete(order);
        var cancelled = TestData.NewOrder(service, customer); service.AssignCheapest(cancelled, new StandardTariff()); service.Cancel(cancelled);
        var reports = new ReportService(service);
        Assert.Equal(4600m, Assert.Single(reports.TopVehicles()).Value);
        Assert.Equal(2, reports.OrdersByStatus().Sum(r => r.Count));
        Assert.Equal(0.5m, Assert.Single(reports.AverageLoads()).Value);
        Assert.Equal(4600m, Assert.Single(reports.CustomersAbove(1000)).Value);
        Assert.Empty(reports.CustomersAbove(4600));
        Assert.Equal(2, reports.CargoOwners().Count());
        foreach (var row in reports.CargoOwners()) Assert.Equal(customer.Name, row.Customer);
        Assert.Equal(1, reports.HazardCounts()[8]);
    }
}
