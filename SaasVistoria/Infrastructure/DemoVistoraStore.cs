using SaasVistoria.Application;
using SaasVistoria.Domain;

namespace SaasVistoria.Infrastructure;

public sealed partial class DemoVistoraStore : IVistoraStore
{
    private readonly object _gate = new();
    private readonly Guid _companyId = Guid.Parse("dcd2d9bc-2e2e-4c95-abcf-010000000001");
    private readonly List<Property> _properties;
    private readonly List<Inspection> _inspections;
    private readonly List<Occurrence> _occurrences;
    private readonly List<AuditEvent> _audit;
    private readonly List<AppUser> _users;
    private readonly List<InspectionTemplate> _templates;
    private readonly Dictionary<Guid, List<InspectionItem>> _items = new();
    private readonly Dictionary<Guid, List<Evidence>> _evidence = new();

    public Company Company { get; }
    public IReadOnlyList<Property> Properties { get { lock (_gate) return _properties.ToList(); } }
    public IReadOnlyList<Inspection> Inspections { get { lock (_gate) return _inspections.ToList(); } }
    public IReadOnlyList<Occurrence> Occurrences { get { lock (_gate) return _occurrences.ToList(); } }
    public IReadOnlyList<AuditEvent> Audit { get { lock (_gate) return _audit.ToList(); } }
    public IReadOnlyList<InspectionTemplate> Templates { get { lock (_gate) return _templates.ToList(); } }

