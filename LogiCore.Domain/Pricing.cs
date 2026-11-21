namespace LogiCore.Domain;

/// <summary>тариф меняет стоимость, которую рассчитал транспорт</summary>
public interface ITariffStrategy
{
    /// <summary>название тарифа</summary>
    string Name { get; }
    /// <summary>стоимость после применения тарифа</summary>
    decimal Calculate(decimal baseCost, Route route, IReadOnlyCollection<Cargo> cargo);
}
/// <summary>обычный тариф без надбавок</summary>
public class StandardTariff : ITariffStrategy
{
    /// <inheritdoc />
    public string Name => "стандартный";
    /// <inheritdoc />
    public decimal Calculate(decimal baseCost, Route route, IReadOnlyCollection<Cargo> cargo) => baseCost;
}
/// <summary>экспресс-тариф с наценкой</summary>
public class ExpressTariff : ITariffStrategy
{
    /// <inheritdoc />
    public string Name => "экспресс";
    /// <inheritdoc />
    public decimal Calculate(decimal baseCost, Route route, IReadOnlyCollection<Cargo> cargo) => baseCost * TariffSettings.Instance.ExpressFactor;
}
/// <summary>доплата за массу сверх установленного порога</summary>
public class HeavyCargoTariff : ITariffStrategy
{
    /// <inheritdoc />
    public string Name => "тяжёлый груз";
    /// <inheritdoc />
    public decimal Calculate(decimal baseCost, Route route, IReadOnlyCollection<Cargo> cargo) =>
        baseCost + Math.Max(0, cargo.Sum(c => c.WeightKg) - TariffSettings.Instance.HeavyThresholdKg) * TariffSettings.Instance.HeavyExtraPerKg;
}
/// <summary>общие настройки тарифов, Lazy создаёт один экземпляр</summary>
public sealed class TariffSettings
{
    static readonly Lazy<TariffSettings> instance = new(() => new TariffSettings());
    TariffSettings() { }
    /// <summary>единственный экземпляр настроек</summary>
    public static TariffSettings Instance => instance.Value;
    /// <summary>множитель экспресс-тарифа</summary>
    public decimal ExpressFactor => 1.4m;
    /// <summary>масса, до которой доплаты нет</summary>
    public decimal HeavyThresholdKg => 10_000;
    /// <summary>доплата за каждый килограмм сверх порога</summary>
    public decimal HeavyExtraPerKg => 2;
    /// <summary>доля стоимости груза, которая идёт на страховку</summary>
    public decimal InsuranceRate => 0.01m;
    /// <summary>множитель срочной доставки</summary>
    public decimal UrgencyFactor => 1.25m;
    /// <summary>упаковка одного хрупкого места</summary>
    public decimal PackagingPerItem => 700;
}
/// <summary>итоговая сумма и шаги расчёта</summary>
public interface IDeliveryCost
{
    /// <summary>сумма в рублях до округления</summary>
    decimal Total { get; }
    /// <summary>промежуточные суммы для вывода</summary>
    IReadOnlyList<string> Steps { get; }
    /// <summary>расчёт стоимости в виде текста</summary>
    string Describe();
}
/// <summary>стоимость транспорта с учётом выбранного тарифа</summary>
public class BaseDeliveryCost : IDeliveryCost
{
    /// <inheritdoc />
    public decimal Total { get; }
    /// <inheritdoc />
    public IReadOnlyList<string> Steps { get; }
    /// <summary>считает стоимость до добавления услуг</summary>
    public BaseDeliveryCost(decimal vehicleCost, ITariffStrategy tariff, Route route, IReadOnlyCollection<Cargo> cargo)
    {
        Guard.NonNegative(vehicleCost, nameof(vehicleCost));
        Total = Guard.NonNegative(tariff.Calculate(vehicleCost, route, cargo), nameof(tariff));
        Steps = Array.AsReadOnly(new[] { $"транспорт: {vehicleCost:F2} руб", $"тариф «{tariff.Name}»: {Total:F2} руб" });
    }
    /// <inheritdoc />
    public string Describe() => string.Join(Environment.NewLine, Steps);
}
/// <summary>основа декораторов, каждая услуга меняет уже рассчитанную сумму</summary>
public abstract class CostDecorator : IDeliveryCost
{
    /// <summary>предыдущий расчёт без текущей услуги</summary>
    protected IDeliveryCost Inner { get; }
    /// <summary>сохраняет расчёт, к которому добавляется услуга</summary>
    protected CostDecorator(IDeliveryCost inner) => Inner = inner;
    /// <inheritdoc />
    public abstract decimal Total { get; }
    /// <summary>название применяемой услуги</summary>
    protected abstract string Label { get; }
    /// <inheritdoc />
    public IReadOnlyList<string> Steps => Array.AsReadOnly(Inner.Steps.Append($"{Label}: {Total:F2} руб").ToArray());
    /// <inheritdoc />
    public string Describe() => string.Join(Environment.NewLine, Steps);
}
/// <summary>добавляет страховку, для хрупкого груза учитывается коэффициент риска</summary>
public class InsuranceCost : CostDecorator
{
    readonly decimal premium;
    /// <summary>считает страховку для всех грузов заказа</summary>
    public InsuranceCost(IDeliveryCost inner, IReadOnlyCollection<Cargo> cargo) : base(inner) =>
        premium = cargo.Sum(c => ((IInsurable)c).InsuredValue * TariffSettings.Instance.InsuranceRate *
            (c is FragileCargo f ? f.RiskFactor : 1));
    /// <inheritdoc />
    public override decimal Total => Inner.Total + premium;
    /// <inheritdoc />
    protected override string Label => "страхование";
}
/// <summary>умножает текущую сумму на коэффициент срочности</summary>
public class UrgencyCost(IDeliveryCost inner) : CostDecorator(inner)
{
    /// <inheritdoc />
    public override decimal Total => Inner.Total * TariffSettings.Instance.UrgencyFactor;
    /// <inheritdoc />
    protected override string Label => "срочность";
}
/// <summary>упаковка оплачивается за каждое хрупкое место</summary>
public class PackagingCost : CostDecorator
{
    readonly decimal fee;
    /// <summary>подсчитывает стоимость упаковки</summary>
    public PackagingCost(IDeliveryCost inner, IReadOnlyCollection<Cargo> cargo) : base(inner) =>
        fee = cargo.OfType<FragileCargo>().Count() * TariffSettings.Instance.PackagingPerItem;
    /// <inheritdoc />
    public override decimal Total => Inner.Total + fee;
    /// <inheritdoc />
    protected override string Label => "упаковка";
}
/// <summary>выбранные услуги, применяются в порядке страховка, срочность, упаковка</summary>
public sealed record ServiceOptions
{
    /// <summary>включить страхование</summary>
    public bool Insurance { get; init; }
    /// <summary>включить срочную доставку</summary>
    public bool Urgent { get; init; }
    /// <summary>упаковать хрупкие грузы</summary>
    public bool Packaging { get; init; }
    /// <summary>добавляет выбранные услуги к расчёту стоимости</summary>
    public IDeliveryCost Apply(IDeliveryCost cost, IReadOnlyCollection<Cargo> cargo)
    {
        if (Insurance) cost = new InsuranceCost(cost, cargo);
        if (Urgent) cost = new UrgencyCost(cost);
        if (Packaging) cost = new PackagingCost(cost, cargo);
        return cost;
    }
}
