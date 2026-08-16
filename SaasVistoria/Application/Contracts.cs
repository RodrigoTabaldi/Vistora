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

    AppUser? Validate(string email, string password);
    AppUser? FindUser(Guid id);

    Property AddProperty(CreateProperty request);
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
    Occurrence? UpdateOccurrenceStatus(Guid id, string status);
}

public sealed record LoginRequest(string Email, string Password);
public sealed record CreateProperty(string Title, PropertyType Type, string Address, string Neighborhood, decimal Area, int Bedrooms, int ParkingSpaces, string Owner);
public sealed record CreateInspection(Guid PropertyId, string Type, DateTime ScheduledAt, string Inspector, Guid? TemplateId);
public sealed record CreateItem(string Room, string Name);
public sealed record UpdateItem(ConditionStatus Condition, string Notes);
public sealed record CreateEvidence(Guid? ItemId, string Room, string DataUrl, decimal? Latitude, decimal? Longitude, decimal? Accuracy);
public sealed record CreateTemplate(string Name, string Description, PropertyType? PropertyType, IReadOnlyList<TemplateRoom> Rooms);
public sealed record CreateOccurrence(Guid InspectionId, string Title, string Priority, DateTime DueDate, decimal EstimatedCost);

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
        return new { accessToken, refreshToken = B64Url(RandomNumberGenerator.GetBytes(32)), expiresIn = 28800, user = new { user.Name, user.Email, user.Role } };
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
