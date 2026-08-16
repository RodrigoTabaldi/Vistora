using Microsoft.AspNetCore.Mvc;
using SaasVistoria.Application;
using SaasVistoria.Domain;

namespace SaasVistoria.Controllers;

/// <summary>
/// Cadastros de apoio à vistoria (partes, contratos, medidores, chaves, inventário),
/// operação em campo (check-in/out) e comparação entrada × saída.
/// </summary>
[ApiController, Route("api")]
public sealed class FluxoController(IVistoraStore store) : ControllerBase
{
    private AppUser? Current => HttpContext.Items["user"] as AppUser;
    private string Actor => Current?.Name ?? "Sistema";

    // ---- Pessoas e contratos ----
    [HttpGet("people")]
    public IActionResult People() =>
        // LGPD: a listagem devolve o documento mascarado; o número completo só é usado na emissão do laudo.
        Ok(store.People.Select(p => new { p.Id, p.Name, document = p.MaskedDocument, p.Email, p.Phone, p.Role }));

    [HttpPost("people"), RequirePermission(Permissions.Create)]
    public IActionResult CreatePerson(CreatePerson request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Informe o nome da pessoa." });
        var digits = new string((request.Document ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length is not (0 or 11 or 14)) return BadRequest(new { message = "Documento deve ser um CPF (11 dígitos) ou CNPJ (14 dígitos)." });
        var person = store.AddPerson(request);
        return Created("api/people", new { person.Id, person.Name, document = person.MaskedDocument, person.Email, person.Phone, person.Role });
    }

    [HttpGet("contracts")]
    public IActionResult Contracts() => Ok(store.Contracts.Select(Describe));

    [HttpPost("contracts"), RequirePermission(Permissions.Create)]
    public IActionResult CreateContract(CreateContract request)
    {
        if (store.FindProperty(request.PropertyId) is null) return NotFound(new { message = "Imóvel não encontrado." });
        if (request.EndsOn <= request.StartsOn) return BadRequest(new { message = "A data final da vigência deve ser posterior à inicial." });
        if (store.People.All(p => p.Id != request.LandlordId) || store.People.All(p => p.Id != request.TenantId))
            return BadRequest(new { message = "Cadastre locador e locatário antes de criar o contrato." });
        return Created("api/contracts", Describe(store.AddContract(request)));
    }

    private object Describe(LeaseContract c) => new
    {
        c.Id, c.Code, c.PropertyId, c.StartsOn, c.EndsOn, c.RentValue, c.Guarantee, c.Status,
        property = store.FindProperty(c.PropertyId)?.Title,
        landlord = store.People.FirstOrDefault(p => p.Id == c.LandlordId)?.Name,
        tenant = store.People.FirstOrDefault(p => p.Id == c.TenantId)?.Name,
        guarantor = c.GuarantorId is { } g ? store.People.FirstOrDefault(p => p.Id == g)?.Name : null
    };

    // ---- Medidores, chaves e inventário ----
    [HttpGet("inspections/{id:guid}/meters")] public IActionResult Meters(Guid id) => Ok(store.GetMeters(id));

    [HttpPost("inspections/{id:guid}/meters"), RequirePermission(Permissions.Edit)]
    public IActionResult AddMeter(Guid id, CreateMeterReading request)
    {
        if (store.FindInspection(id) is null) return NotFound();
        if (request.Value < 0) return BadRequest(new { message = "A leitura não pode ser negativa." });
        return Created($"api/inspections/{id}/meters", store.AddMeter(id, request));
    }

    [HttpGet("inspections/{id:guid}/keys")] public IActionResult Keys(Guid id) => Ok(store.GetKeys(id));

    [HttpPost("inspections/{id:guid}/keys"), RequirePermission(Permissions.Edit)]
    public IActionResult AddKey(Guid id, CreateKeyHandover request)
    {
        if (store.FindInspection(id) is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Description)) return BadRequest(new { message = "Descreva a chave ou controle." });
        return Created($"api/inspections/{id}/keys", store.AddKey(id, request));
    }

    [HttpGet("inspections/{id:guid}/inventory")] public IActionResult Inventory(Guid id) => Ok(store.GetInventory(id));

    [HttpPost("inspections/{id:guid}/inventory"), RequirePermission(Permissions.Edit)]
    public IActionResult AddInventory(Guid id, CreateInventoryAsset request)
    {
        if (store.FindInspection(id) is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { message = "Informe o bem inventariado." });
        return Created($"api/inspections/{id}/inventory", store.AddInventory(id, request));
    }

    // ---- Operação em campo ----
    [HttpPost("inspections/{id:guid}/check-in"), RequirePermission(Permissions.Edit)]
    public IActionResult CheckIn(Guid id, CheckInRequest request) =>
        store.CheckIn(id, request.Latitude, request.Longitude, Actor) is { } i ? Ok(i) : NotFound();

    [HttpPost("inspections/{id:guid}/check-out"), RequirePermission(Permissions.Edit)]
    public IActionResult CheckOut(Guid id) => store.CheckOut(id, Actor) is { } i ? Ok(i) : NotFound();

    /// <summary>Bloqueios e alertas que impedem (ou apenas sinalizam) a conclusão da vistoria.</summary>
    [HttpGet("inspections/{id:guid}/validacao")]
    public IActionResult Validate(Guid id)
    {
        if (store.FindInspection(id) is not { } inspection) return NotFound();
        var issues = InspectionRules.Validate(store, inspection);
        return Ok(new { canComplete = !issues.Any(i => i.Blocking), issues });
    }

    /// <summary>Comparação entre a vistoria de saída e a vistoria de entrada vinculada.</summary>
    [HttpGet("inspections/{id:guid}/comparacao")]
    public IActionResult Comparison(Guid id, [FromQuery] Guid? entryId)
    {
        if (store.FindInspection(id) is not { } exit) return NotFound();
        var previousId = entryId ?? exit.PreviousInspectionId;
        if (previousId is null) return BadRequest(new { message = "Esta vistoria não possui vistoria de entrada vinculada." });
        if (store.FindInspection(previousId.Value) is not { } entry) return NotFound(new { message = "Vistoria de entrada não encontrada." });
        return Ok(ComparisonService.Compare(store, entry, exit));
    }

    [HttpGet("me")]
    public IActionResult Me() => Current is { } u
        ? Ok(new { u.Name, u.Email, u.Role, permissions = Permissions.For(u.Role), company = store.Company.Name })
        : Unauthorized();
}
