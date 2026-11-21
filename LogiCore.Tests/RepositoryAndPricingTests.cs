using LogiCore.Domain;
using Xunit;

namespace LogiCore.Tests;

public class RepositoryAndPricingTests
{
    [Fact]
    public void RepositorySupportsAddRemoveSearchIndexAndIteration()
    {
        var repo = new Repository<Cargo>(); var first = TestData.Box(1); var second = TestData.Box(2);
        repo.Add(first); repo.Add(second);
        Assert.Same(first, repo[first.Id]); Assert.Same(second, repo.GetById(second.Id));
        Assert.Equal([first, second], repo.ToArray());
        Assert.Same(second, Assert.Single(repo.FindAll(c => c.WeightKg > 1)));
        Assert.True(repo.Remove(first.Id)); Assert.False(repo.Remove(first.Id));
        Assert.Null(repo.GetById(first.Id)); Assert.Throws<KeyNotFoundException>(() => repo[first.Id]);
        Assert.Equal(1, repo.Count);
    }
    [Fact]
    public void DuplicateRepositoryKeyDoesNotReplaceOriginal()
    {
        var repo = new Repository<Vehicle>(); var truck = new Truck("TR-100"); repo.Add(truck);
        Assert.Throws<ArgumentException>(() => repo.Add(new Truck("TR-101", truck.Id)));
        Assert.Same(truck, repo[truck.Id]);
    }
    [Fact]
    public void VarianceAssignmentsAreUsable()
    {
        var repo = new Repository<PerishableCargo>();
        repo.Add(new("Сыр", 1, 1, 100, DateOnly.MaxValue, 4));
        IReadOnlyRepository<Cargo> covariant = repo;
        IValidator<Cargo> common = new CargoCompatibilityValidator();
        IValidator<PerishableCargo> contravariant = common;
        contravariant.Validate(Assert.IsType<PerishableCargo>(Assert.Single(covariant.GetAll())));
    }
    [Fact]
    public void DecoratorOrderChangesWhichFeesAreMultiplied()
    {
        Cargo[] cargo = [new FragileCargo("Стекло", 1, 1, 1000, 2)];
        IDeliveryCost basis = new BaseDeliveryCost(100, new StandardTariff(), TestData.Route(), cargo);
        var chain = new PackagingCost(new UrgencyCost(new InsuranceCost(basis, cargo)), cargo);
        var reordered = new UrgencyCost(new PackagingCost(new InsuranceCost(basis, cargo), cargo));
        Assert.Equal(850m, chain.Total); // (100 + 20) * 1.25 + 700
        Assert.Equal(1025m, reordered.Total); // (100 + 20 + 700) * 1.25
        Assert.Equal(5, chain.Steps.Count); Assert.Contains("страхование", chain.Describe());
        Assert.Equal(chain.Total, new ServiceOptions { Insurance = true, Urgent = true, Packaging = true }.Apply(basis, cargo).Total);
    }
    [Fact]
    public void HeavyAndExpressTariffsUseConfiguredThresholds()
    {
        var heavy = new HeavyCargoTariff(); var route = TestData.Route();
        Assert.Equal(100m, heavy.Calculate(100, route, [TestData.Box(10_000)]));
        Assert.Equal(102m, heavy.Calculate(100, route, [TestData.Box(10_001)]));
        Assert.Equal(140m, new ExpressTariff().Calculate(100, route, [TestData.Box()]));
        Assert.Same(TariffSettings.Instance, TariffSettings.Instance);
    }
    [Fact]
    public void RouteUsesAllSegmentsAndCopiesInput()
    {
        var points = new[] { new RoutePoint(0, 0), new RoutePoint(3, 4), new RoutePoint(6, 4) };
        var route = new LogiCore.Domain.Route(points); points[1] = new(999, 999);
        Assert.Equal(8m, route.DistanceKm); Assert.Equal(5, new RoutePoint(0, 0) - new RoutePoint(3, 4));
        Assert.Equal(TimeSpan.FromHours(8d / 70), route.EstimateTime(new Truck("TR-100")));
        Assert.Contains("км", (string)new RoutePoint(1, 2));
        Assert.Throws<RouteNotFoundException>(() => new LogiCore.Domain.Route([new(0, 0)]));
    }
}
