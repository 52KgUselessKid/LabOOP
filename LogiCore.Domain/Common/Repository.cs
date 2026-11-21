using System.Collections;

namespace LogiCore.Domain;

/// <summary>хранит объекты в памяти и находит их по Id</summary>
public class Repository<T> : IReadOnlyRepository<T>, IEnumerable<T> where T : class, IEntity
{
    readonly Dictionary<Guid, T> items = [];
    /// <summary>число объектов</summary>
    public int Count => items.Count;
    /// <summary>получение объекта по Id, для неизвестного Id выдаётся ошибка</summary>
    public T this[Guid id] => items[id];
    /// <summary>добаляет объект, если такого Id ещё нет</summary>
    public void Add(T item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!items.TryAdd(item.Id, item)) throw new ArgumentException("такой Id уже есть", nameof(item));
    }
    /// <summary>удаляет объект по Id</summary>
    public bool Remove(Guid id) => items.Remove(id);
    /// <summary>находит объекты, которые подходят под условие</summary>
    public IEnumerable<T> FindAll(Predicate<T> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        foreach (var item in this) if (predicate(item)) yield return item;
    }
    /// <inheritdoc />
    public T? GetById(Guid id) => items.GetValueOrDefault(id);
    /// <inheritdoc />
    public IEnumerable<T> GetAll() => this;
    /// <summary>по очереди возвращает объекты через yield return</summary>
    public IEnumerator<T> GetEnumerator()
    {
        foreach (var item in items.Values) yield return item;
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
/// <summary>общий вывод строк отчёта</summary>
public static class ReportExtensions
{
    /// <summary>собирает текст отчёта, формат строки задаётся переданной функцией</summary>
    public static string ToReportTable<T>(this IEnumerable<T> rows, Func<T, string> format) =>
        string.Join(Environment.NewLine, rows.Select(format));
}
