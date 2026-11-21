namespace LogiCore.Domain;

/// <summary>объект, у которого есть свой Id</summary>
public interface IEntity
{
    /// <summary>идентификатор объекта</summary>
    Guid Id { get; }
}
/// <summary>репозиторий только для чтения, поэтому T объявлен с out</summary>
public interface IReadOnlyRepository<out T> where T : class, IEntity
{
    /// <summary>возвращает объект по Id или null, если его нет</summary>
    T? GetById(Guid id);
    /// <summary>все объекты репозитория</summary>
    IEnumerable<T> GetAll();
}
/// <summary>проверка объекта, валидатор базового типа подходит и для его наследников</summary>
public interface IValidator<in T>
{
    /// <summary>проверяет объект и выдаёт ошибку, если данные не подходят</summary>
    void Validate(T item);
}
/// <summary>груз с заданной температурой хранения</summary>
public interface ITemperatureSensitive
{
    /// <summary>температура в градусах Цельсия</summary>
    decimal RequiredTemperatureC { get; }
}
/// <summary>стоимость груза для расчёта страховки</summary>
public interface IInsurable
{
    /// <summary>страховая стоимость в рублях</summary>
    decimal InsuredValue { get; }
}
/// <summary>можно ли ставить грузы друг на друга</summary>
public interface IStackable
{
    /// <summary>можно ли ставить другой груз сверху</summary>
    bool CanStack { get; }
}

internal static class Guard
{
    internal static string Text(string value, string name) => !string.IsNullOrWhiteSpace(value)
        ? value.Trim() : throw new ArgumentException("значение не должно быть пустым", name);
    internal static decimal Positive(decimal value, string name) => value > 0
        ? value : throw new CargoValidationException($"{name}: требуется положительное число");
    internal static decimal NonNegative(decimal value, string name) => value >= 0
        ? value : throw new ArgumentOutOfRangeException(name);
}
