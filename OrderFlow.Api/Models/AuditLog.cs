namespace OrderFlow.Api.Models;

public class AuditLog
{
    public int Id { get; set; }

    public string ActorId { get; set; } = string.Empty;

    public string ActorRole { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public string? Details { get; set; }
}