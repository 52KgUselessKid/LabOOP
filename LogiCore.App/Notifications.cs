using LogiCore.Domain;

namespace LogiCore.App;

internal sealed class ConsoleNotifier : IDisposable
{
    readonly DeliveryService service;
    internal ConsoleNotifier(DeliveryService service)
    {
        this.service = service;
        service.OrderCreated += OnCreated;
        service.OrderStatusChanged += OnStatus;
        service.VehicleOverloadAttempt += OnOverload;
        service.DeliveryCompleted += OnCompleted;
    }
    void OnCreated(object? sender, OrderCreatedEventArgs e) => Console.WriteLine($"[приём] {e.Order.Id.ToString()[..8]}: {e.Order.Customer.Name}");
    void OnStatus(object? sender, OrderStatusChangedEventArgs e) => Console.WriteLine($"[статус] {e.Order.Id.ToString()[..8]}: {e.PreviousStatus} → {e.Order.Status}");
    void OnOverload(object? sender, VehicleOverloadEventArgs e) => Console.WriteLine($"[перегруз] {e.Vehicle.RegistrationNumber}: {e.WeightKg} кг / {e.VolumeM3} м³");
    void OnCompleted(object? sender, DeliveryCompletedEventArgs e) => Console.WriteLine($"[доставлено] {e.Order.TotalCost:F2} руб; выручка: {e.Revenue:F2} руб");
    public void Dispose()
    {
        service.OrderCreated -= OnCreated;
        service.OrderStatusChanged -= OnStatus;
        service.VehicleOverloadAttempt -= OnOverload;
        service.DeliveryCompleted -= OnCompleted;
    }
}

internal sealed class FileLogger : IDisposable
{
    readonly DeliveryService service;
    readonly string path;
    internal FileLogger(DeliveryService service, string path)
    {
        this.service = service; this.path = path;
        service.OrderCreated += OnCreated;
        service.OrderStatusChanged += OnStatus;
        service.VehicleOverloadAttempt += OnOverload;
        service.DeliveryCompleted += OnCompleted;
    }
    void Write(string text)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var writer = new StreamWriter(path, append: true);
            writer.WriteLine($"{DateTimeOffset.Now:O} {text}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // ошибка записи в журнал не отменяет операцию
            Console.Error.WriteLine($"журнал недоступен: {ex.Message}");
        }
    }
    void OnCreated(object? sender, OrderCreatedEventArgs e) => Write($"OrderCreated {e.Order.Id} {e.Order.Customer.Name}");
    void OnStatus(object? sender, OrderStatusChangedEventArgs e) => Write($"OrderStatusChanged {e.Order.Id} {e.PreviousStatus} -> {e.Order.Status}");
    void OnOverload(object? sender, VehicleOverloadEventArgs e) => Write($"VehicleOverloadAttempt {e.Vehicle.Id} {e.WeightKg}kg {e.VolumeM3}m3");
    void OnCompleted(object? sender, DeliveryCompletedEventArgs e) => Write($"DeliveryCompleted {e.Order.Id} cost={e.Order.TotalCost} revenue={e.Revenue}");
    public void Dispose()
    {
        service.OrderCreated -= OnCreated;
        service.OrderStatusChanged -= OnStatus;
        service.VehicleOverloadAttempt -= OnOverload;
        service.DeliveryCompleted -= OnCompleted;
    }
}
