using Microsoft.AspNetCore.Mvc;
using SaasVistoria.Application;

namespace SaasVistoria.Controllers;

[ApiController, Route("api")]
public sealed class OccurrencesController(IVistoraStore store) : VistoraApiControllerBase
{
    [HttpGet("occurrences")]
    public IActionResult Occurrences([FromQuery] int page = 1, [FromQuery] int pageSize = 100) => PagedOk(store.Occurrences, page, pageSize);

    [HttpPost("occurrences")]
    public IActionResult CreateOccurrence(CreateOccurrence request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest(new { message = "Informe o título da ocorrência." });
        return Created("api/occurrences", store.AddOccurrence(request, CurrentActor));
    }

    [HttpPut("occurrences/{id:guid}/status")]
    public IActionResult UpdateOccurrence(Guid id, [FromBody] string status) =>
        store.UpdateOccurrenceStatus(id, status) is { } o ? Ok(o) : NotFound();
}
