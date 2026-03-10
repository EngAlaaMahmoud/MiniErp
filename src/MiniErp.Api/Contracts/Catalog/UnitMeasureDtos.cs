namespace MiniErp.Api.Contracts.Catalog;

public sealed record UnitMeasureListItem(Guid Id, string Name, decimal Capacity, bool IsActive);

public sealed record CreateUnitMeasureRequest(string Name, decimal Capacity = 1m, bool IsActive = true);

public sealed record UpdateUnitMeasureRequest(string Name, decimal Capacity = 1m, bool IsActive = true);
