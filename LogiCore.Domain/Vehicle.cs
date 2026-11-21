using System.Text.RegularExpressions;
using System.Text.Json.Serialization;

namespace LogiCore.Domain;

/// <summary>состояние транспорта в парке</summary>
public enum VehicleState
{
    /// <summary>свободен</summary>
    Free,
    /// <summary>занят заказом, транспорт назначен или уже в пути</summary>
    InTransit,
    /// <summary>на обслуживании</summary>
    UnderMaintenance
}
/// <summary>условия перевозки, несколько значений можно объединить через |</summary>
[Flags]
public enum TransportConditions
{
    /// <summary>особых условий нет</summary>
    None = 0,
    /// <summary>холодильная установка</summary>
    Refrigerated = 1,
    /// <summary>герметичность</summary>
    Sealed = 2,
    /// <summary>поддержание давления</summary>
    Pressurized = 4,
    /// <summary>большая дальность</summary>
    LongRange = 8
}
/// <summary>общий класс транспорта, объекты сравниваются по Id</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$vehicle")]
[JsonDerivedType(typeof(Truck), "truck")]
[JsonDerivedType(typeof(RefrigeratorTruck), "refrigerator")]
[JsonDerivedType(typeof(CargoPlane), "plane")]
[JsonDerivedType(typeof(CargoShip), "ship")]
[JsonDerivedType(typeof(DroneCourier), "drone")]
public abstract class Vehicle : IEntity
{
    /// <inheritdoc />
    public Guid Id { get; }
    /// <summary>номер транспорта, например TR-101</summary>
    public string RegistrationNumber { get; }
    /// <summary>предельная масса, кг</summary>
    public decimal MaxLoadKg { get; }
    /// <summary>полезный объём, м³</summary>
    public decimal MaxVolumeM3 { get; }
    /// <summary>средняя скорость, км/ч</summary>
    public decimal AverageSpeedKmH { get; }
    /// <summary>стоимость километра в рублях</summary>
    public decimal BaseRatePerKm { get; }
    /// <summary>состояние транспорта, меняется через методы</summary>
    public VehicleState State { get; private set; }
    /// <summary>условия перевозки</summary>
    public virtual TransportConditions Conditions => TransportConditions.None;
    /// <summary>создаёт транспорт и проверяет его характеристики</summary>
    protected Vehicle(string registrationNumber, decimal maxLoadKg, decimal maxVolumeM3,
        decimal averageSpeedKmH, decimal baseRatePerKm, Guid id)
    {
        if (!Regex.IsMatch(registrationNumber ?? "", "^[A-Z]{2}-[0-9]{3}$"))
            throw new ArgumentException("регистрационный номер должен иметь вид AA-000", nameof(registrationNumber));
        RegistrationNumber = registrationNumber!;
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        MaxLoadKg = Guard.Positive(maxLoadKg, nameof(maxLoadKg));
        MaxVolumeM3 = Guard.Positive(maxVolumeM3, nameof(maxVolumeM3));
        AverageSpeedKmH = Guard.Positive(averageSpeedKmH, nameof(averageSpeedKmH));
        BaseRatePerKm = Guard.Positive(baseRatePerKm, nameof(baseRatePerKm));
    }
    /// <summary>проверяет, помещается ли один груз по массе и объёму</summary>
    public virtual bool CanCarry(Cargo cargo) => cargo.WeightKg <= MaxLoadKg && cargo.VolumeM3 <= MaxVolumeM3;
    /// <summary>проверяет, хватает ли дальности на маршрут</summary>
    public virtual bool CanTravel(Route route) => true;
    /// <summary>расчёт стоимости конкретного транспорта без тарифа и услуг</summary>
    public abstract decimal CalculateDeliveryCost(Route route, IReadOnlyCollection<Cargo> cargo);
    /// <summary>переводит свободное ТС на обслуживание</summary>
    public void BeginMaintenance()
    {
        if (State != VehicleState.Free) throw new InvalidOrderStateException("обслуживание возможно только для свободного ТС");
        State = VehicleState.UnderMaintenance;
    }
    /// <summary>завершает обслуживание</summary>
    public void EndMaintenance()
    {
        if (State != VehicleState.UnderMaintenance) throw new InvalidOrderStateException("тс не находится на обслуживании");
        State = VehicleState.Free;
    }
    internal void Reserve()
    {
        if (State != VehicleState.Free) throw new InvalidOrderStateException("тс уже занято");
        State = VehicleState.InTransit;
    }
    internal void Release() => State = VehicleState.Free;
    /// <inheritdoc />
    public override string ToString() => $"{GetType().Name} {RegistrationNumber} ({State})";
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Vehicle vehicle && vehicle.Id == Id;
    /// <inheritdoc />
    public override int GetHashCode() => Id.GetHashCode();
}
/// <summary>фура, в стоимость включён коэффициент платных дорог</summary>
public class Truck(string registrationNumber, Guid id = default) : Vehicle(registrationNumber, 20_000, 80, 70, 40, id)
{
    /// <summary>коэффициент дорожных сборов</summary>
    public decimal TollFactor => 1.15m;
    /// <inheritdoc />
    public override decimal CalculateDeliveryCost(Route route, IReadOnlyCollection<Cargo> cargo) => route.DistanceKm * BaseRatePerKm * TollFactor;
}
/// <summary>рефрижератор с заданным диапазоном температур</summary>
public class RefrigeratorTruck : Vehicle
{
    const decimal CoolingFactor = 1.2m;
    /// <summary>нижняя граница диапазона, °C</summary>
    public decimal MinTemperatureC { get; }
    /// <summary>верхняя граница диапазона, °C</summary>
    public decimal MaxTemperatureC { get; }
    /// <summary>создаёт рефрижератор и проверяет диапазон</summary>
    public RefrigeratorTruck(string registrationNumber, decimal minTemperatureC = -20, decimal maxTemperatureC = 8, Guid id = default)
        : base(registrationNumber, 12_000, 50, 65, 55, id)
    {
        if (minTemperatureC > maxTemperatureC) throw new ArgumentException("температурный диапазон перевёрнут");
        MinTemperatureC = minTemperatureC; MaxTemperatureC = maxTemperatureC;
    }
    /// <inheritdoc />
    public override TransportConditions Conditions => TransportConditions.Refrigerated | TransportConditions.Sealed;
    /// <inheritdoc />
    public override bool CanCarry(Cargo cargo) => base.CanCarry(cargo) &&
        (cargo is not ITemperatureSensitive t || t.RequiredTemperatureC >= MinTemperatureC && t.RequiredTemperatureC <= MaxTemperatureC);
    /// <inheritdoc />
    public override decimal CalculateDeliveryCost(Route route, IReadOnlyCollection<Cargo> cargo) => route.DistanceKm * BaseRatePerKm * CoolingFactor;
}
/// <summary>самолёт, доплата зависит от массы, опасные грузы классов 1 и 7 запрещены</summary>
public class CargoPlane(string registrationNumber, Guid id = default) : Vehicle(registrationNumber, 40_000, 150, 750, 250, id)
{
    const decimal WeightSurchargePerKg = 12m;
    /// <inheritdoc />
    public override TransportConditions Conditions => TransportConditions.Pressurized | TransportConditions.LongRange;
    /// <inheritdoc />
    public override bool CanCarry(Cargo cargo) => base.CanCarry(cargo) && cargo is not DangerousCargo { HazardClass: 1 or 7 };
    /// <inheritdoc />
    public override decimal CalculateDeliveryCost(Route route, IReadOnlyCollection<Cargo> cargo) =>
        route.DistanceKm * BaseRatePerKm + cargo.Sum(c => c.WeightKg) * WeightSurchargePerKg;
}
/// <summary>судно, за негабарит добавляется плата за крепление</summary>
public class CargoShip(string registrationNumber, Guid id = default) : Vehicle(registrationNumber, 500_000, 2000, 30, 15, id)
{
    const decimal OversizedSecuringFee = 3500m;
    /// <inheritdoc />
    public override TransportConditions Conditions => TransportConditions.LongRange;
    /// <inheritdoc />
    public override decimal CalculateDeliveryCost(Route route, IReadOnlyCollection<Cargo> cargo) =>
        route.DistanceKm * BaseRatePerKm + cargo.OfType<OversizedCargo>().Count() * OversizedSecuringFee;
}
/// <summary>курьерский дрон, наследоваться от него нельзя</summary>
public sealed class DroneCourier(string registrationNumber, Guid id = default) : Vehicle(registrationNumber, 5, 0.08m, 45, 8, id)
{
    const decimal HandlingPerKg = 5m;
    /// <summary>дальность в одну сторону, км</summary>
    public decimal MaxRangeKm => 25;
    /// <inheritdoc />
    public override bool CanCarry(Cargo cargo) => base.CanCarry(cargo) && cargo is not DangerousCargo and not OversizedCargo;
    /// <inheritdoc />
    public override bool CanTravel(Route route) => route.DistanceKm <= MaxRangeKm;
    /// <inheritdoc />
    public override decimal CalculateDeliveryCost(Route route, IReadOnlyCollection<Cargo> cargo) =>
        route.DistanceKm * BaseRatePerKm + cargo.Sum(c => c.WeightKg) * HandlingPerKg;
}
