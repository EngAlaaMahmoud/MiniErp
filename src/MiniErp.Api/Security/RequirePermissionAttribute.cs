using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MiniErp.Api.Data;

namespace MiniErp.Api.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionAttribute(string permissionKey) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (string.IsNullOrWhiteSpace(permissionKey))
        {
            context.Result = new ForbidResult();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var perms = context.HttpContext.RequestServices.GetRequiredService<PermissionService>();

        var tenantId = db.TenantId;
        if (tenantId == Guid.Empty)
        {
            context.Result = new BadRequestObjectResult(new { error = "TENANT_REQUIRED" });
            return;
        }

        var role = PermissionService.TryGetRole(context.HttpContext.User);
        if (role is null)
        {
            context.Result = new ForbidResult();
            return;
        }

        var userId = PermissionService.TryGetUserId(context.HttpContext.User);
        if (userId == Guid.Empty)
        {
            context.Result = new ForbidResult();
            return;
        }

        var allowed = await perms.GetEffectivePermissionKeysAsync(tenantId, userId, role.Value, context.HttpContext.RequestAborted);
        if (!allowed.Contains(permissionKey))
        {
            context.Result = new ForbidResult();
            return;
        }

        await next();
    }
}
