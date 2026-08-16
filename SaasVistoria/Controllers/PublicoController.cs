using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using SaasVistoria.Application;
using SaasVistoria.Domain;

namespace SaasVistoria.Controllers;

/// <summary>
/// Rotas sem autenticação: validação pública de laudo (conferência de integridade) e
/// assinatura por link com prazo de expiração. Expõem o mínimo de dados pessoais possível.
/// </summary>
[ApiController, Route("api/publico")]
public sealed class PublicoController(IVistoraStore store) : ControllerBase
{
    [HttpGet("laudos/{number}")]
    public IActionResult Validate(string number)
    {
        if (store.FindReportByNumber(number) is not { } report) return NotFound(new { message = "Documento não localizado." });
        var latest = store.Reports.Where(r => r.Number == report.Number).OrderByDescending(r => r.Version).First();
        var inspection = store.FindInspection(latest.InspectionId);
        return Ok(new
        {
            latest.Number,
            latest.Version,
            latest.IssuedAt,
            latest.Hash,
            integrity = ReportRenderer.VerifyIntegrity(latest) ? "íntegro" : "divergente",
            company = store.Company.Name,
            inspection = inspection?.Code,
            type = inspection?.Type,
            // Sem endereço completo nem dados das partes: a validação pública confere apenas a autenticidade.
            property = inspection?.PropertyName,
            signatures = store.GetSignatures(latest.Id).Select(s => new { s.SignerName, s.Role, s.SignedAt, s.Refused })
        });
    }

    [HttpGet("assinaturas/{token}")]
    public IActionResult SignatureInfo(string token)
    {
        if (store.FindSignatureRequest(token) is not { } request) return NotFound(new { message = "Link inválido ou expirado." });
        if (request.Completed) return Conflict(new { message = "Este convite de assinatura já foi utilizado." });
        var report = store.FindReport(request.ReportId);
        return Ok(new { request.SignerName, request.Role, request.ExpiresAt, requiresOtp = request.OtpCode is not null, report = report?.Number, reportId = request.ReportId, inspection = store.FindInspection(report?.InspectionId ?? Guid.Empty)?.PropertyName });
    }

    [HttpPost("assinaturas")]
    public IActionResult Sign(SignRequest request)
    {
        if (store.FindSignatureRequest(request.Token) is not { } signatureRequest) return NotFound(new { message = "Link inválido ou expirado." });
        if (signatureRequest.Completed) return Conflict(new { message = "Este convite de assinatura já foi utilizado." });
        if (signatureRequest.OtpCode is { } otp && otp != request.Otp) return BadRequest(new { message = "Código de verificação incorreto." });
        if (!request.Refused && string.IsNullOrWhiteSpace(request.ImageDataUrl)) return BadRequest(new { message = "Desenhe sua assinatura para concluir." });

        var report = store.FindReport(signatureRequest.ReportId);
        if (report is null) return NotFound();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconhecido";
        var userAgent = Request.Headers.UserAgent.ToString();
        // Evidências da assinatura eletrônica avançada: identidade, momento, IP, dispositivo, geolocalização e vínculo com o hash do laudo.
        var trail = $"{report.Hash}|{signatureRequest.SignerEmail}|{DateTime.UtcNow:O}|{ip}|{userAgent}|{request.Latitude}|{request.Longitude}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(trail))).ToLowerInvariant();

        // Baixa o convite antes de registrar a assinatura: o store conclui a vistoria quando não resta convite pendente.
        store.CompleteSignatureRequest(signatureRequest.Id);
        var signature = store.AddSignature(new(Guid.NewGuid(), store.Company.Id, report.Id, report.InspectionId,
            signatureRequest.SignerName, signatureRequest.SignerEmail, signatureRequest.Role,
            signatureRequest.OtpCode is null ? SignatureMethod.LinkRemoto : SignatureMethod.Otp,
            DateTime.Now, ip, userAgent, request.Latitude, request.Longitude, request.ImageDataUrl, hash, request.Refused, request.RefusalReason));

        return Ok(new { signature.Id, signature.SignedAt, signature.Hash, signature.Refused, message = request.Refused ? "Recusa registrada com o motivo informado." : "Assinatura registrada com sucesso." });
    }
}
