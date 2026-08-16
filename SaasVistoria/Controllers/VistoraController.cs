using Microsoft.AspNetCore.Mvc;
using SaasVistoria.Application;
using SaasVistoria.Domain;

namespace SaasVistoria.Controllers;
[ApiController, Route("api")]
public sealed class VistoraController(IVistoraStore store) : ControllerBase
{
    private string CurrentActor => (HttpContext.Items["user"] as AppUser)?.Name ?? "Sistema";

    [HttpGet("dashboard")]
    public IActionResult Dashboard()
    {
        var inspections = store.Inspections;
        var occurrences = store.Occurrences;
        var pending = inspections.Count(x => x.Status is not InspectionStatus.Concluida);
        var attention = inspections.Count(x => x.PendingItems > 0);
        var openOccurrences = occurrences.Where(x => x.Status != "Concluída").ToList();
        var avgCompletion = inspections.Count == 0 ? 0 : (int)Math.Round(inspections.Average(x => x.Completion));
        var estimated = openOccurrences.Sum(x => x.EstimatedCost);
        return Ok(new
        {
            company = store.Company,
            metrics = new[]
            {
                new { label = "Imóveis ativos", value = store.Properties.Count.ToString(), trend = "Portfólio atual" },
                new { label = "Vistorias pendentes", value = pending.ToString(), trend = $"{attention} exigem atenção" },
                new { label = "Conclusão média", value = $"{avgCompletion}%", trend = "checklist preenchido" },
                new { label = "Ocorrências abertas", value = openOccurrences.Count.ToString(), trend = estimated.ToString("C0", new System.Globalization.CultureInfo("pt-BR")) + " estimados" }
            },
            inspections,
            occurrences,
            audit = store.Audit
        });
    }

    [HttpGet("properties")]
    public IActionResult Properties([FromQuery] string? search) =>
        Ok(store.Properties.Where(p => string.IsNullOrWhiteSpace(search) || p.Title.Contains(search, StringComparison.OrdinalIgnoreCase) || p.Neighborhood.Contains(search, StringComparison.OrdinalIgnoreCase)));

    [HttpPost("properties")]
    public IActionResult CreateProperty(CreateProperty request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest(new { message = "Informe o nome do imóvel." });
        if (store.Properties.Count >= store.Company.PropertyLimit) return StatusCode(402, new { message = "Limite de imóveis do plano atingido." });
        return Created("api/properties", store.AddProperty(request));
    }

    [HttpGet("templates")] public IActionResult Templates() => Ok(store.Templates);
    [HttpPost("templates")]
    public IActionResult CreateTemplate(CreateTemplate request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Rooms is null || request.Rooms.Count == 0)
            return BadRequest(new { message = "Informe um nome e ao menos um ambiente." });
        return Created("api/templates", store.AddTemplate(request));
    }
    [HttpDelete("templates/{id:guid}")]
    public IActionResult DeleteTemplate(Guid id) => store.RemoveTemplate(id) ? NoContent() : NotFound(new { message = "Modelo não encontrado ou é um modelo do sistema." });

    [HttpGet("inspections")] public IActionResult Inspections() => Ok(store.Inspections);
    [HttpGet("inspections/{id:guid}")]
    public IActionResult Inspection(Guid id) => store.FindInspection(id) is { } i ? Ok(i) : NotFound();
    [HttpPost("inspections")]
    public IActionResult CreateInspection(CreateInspection request)
    {
        if (store.Properties.All(p => p.Id != request.PropertyId))
            return NotFound(new { message = "Imóvel não encontrado." });
        try { return Created("api/inspections", store.AddInspection(request)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
    [HttpPost("inspections/{id:guid}/complete")]
    public IActionResult Complete(Guid id) => store.CompleteInspection(id, CurrentActor) is { } i ? Ok(i) : NotFound();

    [HttpGet("inspections/{id:guid}/items")] public IActionResult Items(Guid id) => Ok(store.GetItems(id));
    [HttpPost("inspections/{id:guid}/items")]
    public IActionResult AddItem(Guid id, CreateItem request)
    {
        if (store.FindInspection(id) is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Room) || string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Informe ambiente e tópico." });
        return Created($"api/inspections/{id}/items", store.AddItem(id, request));
    }
    [HttpPut("inspections/{id:guid}/items/{itemId:guid}")]
    public IActionResult UpdateItem(Guid id, Guid itemId, UpdateItem request) =>
        store.UpdateItem(id, itemId, request) is { } item ? Ok(item) : NotFound();
    [HttpDelete("inspections/{id:guid}/items/{itemId:guid}")]
    public IActionResult DeleteItem(Guid id, Guid itemId) => store.RemoveItem(id, itemId) ? NoContent() : NotFound();

    [HttpGet("inspections/{id:guid}/evidence")] public IActionResult Evidence(Guid id) => Ok(store.GetEvidence(id));
    [HttpPost("inspections/{id:guid}/evidence")]
    public IActionResult AddEvidence(Guid id, CreateEvidence request)
    {
        if (store.FindInspection(id) is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.DataUrl)) return BadRequest(new { message = "Envie a imagem." });
        return Created($"api/inspections/{id}/evidence", store.AddEvidence(id, request, CurrentActor));
    }

    [HttpGet("occurrences")] public IActionResult Occurrences() => Ok(store.Occurrences);
    [HttpPost("occurrences")]
    public IActionResult CreateOccurrence(CreateOccurrence request)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest(new { message = "Informe o título da ocorrência." });
        return Created("api/occurrences", store.AddOccurrence(request));
    }
    [HttpPut("occurrences/{id:guid}/status")]
    public IActionResult UpdateOccurrence(Guid id, [FromBody] string status) =>
        store.UpdateOccurrenceStatus(id, status) is { } o ? Ok(o) : NotFound();
}