    public DemoVistoraStore()
    {
        Company = new(_companyId, "Atelier Imóveis", "atelier-imoveis", "Profissional", 30, 500);
        _users = [new(Guid.NewGuid(), _companyId, "Mariana Costa", "admin@atelierimoveis.com.br", "Administrador", PasswordHasher.Hash("Vistora@2026"))];
        _properties = [
            new(Guid.Parse("00000000-0000-0000-0000-000000000101"), _companyId, "Apartamento Aurora 1204", PropertyType.Apartamento, "Rua Oscar Freire, 812", "Jardins · São Paulo", 112, 3, 2, "Ocupado", "Helena Sampaio", "https://images.unsplash.com/photo-1600607687920-4e2a09cf159d?auto=format&fit=crop&w=900&q=80", -23.5613m, -46.6690m),
            new(Guid.Parse("00000000-0000-0000-0000-000000000102"), _companyId, "Casa Bosque Alto", PropertyType.Casa, "Alameda dos Ipês, 48", "Alto de Pinheiros · São Paulo", 268, 4, 3, "Desocupado", "Ricardo Nobre", "https://images.unsplash.com/photo-1600585154340-be6161a56a0c?auto=format&fit=crop&w=900&q=80", -23.546m, -46.714m),
            new(Guid.Parse("00000000-0000-0000-0000-000000000103"), _companyId, "Loja Galeria Centro", PropertyType.Comercial, "Rua Avanhandava, 91", "Bela Vista · São Paulo", 76, 0, 0, "Ocupado", "Grupo Horizonte", "https://images.unsplash.com/photo-1497366754035-f200968a6e72?auto=format&fit=crop&w=900&q=80", -23.552m, -46.646m)
        ];
        // A vistoria de entrada 0200 é a referência da vistoria de saída 0201 (comparação entrada × saída).
        _inspections = [
            new(Guid.Parse("00000000-0000-0000-0000-000000000200"), _companyId, _properties[0].Id, "VIS-2024-0106", _properties[0].Title, "Vistoria de entrada", InspectionStatus.Concluida, DateTime.Today.AddYears(-2), "Ana Ribeiro", 100, 0, _properties[0].ImageUrl, InspectionKind.Entrada),
            new(Guid.Parse("00000000-0000-0000-0000-000000000201"), _companyId, _properties[0].Id, "VIS-2026-0418", _properties[0].Title, "Vistoria de saída", InspectionStatus.EmAndamento, DateTime.Today.AddHours(14), "Lucas Mendes", 68, 3, _properties[0].ImageUrl, InspectionKind.Saida, null, Guid.Parse("00000000-0000-0000-0000-000000000200")),
            new(Guid.Parse("00000000-0000-0000-0000-000000000202"), _companyId, _properties[1].Id, "VIS-2026-0417", _properties[1].Title, "Vistoria de entrada", InspectionStatus.AguardandoAssinatura, DateTime.Today.AddDays(1).AddHours(10), "Ana Ribeiro", 100, 0, _properties[1].ImageUrl, InspectionKind.Entrada),
            new(Guid.Parse("00000000-0000-0000-0000-000000000203"), _companyId, _properties[2].Id, "VIS-2026-0416", _properties[2].Title, "Vistoria periódica", InspectionStatus.Agendada, DateTime.Today.AddDays(2).AddHours(15), "Lucas Mendes", 0, 0, _properties[2].ImageUrl, InspectionKind.Periodica)
        ];
        _occurrences = [
            new(Guid.NewGuid(), _companyId, _inspections[1].Id, "Infiltração junto à janela", "Alta", "Em análise", DateTime.Today.AddDays(3), 1800, _properties[0].Title, "Proprietário"),
            new(Guid.NewGuid(), _companyId, _inspections[1].Id, "Tomada sem espelho", "Média", "Aberta", DateTime.Today.AddDays(7), 120, _properties[0].Title, "Locatário")
        ];
        _audit = [
            new(Guid.NewGuid(), _companyId, "Iniciou vistoria", "VIS-2026-0418", "Lucas Mendes", DateTime.Now.AddMinutes(-22), "Check-in confirmado por geolocalização"),
            new(Guid.NewGuid(), _companyId, "Enviou para assinatura", "VIS-2026-0417", "Ana Ribeiro", DateTime.Now.AddHours(-2), "Link seguro enviado aos participantes")
        ];
        _templates = TemplateCatalog.All
            .Select(t => new InspectionTemplate(Guid.NewGuid(), _companyId, t.Name, t.Description, t.Type, true, t.Rooms))
            .ToList();

        // Vistoria de entrada (referência histórica) e vistoria de saída em andamento — mesmos tópicos, para comparação.
        SeedItems(_inspections[0].Id,
            ("Sala de estar", "Paredes e pintura", ConditionStatus.Otimo, "Pintura nova.", 2),
            ("Sala de estar", "Piso de madeira", ConditionStatus.Otimo, "Sem avarias.", 3),
            ("Cozinha", "Bancada e cuba", ConditionStatus.Otimo, "Rejunte íntegro.", 2),
            ("Quarto principal", "Janela", ConditionStatus.Bom, "Vedação em ordem.", 1));
        SeedItems(_inspections[1].Id,
            ("Sala de estar", "Paredes e pintura", ConditionStatus.Bom, "Pequena marca na parede norte.", 2),
            ("Sala de estar", "Piso de madeira", ConditionStatus.Otimo, "Sem avarias visíveis.", 3),
            ("Cozinha", "Bancada e cuba", ConditionStatus.Regular, "Rejunte com desgaste leve.", 2),
            ("Quarto principal", "Janela", ConditionStatus.Danificado, "Vedação ressecada.", 1));
        _evidence[_inspections[1].Id] = [
            new(Guid.NewGuid(), _inspections[1].Id, null, "Sala de estar", "https://images.unsplash.com/photo-1600210492486-724fe5c67fb0?auto=format&fit=crop&w=900&q=80", DateTime.Now.AddMinutes(-18), -23.5613m, -46.669m, 8m, "SHA-256: 08f5...b1a9", "Lucas Mendes"),
            new(Guid.NewGuid(), _inspections[1].Id, null, "Cozinha", "https://images.unsplash.com/photo-1556912172-45b7abe8b7e1?auto=format&fit=crop&w=900&q=80", DateTime.Now.AddMinutes(-11), -23.5612m, -46.6689m, 6m, "SHA-256: 51c2...7e04", "Lucas Mendes")
        ];
        SeedParties();
    }

    private void SeedItems(Guid inspectionId, params (string Room, string Name, ConditionStatus Condition, string Notes, int Photos)[] rows)
        => _items[inspectionId] = rows.Select(r => new InspectionItem(Guid.NewGuid(), inspectionId, r.Room, r.Name, r.Condition, r.Notes, r.Photos)).ToList();

    public AppUser? Validate(string email, string password)
    {
        lock (_gate)
        {
            var user = _users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
            // Usuário desativado não acessa, mas o histórico dele permanece na auditoria.
            return user is { Active: true } && PasswordHasher.Verify(password, user.PasswordHash) ? user : null;
        }
    }

    public AppUser? FindUser(Guid id) { lock (_gate) return _users.FirstOrDefault(u => u.Id == id); }
    public Inspection? FindInspection(Guid id) { lock (_gate) return _inspections.FirstOrDefault(i => i.Id == id); }

    public Property AddProperty(CreateProperty r)
    {
        lock (_gate)
        {
            var p = new Property(Guid.NewGuid(), _companyId, r.Title, r.Type, r.Address, r.Neighborhood, r.Area, r.Bedrooms, r.ParkingSpaces, "Desocupado", r.Owner, "https://images.unsplash.com/photo-1600585152915-d208bec867a1?auto=format&fit=crop&w=900&q=80", 0, 0);
            _properties.Add(p);
            Log("Cadastrou imóvel", p.Title, "Sistema", "Novo imóvel adicionado ao portfólio");
            return p;
        }
    }

