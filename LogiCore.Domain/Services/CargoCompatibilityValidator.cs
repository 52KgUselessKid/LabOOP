namespace LogiCore.Domain;

/// <summary>провреяет срок годности, совместимость грузов и ограничения транспорта</summary>
public class CargoCompatibilityValidator(TimeProvider? timeProvider = null) : IValidator<Cargo>
{
    readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    /// <summary>сравнивает срок годности с сегодняшней датой</summary>
    public void Validate(Cargo item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is PerishableCargo p && p.ExpirationDate < DateOnly.FromDateTime(clock.GetLocalNow().DateTime))
            throw new CargoValidationException($"истёк срок годности: {item.Description}");
    }
    /// <summary>проверяет грузы до выбора транспорта</summary>
    public void ValidateSet(IReadOnlyCollection<Cargo> cargo)
    {
        if (cargo.Count == 0) throw new CargoValidationException("в заказе нет грузов");
        if (cargo.Select(c => c.Id).Distinct().Count() != cargo.Count)
            throw new CargoValidationException("один груз добавлен дважды");
        foreach (var item in cargo) Validate(item);
        if (cargo.Any(c => c is DangerousCargo) && cargo.Any(c => c is PerishableCargo))
            throw new IncompatibleCargoException("опасные и скоропортящиеся грузы нельзя везти вместе");
        if (cargo.OfType<ITemperatureSensitive>().Select(c => c.RequiredTemperatureC).Distinct().Count() > 1)
            throw new IncompatibleCargoException("однокамерный рефрижератор не поддерживает разные температуры одновременно");
    }
    /// <summary>проверяет, можно ли загрузить все грузы в этот транспорт</summary>
    public void ValidateForVehicle(Vehicle vehicle, IReadOnlyCollection<Cargo> cargo)
    {
        ValidateSet(cargo);
        if (IsOverloaded(vehicle, cargo)) throw new VehicleOverloadException($"перегруз {vehicle.RegistrationNumber}");
        if (!SupportsTemperature(vehicle, cargo) || cargo.Any(c => !vehicle.CanCarry(c)))
            throw new IncompatibleCargoException($"тс {vehicle.RegistrationNumber} не подходит для этой партии");
    }
    /// <summary>проверяет перегруз по общей массе и объёму</summary>
    public bool IsOverloaded(Vehicle vehicle, IReadOnlyCollection<Cargo> cargo) =>
        cargo.Sum(c => c.WeightKg) > vehicle.MaxLoadKg || cargo.Sum(c => c.VolumeM3) > vehicle.MaxVolumeM3;
    /// <summary>проверяет транспорт при подборе, неподходящий вариант возвращает false</summary>
    public bool CanLoad(Vehicle vehicle, IReadOnlyCollection<Cargo> cargo) =>
        !IsOverloaded(vehicle, cargo) && SupportsTemperature(vehicle, cargo) && cargo.All(vehicle.CanCarry);
    static bool SupportsTemperature(Vehicle vehicle, IReadOnlyCollection<Cargo> cargo) =>
        !cargo.Any(c => c is ITemperatureSensitive) ||
        (vehicle.Conditions & TransportConditions.Refrigerated) != 0 && cargo.All(vehicle.CanCarry);
}
