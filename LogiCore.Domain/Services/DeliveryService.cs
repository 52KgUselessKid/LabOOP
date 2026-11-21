namespace LogiCore.Domain;

/// <summary>принимает заказы и управляет доставкой</summary>
public class DeliveryService
{
    readonly Repository<Vehicle> vehicles = new();
    readonly Repository<Customer> customers = new();
    readonly Repository<Order> orders = new();
    readonly CargoCompatibilityValidator validator;
    /// <summary>создаёт диспетчер, при необходимости можно передать свой валидатор</summary>
    public DeliveryService(CargoCompatibilityValidator? validator = null) => this.validator = validator ?? new();
    /// <summary>транспорт компании, доступен только для чтения</summary>
    public IReadOnlyRepository<Vehicle> Vehicles => vehicles;
    /// <summary>зарегистрированные клиенты</summary>
    public IReadOnlyRepository<Customer> Customers => customers;
    /// <summary>принятые заказы</summary>
    public IReadOnlyRepository<Order> Orders => orders;
    /// <summary>сумма только по завершённым доставкам</summary>
    public decimal Revenue => orders.Where(o => o.Status == OrderStatus.Delivered).Sum(o => o.TotalCost);
    /// <summary>заказ принят</summary>
    public event LogisticsEventHandler<OrderCreatedEventArgs>? OrderCreated;
    /// <summary>изменился статус заказа</summary>
    public event LogisticsEventHandler<OrderStatusChangedEventArgs>? OrderStatusChanged;
    /// <summary>грузы превышают допустимую массу или объём транспорта</summary>
    public event LogisticsEventHandler<VehicleOverloadEventArgs>? VehicleOverloadAttempt;
    /// <summary>доставка завершена</summary>
    public event LogisticsEventHandler<DeliveryCompletedEventArgs>? DeliveryCompleted;
    /// <summary>добавляет транспорт в парк</summary>
    public void RegisterVehicle(Vehicle vehicle)
    {
        if (vehicles.Any(v => v.RegistrationNumber == vehicle.RegistrationNumber))
            throw new ArgumentException("номер уже занят", nameof(vehicle));
        vehicles.Add(vehicle);
    }
    /// <summary>регистрирует клиента</summary>
    public void RegisterCustomer(Customer customer) => customers.Add(customer);
    /// <summary>проверяет грузы и добавляет заказ в историю клиента</summary>
    public Order CreateOrder(Customer customer, IEnumerable<Cargo> cargo, Route route)
    {
        if (!ReferenceEquals(customers.GetById(customer.Id), customer)) throw new ArgumentException("клиент не зарегистрирован");
        var batch = cargo.ToArray(); validator.ValidateSet(batch);
        if (orders.SelectMany(o => o.Cargo).Any(c => batch.Any(b => b.Id == c.Id)))
            throw new CargoValidationException("этот груз уже включён в другой заказ");
        var order = new Order(customer, batch, route);
        orders.Add(order); customer.AddOrder(order);
        OrderCreated?.Invoke(this, new(order));
        return order;
    }
    /// <summary>выбирает самый дешёвый подходящий транспорт, потом добавляет тариф и услуги</summary>
    public void AssignCheapest(Order order, ITariffStrategy tariff, ServiceOptions? options = null)
    {
        EnsureOwned(order);
        if (order.Status != OrderStatus.Created) throw new InvalidOrderStateException("назначить ТС можно только новому заказу");
        validator.ValidateSet(order.Cargo);
        var vehicle = vehicles.Where(v => v.State == VehicleState.Free && v.CanTravel(order.Route) && validator.CanLoad(v, order.Cargo))
            .OrderBy(v => v.CalculateDeliveryCost(order.Route, order.Cargo)).ThenBy(v => v.RegistrationNumber).FirstOrDefault();
        if (vehicle is null) throw new RouteNotFoundException("нет свободного транспорта для этой партии и маршрута");
        AssignTo(order, vehicle, tariff, options);
    }
    /// <summary>назначает указанный транспорт, при перегрузе вызывает событие и выдаёт ошибку</summary>
    public void AssignTo(Order order, Vehicle vehicle, ITariffStrategy tariff, ServiceOptions? options = null)
    {
        EnsureOwned(order);
        if (order.Status != OrderStatus.Created) throw new InvalidOrderStateException("заказ уже обработан");
        if (!ReferenceEquals(vehicles.GetById(vehicle.Id), vehicle)) throw new ArgumentException("тс не из этого парка");
        validator.ValidateSet(order.Cargo);
        if (validator.IsOverloaded(vehicle, order.Cargo))
        {
            VehicleOverloadAttempt?.Invoke(this, new(vehicle, order.Cargo.Sum(c => c.WeightKg), order.Cargo.Sum(c => c.VolumeM3)));
            throw new VehicleOverloadException($"партия не помещается в {vehicle.RegistrationNumber}");
        }
        validator.ValidateForVehicle(vehicle, order.Cargo);
        if (!vehicle.CanTravel(order.Route)) throw new RouteNotFoundException("превышена дальность ТС");
        IDeliveryCost cost = new BaseDeliveryCost(vehicle.CalculateDeliveryCost(order.Route, order.Cargo), tariff, order.Route, order.Cargo);
        cost = (options ?? new()).Apply(cost, order.Cargo);
        order.Assign(vehicle, cost);
        OrderStatusChanged?.Invoke(this, new(order, OrderStatus.Created));
    }
    /// <summary>отправляет назначенный заказ</summary>
    public void StartDelivery(Order order)
    {
        EnsureOwned(order);
        if (order.Status != OrderStatus.Assigned) throw new InvalidOrderStateException("отправить можно только назначенный заказ");
        validator.ValidateSet(order.Cargo);
        Change(order, order.StartDelivery);
    }
    /// <summary>завершает доставку и вызывает событие с новой выручкой</summary>
    public void Complete(Order order)
    {
        EnsureOwned(order); Change(order, order.Complete);
        DeliveryCompleted?.Invoke(this, new(order, Revenue));
    }
    /// <summary>отменяет заказ до отправки</summary>
    public void Cancel(Order order) { EnsureOwned(order); Change(order, order.Cancel); }
    /// <summary>удаляет заказы, клиентов и транспорт из памяти</summary>
    public void Clear()
    {
        foreach (var order in orders.ToArray()) orders.Remove(order.Id);
        foreach (var customer in customers.ToArray()) { customer.ClearHistory(); customers.Remove(customer.Id); }
        foreach (var vehicle in vehicles.ToArray()) { vehicle.Release(); vehicles.Remove(vehicle.Id); }
    }
    internal void RestoreOrder(Order order) { orders.Add(order); order.Customer.AddOrder(order); }
    void EnsureOwned(Order order)
    {
        if (!ReferenceEquals(orders.GetById(order.Id), order)) throw new ArgumentException("заказ не принадлежит диспетчеру");
    }
    void Change(Order order, Action transition)
    {
        var previous = order.Status; transition(); OrderStatusChanged?.Invoke(this, new(order, previous));
    }
}
