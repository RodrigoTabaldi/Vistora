using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using SaasVistoria.Domain;

namespace SaasVistoria.Application;

/// <summary>
/// Regras de negócio de conclusão e emissão de laudo. Separa bloqueios (impedem a conclusão)
/// de alertas (apenas avisam o vistoriador).
/// </summary>
public static class InspectionRules
{
    public static IReadOnlyList<ValidationIssue> Validate(IVistoraStore store, Inspection inspection)
    {
        var issues = new List<ValidationIssue>();
        var items = store.GetItems(inspection.Id);
        var evidence = store.GetEvidence(inspection.Id);
        var meters = store.GetMeters(inspection.Id);
        var keys = store.GetKeys(inspection.Id);
        var isEntryOrExit = inspection.Kind is InspectionKind.Entrada or InspectionKind.Saida;

        // Vistoria de saída precisa da vistoria de entrada correspondente (base da comparação).
        if (inspection.Kind is InspectionKind.Saida && inspection.PreviousInspectionId is null)
            issues.Add(new("saida-sem-entrada", "Vincule a vistoria de entrada correspondente antes de concluir a vistoria de saída.", true));

        var missingRequired = items.Where(i => i.Required && i.Condition == ConditionStatus.NaoAvaliado).ToList();
        if (missingRequired.Count > 0)
            issues.Add(new("itens-obrigatorios", $"{missingRequired.Count} item(ns) obrigatório(s) sem avaliação: {string.Join(", ", missingRequired.Take(3).Select(i => $"{i.Room} · {i.Name}"))}.", true));

        // Item crítico ou danificado exige foto — é a evidência que sustenta o laudo.
        var criticalWithoutPhoto = items
            .Where(i => (i.Condition is ConditionStatus.Danificado or ConditionStatus.Ruim || i.Severity is Severity.Alta or Severity.Critica)
                        && !evidence.Any(e => e.ItemId == i.Id) && i.PhotoCount == 0)
            .ToList();
        if (criticalWithoutPhoto.Count > 0)
            issues.Add(new("foto-obrigatoria", $"{criticalWithoutPhoto.Count} item(ns) com dano ou severidade alta estão sem foto.", true));

        if (items.Count == 0)
            issues.Add(new("checklist-vazio", "O checklist está vazio. Aplique um modelo ou adicione itens.", true));

        if (isEntryOrExit && meters.Count == 0)
            issues.Add(new("sem-medidores", "Não há leitura de medidores (água, energia ou gás) registrada.", false));

        if (isEntryOrExit && keys.Count == 0)
            issues.Add(new("sem-chaves", "Não há relação de chaves e controles registrada.", false));

        var notEvaluated = items.Count(i => i.Condition == ConditionStatus.NaoAvaliado);
        if (notEvaluated > 0)
            issues.Add(new("itens-nao-avaliados", $"{notEvaluated} item(ns) ainda constam como não avaliados.", false));

        if (store.FindContract(inspection.ContractId ?? Guid.Empty) is { } contract)
        {
            var reference = inspection.Kind == InspectionKind.Entrada ? contract.StartsOn : contract.EndsOn;
            if (isEntryOrExit && Math.Abs((inspection.ScheduledAt.Date - reference.Date).TotalDays) > 30)
                issues.Add(new("data-fora-vigencia", $"A data da vistoria está a mais de 30 dias da vigência do contrato ({reference:dd/MM/yyyy}).", false));
        }

        return issues;
    }
}

/// <summary>Comparação item a item entre a vistoria de entrada e a de saída.</summary>
public static class ComparisonService
{
    private static readonly Dictionary<ConditionStatus, int> Rank = new()
    {
        [ConditionStatus.Otimo] = 5, [ConditionStatus.Bom] = 4, [ConditionStatus.Regular] = 3,
        [ConditionStatus.Ruim] = 2, [ConditionStatus.Danificado] = 1, [ConditionStatus.Inexistente] = 0,
        [ConditionStatus.NaoAvaliado] = -1
    };

