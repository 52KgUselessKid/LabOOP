namespace LogiCore.Domain;

/// <summary>возможные статусы заказа</summary>
public enum OrderStatus
{
    /// <summary>принят</summary>
    Created,
    /// <summary>транспорт зарезервирован</summary>
    Assigned,
    /// <summary>в пути</summary>
    InTransit,
    /// <summary>доставлен</summary>
    Delivered,
    /// <summary>отменён до отправки</summary>
    Cancelled
}
/// <summary>клиент и истроия его заказов</summary>
public class Customer : IEntity
{
    readonly List<Order> history = [];
    /// <inheritdoc />
    public Guid Id { get; }
    /// <summary>имя или название организации</summary>
    public string Name { get; }
    /// <summary>контакт для связи</summary>
    public string Contact { get; }
    /// <summary>заказы клиента, менять список снаружи нельзя</summary>
    public IReadOnlyCollection<Order> Orders => history.AsReadOnly();
    /// <summary>создаёт клиента</summary>
    public Customer(string name, string contact, Guid id = default)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Name = Guard.Text(name, nameof(name)); Contact = Guard.Text(contact, nameof(contact));
    }
    internal void AddOrder(Order order) => history.Add(order);
    internal void ClearHistory() => history.Clear();
}
/// <summary>заказ, статус меняется только через методы</summary>
public class Order : IEntity
{
    /// <inheritdoc />
    public Guid Id { get; }
    /// <summary>заказчик</summary>
    public Customer Customer { get; }
    /// <summary>грузы заказа, менять список снаружи нельзя</summary>
    public IReadOnlyCollection<Cargo> Cargo { get; }
    /// <summary>маршут доставки</summary>
    public Route Route { get; }
    /// <summary>назначенный транспорт</summary>
    public Vehicle? AssignedVehicle { get; private set; }
    /// <summary>итоговая стоимость в рублях</summary>
    public decimal TotalCost { get; private set; }
    /// <summary>шаги, по которым получилась итоговая стоимость</summary>
    public IReadOnlyList<string> CostSteps { get; private set; } = Array.Empty<string>();
    /// <summary>текущий статус заказа</summary>
    public OrderStatus Status { get; private set; }
    internal Order(Customer customer, IEnumerable<Cargo> cargo, Route route, Guid id = default)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        Customer = customer; Cargo = Array.AsReadOnly(cargo.ToArray()); Route = route;
    }
    /// <summary>назначает свободный транспорт новому заказу</summary>
    public void Assign(Vehicle vehicle, IDeliveryCost cost)
    {
        Require(OrderStatus.Created);
        var total = decimal.Round(Guard.NonNegative(cost.Total, nameof(cost)), 2, MidpointRounding.AwayFromZero);
        var steps = Array.AsReadOnly(cost.Steps.ToArray());
        new CargoCompatibilityValidator().ValidateForVehicle(vehicle, Cargo);
        if (!vehicle.CanTravel(Route)) throw new RouteNotFoundException("тс не может пройти маршрут");
        vehicle.Reserve(); AssignedVehicle = vehicle; TotalCost = total; CostSteps = steps;
        Status = OrderStatus.Assigned;
    }
    /// <summary>отправляет заказ, для которого уже выбран транспорт</summary>
    public void StartDelivery() { Require(OrderStatus.Assigned); Status = OrderStatus.InTransit; }
    /// <summary>завершает доставку и освобождает транспорт</summary>
    public void Complete()
    {
        Require(OrderStatus.InTransit); Status = OrderStatus.Delivered; AssignedVehicle!.Release();
    }
    /// <summary>отменяет заказ, пока доставка не началась</summary>
    public void Cancel()
    {
        if (Status is not (OrderStatus.Created or OrderStatus.Assigned))
            throw new InvalidOrderStateException($"нельзя отменить заказ в состоянии {Status}");
        if (Status == OrderStatus.Assigned) AssignedVehicle!.Release();
        Status = OrderStatus.Cancelled;
    }
    void Require(OrderStatus required)
    {
        if (Status != required) throw new InvalidOrderStateException($"ожидалось {required}, фактически {Status}");
    }
    internal void Restore(Vehicle? vehicle, OrderStatus status, decimal cost, IReadOnlyList<string> steps)
    {
        // старые заказы загружаются даже тогда, когда срок годности груза уже прошёл
        if (!Enum.IsDefined(status) || cost < 0 || (status is OrderStatus.Assigned or OrderStatus.InTransit or OrderStatus.Delivered) && vehicle is null)
            throw new StateStorageException("некорректный статус или назначение в снимке");
        if (status == OrderStatus.Created && (vehicle is not null || cost != 0))
            throw new StateStorageException("новый заказ не должен иметь назначения");
        if (vehicle is null && cost != 0) throw new StateStorageException("стоимость без транспорта");
        if (status is OrderStatus.Assigned or OrderStatus.InTransit) vehicle!.Reserve();
        AssignedVehicle = vehicle; Status = status; TotalCost = cost;
        CostSteps = Array.AsReadOnly(steps.ToArray());
    }
}
