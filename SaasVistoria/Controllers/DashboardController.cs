using Microsoft.AspNetCore.Mvc;
using SaasVistoria.Application;
using SaasVistoria.Domain;

namespace SaasVistoria.Controllers;

[ApiController, Route("api")]
public sealed class DashboardController(IVistoraStore store) : VistoraApiControllerBase
{
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
}
