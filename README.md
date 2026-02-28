# MiniErp (Dev Quickstart)

## Run
- API: `dotnet run --project .\src\MiniErp.Api\MiniErp.Api.csproj`
- Web: `dotnet run --project .\src\MiniErp.Web\MiniErp.Web.csproj`

Default URLs (Development):
- API: `http://localhost:5067` (+ Swagger at `/swagger`)
- Web: `http://localhost:5049`

## Demo Login (Development)
- TenantId: `11111111-1111-1111-1111-111111111111`
- BranchId: `22222222-2222-2222-2222-222222222222`
- DeviceId: `33333333-3333-3333-3333-333333333333`
- User: `owner`
- PIN: `1234`

## View Database in SSMS (SQL Server LocalDB)
1. Open SQL Server Management Studio
2. Server name: `(localdb)\MSSQLLocalDB`
3. Expand **Databases** → `mini_erp` → **Tables**

Connection string (Development) lives in `src/MiniErp.Api/appsettings.Development.json`.

## Common dev issue: “file is being used by another process”
If `dotnet run`/`dotnet build` fails with a locked `MiniErp.Api.exe` or `MiniErp.Web.exe`, stop the running process first:
- `taskkill /PID <pid> /F`