    public static ComparisonResult Compare(IVistoraStore store, Inspection entry, Inspection exit)
    {
        var before = store.GetItems(entry.Id);
        var after = store.GetItems(exit.Id);
        string Key(InspectionItem i) => $"{i.Room.Trim().ToLowerInvariant()}|{i.Name.Trim().ToLowerInvariant()}";
        var beforeMap = before.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
        var afterMap = after.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
        var lines = new List<ComparisonLine>();

        foreach (var key in beforeMap.Keys.Union(afterMap.Keys))
        {
            beforeMap.TryGetValue(key, out var b);
            afterMap.TryGetValue(key, out var a);
            var room = (a ?? b)!.Room;
            var name = (a ?? b)!.Name;

            if (a is null) { lines.Add(new(room, name, b!.Condition, null, ComparisonVerdict.ItemRemovido, Severity.Media, IssueClass.Inconclusivo, "Item presente na entrada e ausente na saída.", 0)); continue; }
            if (b is null) { lines.Add(new(room, name, null, a.Condition, ComparisonVerdict.ItemAdicionado, Severity.Baixa, IssueClass.NaoClassificado, "Item registrado apenas na saída.", 0)); continue; }

            var delta = Rank[a.Condition] - Rank[b.Condition];
            if (b.Condition == ConditionStatus.NaoAvaliado || a.Condition == ConditionStatus.NaoAvaliado)
                lines.Add(new(room, name, b.Condition, a.Condition, ComparisonVerdict.AlteracaoNaoIdentificada, Severity.Baixa, IssueClass.Inconclusivo, "Comparação inconclusiva: houve item sem avaliação.", 0));
            else if (delta == 0)
                lines.Add(new(room, name, b.Condition, a.Condition, ComparisonVerdict.SemAlteracao, Severity.Nenhuma, IssueClass.NaoClassificado, "Estado mantido.", 0));
            else if (delta > 0)
                lines.Add(new(room, name, b.Condition, a.Condition, ComparisonVerdict.Melhoria, Severity.Nenhuma, IssueClass.ManutencaoProprietario, "Estado melhor que na entrada.", 0));
            else
            {
                // Sugestão automática: queda de 1 nível tende a desgaste natural; quedas maiores exigem análise.
                var severity = delta <= -3 ? Severity.Critica : delta == -2 ? Severity.Alta : Severity.Media;
                var suggestion = delta == -1 ? IssueClass.DesgasteNatural : IssueClass.DanoLocatario;
                if (a.IssueClass != IssueClass.NaoClassificado) suggestion = a.IssueClass; // classificação humana prevalece
                lines.Add(new(room, name, b.Condition, a.Condition, ComparisonVerdict.NovoDano, severity, suggestion,
                    "Sugestão automática — depende de validação do vistoriador (art. 23, III, Lei 8.245/1991: desgaste natural não é imputável ao locatário).", a.EstimatedCost));
            }
        }

        var ordered = lines.OrderBy(l => l.Verdict == ComparisonVerdict.SemAlteracao).ThenBy(l => l.Room).ToList();
        var divergences = ordered.Count(l => l.Verdict != ComparisonVerdict.SemAlteracao);
        return new(entry.Id, exit.Id, entry.Code, exit.Code, exit.PropertyName, ordered, divergences, ordered.Sum(l => l.EstimatedCost));
    }
}

/// <summary>Renderiza o laudo em HTML pronto para impressão/PDF, com numeração, versão e hash de integridade.</summary>
public static class ReportRenderer
{
    private static readonly CultureInfo PtBr = new("pt-BR");
    private static string E(string? v) => WebUtility.HtmlEncode(v ?? "");
    private static string Money(decimal v) => v.ToString("C2", PtBr);

