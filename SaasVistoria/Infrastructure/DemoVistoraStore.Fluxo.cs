using SaasVistoria.Application;
using SaasVistoria.Domain;

namespace SaasVistoria.Infrastructure;

/// <summary>
/// Segunda metade do store de demonstração: partes e contratos, medidores, chaves, inventário,
/// check-in/check-out, laudos, assinaturas e contestações.
/// </summary>
public sealed partial class DemoVistoraStore
{
    private readonly List<Person> _people = [];
    private readonly List<LeaseContract> _contracts = [];
    private readonly Dictionary<Guid, List<MeterReading>> _meters = new();
    private readonly Dictionary<Guid, List<KeyHandover>> _keys = new();
    private readonly Dictionary<Guid, List<InventoryAsset>> _inventory = new();
    private readonly List<InspectionReport> _reports = [];
    private readonly List<Signature> _signatures = [];
    private readonly List<SignatureRequest> _signatureRequests = [];
    private readonly List<Contestation> _contestations = [];

    public IReadOnlyList<Person> People { get { lock (_gate) return _people.ToList(); } }
    public IReadOnlyList<LeaseContract> Contracts { get { lock (_gate) return _contracts.ToList(); } }
    public IReadOnlyList<AppUser> Users { get { lock (_gate) return _users.ToList(); } }
    public IReadOnlyList<InspectionReport> Reports { get { lock (_gate) return _reports.ToList(); } }
    public IReadOnlyList<Contestation> Contestations { get { lock (_gate) return _contestations.ToList(); } }

    private void SeedParties()
    {
        _users.AddRange([
            new(Guid.NewGuid(), _companyId, "Lucas Mendes", "lucas@atelierimoveis.com.br", "Vistoriador", PasswordHasher.Hash("Vistora@2026")),
            new(Guid.NewGuid(), _companyId, "Helena Sampaio", "helena@proprietaria.com.br", "Proprietário", PasswordHasher.Hash("Vistora@2026")),
            new(Guid.NewGuid(), _companyId, "Bruno Tavares", "bruno@locatario.com.br", "Locatário", PasswordHasher.Hash("Vistora@2026"))
        ]);

        var landlord = new Person(Guid.NewGuid(), _companyId, "Helena Sampaio", "31245678900", "helena@proprietaria.com.br", "(11) 98888-1010", PartyRole.Locador);
        var tenant = new Person(Guid.NewGuid(), _companyId, "Bruno Tavares", "40988877612", "bruno@locatario.com.br", "(11) 97777-2020", PartyRole.Locatario);
        var guarantor = new Person(Guid.NewGuid(), _companyId, "Sofia Tavares", "22133344455", "sofia@fiadora.com.br", "(11) 96666-3030", PartyRole.Fiador);
        _people.AddRange([landlord, tenant, guarantor]);

        var contract = new LeaseContract(Guid.NewGuid(), _companyId, _properties[0].Id, "LOC-2024-0031", landlord.Id, tenant.Id, guarantor.Id,
            DateTime.Today.AddYears(-2), DateTime.Today.AddDays(20), 6400, "Fiador", "Vigente");
        _contracts.Add(contract);
        _inspections[1] = _inspections[1] with { ContractId = contract.Id };
        _inspections[0] = _inspections[0] with { ContractId = contract.Id };

        _meters[_inspections[0].Id] = [
            new(Guid.NewGuid(), _inspections[0].Id, UtilityKind.Agua, "A-99120", 1420, DateTime.Today.AddYears(-2), null),
            new(Guid.NewGuid(), _inspections[0].Id, UtilityKind.Energia, "E-55471", 8410, DateTime.Today.AddYears(-2), null)
        ];
        _keys[_inspections[0].Id] = [
            new(Guid.NewGuid(), _inspections[0].Id, "Chave da porta social", 2, ConditionStatus.Otimo),
            new(Guid.NewGuid(), _inspections[0].Id, "Controle do portão da garagem", 1, ConditionStatus.Bom)
        ];
    }