    public Inspection AddInspection(CreateInspection r)
    {
        lock (_gate)
        {
            var p = _properties.FirstOrDefault(x => x.Id == r.PropertyId) ?? throw new KeyNotFoundException("Imóvel não encontrado.");
            var code = $"VIS-{DateTime.Today:yyyy}-{(_inspections.Count == 0 ? 0 : _inspections.Max(NumberOf)) + 1:0000}";
            var i = new Inspection(Guid.NewGuid(), _companyId, r.PropertyId, code, p.Title, r.Type, InspectionStatus.Agendada, r.ScheduledAt, r.Inspector, 0, 0, p.ImageUrl,
                r.Kind, r.ContractId, r.PreviousInspectionId);
            _inspections.Add(i);

            var template = r.TemplateId is { } tid ? _templates.FirstOrDefault(t => t.Id == tid) : null;
            var items = new List<InspectionItem>();
            if (template is not null)
                foreach (var room in template.Rooms)
                    foreach (var topic in room.Topics)
                        items.Add(new(Guid.NewGuid(), i.Id, room.Name, topic, ConditionStatus.NaoAvaliado, "", 0, IsCritical(topic)));
            _items[i.Id] = items;
            Log("Agendou vistoria", code, r.Inspector, template is null ? "Vistoria em branco" : $"Modelo: {template.Name} ({items.Count} itens)");
            return i;
        }
        static int NumberOf(Inspection x) => int.TryParse(x.Code.Split('-').Last(), out var n) ? n : 0;
    }

    // Tópicos ligados a segurança e a itens de entrega nascem obrigatórios; o vistoriador pode ajustar em campo.
    private static readonly string[] CriticalTopics = ["quadro", "disjuntor", "gás", "elétric", "chuveiro", "hidrômetro", "medidor", "fechadura", "extintor", "aquecedor", "infiltra", "estrutur"];
    private static bool IsCritical(string topic) => CriticalTopics.Any(k => topic.Contains(k, StringComparison.OrdinalIgnoreCase));

    public Inspection? CompleteInspection(Guid id, string actor)
    {
        lock (_gate)
        {
            var idx = _inspections.FindIndex(i => i.Id == id);
            if (idx < 0) return null;
            var items = _items.GetValueOrDefault(id) ?? [];
            var evaluated = items.Count(i => i.Condition != ConditionStatus.NaoAvaliado);
            var pending = items.Count(i => i.Condition is ConditionStatus.Ruim or ConditionStatus.Danificado);
            var completion = items.Count == 0 ? 100 : (int)Math.Round(evaluated * 100.0 / items.Count);
            var updated = _inspections[idx] with { Status = InspectionStatus.EmRevisao, Completion = completion, PendingItems = pending };
            _inspections[idx] = updated;
            Log("Concluiu vistoria", updated.Code, actor, $"{completion}% preenchido · {pending} pendência(s)");
            return updated;
        }
    }

    public IReadOnlyList<InspectionItem> GetItems(Guid inspectionId) { lock (_gate) return (_items.GetValueOrDefault(inspectionId) ?? []).ToList(); }

    public InspectionItem AddItem(Guid inspectionId, CreateItem r)
    {
        lock (_gate)
        {
            var list = _items.TryGetValue(inspectionId, out var l) ? l : _items[inspectionId] = [];
            var item = new InspectionItem(Guid.NewGuid(), inspectionId, r.Room.Trim(), r.Name.Trim(), ConditionStatus.NaoAvaliado, "", 0, r.Required);
            list.Add(item);
            RecalcCompletion(inspectionId);
            return item;
        }
    }

