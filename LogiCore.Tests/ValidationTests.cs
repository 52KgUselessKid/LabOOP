using LogiCore.Domain;
using Xunit;

namespace LogiCore.Tests;

public class ValidationTests
{
    readonly CargoCompatibilityValidator validator = new(new FixedClock());
    static PerishableCargo Food(decimal temperature = 4, int day = 20) =>
        new("Сыр", 10, 1, 1000, new DateOnly(2030, 5, day), temperature);
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    public void CargoRejectsNonPositiveDimensions(decimal weight, decimal volume) =>
        Assert.Throws<CargoValidationException>(() => TestData.Box(weight, volume));
    [Fact]
    public void DangerousAndPerishableCannotShareVehicle() =>
        Assert.Throws<IncompatibleCargoException>(() => validator.ValidateSet([Food(), new DangerousCargo("Кислота", 10, 1, 100, 8)]));
    [Fact]
    public void PerishableRequiresRefrigeratedVehicle() =>
        Assert.Throws<IncompatibleCargoException>(() => validator.ValidateForVehicle(new Truck("TR-100"), [Food()]));
    [Fact]
    public void TemperatureRangeIncludesItsEdges()
    {
        var truck = new RefrigeratorTruck("RF-100", -20, 8);
        validator.ValidateForVehicle(truck, [Food(-20)]);
        validator.ValidateForVehicle(truck, [Food(8)]);
        Assert.Throws<IncompatibleCargoException>(() => validator.ValidateForVehicle(truck, [Food(8.1m)]));
    }
    [Fact]
    public void SingleChamberRejectsDifferentRequiredTemperatures() =>
        Assert.Throws<IncompatibleCargoException>(() => validator.ValidateSet([Food(2), Food(4)]));
    [Fact]
    public void ExpiredFoodIsRejectedButExpirationDayIsAccepted()
    {
        Assert.Throws<CargoValidationException>(() => validator.Validate(Food(day: 19)));
        validator.Validate(Food(day: 20));
    }
    [Fact]
    public void TotalMassAndVolumeAreCheckedNotJustIndividualItems()
    {
        var drone = new DroneCourier("DR-100");
        Assert.Throws<VehicleOverloadException>(() => validator.ValidateForVehicle(drone, [TestData.Box(3, 0.01m), TestData.Box(3, 0.01m)]));
        Assert.Throws<VehicleOverloadException>(() => validator.ValidateForVehicle(drone, [TestData.Box(1, 0.05m), TestData.Box(1, 0.05m)]));
        validator.ValidateForVehicle(drone, [TestData.Box(2, 0.04m), TestData.Box(3, 0.04m)]);
    }
    [Fact]
    public void EmptyAndDuplicateCargoAreRejected()
    {
        Assert.Throws<CargoValidationException>(() => validator.ValidateSet([]));
        var cargo = TestData.Box();
        Assert.Throws<CargoValidationException>(() => validator.ValidateSet([cargo, cargo]));
    }
    sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2030, 5, 20, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
