namespace LogiCore.Domain;

/// <summary>точка на плоскости, координаты задаются в километрах</summary>
public readonly record struct RoutePoint
{
    /// <summary>координата X</summary>
    public double X { get; }
    /// <summary>координата Y</summary>
    public double Y { get; }
    /// <summary>создаёт точку, NaN и бесконечность не допускаются</summary>
    [System.Text.Json.Serialization.JsonConstructor]
    public RoutePoint(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x));
        X = x; Y = y;
    }
    /// <summary>расстояние между двумя точками в километрах</summary>
    public static double operator -(RoutePoint left, RoutePoint right) =>
        Math.Sqrt(Math.Pow(left.X - right.X, 2) + Math.Pow(left.Y - right.Y, 2));
    /// <summary>преобразует точку в строку</summary>
    public static explicit operator string(RoutePoint point) => point.ToString();
    /// <inheritdoc />
    public override string ToString() => $"({X:0.##}; {Y:0.##}) км";
}
/// <summary>маршрут из отрезков между точками</summary>
public class Route
{
    /// <summary>точки маршрута, не менее двух</summary>
    public IReadOnlyList<RoutePoint> Points { get; }
    /// <summary>общая длина маршрута в килметрах</summary>
    public decimal DistanceKm => (decimal)Points.Zip(Points.Skip(1), (a, b) => a - b).Sum();
    /// <summary>сохрняет копию точек, чтобы исходный список не менял маршрут</summary>
    public Route(IEnumerable<RoutePoint> points)
    {
        var copy = points.ToArray();
        if (copy.Length < 2) throw new RouteNotFoundException("маршруту нужны хотя бы две точки");
        Points = Array.AsReadOnly(copy);
        if (!double.IsFinite(copy.Zip(copy.Skip(1), (a, b) => a - b).Sum()))
            throw new RouteNotFoundException("длина маршрута выходит за допустимые пределы");
    }
    /// <summary>примерное время в пути без остановок и погрузки</summary>
    public TimeSpan EstimateTime(Vehicle vehicle) => TimeSpan.FromHours((double)(DistanceKm / vehicle.AverageSpeedKmH));
}
