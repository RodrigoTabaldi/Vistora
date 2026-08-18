using Microsoft.AspNetCore.Mvc;
using SaasVistoria.Application;

namespace SaasVistoria.Controllers;

[ApiController, Route("api")]
public sealed class InspectionsController(IVistoraStore store) : VistoraApiControllerBase
{
    private const int MaxEvidenceDataUrlLength = 12_000_000; // ~8-9 MB de imagem após overhead do base64

    [HttpGet("inspections")]
    public IActionResult Inspections([FromQuery] int page = 1, [FromQuery] int pageSize = 100) => PagedOk(store.Inspections, page, pageSize);

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

    [HttpGet("inspections/{id:guid}/items")]
    public IActionResult Items(Guid id) => Ok(store.GetItems(id));

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

    [HttpGet("inspections/{id:guid}/evidence")]
    public IActionResult Evidence(Guid id) => Ok(store.GetEvidence(id));

    [HttpPost("inspections/{id:guid}/evidence")]
    public IActionResult AddEvidence(Guid id, CreateEvidence request)
    {
        if (store.FindInspection(id) is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.DataUrl)) return BadRequest(new { message = "Envie a imagem." });
        if (!request.DataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Formato de evidência inválido. Envie uma imagem." });
        if (request.DataUrl.Length > MaxEvidenceDataUrlLength)
            return BadRequest(new { message = "Imagem muito grande. O limite é de aproximadamente 8 MB por foto." });
        try { return Created($"api/inspections/{id}/evidence", store.AddEvidence(id, request, CurrentActor)); }
        catch (FormatException) { return BadRequest(new { message = "Imagem inválida (dado corrompido)." }); }
    }
}
