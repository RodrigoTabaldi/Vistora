using Microsoft.AspNetCore.Mvc;
using SaasVistoria.Application;

namespace SaasVistoria.Controllers;

[ApiController, Route("api")]
public sealed class PropertiesController(IVistoraStore store) : VistoraApiControllerBase
{
    [HttpGet("properties")]
    public IActionResult Properties([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 100) =>
        PagedOk(store.Properties.Where(p => string.IsNullOrWhiteSpace(search) || p.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || p.Neighborhood.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList(), page, pageSize);

    [HttpPost("properties"), RequireRole("Administrador")]
    public IActionResult CreateProperty(CreateProperty request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest(new { message = "Informe o nome do imóvel." });
        if (store.Properties.Count >= store.Company.PropertyLimit) return StatusCode(402, new { message = "Limite de imóveis do plano atingido." });
        return Created("api/properties", store.AddProperty(request));
    }
}
