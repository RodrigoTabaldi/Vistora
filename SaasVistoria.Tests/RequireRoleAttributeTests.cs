using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SaasVistoria.Application;
using SaasVistoria.Domain;

namespace SaasVistoria.Tests;

public class RequireRoleAttributeTests
{
    private static readonly AppUser Admin = new(Guid.NewGuid(), Guid.NewGuid(), "Mariana Costa", "admin@atelierimoveis.com.br", "Administrador", "hash");
    private static readonly AppUser Inspector = Admin with { Role = "Vistoriador" };

    private static AuthorizationFilterContext Context(AppUser? user)
    {
        var httpContext = new DefaultHttpContext();
        if (user is not null) httpContext.Items["user"] = user;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new AuthorizationFilterContext(actionContext, []);
    }

    [Fact]
    public void OnAuthorization_AllowsAMatchingRole()
    {
        var context = Context(Admin);
        new RequireRoleAttribute("Administrador").OnAuthorization(context);
        Assert.Null(context.Result);
    }

    [Fact]
    public void OnAuthorization_BlocksANonMatchingRole()
    {
        var context = Context(Inspector);
        new RequireRoleAttribute("Administrador").OnAuthorization(context);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }

    [Fact]
    public void OnAuthorization_BlocksWhenNoAuthenticatedUserIsPresent()
    {
        var context = Context(null);
        new RequireRoleAttribute("Administrador").OnAuthorization(context);
        var result = Assert.IsType<ObjectResult>(context.Result);
        Assert.Equal(403, result.StatusCode);
    }
}
