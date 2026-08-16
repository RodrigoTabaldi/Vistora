namespace SaasVistoria.Domain;

public enum InspectionStatus { Rascunho, Agendada, EmAndamento, EmRevisao, AguardandoAssinatura, Concluida, Contestada }
public enum PropertyType { Apartamento, Casa, Comercial, Terreno, Condominio }
public enum ConditionStatus { Otimo, Bom, Regular, Ruim, Danificado, Inexistente, NaoAvaliado }

// Tipo de vistoria — regra de negócio depende dele (ex.: saída exige vistoria de entrada vinculada).
public enum InspectionKind { Entrada, Saida, Periodica, Manutencao, Recebimento, PreCompraVenda, Captacao, Temporada, Sinistro, InspecaoPredial }
public enum Severity { Nenhuma, Baixa, Media, Alta, Critica }
// Classificação de responsabilidade: sempre sugestão sujeita a validação humana (Lei 8.245/1991, art. 23, III — ressalva do desgaste natural).
public enum IssueClass { NaoClassificado, DesgasteNatural, DanoAnterior, DanoLocatario, VicioConstrutivo, ManutencaoProprietario, UrgenciaSeguranca, Inconclusivo }
public enum TestOutcome { NaoTestado, Aprovado, Reprovado, Parcial }
public enum PartyRole { Locador, Locatario, Fiador, Procurador, Vistoriador, Corretor, Testemunha }
public enum SignatureMethod { Desenhada, LinkRemoto, Otp }
public enum ContestationStatus { Aberta, EmAnalise, EvidenciaSolicitada, Aceita, Rejeitada, ParcialmenteAceita, Resolvida }
public enum UtilityKind { Agua, Energia, Gas }
public enum ComparisonVerdict { SemAlteracao, NovoDano, Melhoria, ItemRemovido, ItemAdicionado, AlteracaoNaoIdentificada }

public sealed record Company(Guid Id, string Name, string Slug, string Plan, int UserLimit, int PropertyLimit, string? LogoUrl = null, int ContestationDays = 7);
public sealed record Property(Guid Id, Guid CompanyId, string Title, PropertyType Type, string Address, string Neighborhood, decimal Area, int Bedrooms, int ParkingSpaces, string Occupancy, string Owner, string ImageUrl, decimal Latitude, decimal Longitude);
public sealed record Inspection(
    Guid Id, Guid CompanyId, Guid PropertyId, string Code, string PropertyName, string Type, InspectionStatus Status,
    DateTime ScheduledAt, string Inspector, int Completion, int PendingItems, string? CoverUrl,
    InspectionKind Kind = InspectionKind.Entrada, Guid? ContractId = null, Guid? PreviousInspectionId = null,
    DateTime? CheckInAt = null, decimal? CheckInLatitude = null, decimal? CheckInLongitude = null, DateTime? CheckOutAt = null);

public sealed record InspectionItem(
    Guid Id, Guid InspectionId, string Room, string Name, ConditionStatus Condition, string Notes, int PhotoCount,
    bool Required = false, Severity Severity = Severity.Nenhuma, IssueClass IssueClass = IssueClass.NaoClassificado,
    TestOutcome Test = TestOutcome.NaoTestado, string? Recommendation = null, string? ResponsibleParty = null,
    DateTime? DueDate = null, decimal EstimatedCost = 0);

public sealed record Evidence(Guid Id, Guid InspectionId, Guid? ItemId, string Room, string Url, DateTime CapturedAt, decimal? Latitude, decimal? Longitude, decimal? Accuracy, string IntegrityHash, string CapturedBy);
public sealed record Occurrence(Guid Id, Guid CompanyId, Guid InspectionId, string Title, string Priority, string Status, DateTime DueDate, decimal EstimatedCost, string PropertyName, string Responsible = "Imobiliária", Guid? ItemId = null, string? Resolution = null);
public sealed record AuditEvent(Guid Id, Guid CompanyId, string Action, string Entity, string Actor, DateTime OccurredAt, string Detail);
public sealed record AppUser(Guid Id, Guid CompanyId, string Name, string Email, string Role, string PasswordHash, bool Active = true);

// Templates de vistoria: ambientes (rooms) com tópicos personalizáveis
public sealed record TemplateRoom(string Name, IReadOnlyList<string> Topics);
public sealed record InspectionTemplate(Guid Id, Guid CompanyId, string Name, string Description, PropertyType? PropertyType, bool IsSystem, IReadOnlyList<TemplateRoom> Rooms);

// ---- Partes, contratos e cadastros de apoio ----
public sealed record Person(Guid Id, Guid CompanyId, string Name, string Document, string Email, string Phone, PartyRole Role)
{
    // LGPD: nunca trafegar o CPF completo em telas de consulta.
    public string MaskedDocument => Document.Length >= 11 ? $"***.{Document[3..6]}.{Document[6..9]}-**" : "***";
}

public sealed record LeaseContract(Guid Id, Guid CompanyId, Guid PropertyId, string Code, Guid LandlordId, Guid TenantId, Guid? GuarantorId, DateTime StartsOn, DateTime EndsOn, decimal RentValue, string Guarantee, string Status);

public sealed record MeterReading(Guid Id, Guid InspectionId, UtilityKind Kind, string MeterNumber, decimal Value, DateTime ReadAt, string? PhotoUrl);
public sealed record KeyHandover(Guid Id, Guid InspectionId, string Description, int Quantity, ConditionStatus Condition);
public sealed record InventoryAsset(Guid Id, Guid InspectionId, string Room, string Name, string Brand, string Model, string SerialNumber, int Quantity, ConditionStatus Condition, decimal ReferenceValue, bool Working);

// ---- Laudo, assinaturas e contestações ----
// Laudo selado é imutável: correções geram nova versão com trilha de auditoria.
public sealed record InspectionReport(Guid Id, Guid CompanyId, Guid InspectionId, string Number, int Version, DateTime IssuedAt, string IssuedBy, string Hash, bool Sealed_, string Html);

public sealed record Signature(
    Guid Id, Guid CompanyId, Guid ReportId, Guid InspectionId, string SignerName, string SignerEmail, PartyRole Role,
    SignatureMethod Method, DateTime SignedAt, string Ip, string UserAgent, decimal? Latitude, decimal? Longitude,
    string? ImageDataUrl, string Hash, bool Refused = false, string? RefusalReason = null);

public sealed record SignatureRequest(Guid Id, Guid CompanyId, Guid ReportId, string SignerName, string SignerEmail, PartyRole Role, string Token, string? OtpCode, DateTime ExpiresAt, bool Completed);

public sealed record ContestationMessage(Guid Id, string Author, string Text, DateTime SentAt, string? AttachmentUrl);
public sealed record Contestation(Guid Id, Guid CompanyId, Guid InspectionId, Guid? ItemId, string ItemLabel, string Author, string Reason, ContestationStatus Status, DateTime OpenedAt, DateTime Deadline, string? Decision, IReadOnlyList<ContestationMessage> Messages);

// ---- Comparação entrada × saída ----
public sealed record ComparisonLine(string Room, string Item, ConditionStatus? Before, ConditionStatus? After, ComparisonVerdict Verdict, Severity Severity, IssueClass SuggestedClass, string Note, decimal EstimatedCost);
public sealed record ComparisonResult(Guid EntryInspectionId, Guid ExitInspectionId, string EntryCode, string ExitCode, string PropertyName, IReadOnlyList<ComparisonLine> Lines, int Divergences, decimal EstimatedTotal);

// ---- Bloqueios e alertas de conclusão ----
public sealed record ValidationIssue(string Code, string Message, bool Blocking);
