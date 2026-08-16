using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SaasVistoria.Domain;

namespace SaasVistoria.Application;

/// <summary>
/// Autorização por permissão granular. O usuário autenticado é colocado em HttpContext.Items["user"]
/// pelo middleware de token em Program.cs.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequirePermissionAttribute(string permission) : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.Items["user"] as AppUser;
        if (user is null || !user.Active || !Permissions.Allows(user.Role, permission))
            context.Result = new ObjectResult(new { message = $"Seu perfil não possui a permissão \"{permission}\" para esta operação." }) { StatusCode = 403 };
    }
}
