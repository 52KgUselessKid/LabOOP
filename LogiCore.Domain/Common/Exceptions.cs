namespace LogiCore.Domain;

/// <summary>общая ошибка для операций доставки</summary>
public class LogisticsException : Exception
{
    /// <summary>сохраняет сообщение и исходную ошибку</summary>
    public LogisticsException(string message, Exception? inner = null) : base(message, inner) { }
}
/// <summary>груз не прошёл проверку данных</summary>
public class CargoValidationException(string message) : LogisticsException(message);
/// <summary>грузы нельзя перевозить вместе или не подходит температура</summary>
public class IncompatibleCargoException(string message) : LogisticsException(message);
/// <summary>грузы не помещаются по массе или объёму</summary>
public class VehicleOverloadException(string message) : LogisticsException(message);
/// <summary>маршрут не задан или транспорт не может его пройти</summary>
public class RouteNotFoundException(string message) : LogisticsException(message);
/// <summary>действие недоступно при текущем статусе заказа</summary>
public class InvalidOrderStateException(string message) : LogisticsException(message);
/// <summary>файл состояния не найден или содержит неверные данные</summary>
public class StateStorageException(string message, Exception? inner = null) : LogisticsException(message, inner);