    // Rótulos em pt-BR: o laudo é documento para as partes, não deve exibir o nome técnico do enum.
    private static readonly Dictionary<ConditionStatus, string> ConditionLabel = new()
    {
        [ConditionStatus.Otimo] = "Ótimo", [ConditionStatus.Bom] = "Bom", [ConditionStatus.Regular] = "Regular",
        [ConditionStatus.Ruim] = "Ruim", [ConditionStatus.Danificado] = "Danificado",
        [ConditionStatus.Inexistente] = "Ausente", [ConditionStatus.NaoAvaliado] = "Não avaliado"
    };

    private static readonly Dictionary<IssueClass, string> ClassLabel = new()
    {
        [IssueClass.NaoClassificado] = "—", [IssueClass.DesgasteNatural] = "Desgaste natural",
        [IssueClass.DanoAnterior] = "Dano anterior", [IssueClass.DanoLocatario] = "Dano do locatário",
        [IssueClass.VicioConstrutivo] = "Vício construtivo", [IssueClass.ManutencaoProprietario] = "Manutenção do proprietário",
        [IssueClass.UrgenciaSeguranca] = "Urgência de segurança", [IssueClass.Inconclusivo] = "Inconclusivo"
    };

    private static readonly Dictionary<TestOutcome, string> TestLabel = new()
    {
        [TestOutcome.NaoTestado] = "Não testado", [TestOutcome.Aprovado] = "Aprovado",
        [TestOutcome.Reprovado] = "Reprovado", [TestOutcome.Parcial] = "Parcial"
    };

    private static readonly Dictionary<Severity, string> SeverityLabel = new()
    {
        [Severity.Nenhuma] = "—", [Severity.Baixa] = "Baixa", [Severity.Media] = "Média",
        [Severity.Alta] = "Alta", [Severity.Critica] = "Crítica"
    };

    private static readonly Dictionary<UtilityKind, string> UtilityLabel = new()
    {
        [UtilityKind.Agua] = "Água", [UtilityKind.Energia] = "Energia", [UtilityKind.Gas] = "Gás"
    };

    private static readonly Dictionary<ComparisonVerdict, string> VerdictLabel = new()
    {
        [ComparisonVerdict.SemAlteracao] = "Sem alteração", [ComparisonVerdict.NovoDano] = "Novo dano",
        [ComparisonVerdict.Melhoria] = "Melhoria", [ComparisonVerdict.ItemRemovido] = "Item removido",
        [ComparisonVerdict.ItemAdicionado] = "Item adicionado", [ComparisonVerdict.AlteracaoNaoIdentificada] = "Alteração não identificada"
    };

    public static string Label(ConditionStatus v) => ConditionLabel[v];
    public static string Label(IssueClass v) => ClassLabel[v];
    public static string Label(TestOutcome v) => TestLabel[v];
    public static string Label(Severity v) => SeverityLabel[v];
    public static string Label(UtilityKind v) => UtilityLabel[v];
    public static string Label(ComparisonVerdict v) => VerdictLabel[v];

    public static string Hash(string content)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private const string SealMarker = "<div class=\"selo\">";

    /// <summary>Recalcula o hash do corpo do laudo (tudo antes do selo) e compara com o hash registrado.</summary>
    public static bool VerifyIntegrity(InspectionReport report)
    {
        var cut = report.Html.IndexOf(SealMarker, StringComparison.Ordinal);
        if (cut < 0) return false;
        return Hash($"{report.Number}|{report.Version}|{report.Html[..cut]}") == report.Hash;
    }

