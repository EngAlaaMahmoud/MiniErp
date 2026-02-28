using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniErp.Api.Contracts.Accounts;
using MiniErp.Api.Data;
using MiniErp.Api.Domain;
using MiniErp.Api.Domain.Enums;
using MiniErp.Api.Security;

namespace MiniErp.Api.Controllers;

[ApiController]
[Route("accounts")]
[Authorize]
public sealed class AccountsController(AppDbContext db) : ControllerBase
{
    [HttpGet("cash")]
    [RequirePermission(PermissionKeys.AccountsView)]
    public async Task<ActionResult<IReadOnlyList<CashTxnListItem>>> ListCash([FromQuery] Guid branchId, CancellationToken ct)
    {
        if (branchId == Guid.Empty)
        {
            return BadRequest(new { error = "BRANCH_REQUIRED" });
        }

        var items = await db.CashTxns
            .Where(x => x.BranchId == branchId)
            .OrderByDescending(x => x.At)
            .Take(200)
            .Select(x => new CashTxnListItem(x.Id, x.BranchId, x.Type, x.Amount, x.Note, x.RefType, x.RefId, x.At))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpGet("coa")]
    [RequirePermission(PermissionKeys.AccountsView)]
    public async Task<ActionResult<IReadOnlyList<ChartAccountListItem>>> ListChartAccounts(CancellationToken ct)
    {
        var items = await db.ChartAccounts
            .OrderBy(x => x.Code)
            .ThenBy(x => x.Name)
            .Select(x => new ChartAccountListItem(x.Id, x.Code, x.Name, (int)x.AccountType, x.ParentAccountId, x.IsPosting, x.IsActive))
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost("coa")]
    [RequirePermission(PermissionKeys.AccountsManage)]
    public async Task<IActionResult> CreateChartAccount([FromBody] CreateChartAccountRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "INVALID_REQUEST" });
        }

        if (!Enum.IsDefined(typeof(AccountType), request.AccountType))
        {
            return BadRequest(new { error = "INVALID_ACCOUNT_TYPE" });
        }

        var code = request.Code.Trim();
        var name = request.Name.Trim();

        if (await db.ChartAccounts.AnyAsync(x => x.Code == code, ct))
        {
            return Conflict(new { error = "DUPLICATE_CODE" });
        }

        Guid? parentId = null;
        if (request.ParentAccountId is not null && request.ParentAccountId.Value != Guid.Empty)
        {
            var parentExists = await db.ChartAccounts.AnyAsync(x => x.Id == request.ParentAccountId.Value, ct);
            if (!parentExists)
            {
                return BadRequest(new { error = "INVALID_PARENT" });
            }

            parentId = request.ParentAccountId.Value;
        }

        var entity = new ChartAccount
        {
            Id = Guid.NewGuid(),
            TenantId = db.TenantId,
            Code = code,
            Name = name,
            AccountType = (AccountType)request.AccountType,
            ParentAccountId = parentId,
            IsPosting = request.IsPosting,
            IsActive = request.IsActive,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.ChartAccounts.Add(entity);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ListChartAccounts), new { }, new { entity.Id });
    }
}
