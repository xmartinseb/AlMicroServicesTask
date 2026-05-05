using Alza.ProductService.Models;

namespace Alza.ProductService.Data;

public sealed class PseudoProductDb : IProductDb
{
    public async Task<Product> GetProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(200 * Random.Shared.NextDouble()), cancellationToken);
        return new Product(productId, $"https://example.com/product/xl/{productId}", GetRandomProductName());
    }

    static string GetRandomProductName() => RandomProcuctNames[Random.Shared.Next(RandomProcuctNames.Length)];

    static readonly string[] RandomProcuctNames = 
    [
        "MacBook Pro",
        "Nokia",
        "Domácí pekárna",
        "Přenosný vařič",
        "LG ultrawide monitor",
        "Automatický mixér",
        "Robotický vysavač",
        "Mýdlo na ruce",
        "Notebook Lenovo Thinkpad",
        "Stolní lampa táborová",
        "Polštářek do letadla",
        "Kancelářská židle ultralight",
        "Kojenecká voda",
        "Lednice s mrazákem",
        "Nabíječka",
        "Canon EOS R6 Mark III"
    ];
}