    public static (string Html, string Hash) Render(IVistoraStore store, Inspection inspection, string number, int version, string issuedBy, string baseUrl)
    {
        var company = store.Company;
        var property = store.FindProperty(inspection.PropertyId);
        var contract = inspection.ContractId is { } cid ? store.FindContract(cid) : null;
        var landlord = contract is null ? null : store.People.FirstOrDefault(p => p.Id == contract.LandlordId);
        var tenant = contract is null ? null : store.People.FirstOrDefault(p => p.Id == contract.TenantId);
        var items = store.GetItems(inspection.Id);
        var evidence = store.GetEvidence(inspection.Id);
        var meters = store.GetMeters(inspection.Id);
        var keys = store.GetKeys(inspection.Id);
        var inventory = store.GetInventory(inspection.Id);
        var occurrences = store.Occurrences.Where(o => o.InspectionId == inspection.Id).ToList();
        var photoIndex = 0;

        var sb = new StringBuilder();
        sb.Append($$"""
        <!doctype html><html lang="pt-BR"><head><meta charset="utf-8">
        <title>Laudo {{E(number)}}</title>
        <style>
          :root { color-scheme: light; }
          body { font: 12px/1.55 "Segoe UI", system-ui, sans-serif; color: #17211f; margin: 0; padding: 32px; background: #fff; }
          h1 { font-size: 22px; margin: 0 0 4px; } h2 { font-size: 15px; margin: 28px 0 8px; border-bottom: 2px solid #0d3b33; padding-bottom: 4px; color: #0d3b33; }
          .capa { border: 1px solid #d6ddda; border-radius: 10px; padding: 20px; margin-bottom: 20px; }
          .grid { display: grid; grid-template-columns: repeat(2, 1fr); gap: 6px 24px; margin-top: 10px; }
          .grid b { color: #5b6b67; font-weight: 600; }
          table { width: 100%; border-collapse: collapse; margin-top: 6px; }
          th, td { border: 1px solid #dde4e1; padding: 6px 8px; text-align: left; vertical-align: top; }
          th { background: #f1f5f3; font-size: 11px; text-transform: uppercase; letter-spacing: .04em; }
          .fotos { display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px; }
          .foto { border: 1px solid #dde4e1; border-radius: 8px; overflow: hidden; }
          .foto img { width: 100%; height: 130px; object-fit: cover; display: block; }
          .foto figcaption { font-size: 10px; padding: 5px 7px; color: #5b6b67; }
          .selo { margin-top: 28px; border: 1px dashed #0d3b33; border-radius: 10px; padding: 14px; font-size: 11px; word-break: break-all; }
          .aviso { margin-top: 18px; background: #fff7e6; border-left: 4px solid #e0a93f; padding: 10px 14px; font-size: 11px; }
          .dano { background: #fdeceb; } .atencao { background: #fff6e5; }
          @media print { body { padding: 0; } h2 { page-break-after: avoid; } table, .foto { page-break-inside: avoid; } }
        </style></head><body>
        <div class="capa">
          <h1>{{E(company.Name)}} — Laudo de vistoria</h1>
          <p><strong>{{E(inspection.Type)}}</strong> · {{E(inspection.PropertyName)}}</p>
          <div class="grid">
            <span><b>Documento:</b> {{E(number)}} (versão {{version}})</span>
            <span><b>Emitido em:</b> {{DateTime.Now.ToString("dd/MM/yyyy HH:mm", PtBr)}}</span>
            <span><b>Vistoria:</b> {{E(inspection.Code)}}</span>
            <span><b>Vistoriador:</b> {{E(inspection.Inspector)}}</span>
            <span><b>Endereço:</b> {{E(property?.Address)}} — {{E(property?.Neighborhood)}}</span>
            <span><b>Área:</b> {{property?.Area.ToString("0.##", PtBr)}} m²</span>
            <span><b>Locador:</b> {{E(landlord?.Name ?? property?.Owner)}}</span>
            <span><b>Locatário:</b> {{E(tenant?.Name ?? "—")}}</span>
            <span><b>Contrato:</b> {{E(contract?.Code ?? "sem contrato vinculado")}}</span>
            <span><b>Check-in:</b> {{(inspection.CheckInAt is { } ci ? ci.ToString("dd/MM/yyyy HH:mm", PtBr) : "—")}}</span>
          </div>
        </div>
        """);

        sb.Append("<h2>1. Resumo executivo</h2><table><tr><th>Itens vistoriados</th><th>Preenchimento</th><th>Itens com dano</th><th>Pendências abertas</th><th>Custo estimado</th></tr>");
        sb.Append($"<tr><td>{items.Count}</td><td>{inspection.Completion}%</td><td>{items.Count(i => i.Condition is ConditionStatus.Ruim or ConditionStatus.Danificado)}</td><td>{occurrences.Count(o => o.Status != "Concluída")}</td><td>{Money(occurrences.Sum(o => o.EstimatedCost))}</td></tr></table>");

        var section = 2;
        sb.Append($"<h2>{section++}. Estado de conservação por ambiente</h2>");
        foreach (var room in items.GroupBy(i => i.Room))
        {
            sb.Append($"<h3>{E(room.Key)}</h3><table><tr><th style=\"width:26%\">Item</th><th>Estado</th><th>Severidade</th><th>Classificação</th><th>Teste</th><th>Observação</th></tr>");
            foreach (var i in room)
            {
                var css = i.Condition is ConditionStatus.Danificado ? " class=\"dano\"" : i.Condition is ConditionStatus.Ruim or ConditionStatus.Regular ? " class=\"atencao\"" : "";
                sb.Append($"<tr{css}><td>{E(i.Name)}{(i.Required ? " *" : "")}</td><td>{Label(i.Condition)}</td><td>{Label(i.Severity)}</td><td>{Label(i.IssueClass)}</td><td>{Label(i.Test)}</td><td>{E(i.Notes)}{(string.IsNullOrWhiteSpace(i.Recommendation) ? "" : $"<br><em>Recomendação: {E(i.Recommendation)}</em>")}</td></tr>");
            }
            sb.Append("</table>");
        }

        if (meters.Count > 0)
        {
            sb.Append($"<h2>{section++}. Leituras de medidores</h2><table><tr><th>Utilidade</th><th>Nº do medidor</th><th>Leitura</th><th>Data</th></tr>");
            foreach (var m in meters) sb.Append($"<tr><td>{Label(m.Kind)}</td><td>{E(m.MeterNumber)}</td><td>{m.Value.ToString("0.##", PtBr)}</td><td>{m.ReadAt:dd/MM/yyyy HH:mm}</td></tr>");
            sb.Append("</table>");
        }

        if (keys.Count > 0)
        {
            sb.Append($"<h2>{section++}. Relação de chaves e controles</h2><table><tr><th>Descrição</th><th>Quantidade</th><th>Estado</th></tr>");
            foreach (var k in keys) sb.Append($"<tr><td>{E(k.Description)}</td><td>{k.Quantity}</td><td>{Label(k.Condition)}</td></tr>");
            sb.Append("</table>");
        }

        if (inventory.Count > 0)
        {
            sb.Append($"<h2>{section++}. Inventário de bens</h2><table><tr><th>Ambiente</th><th>Bem</th><th>Marca/Modelo</th><th>Série</th><th>Qtd.</th><th>Estado</th><th>Funciona</th><th>Valor ref.</th></tr>");
            foreach (var a in inventory) sb.Append($"<tr><td>{E(a.Room)}</td><td>{E(a.Name)}</td><td>{E($"{a.Brand} {a.Model}".Trim())}</td><td>{E(a.SerialNumber)}</td><td>{a.Quantity}</td><td>{Label(a.Condition)}</td><td>{(a.Working ? "Sim" : "Não")}</td><td>{Money(a.ReferenceValue)}</td></tr>");
            sb.Append("</table>");
        }

        if (occurrences.Count > 0)
        {
            sb.Append($"<h2>{section++}. Pendências e recomendações</h2><table><tr><th>Pendência</th><th>Responsável</th><th>Prioridade</th><th>Prazo</th><th>Estimativa</th><th>Status</th></tr>");
            foreach (var o in occurrences) sb.Append($"<tr><td>{E(o.Title)}</td><td>{E(o.Responsible)}</td><td>{E(o.Priority)}</td><td>{o.DueDate:dd/MM/yyyy}</td><td>{Money(o.EstimatedCost)}</td><td>{E(o.Status)}</td></tr>");
            sb.Append("</table>");
        }

        if (evidence.Count > 0)
        {
            sb.Append($"<h2>{section++}. Registro fotográfico</h2><div class=\"fotos\">");
            foreach (var e in evidence)
            {
                photoIndex++;
                var geo = e.Latitude is null ? "sem GPS" : $"{e.Latitude:0.0000}, {e.Longitude:0.0000}";
                sb.Append($"<figure class=\"foto\"><img src=\"{E(e.Url)}\" alt=\"Foto {photoIndex} — {E(e.Room)}\"><figcaption><strong>Foto {photoIndex}</strong> · {E(e.Room)}<br>{e.CapturedAt:dd/MM/yyyy HH:mm} · {E(e.CapturedBy)}<br>{geo}<br>{E(e.IntegrityHash)}</figcaption></figure>");
            }
            sb.Append("</div>");
        }

        // Comparação entrada × saída, quando houver vistoria de referência.
        if (inspection.PreviousInspectionId is { } prevId && store.FindInspection(prevId) is { } previous)
        {
            var comparison = ComparisonService.Compare(store, previous, inspection);
            sb.Append($"<h2>{section++}. Comparação com a vistoria de entrada ({E(previous.Code)})</h2>");
            sb.Append($"<p>{comparison.Divergences} divergência(s) identificada(s). Estimativa de reparos: {Money(comparison.EstimatedTotal)}.</p>");
            sb.Append("<table><tr><th>Ambiente</th><th>Item</th><th>Entrada</th><th>Saída</th><th>Constatação</th><th>Severidade</th><th>Classificação sugerida</th></tr>");
            foreach (var l in comparison.Lines.Where(l => l.Verdict != ComparisonVerdict.SemAlteracao))
                sb.Append($"<tr><td>{E(l.Room)}</td><td>{E(l.Item)}</td><td>{(l.Before is { } b2 ? Label(b2) : "—")}</td><td>{(l.After is { } a2 ? Label(a2) : "—")}</td><td>{Label(l.Verdict)}</td><td>{Label(l.Severity)}</td><td>{Label(l.SuggestedClass)}</td></tr>");
            sb.Append("</table>");
        }

        sb.Append($"""
        <div class="aviso">
          Este documento organiza constatações visuais e evidências da vistoria. <strong>Não substitui parecer técnico, perícia ou laudo
          de responsabilidade de engenheiro, arquiteto ou outro profissional legalmente habilitado.</strong> As classificações de
          responsabilidade são sugestões sujeitas à validação humana e ao contraditório entre as partes, observado o art. 23, III, da
          Lei nº 8.245/1991 (devolução do imóvel no estado em que foi recebido, salvo desgaste natural).
        </div>
        <h2>{section}. Assinaturas</h2>
        <table><tr><th>Parte</th><th>Nome</th><th>Data/hora</th><th>Método</th><th>Evidências</th></tr><tbody id="assinaturas"><tr><td colspan="5">Aguardando coleta de assinaturas.</td></tr></tbody></table>
        """);

        var body = sb.ToString();
        var hash = Hash($"{number}|{version}|{body}");
        sb.Append($"""
        <div class="selo">
          <strong>Documento nº {E(number)} · versão {version}</strong><br>
          Emitido por {E(issuedBy)} em {DateTime.Now.ToString("dd/MM/yyyy HH:mm", PtBr)}<br>
          Hash SHA-256: {hash}<br>
          Validação pública: {E(baseUrl)}/api/publico/laudos/{E(number)}
        </div></body></html>
        """);
        return (sb.ToString(), hash);
    }
}
