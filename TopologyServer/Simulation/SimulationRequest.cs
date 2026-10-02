namespace TopologyServer;

public enum SimulationRequestOutcome { InFlight, Succeeded, Failed, TimedOut }

public sealed class SimulationRequest(long id, long createdAt, long deadline, bool isRead)
{
    public long Id { get; } = id;
    public long CreatedAt { get; } = createdAt;
    public long Deadline { get; } = deadline;
    public bool IsRead { get; } = isRead;
    public SimulationRequestOutcome Outcome { get; private set; }
    public long? FinishedAt { get; private set; }
    public string? FailureCause { get; private set; }
    public bool DatabaseExecuting { get; set; }
    public bool IsPending => Outcome == SimulationRequestOutcome.InFlight;

    public bool Finish(SimulationRequestOutcome outcome, long now, string? cause = null)
    {
        if (!IsPending) return false;
        Outcome = outcome;
        FinishedAt = now;
        FailureCause = cause;
        return true;
    }
}