    public InspectionItem? UpdateItem(Guid inspectionId, Guid itemId, UpdateItem r)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(inspectionId, out var list)) return null;
            var idx = list.FindIndex(x => x.Id == itemId);
            if (idx < 0) return null;
            var updated = list[idx] with
            {
                Condition = r.Condition, Notes = r.Notes, Severity = r.Severity, IssueClass = r.IssueClass,
                Test = r.Test, Recommendation = r.Recommendation, ResponsibleParty = r.ResponsibleParty,
                DueDate = r.DueDate, EstimatedCost = r.EstimatedCost, Required = r.Required ?? list[idx].Required
            };
            list[idx] = updated;
            RecalcCompletion(inspectionId);
            return updated;
        }
    }

    public bool RemoveItem(Guid inspectionId, Guid itemId)
    {
        lock (_gate)
        {
            if (!_items.TryGetValue(inspectionId, out var list)) return false;
            var removed = list.RemoveAll(x => x.Id == itemId) > 0;
            if (removed) RecalcCompletion(inspectionId);
            return removed;
        }
    }

    public IReadOnlyList<Evidence> GetEvidence(Guid inspectionId) { lock (_gate) return (_evidence.GetValueOrDefault(inspectionId) ?? []).ToList(); }

    public Evidence AddEvidence(Guid inspectionId, CreateEvidence r, string capturedBy)
    {
        lock (_gate)
        {
            var list = _evidence.TryGetValue(inspectionId, out var l) ? l : _evidence[inspectionId] = [];
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(r.DataUrl + DateTime.UtcNow.Ticks));
            var shortHash = $"SHA-256: {Convert.ToHexString(hash)[..4].ToLowerInvariant()}...{Convert.ToHexString(hash)[^4..].ToLowerInvariant()}";
            var evidence = new Evidence(Guid.NewGuid(), inspectionId, r.ItemId, r.Room, r.DataUrl, DateTime.Now, r.Latitude, r.Longitude, r.Accuracy, shortHash, capturedBy);
            list.Add(evidence);
            if (r.ItemId is { } itemId && _items.TryGetValue(inspectionId, out var items))
            {
                var idx = items.FindIndex(x => x.Id == itemId);
                if (idx >= 0) items[idx] = items[idx] with { PhotoCount = items[idx].PhotoCount + 1 };
            }
            return evidence;
        }
    }

    public Property? FindProperty(Guid id) { lock (_gate) return _properties.FirstOrDefault(p => p.Id == id); }

    public InspectionTemplate AddTemplate(CreateTemplate r)
    {
        lock (_gate)
        {
            var template = new InspectionTemplate(Guid.NewGuid(), _companyId, r.Name.Trim(), r.Description?.Trim() ?? "", r.PropertyType, false,
                r.Rooms.Select(room => new TemplateRoom(room.Name.Trim(), room.Topics.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList())).ToList());
            _templates.Add(template);
            Log("Criou modelo", template.Name, "Sistema", $"{template.Rooms.Count} ambiente(s)");
            return template;
        }
    }

    public bool RemoveTemplate(Guid id)
    {
        lock (_gate)
        {
            var t = _templates.FirstOrDefault(x => x.Id == id);
            if (t is null || t.IsSystem) return false;
            return _templates.Remove(t);
        }
    }

    public Occurrence AddOccurrence(CreateOccurrence r)
    {
        lock (_gate)
        {
            var inspection = _inspections.FirstOrDefault(i => i.Id == r.InspectionId);
            var o = new Occurrence(Guid.NewGuid(), _companyId, r.InspectionId, r.Title.Trim(), r.Priority, "Aberta", r.DueDate, r.EstimatedCost, inspection?.PropertyName ?? "", r.Responsible, r.ItemId);
            _occurrences.Add(o);
            Log("Abriu ocorrência", o.Title, "Sistema", $"Prioridade {o.Priority}");
            return o;
        }
    }

    public Occurrence? UpdateOccurrenceStatus(Guid id, string status, string? resolution)
    {
        lock (_gate)
        {
            var idx = _occurrences.FindIndex(o => o.Id == id);
            if (idx < 0) return null;
            var updated = _occurrences[idx] = _occurrences[idx] with { Status = status, Resolution = resolution ?? _occurrences[idx].Resolution };
            Log("Atualizou ocorrência", updated.Title, "Sistema", $"Status: {status}");
            return updated;
        }
    }

    private void RecalcCompletion(Guid inspectionId)
    {
        var idx = _inspections.FindIndex(i => i.Id == inspectionId);
        if (idx < 0) return;
        var items = _items.GetValueOrDefault(inspectionId) ?? [];
        var evaluated = items.Count(i => i.Condition != ConditionStatus.NaoAvaliado);
        var pending = items.Count(i => i.Condition is ConditionStatus.Ruim or ConditionStatus.Danificado);
        var completion = items.Count == 0 ? 0 : (int)Math.Round(evaluated * 100.0 / items.Count);
        var status = _inspections[idx].Status;
        if (status is InspectionStatus.Agendada && evaluated > 0) status = InspectionStatus.EmAndamento;
        _inspections[idx] = _inspections[idx] with { Completion = completion, PendingItems = pending, Status = status };
    }

    // O lock é reentrante: Log também é chamado de dentro de operações que já seguram _gate.
    public void Log(string action, string entity, string actor, string detail)
    {
        lock (_gate) _audit.Insert(0, new(Guid.NewGuid(), _companyId, action, entity, actor, DateTime.Now, detail));
    }
}
