namespace TopologyServer;

public static class SimulationTime
{
    public static long FromMilliseconds(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0 ||
            milliseconds > SimulationExecutionLimits.MaximumDurationSeconds * 1000.0)
        {
            throw new ArgumentOutOfRangeException(nameof(milliseconds));
        }

        return checked((long)Math.Round(milliseconds * 1000, MidpointRounding.AwayFromZero));
    }
}
