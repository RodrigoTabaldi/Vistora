using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SaasVistoria.Domain;

namespace SaasVistoria.Application;

// A autenticação (Program.cs) já garante que HttpContext.Items["user"] existe em toda rota /api
// protegida; este filtro só decide se o papel do usuário autorizado tem permissão para a ação.
public sealed class RequireRoleAttribute(params string[] roles) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.Items["user"] as AppUser;
        if (user is null || !roles.Contains(user.Role, StringComparer.OrdinalIgnoreCase))
            context.Result = new ObjectResult(new { message = "Você não tem permissão para esta ação." }) { StatusCode = 403 };
    }
}
