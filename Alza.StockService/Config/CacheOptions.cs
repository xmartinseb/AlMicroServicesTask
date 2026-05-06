namespace Alza.StockService.Config;

public sealed class CacheOptions
{
    public TimeSpan DataTTL { get; init; } = TimeSpan.FromMinutes(1);
}