    // ---- Partes e contratos ----
    public Person AddPerson(CreatePerson r)
    {
        lock (_gate)
        {
            var person = new Person(Guid.NewGuid(), _companyId, r.Name.Trim(), new string(r.Document.Where(char.IsDigit).ToArray()), r.Email.Trim(), r.Phone.Trim(), r.Role);
            _people.Add(person);
            Log("Cadastrou pessoa", person.Name, "Sistema", $"Perfil: {person.Role}");
            return person;
        }
    }

    public LeaseContract AddContract(CreateContract r)
    {
        lock (_gate)
        {
            var code = $"LOC-{DateTime.Today:yyyy}-{_contracts.Count + 1:0000}";
            var contract = new LeaseContract(Guid.NewGuid(), _companyId, r.PropertyId, code, r.LandlordId, r.TenantId, r.GuarantorId, r.StartsOn, r.EndsOn, r.RentValue, r.Guarantee, "Vigente");
            _contracts.Add(contract);
            Log("Cadastrou contrato", code, "Sistema", $"Vigência {r.StartsOn:dd/MM/yyyy} a {r.EndsOn:dd/MM/yyyy}");
            return contract;
        }
    }

    public LeaseContract? FindContract(Guid id) { lock (_gate) return _contracts.FirstOrDefault(c => c.Id == id); }

    // ---- Medidores, chaves e inventário ----
    public IReadOnlyList<MeterReading> GetMeters(Guid id) { lock (_gate) return (_meters.GetValueOrDefault(id) ?? []).ToList(); }

    public MeterReading AddMeter(Guid id, CreateMeterReading r)
    {
        lock (_gate)
        {
            var list = _meters.TryGetValue(id, out var l) ? l : _meters[id] = [];
            var reading = new MeterReading(Guid.NewGuid(), id, r.Kind, r.MeterNumber.Trim(), r.Value, DateTime.Now, r.PhotoUrl);
            list.Add(reading);
            return reading;
        }
    }

    public IReadOnlyList<KeyHandover> GetKeys(Guid id) { lock (_gate) return (_keys.GetValueOrDefault(id) ?? []).ToList(); }

    public KeyHandover AddKey(Guid id, CreateKeyHandover r)
    {
        lock (_gate)
        {
            var list = _keys.TryGetValue(id, out var l) ? l : _keys[id] = [];
            var key = new KeyHandover(Guid.NewGuid(), id, r.Description.Trim(), Math.Max(1, r.Quantity), r.Condition);
            list.Add(key);
            return key;
        }
    }

    public IReadOnlyList<InventoryAsset> GetInventory(Guid id) { lock (_gate) return (_inventory.GetValueOrDefault(id) ?? []).ToList(); }

    public InventoryAsset AddInventory(Guid id, CreateInventoryAsset r)
    {
        lock (_gate)
        {
            var list = _inventory.TryGetValue(id, out var l) ? l : _inventory[id] = [];
            var asset = new InventoryAsset(Guid.NewGuid(), id, r.Room.Trim(), r.Name.Trim(), r.Brand ?? "", r.Model ?? "", r.SerialNumber ?? "", Math.Max(1, r.Quantity), r.Condition, r.ReferenceValue, r.Working);
            list.Add(asset);
            return asset;
        }
    }

    // ---- Check-in / check-out ----
    public Inspection? CheckIn(Guid id, decimal? latitude, decimal? longitude, string actor)
    {
        lock (_gate)
        {
            var idx = _inspections.FindIndex(i => i.Id == id);
            if (idx < 0) return null;
            var updated = _inspections[idx] with { CheckInAt = DateTime.Now, CheckInLatitude = latitude, CheckInLongitude = longitude, Status = InspectionStatus.EmAndamento };
            _inspections[idx] = updated;
            Log("Check-in em campo", updated.Code, actor, latitude is null ? "Sem geolocalização autorizada" : $"GPS {latitude:0.0000}, {longitude:0.0000}");
            return updated;
        }
    }

