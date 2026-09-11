namespace TopologyServer;

public class DesignValidationException : Exception
{
    public DesignValidationException(DesignValidationResult result)
        : base("Design validation failed")
    {
        Result = result;
    }

    public DesignValidationResult Result { get; }
}