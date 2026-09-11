namespace TopologyServer;

public class DesignValidationIssue
{
    public ValidationSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? NodeId { get; set; }
    public string? EdgeId { get; set; }
    public string? PropertyKey { get; set; }
}