using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TopologyServer;

public class ConnectionRule
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public ComponentType SourceType { get; set; }

    public ComponentType TargetType { get; set; }

    public bool IsAllowed { get; set; }

    public ValidationSeverity Severity { get; set; }

    public string Message { get; set; } = string.Empty;
}