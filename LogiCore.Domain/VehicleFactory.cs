namespace LogiCore.Domain;

/// <summary>фабрика транспорта, конкретный тип создают наследники</summary>
public abstract class VehicleFactory
{
    /// <summary>создаёт транспорт с указанным номером</summary>
    public abstract Vehicle Create(string registrationNumber);
}
/// <summary>фабрика фур</summary>
public sealed class TruckFactory : VehicleFactory
{
    /// <inheritdoc />
    public override Vehicle Create(string registrationNumber) => new Truck(registrationNumber);
}
/// <summary>создаёт рефрижераторы со стандартным диапазоном температур</summary>
public sealed class RefrigeratorFactory : VehicleFactory
{
    /// <inheritdoc />
    public override Vehicle Create(string registrationNumber) => new RefrigeratorTruck(registrationNumber);
}
/// <summary>фабрика самолётов</summary>
public sealed class PlaneFactory : VehicleFactory
{
    /// <inheritdoc />
    public override Vehicle Create(string registrationNumber) => new CargoPlane(registrationNumber);
}
/// <summary>фабрика судов</summary>
public sealed class ShipFactory : VehicleFactory
{
    /// <inheritdoc />
    public override Vehicle Create(string registrationNumber) => new CargoShip(registrationNumber);
}
/// <summary>фабрика дронов</summary>
public sealed class DroneFactory : VehicleFactory
{
    /// <inheritdoc />
    public override Vehicle Create(string registrationNumber) => new DroneCourier(registrationNumber);
}
/// <summary>фабрика грузов, способ создания передаётся функцией</summary>
public sealed class CargoFactory<T> where T : Cargo
{
    readonly Func<T> creator;
    /// <summary>сохраняет функцию создания груза</summary>
    public CargoFactory(Func<T> creator) => this.creator = creator;
    /// <summary>вызывает переданную функцию и возвращает новый груз</summary>
    public T Create() => creator();
}
