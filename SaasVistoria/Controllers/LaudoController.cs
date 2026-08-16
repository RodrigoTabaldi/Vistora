using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using SaasVistoria.Application;
using SaasVistoria.Domain;

namespace SaasVistoria.Controllers;

/// <summary>Emissão de laudos versionados, coleta de assinaturas e contestações.</summary>
[ApiController, Route("api")]
public sealed class LaudoController(IVistoraStore store) : ControllerBase
{
    private AppUser? Current => HttpContext.Items["user"] as AppUser;
    private string Actor => Current?.Name ?? "Sistema";
    private string BaseUrl => $"{Request.Scheme}://{Request.Host}";

    // ---- Laudos ----
    [HttpGet("inspections/{id:guid}/laudos")]
    public IActionResult Reports(Guid id) =>
        Ok(store.Reports.Where(r => r.InspectionId == id).OrderByDescending(r => r.Version).Select(Describe));

    /// <summary>Prévia não selada: não gera versão nem trilha de auditoria.</summary>
    [HttpGet("inspections/{id:guid}/laudos/previa")]
    public IActionResult Preview(Guid id)
    {
        if (store.FindInspection(id) is not { } inspection) return NotFound();
        var (html, _) = ReportRenderer.Render(store, inspection, "PRÉVIA", 0, Actor, BaseUrl);
        return Content(html, "text/html; charset=utf-8");
    }

    /// <summary>
    /// Emite (sela) uma versão do laudo. Laudo assinado nunca é sobrescrito: uma nova emissão
    /// cria a versão seguinte, preservando a anterior e o respectivo hash.
    /// </summary>
    [HttpPost("inspections/{id:guid}/laudos"), RequirePermission(Permissions.Approve)]
    public IActionResult Emit(Guid id)
    {
        if (store.FindInspection(id) is not { } inspection) return NotFound();

        var issues = InspectionRules.Validate(store, inspection);
        if (issues.FirstOrDefault(i => i.Blocking) is { } blocker)
            return UnprocessableEntity(new { message = blocker.Message, issues });

        var previous = store.Reports.Where(r => r.InspectionId == id).ToList();
        var version = previous.Count == 0 ? 1 : previous.Max(r => r.Version) + 1;
        var number = previous.Count == 0 ? $"LAU-{DateTime.Today:yyyy}-{store.Reports.Count + 1:0000}" : previous[0].Number;
        var (html, hash) = ReportRenderer.Render(store, inspection, number, version, Actor, BaseUrl);
        var report = store.AddReport(new(Guid.NewGuid(), store.Company.Id, id, number, version, DateTime.Now, Actor, hash, true, html));
        return Created($"api/laudos/{report.Id}", Describe(report));
    }

    [HttpGet("laudos/{id:guid}")]
    public IActionResult Report(Guid id) => store.FindReport(id) is { } r ? Ok(Describe(r)) : NotFound();

    [HttpGet("laudos/{id:guid}/html")]
    public IActionResult ReportHtml(Guid id)
    {
        if (store.FindReport(id) is not { } report) return NotFound();
        var signatures = store.GetSignatures(id);
        var html = report.Html;
        if (signatures.Count > 0)
        {
            var rows = string.Join("", signatures.Select(s =>
                $"<tr><td>{s.Role}</td><td>{System.Net.WebUtility.HtmlEncode(s.SignerName)}</td><td>{s.SignedAt:dd/MM/yyyy HH:mm}</td><td>{(s.Refused ? "Recusou" : s.Method.ToString())}</td>" +
                $"<td>IP {s.Ip} · {(s.Latitude is null ? "sem GPS" : $"{s.Latitude:0.0000}, {s.Longitude:0.0000}")}<br>hash {s.Hash[..12]}…{(s.Refused ? $"<br>Motivo: {System.Net.WebUtility.HtmlEncode(s.RefusalReason)}" : "")}</td></tr>"));
            // O corpo selado permanece intacto; as assinaturas são anexadas na renderização.
            html = html.Replace("<tbody id=\"assinaturas\"><tr><td colspan=\"5\">Aguardando coleta de assinaturas.</td></tr></tbody>", $"<tbody id=\"assinaturas\">{rows}</tbody>");
        }
        return Content(html, "text/html; charset=utf-8");
    }