    public Inspection? CheckOut(Guid id, string actor)
    {
        lock (_gate)
        {
            var idx = _inspections.FindIndex(i => i.Id == id);
            if (idx < 0) return null;
            var updated = _inspections[idx] with { CheckOutAt = DateTime.Now };
            _inspections[idx] = updated;
            Log("Check-out em campo", updated.Code, actor, "Visita encerrada");
            return updated;
        }
    }

    // ---- Laudos ----
    public InspectionReport? FindReport(Guid id) { lock (_gate) return _reports.FirstOrDefault(r => r.Id == id); }
    public InspectionReport? FindReportByNumber(string number) { lock (_gate) return _reports.FirstOrDefault(r => r.Number.Equals(number, StringComparison.OrdinalIgnoreCase)); }

    public InspectionReport AddReport(InspectionReport report)
    {
        lock (_gate)
        {
            _reports.Add(report);
            var idx = _inspections.FindIndex(i => i.Id == report.InspectionId);
            if (idx >= 0) _inspections[idx] = _inspections[idx] with { Status = InspectionStatus.AguardandoAssinatura };
            Log("Emitiu laudo", report.Number, report.IssuedBy, $"Versão {report.Version} · hash {report.Hash[..12]}…");
            return report;
        }
    }

    // ---- Assinaturas ----
    public IReadOnlyList<Signature> GetSignatures(Guid reportId) { lock (_gate) return _signatures.Where(s => s.ReportId == reportId).ToList(); }

    public Signature AddSignature(Signature signature)
    {
        lock (_gate)
        {
            _signatures.Add(signature);
            Log(signature.Refused ? "Recusou assinatura" : "Assinou laudo", signature.SignerName, signature.SignerName,
                signature.Refused ? signature.RefusalReason ?? "Sem motivo informado" : $"{signature.Method} · IP {signature.Ip}");

            // Todas as partes convidadas assinaram → vistoria concluída.
            var pending = _signatureRequests.Any(x => x.ReportId == signature.ReportId && !x.Completed);
            if (!pending && !signature.Refused)
            {
                var idx = _inspections.FindIndex(i => i.Id == signature.InspectionId);
                if (idx >= 0) _inspections[idx] = _inspections[idx] with { Status = InspectionStatus.Concluida };
            }
            return signature;
        }
    }

    public SignatureRequest AddSignatureRequest(SignatureRequest request)
    {
        lock (_gate) { _signatureRequests.Add(request); return request; }
    }

    public SignatureRequest? FindSignatureRequest(string token)
    {
        lock (_gate) return _signatureRequests.FirstOrDefault(x => x.Token == token && x.ExpiresAt > DateTime.Now);
    }

    public void CompleteSignatureRequest(Guid id)
    {
        lock (_gate)
        {
            var idx = _signatureRequests.FindIndex(x => x.Id == id);
            if (idx >= 0) _signatureRequests[idx] = _signatureRequests[idx] with { Completed = true };
        }
    }

    // ---- Contestações ----
    public Contestation AddContestation(Contestation contestation)
    {
        lock (_gate)
        {
            _contestations.Add(contestation);
            var idx = _inspections.FindIndex(i => i.Id == contestation.InspectionId);
            if (idx >= 0) _inspections[idx] = _inspections[idx] with { Status = InspectionStatus.Contestada };
            Log("Abriu contestação", contestation.ItemLabel, contestation.Author, contestation.Reason);
            return contestation;
        }
    }

    public Contestation? UpdateContestation(Guid id, ContestationStatus status, string? decision, ContestationMessage? message)
    {
        lock (_gate)
        {
            var idx = _contestations.FindIndex(c => c.Id == id);
            if (idx < 0) return null;
            var current = _contestations[idx];
            var messages = message is null ? current.Messages : [.. current.Messages, message];
            var updated = current with { Status = status, Decision = decision ?? current.Decision, Messages = messages };
            _contestations[idx] = updated;
            Log("Atualizou contestação", updated.ItemLabel, message?.Author ?? "Sistema", $"Status: {status}");
            return updated;
        }
    }
}
