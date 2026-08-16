using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SaasVistoria.Domain;

namespace SaasVistoria.Application;

public interface IVistoraStore
{
    Company Company { get; }
    IReadOnlyList<Property> Properties { get; }
    IReadOnlyList<Inspection> Inspections { get; }
    IReadOnlyList<Occurrence> Occurrences { get; }
    IReadOnlyList<AuditEvent> Audit { get; }
    IReadOnlyList<InspectionTemplate> Templates { get; }
    IReadOnlyList<Person> People { get; }
    IReadOnlyList<LeaseContract> Contracts { get; }
    IReadOnlyList<AppUser> Users { get; }

    AppUser? Validate(string email, string password);
    AppUser? FindUser(Guid id);
    void Log(string action, string entity, string actor, string detail);

    Property AddProperty(CreateProperty request);
    Property? FindProperty(Guid id);
    Inspection AddInspection(CreateInspection request);
    Inspection? FindInspection(Guid id);
    Inspection? CompleteInspection(Guid id, string actor);

    IReadOnlyList<InspectionItem> GetItems(Guid inspectionId);
    InspectionItem AddItem(Guid inspectionId, CreateItem request);
    InspectionItem? UpdateItem(Guid inspectionId, Guid itemId, UpdateItem request);
    bool RemoveItem(Guid inspectionId, Guid itemId);

    IReadOnlyList<Evidence> GetEvidence(Guid inspectionId);
    Evidence AddEvidence(Guid inspectionId, CreateEvidence request, string capturedBy);

    InspectionTemplate AddTemplate(CreateTemplate request);
    bool RemoveTemplate(Guid id);

    Occurrence AddOccurrence(CreateOccurrence request);
    Occurrence? UpdateOccurrenceStatus(Guid id, string status, string? resolution);

    // ---- Partes e contratos ----
    Person AddPerson(CreatePerson request);
    LeaseContract AddContract(CreateContract request);
    LeaseContract? FindContract(Guid id);

    // ---- Medidores, chaves e inventário ----
    IReadOnlyList<MeterReading> GetMeters(Guid inspectionId);
    MeterReading AddMeter(Guid inspectionId, CreateMeterReading request);
    IReadOnlyList<KeyHandover> GetKeys(Guid inspectionId);
    KeyHandover AddKey(Guid inspectionId, CreateKeyHandover request);
    IReadOnlyList<InventoryAsset> GetInventory(Guid inspectionId);
    InventoryAsset AddInventory(Guid inspectionId, CreateInventoryAsset request);

    // ---- Check-in / check-out ----
    Inspection? CheckIn(Guid id, decimal? latitude, decimal? longitude, string actor);
    Inspection? CheckOut(Guid id, string actor);

    // ---- Laudo, assinaturas e contestações ----
    IReadOnlyList<InspectionReport> Reports { get; }
    InspectionReport? FindReport(Guid id);
    InspectionReport? FindReportByNumber(string number);
    InspectionReport AddReport(InspectionReport report);
    IReadOnlyList<Signature> GetSignatures(Guid reportId);
    Signature AddSignature(Signature signature);
    SignatureRequest AddSignatureRequest(SignatureRequest request);
    SignatureRequest? FindSignatureRequest(string token);
    void CompleteSignatureRequest(Guid id);
    IReadOnlyList<Contestation> Contestations { get; }
    Contestation AddContestation(Contestation contestation);
    Contestation? UpdateContestation(Guid id, ContestationStatus status, string? decision, ContestationMessage? message);
}