    private object Describe(InspectionReport r) => new
    {
        r.Id, r.InspectionId, r.Number, r.Version, r.IssuedAt, r.IssuedBy, r.Hash,
        integrity = ReportRenderer.VerifyIntegrity(r) ? "íntegro" : "divergente",
        signatures = store.GetSignatures(r.Id).Select(s => new { s.SignerName, s.Role, s.SignedAt, s.Method, s.Refused }),
        validationUrl = $"{BaseUrl}/api/publico/laudos/{r.Number}"
    };

    // ---- Assinaturas ----
    [HttpPost("laudos/{id:guid}/assinaturas/solicitar"), RequirePermission(Permissions.Approve)]
    public IActionResult RequestSignature(Guid id, RequestSignature request)
    {
        if (store.FindReport(id) is not { } report) return NotFound();
        if (string.IsNullOrWhiteSpace(request.SignerEmail)) return BadRequest(new { message = "Informe o e-mail do signatário." });

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var otp = request.Method == SignatureMethod.Otp ? RandomNumberGenerator.GetInt32(100000, 999999).ToString() : null;
        var signatureRequest = store.AddSignatureRequest(new(Guid.NewGuid(), store.Company.Id, id, request.SignerName, request.SignerEmail, request.Role, token, otp, DateTime.Now.AddDays(7), false));
        store.Log("Solicitou assinatura", report.Number, Actor, $"{request.SignerName} ({request.Role}) · {request.Method}");

        // Demo: o link e o código voltam na resposta. Em produção seguem por e-mail/SMS/WhatsApp.
        return Ok(new { signatureRequest.Id, link = $"{BaseUrl}/assinar?token={token}", otp, expiresAt = signatureRequest.ExpiresAt });
    }

    [HttpGet("laudos/{id:guid}/assinaturas")]
    public IActionResult Signatures(Guid id) => Ok(store.GetSignatures(id));

    // ---- Contestações ----
    [HttpGet("contestacoes")]
    public IActionResult Contestations() => Ok(store.Contestations);

    [HttpPost("contestacoes")]
    public IActionResult OpenContestation(OpenContestation request)
    {
        if (store.FindInspection(request.InspectionId) is not { } inspection) return NotFound();
        if (string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { message = "Descreva o motivo da contestação." });

        var label = request.ItemId is { } itemId
            ? store.GetItems(inspection.Id).FirstOrDefault(i => i.Id == itemId) is { } item ? $"{item.Room} · {item.Name}" : "Item não localizado"
            : $"Vistoria {inspection.Code}";
        var author = string.IsNullOrWhiteSpace(request.Author) ? Actor : request.Author;
        var contestation = new Contestation(Guid.NewGuid(), store.Company.Id, inspection.Id, request.ItemId, label, author, request.Reason.Trim(),
            ContestationStatus.Aberta, DateTime.Now, DateTime.Now.AddDays(store.Company.ContestationDays), null,
            [new(Guid.NewGuid(), author, request.Reason.Trim(), DateTime.Now, null)]);
        return Created("api/contestacoes", store.AddContestation(contestation));
    }

    [HttpPut("contestacoes/{id:guid}"), RequirePermission(Permissions.Edit)]
    public IActionResult UpdateContestation(Guid id, UpdateContestationRequest request)
    {
        var message = string.IsNullOrWhiteSpace(request.Message) ? null
            : new ContestationMessage(Guid.NewGuid(), request.Author ?? Actor, request.Message.Trim(), DateTime.Now, request.AttachmentUrl);
        return store.UpdateContestation(id, request.Status, request.Decision, message) is { } c ? Ok(c) : NotFound();
    }
}
