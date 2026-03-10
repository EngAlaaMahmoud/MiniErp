namespace MiniErp.Api.Contracts.Catalog;

public sealed record UnitMeasureListItem(Guid Id, string Name, bool IsActive);

public sealed record CreateUnitMeasureRequest(string Name, bool IsActive = true);

public sealed record UpdateUnitMeasureRequest(string Name, bool IsActive = true);