public sealed record LoginRequest(string Email, string Password);
public sealed record CreateProperty(string Title, PropertyType Type, string Address, string Neighborhood, decimal Area, int Bedrooms, int ParkingSpaces, string Owner);
public sealed record CreateInspection(Guid PropertyId, string Type, DateTime ScheduledAt, string Inspector, Guid? TemplateId, InspectionKind Kind = InspectionKind.Entrada, Guid? ContractId = null, Guid? PreviousInspectionId = null);
public sealed record CreateItem(string Room, string Name, bool Required = false);
public sealed record UpdateItem(ConditionStatus Condition, string Notes, Severity Severity = Severity.Nenhuma, IssueClass IssueClass = IssueClass.NaoClassificado, TestOutcome Test = TestOutcome.NaoTestado, string? Recommendation = null, string? ResponsibleParty = null, DateTime? DueDate = null, decimal EstimatedCost = 0, bool? Required = null);
public sealed record CreateEvidence(Guid? ItemId, string Room, string DataUrl, decimal? Latitude, decimal? Longitude, decimal? Accuracy);
public sealed record CreateTemplate(string Name, string Description, PropertyType? PropertyType, IReadOnlyList<TemplateRoom> Rooms);
public sealed record CreateOccurrence(Guid InspectionId, string Title, string Priority, DateTime DueDate, decimal EstimatedCost, string Responsible = "Imobiliária", Guid? ItemId = null);
public sealed record UpdateOccurrenceRequest(string Status, string? Resolution);
public sealed record CreatePerson(string Name, string Document, string Email, string Phone, PartyRole Role);
public sealed record CreateContract(Guid PropertyId, Guid LandlordId, Guid TenantId, Guid? GuarantorId, DateTime StartsOn, DateTime EndsOn, decimal RentValue, string Guarantee);
public sealed record CreateMeterReading(UtilityKind Kind, string MeterNumber, decimal Value, string? PhotoUrl);
public sealed record CreateKeyHandover(string Description, int Quantity, ConditionStatus Condition);
public sealed record CreateInventoryAsset(string Room, string Name, string Brand, string Model, string SerialNumber, int Quantity, ConditionStatus Condition, decimal ReferenceValue, bool Working);
public sealed record CheckInRequest(decimal? Latitude, decimal? Longitude);
public sealed record RequestSignature(string SignerName, string SignerEmail, PartyRole Role, SignatureMethod Method);
public sealed record SignRequest(string Token, string? Otp, string? ImageDataUrl, decimal? Latitude, decimal? Longitude, bool Refused = false, string? RefusalReason = null);
public sealed record OpenContestation(Guid InspectionId, Guid? ItemId, string Author, string Reason);
public sealed record UpdateContestationRequest(ContestationStatus Status, string? Decision, string? Message, string? Author, string? AttachmentUrl);

// Perfis e permissões granulares por função (visualizar, criar, editar, aprovar, assinar, exportar, excluir).
public static class Permissions
{
    public const string View = "visualizar", Create = "criar", Edit = "editar", Approve = "aprovar", Sign = "assinar", Export = "exportar", Delete = "excluir";

    private static readonly Dictionary<string, string[]> ByRole = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Administrador"] = [View, Create, Edit, Approve, Sign, Export, Delete],
        ["Gestor"] = [View, Create, Edit, Approve, Export],
        ["Vistoriador"] = [View, Create, Edit, Sign],
        ["Vistoriador terceirizado"] = [View, Create, Edit],
        ["Corretor"] = [View, Export],
        ["Proprietário"] = [View, Sign, Export],
        ["Locatário"] = [View, Sign, Export],
        ["Auditor"] = [View, Export]
    };

    public static string[] For(string role) => ByRole.TryGetValue(role, out var p) ? p : [View];
    public static bool Allows(string role, string permission) => For(role).Contains(permission);
}

// Hashing PBKDF2 (SHA-256, 120k iterações, salt de 16 bytes) — formato: iterations.salt.hash em base64
public static class PasswordHasher
{
    private const int Iterations = 120_000, SaltSize = 16, KeySize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[1]);
        var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

// JWT assinado com HMAC-SHA256 (sem dependências externas)
public sealed class TokenService(IConfiguration config)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private byte[] Key => Encoding.UTF8.GetBytes(config["Jwt:Key"] is { Length: >= 32 } k ? k : "vistora-chave-de-desenvolvimento-local-minimo-32-caracteres");

    private static string B64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromB64Url(string s) => Convert.FromBase64String(s.Replace('-', '+').Replace('_', '/').PadRight(s.Length + (4 - s.Length % 4) % 4, '='));

    public object Create(AppUser user)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(8);
        var header = B64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "HS256", typ = "JWT" }));
        var payload = B64Url(JsonSerializer.SerializeToUtf8Bytes(new { sub = user.Id, email = user.Email, name = user.Name, role = user.Role, exp = expires.ToUnixTimeSeconds() }));
        var signature = B64Url(HMACSHA256.HashData(Key, Encoding.ASCII.GetBytes($"{header}.{payload}")));
        var accessToken = $"{header}.{payload}.{signature}";
        return new { accessToken, refreshToken = B64Url(RandomNumberGenerator.GetBytes(32)), expiresIn = 28800, user = new { user.Name, user.Email, user.Role, permissions = Permissions.For(user.Role) } };
    }

    public Guid? ValidateAndGetUserId(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 3) return null;
        var expected = B64Url(HMACSHA256.HashData(Key, Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}")));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2]))) return null;
        try
        {
            var claims = JsonSerializer.Deserialize<JwtClaims>(FromB64Url(parts[1]), Json);
            if (claims is null || claims.Exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return null;
            return claims.Sub;
        }
        catch { return null; }
    }

    private sealed record JwtClaims(Guid Sub, long Exp);
}
