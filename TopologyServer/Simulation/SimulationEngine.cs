namespace TopologyServer;

public enum SimulationEventPriority { ScenarioChange, Deadline, Ordinary }

public sealed record SimulationEngineRunResult(
    string EngineVersion,
    SimulationExecutionStatus Status,
    SimulationStopReason StopReason,
    long ElapsedMicroseconds,
    long ProcessedEvents,
    int PendingEvents,
    int GeneratedRequests,
    int RetainedRecords);

public sealed class SimulationEngine
{
    public const string Version = "discrete-event-v1-splitmix64";

    private readonly PriorityQueue<Action<SimulationEngine>, (long Time, int Priority, long Sequence)> _events = new();
    private readonly int _maxProcessedEvents;
    private readonly int _maxPendingEvents;
    private readonly int _maxGeneratedRequests;
    private readonly int _maxRetainedRecords;
    private long _sequence;
    private bool _started;
    private bool _finished;
    private SimulationEventPriority? _currentPriority;
    private SimulationStopReason? _stopReason;

    public long NowMicroseconds { get; private set; }
    public long DurationMicroseconds { get; }
    public long ProcessedEvents { get; private set; }
    public int PendingEvents => _events.Count;
    public int GeneratedRequests { get; private set; }
    public int RetainedRecords { get; private set; }
    public SimulationRandom Random { get; }

    public SimulationEngine(int durationSeconds = 60, int randomSeed = 1, SimulationExecutionLimits? limits = null)
    {
        ValidateLimit(durationSeconds, SimulationExecutionLimits.MaximumDurationSeconds, nameof(durationSeconds));
        limits ??= new SimulationExecutionLimits();
        ValidateLimit(limits.MaxProcessedEvents, SimulationExecutionLimits.MaximumProcessedEvents, nameof(limits.MaxProcessedEvents));
        ValidateLimit(limits.MaxPendingEvents, SimulationExecutionLimits.MaximumPendingEvents, nameof(limits.MaxPendingEvents));
        ValidateLimit(limits.MaxGeneratedRequests, SimulationExecutionLimits.MaximumGeneratedRequests, nameof(limits.MaxGeneratedRequests));
        ValidateLimit(limits.MaxRetainedRecords, SimulationExecutionLimits.MaximumRetainedRecords, nameof(limits.MaxRetainedRecords));
        _maxProcessedEvents = limits.MaxProcessedEvents;
        _maxPendingEvents = limits.MaxPendingEvents;
        _maxGeneratedRequests = limits.MaxGeneratedRequests;
        _maxRetainedRecords = limits.MaxRetainedRecords;
        DurationMicroseconds = durationSeconds * 1_000_000L;
        Random = new SimulationRandom(randomSeed);
    }

    public bool Schedule(long atMicroseconds, SimulationEventPriority priority, Action<SimulationEngine> action)
    {
        EnsureNotFinished();
        ArgumentNullException.ThrowIfNull(action);
        if (!Enum.IsDefined(priority)) throw new ArgumentOutOfRangeException(nameof(priority));
        if (atMicroseconds < NowMicroseconds) throw new ArgumentOutOfRangeException(nameof(atMicroseconds));
        if (atMicroseconds == NowMicroseconds && _currentPriority.HasValue && priority < _currentPriority.Value)
        {
            throw new InvalidOperationException("An event cannot schedule an earlier priority at the current time.");
        }
        if (_stopReason.HasValue || atMicroseconds > DurationMicroseconds) return false;
        if (_events.Count >= _maxPendingEvents)
        {
            _stopReason = SimulationStopReason.PendingEventLimit;
            return false;
        }

        _events.Enqueue(action, (atMicroseconds, (int)priority, _sequence++));
        return true;
    }

    public bool TryRegisterRequest()
    {
        EnsureNotFinished();
        if (_stopReason.HasValue || NowMicroseconds >= DurationMicroseconds) return false;
        if (GeneratedRequests >= _maxGeneratedRequests)
        {
            _stopReason = SimulationStopReason.GeneratedRequestLimit;
            return false;
        }
        GeneratedRequests++;
        return true;
    }

    public bool TryRetainRecords(int count = 1)
    {
        EnsureNotFinished();
        if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (_stopReason.HasValue) return false;
        if (count > _maxRetainedRecords - RetainedRecords)
        {
            _stopReason = SimulationStopReason.RetainedRecordLimit;
            return false;
        }
        RetainedRecords += count;
        return true;
    }

    public SimulationEngineRunResult Run(CancellationToken cancellationToken = default)
    {
        if (_started) throw new InvalidOperationException("Create a new engine for each run.");
        _started = true;
        try
        {
            while (!_stopReason.HasValue)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _stopReason = SimulationStopReason.Cancelled;
                    break;
                }
                if (!_events.TryPeek(out _, out var next))
                {
                    NowMicroseconds = DurationMicroseconds;
                    _stopReason = SimulationStopReason.DurationReached;
                    break;
                }
                if (ProcessedEvents >= _maxProcessedEvents)
                {
                    _stopReason = SimulationStopReason.ProcessedEventLimit;
                    break;
                }

                var action = _events.Dequeue();
                NowMicroseconds = next.Time;
                _currentPriority = (SimulationEventPriority)next.Priority;
                ProcessedEvents++;
                action(this);
                _currentPriority = null;
            }

            return new SimulationEngineRunResult(Version,
                _stopReason == SimulationStopReason.DurationReached ? SimulationExecutionStatus.Completed : SimulationExecutionStatus.Incomplete,
                _stopReason!.Value, NowMicroseconds, ProcessedEvents, PendingEvents, GeneratedRequests, RetainedRecords);
        }
        finally
        {
            _finished = true;
            _currentPriority = null;
            _events.Clear();
        }
    }

    private void EnsureNotFinished()
    {
        if (_finished) throw new InvalidOperationException("The simulation has already ended.");
    }

    private static void ValidateLimit(int value, int maximum, string name)
    {
        if (value < 1 || value > maximum) throw new ArgumentOutOfRangeException(name, $"Must be between 1 and {maximum}.");
    }
}
