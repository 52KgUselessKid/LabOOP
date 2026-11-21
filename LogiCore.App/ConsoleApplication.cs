using System.Globalization;
using System.Text;
using LogiCore.Domain;
using Route = LogiCore.Domain.Route;

namespace LogiCore.App;

internal sealed class ConsoleApplication
{
    DeliveryService service = new();
    readonly JsonStateStore store = new();
    readonly ReportPrinter reports = new();
    ConsoleNotifier? notifier;
    FileLogger? logger;
    readonly string dataDirectory = Path.Combine(Environment.CurrentDirectory, "data");
    internal int Run()
    {
        Console.OutputEncoding = Encoding.UTF8;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        try
        {
            AttachObservers();
            new DemoScenario().Run(service);
            reports.Print(service);
            var path = Path.Combine(dataDirectory, "demo-state.json");
            store.Save(path, service);
            var before = Fingerprint(service);
            service.Clear();
            Console.WriteLine($"\nпосле очистки: парк={service.Vehicles.GetAll().Count()}, клиенты={service.Customers.GetAll().Count()}, заказы={service.Orders.GetAll().Count()}, выручка={service.Revenue}");
            ReplaceService(store.Load(path));
            var after = Fingerprint(service);
            if (before != after) throw new StateStorageException("данные после загрузки не совпали");
            Console.WriteLine($"загрузка: {after}, данные совпали");
            reports.Print(service);
            Menu();
            return 0;
        }
        catch (Exception ex) when (ex is LogisticsException or IOException or UnauthorizedAccessException)
        { Console.Error.WriteLine($"ошибка: {ex.Message}"); return 1; }
        finally { notifier?.Dispose(); logger?.Dispose(); }
    }
    static string Fingerprint(DeliveryService state) =>
        $"тс={state.Vehicles.GetAll().Count()}; клиентов={state.Customers.GetAll().Count()}; заказов={state.Orders.GetAll().Count()}; " +
        $"грузов={state.Orders.GetAll().Sum(o => o.Cargo.Count)}; выручка={state.Revenue:F2}; " +
        string.Join(", ", state.Orders.GetAll().GroupBy(o => o.Status).OrderBy(g => g.Key).Select(g => $"{g.Key}={g.Count()}"));
    void AttachObservers()
    {
        notifier = new(service); logger = new(service, Path.Combine(dataDirectory, "delivery.log"));
    }
    void ReplaceService(DeliveryService restored)
    {
        notifier?.Dispose(); logger?.Dispose(); service = restored; AttachObservers();
    }
    void Menu()
    {
        while (true)
        {
            Console.WriteLine("\n1 - создать заказ, 2 - отчёты, 3 - сохранить, 4 - загрузить, 5 - заказы и действия, 6 - парк, 0 - выход");
            var choice = Console.ReadLine();
            if (choice is null or "0") return;
            try
            {
                switch (choice.Trim())
                {
                    case "1": CreateOrder(); break;
                    case "2": reports.Print(service); break;
                    case "3": store.Save(Path.Combine(dataDirectory, "state.json"), service); Console.WriteLine("состояние сохранено в data/state.json"); break;
                    case "4": ReplaceService(store.Load(Path.Combine(dataDirectory, "state.json"))); Console.WriteLine("состояние загружено"); break;
                    case "5": ManageOrders(); break;
                    case "6": Console.WriteLine(service.Vehicles.GetAll().ToReportTable(v => $"{v}; {v.MaxLoadKg} кг / {v.MaxVolumeM3} м³")); break;
                    default: Console.WriteLine("выберите пункт из меню"); break;
                }
            }
            catch (Exception ex) when (ex is LogisticsException or ArgumentException or OverflowException)
            { Console.WriteLine($"опрация не выполнена: {ex.Message}"); }
        }
    }
    void CreateOrder()
    {
        var customers = service.Customers.GetAll().ToArray();
        for (var i = 0; i < customers.Length; i++) Console.WriteLine($"{i + 1} - {customers[i].Name}");
        var customerIndex = ReadInt("клиент (0 - новый): ", 0, customers.Length);
        Customer customer;
        if (customerIndex == 0)
        {
            customer = new(ReadText("имя: "), ReadText("контакт: ")); service.RegisterCustomer(customer);
        }
        else customer = customers[customerIndex - 1];
        var distance = ReadDecimal("расстояние, км: ", allowZero: true);
        var route = new Route([new(0, 0), new((double)distance, 0)]);
        var cargo = new List<Cargo>();
        var count = ReadInt("количество грузовых мест (1-20): ", 1, 20);
        for (var i = 0; i < count; i++) cargo.Add(ReadCargo());
        var order = service.CreateOrder(customer, cargo, route);
        Assign(order);
    }
    Cargo ReadCargo()
    {
        var kind = ReadInt("груз: 1 обычный, 2 скоропортящийся, 3 хрупкий, 4 опасный, 5 негабарит: ", 1, 5);
        var description = ReadText("описание: ");
        var weight = ReadDecimal("масса, кг: "); var volume = ReadDecimal("объём, м^3: ");
        var value = ReadDecimal("объявленная стоимость, руб: ", allowZero: true);
        return kind switch
        {
            1 => new StandardCargo(description, weight, volume, value),
            2 => new PerishableCargo(description, weight, volume, value, ReadDate(), ReadDecimal("температура, °C: ", signed: true)),
            3 => new FragileCargo(description, weight, volume, value, ReadDecimal("коэффициент риска (>=1): ")),
            4 => new DangerousCargo(description, weight, volume, value, ReadInt("класс опасности (1-9): ", 1, 9)),
            _ => new OversizedCargo(description, weight, volume, value)
        };
    }
    void Assign(Order order)
    {
        var tariff = ReadInt("тариф: 1 стандартный, 2 экспресс, 3 тяжёлый: ", 1, 3);
        ITariffStrategy strategy = tariff switch { 2 => new ExpressTariff(), 3 => new HeavyCargoTariff(), _ => new StandardTariff() };
        var options = new ServiceOptions
        {
            Insurance = ReadInt("страхование? 1 да / 0 нет: ", 0, 1) == 1,
            Urgent = ReadInt("срочность? 1 да / 0 нет: ", 0, 1) == 1,
            Packaging = ReadInt("упаковка хрупкого? 1 да / 0 нет: ", 0, 1) == 1
        };
        service.AssignCheapest(order, strategy, options);
        Console.WriteLine($"назначено: {order.AssignedVehicle}");
        Console.WriteLine(string.Join(Environment.NewLine, order.CostSteps));
        Console.WriteLine($"итого: {order.TotalCost:F2} руб");
    }
    void ManageOrders()
    {
        var orders = service.Orders.GetAll().ToArray();
        if (orders.Length == 0) { Console.WriteLine("заказов нет"); return; }
        for (var i = 0; i < orders.Length; i++)
            Console.WriteLine($"{i + 1}: {orders[i].Customer.Name}, {orders[i].Status}, {orders[i].TotalCost:F2} руб");
        var index = ReadInt("номер заказа (0 - назад): ", 0, orders.Length);
        if (index == 0) return;
        var order = orders[index - 1];
        switch (ReadInt("1 назначить ТС, 2 отправить, 3 завершить, 4 отменить, 0 назад: ", 0, 4))
        {
            case 1: Assign(order); break;
            case 2: service.StartDelivery(order); break;
            case 3: service.Complete(order); break;
            case 4: service.Cancel(order); break;
        }
    }
    static string ReadText(string prompt)
    {
        while (true)
        {
            Console.Write(prompt); var value = Console.ReadLine();
            if (value is null) throw new CargoValidationException("ввод прерван");
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            Console.WriteLine("пустой ввод");
        }
    }
    static int ReadInt(string prompt, int min, int max)
    {
        while (true)
        {
            if (int.TryParse(ReadText(prompt), out var value) && value >= min && value <= max) return value;
            Console.WriteLine($"введите целое число: {min}-{max}");
        }
    }
    static decimal ReadDecimal(string prompt, bool allowZero = false, bool signed = false)
    {
        while (true)
        {
            var text = ReadText(prompt).Replace(',', '.');
            if (decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
                && (signed || value > 0 || allowZero && value == 0)) return value;
            Console.WriteLine("неверное число");
        }
    }
    static DateOnly ReadDate()
    {
        while (true)
        {
            if (DateOnly.TryParseExact(ReadText("годен до (дд.мм.гггг): "), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)) return value;
            Console.WriteLine("неверная дата");
        }
    }
}
