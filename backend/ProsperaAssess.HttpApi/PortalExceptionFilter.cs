using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;
namespace ProsperaAssess;

public class PortalExceptionFilter(IUnitOfWorkManager units) : IAsyncExceptionFilter, ITransientDependency, IOrderedFilter
{
    public int Order => int.MaxValue;
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is Volo.Abp.Authorization.AbpAuthorizationException)
        {
            if (units.Current is { IsCompleted: false } deniedUnit) await deniedUnit.RollbackAsync();
            context.Result = new ObjectResult(new { error = "Your account does not have permission for this operation." }) { StatusCode = context.HttpContext.User.Identity?.IsAuthenticated == true ? 403 : 401 }; context.ExceptionHandled = true; return;
        }
        if (context.Exception is Volo.Abp.Data.AbpDbConcurrencyException)
        {
            context.Result = new ObjectResult(new { error = "The record changed. Refresh before saving again." }) { StatusCode = 409 }; context.ExceptionHandled = true; return;
        }
        if (context.Exception is not PortalBusinessException error) return;
        if (units.Current is { IsCompleted: false } unit) await unit.RollbackAsync();
        context.Result = new ObjectResult(error.ErrorData ?? new { error = error.Status == 404 ? "Not found." : "Request rejected." }) { StatusCode = error.Status }; context.ExceptionHandled = true;
    }
}
