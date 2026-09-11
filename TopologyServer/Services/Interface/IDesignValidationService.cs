namespace TopologyServer;

public interface IDesignValidationService
{
    Task<DesignValidationResult> ValidateDesignAsync(SaveDesignDto design);
}