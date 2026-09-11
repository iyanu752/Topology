namespace TopologyServer;

public class DesignValidationResult
{
    public bool IsValid => Issues.All(issue => issue.Severity != ValidationSeverity.Error);
    public List<DesignValidationIssue> Issues { get; set; } = [];
}