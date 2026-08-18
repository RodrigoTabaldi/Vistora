using Microsoft.AspNetCore.Mvc;
using SaasVistoria.Application;

namespace SaasVistoria.Controllers;

[ApiController, Route("api")]
public sealed class TemplatesController(IVistoraStore store) : VistoraApiControllerBase
{
    [HttpGet("templates")]
    public IActionResult Templates([FromQuery] int page = 1, [FromQuery] int pageSize = 100) => PagedOk(store.Templates, page, pageSize);

    [HttpPost("templates"), RequireRole("Administrador")]
    public IActionResult CreateTemplate(CreateTemplate request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Rooms is null || request.Rooms.Count == 0)
            return BadRequest(new { message = "Informe um nome e ao menos um ambiente." });
        return Created("api/templates", store.AddTemplate(request));
    }

    [HttpDelete("templates/{id:guid}"), RequireRole("Administrador")]
    public IActionResult DeleteTemplate(Guid id) => store.RemoveTemplate(id) ? NoContent() : NotFound(new { message = "Modelo não encontrado ou é um modelo do sistema." });
}
