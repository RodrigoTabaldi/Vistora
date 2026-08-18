using Microsoft.AspNetCore.Mvc;
using SaasVistoria.Domain;

namespace SaasVistoria.Controllers;

public abstract class VistoraApiControllerBase : ControllerBase
{
    protected string CurrentActor => (HttpContext.Items["user"] as AppUser)?.Name ?? "Sistema";

    // Fatia a coleção e expõe o total em X-Total-Count; por padrão devolve tudo (pageSize=100
    // cobre hoje qualquer lista do seed), então clientes existentes não quebram sem passar page/pageSize.
    protected IActionResult PagedOk<T>(IReadOnlyList<T> items, int page, int pageSize)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 500);
        Response.Headers["X-Total-Count"] = items.Count.ToString();
        return Ok(items.Skip((page - 1) * pageSize).Take(pageSize));
    }
}
