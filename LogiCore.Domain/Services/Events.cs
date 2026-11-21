namespace LogiCore.Domain;

/// <summary>сигнатура обработчиков событий диспетчера</summary>
public delegate void LogisticsEventHandler<in TArgs>(object? sender, TArgs args) where TArgs : EventArgs;
/// <summary>данные принятого заказа</summary>
public sealed class OrderCreatedEventArgs(Order order) : EventArgs
{
    /// <summary>созданный заказ</summary>
    public Order Order { get; } = order;
}
/// <summary>данные об изменении статуса заказа</summary>
public sealed class OrderStatusChangedEventArgs(Order order, OrderStatus previousStatus) : EventArgs
{
    /// <summary>заказ после перехода</summary>
    public Order Order { get; } = order;
    /// <summary>состояние до перехода</summary>
    public OrderStatus PreviousStatus { get; } = previousStatus;
}
/// <summary>данные попытки перегрузить транспорт</summary>
public sealed class VehicleOverloadEventArgs(Vehicle vehicle, decimal weightKg, decimal volumeM3) : EventArgs
{
    /// <summary>транспорт, в который пытались загрузить слишком много груза</summary>
    public Vehicle Vehicle { get; } = vehicle;
    /// <summary>масса партии</summary>
    public decimal WeightKg { get; } = weightKg;
    /// <summary>объём партии</summary>
    public decimal VolumeM3 { get; } = volumeM3;
}
/// <summary>данные завершённой доставки</summary>
public sealed class DeliveryCompletedEventArgs(Order order, decimal revenue) : EventArgs
{
    /// <summary>доставленный заказ</summary>
    public Order Order { get; } = order;
    /// <summary>общая выручка после доставки</summary>
    public decimal Revenue { get; } = revenue;
}
