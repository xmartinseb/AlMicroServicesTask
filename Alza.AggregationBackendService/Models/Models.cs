using Alza.HttpExtensions;

namespace Alza.AggregationBackendService.Models;

public sealed record ProductAggregaredInfo(Guid Id, string? Name, string? ImageUrl, double? Price, int? Availability, IReadOnlyList<SharedErrorModel> MicroserviceErrors);