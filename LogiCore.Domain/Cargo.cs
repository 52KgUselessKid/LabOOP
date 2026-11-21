using System.Text.Json.Serialization;

namespace LogiCore.Domain;

/// <summary>общие данные груза, после создания их нельзя изменить</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$cargo")]
[JsonDerivedType(typeof(StandardCargo), "standard")]
[JsonDerivedType(typeof(PerishableCargo), "perishable")]
[JsonDerivedType(typeof(FragileCargo), "fragile")]
[JsonDerivedType(typeof(DangerousCargo), "dangerous")]
[JsonDerivedType(typeof(OversizedCargo), "oversized")]
public abstract class Cargo : IEntity, IInsurable
{
    /// <inheritdoc />
    public Guid Id { get; }
    /// <summary>описание груза</summary>
    public string Description { get; }
    /// <summary>масса в килограммах</summary>
    public decimal WeightKg { get; }
    /// <summary>объём в кубических метрах</summary>
    public decimal VolumeM3 { get; }
    /// <summary>объявленная стоимость в рублях</summary>
    public decimal DeclaredValue { get; }
    // явная реализация оставляет страховую стоимость доступной только через IInsurable
    decimal IInsurable.InsuredValue => DeclaredValue;
    /// <summary>проверяет общие характеристики груза</summary>
    protected Cargo(string description, decimal weightKg, decimal volumeM3, decimal declaredValue, Guid id)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Description = Guard.Text(description, nameof(description));
        WeightKg = Guard.Positive(weightKg, nameof(weightKg));
        VolumeM3 = Guard.Positive(volumeM3, nameof(volumeM3));
        DeclaredValue = Guard.NonNegative(declaredValue, nameof(declaredValue));
    }
    /// <inheritdoc />
    public override string ToString() => $"{Description}: {WeightKg} кг, {VolumeM3} м³";
}
/// <summary>обычный груз, сверху можно поставить другой груз</summary>
public class StandardCargo(string description, decimal weightKg, decimal volumeM3, decimal declaredValue, Guid id = default)
    : Cargo(description, weightKg, volumeM3, declaredValue, id), IStackable
{
    /// <inheritdoc />
    public bool CanStack => true;
}
/// <summary>скоропортящийся груз со сроком годности и температурой хранения</summary>
public class PerishableCargo : Cargo, ITemperatureSensitive
{
    /// <summary>срок годности, в этот день груз ещё можно перевозить</summary>
    public DateOnly ExpirationDate { get; }
    /// <inheritdoc />
    public decimal RequiredTemperatureC { get; }
    /// <summary>создаёт груз, срок годности проверяется при оформлении заказа</summary>
    public PerishableCargo(string description, decimal weightKg, decimal volumeM3, decimal declaredValue,
        DateOnly expirationDate, decimal requiredTemperatureC, Guid id = default)
        : base(description, weightKg, volumeM3, declaredValue, id)
    {
        ExpirationDate = expirationDate;
        RequiredTemperatureC = requiredTemperatureC;
    }
}
/// <summary>хрупкий груз, коэффициент риска влияет на страховку</summary>
public class FragileCargo : Cargo, IStackable
{
    /// <summary>коэффициент риска, минимум 1</summary>
    public decimal RiskFactor { get; }
    /// <inheritdoc />
    public bool CanStack => false;
    /// <summary>создаёт хрупкий груз</summary>
    public FragileCargo(string description, decimal weightKg, decimal volumeM3, decimal declaredValue,
        decimal riskFactor = 1.5m, Guid id = default) : base(description, weightKg, volumeM3, declaredValue, id)
    {
        RiskFactor = riskFactor >= 1 ? riskFactor : throw new CargoValidationException("коэффициент риска меньше 1");
    }
}
/// <summary>опасный груз с классом опасности от 1 до 9</summary>
public class DangerousCargo : Cargo
{
    /// <summary>класс опасности от 1 до 9</summary>
    public int HazardClass { get; }
    /// <summary>проверяет класс опасности при создании</summary>
    public DangerousCargo(string description, decimal weightKg, decimal volumeM3, decimal declaredValue,
        int hazardClass, Guid id = default) : base(description, weightKg, volumeM3, declaredValue, id)
    {
        HazardClass = hazardClass is >= 1 and <= 9 ? hazardClass : throw new CargoValidationException("класс опасности: 1-9");
    }
}
/// <summary>негабаритный груз, на судне оплачивается дополнительное крепление</summary>
public class OversizedCargo(string description, decimal weightKg, decimal volumeM3, decimal declaredValue, Guid id = default)
    : Cargo(description, weightKg, volumeM3, declaredValue, id);
