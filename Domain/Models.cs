namespace SaasVistoria.Domain;

public enum InspectionStatus { Rascunho, Agendada, EmAndamento, EmRevisao, AguardandoAssinatura, Concluida, Contestada }
public enum PropertyType { Apartamento, Casa, Comercial, Terreno, Condominio }
public enum ConditionStatus { Otimo, Bom, Regular, Ruim, Danificado, Inexistente, NaoAvaliado }

public sealed record Company(Guid Id, string Name, string Slug, string Plan, int UserLimit, int PropertyLimit);
public sealed record Property(Guid Id, Guid CompanyId, string Title, PropertyType Type, string Address, string Neighborhood, decimal Area, int Bedrooms, int ParkingSpaces, string Occupancy, string Owner, string ImageUrl, decimal Latitude, decimal Longitude);
public sealed record Inspection(Guid Id, Guid CompanyId, Guid PropertyId, string Code, string PropertyName, string Type, InspectionStatus Status, DateTime ScheduledAt, string Inspector, int Completion, int PendingItems, string? CoverUrl, string? SignedBy = null, DateTime? SignedAt = null);
public sealed record InspectionItem(Guid Id, Guid InspectionId, string Room, string Name, ConditionStatus Condition, string Notes, int PhotoCount);
public sealed record Evidence(Guid Id, Guid InspectionId, Guid? ItemId, string Room, string Url, DateTime CapturedAt, decimal? Latitude, decimal? Longitude, decimal? Accuracy, string IntegrityHash, string CapturedBy);
// ItemId != null identifica a ocorrência aberta automaticamente por um item irregular do checklist;
// as abertas à mão pelo vistoriador ficam com ItemId nulo e nunca são mexidas pela automação.
public sealed record Occurrence(Guid Id, Guid CompanyId, Guid InspectionId, string Title, string Priority, string Status, DateTime DueDate, decimal EstimatedCost, string PropertyName, string? Room = null, Guid? ItemId = null);
public sealed record AuditEvent(Guid Id, Guid CompanyId, string Action, string Entity, string Actor, DateTime OccurredAt, string Detail);
public sealed record AppUser(Guid Id, Guid CompanyId, string Name, string Email, string Role, string PasswordHash);

// Templates de vistoria: ambientes (rooms) com tópicos personalizáveis
public sealed record TemplateRoom(string Name, IReadOnlyList<string> Topics);
public sealed record InspectionTemplate(Guid Id, Guid CompanyId, string Name, string Description, PropertyType? PropertyType, bool IsSystem, IReadOnlyList<TemplateRoom> Rooms);
