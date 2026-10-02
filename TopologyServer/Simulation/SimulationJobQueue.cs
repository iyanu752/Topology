namespace TopologyServer;

public sealed class SimulationJobQueue(SimulationEngine engine, SimulationQueueSettings settings)
{
    private sealed class Job(int key)
    {
        public int Key { get; } = key;
        public int Deliveries { get; set; }
        public long Lease { get; set; }
        public bool Active { get; set; }
    }

    private readonly LinkedList<Job> _waiting = [];
    private readonly HashSet<Job> _jobs = [];
    private readonly SimulationJobMetrics _metrics = new();
    private long _attemptId = -1;
    public bool IsAvailable { get; private set; } = true;
    public Func<bool> CanDeliver { get; set; } = () => false;
    public Action<SimulationRequest, Action<string?>> Deliver { get; set; } = (_, _) => { };
    public Action<SimulationRequest> CancelAttempt { get; set; } = _ => { };

    public bool Enqueue(int key)
    {
        if (!IsAvailable || _jobs.Count >= settings.Capacity) { _metrics.Rejected++; return false; }
        var job = new Job(key);
        _jobs.Add(job);
        _waiting.AddLast(job);
        _metrics.Accepted++;
        Pump();
        return true;
    }

    public void Pump()
    {
        while (IsAvailable && _waiting.First is { } first && CanDeliver())
        {
            var job = first.Value;
            _waiting.RemoveFirst();
            job.Active = true;
            job.Deliveries++;
            var lease = ++job.Lease;
            var request = new SimulationRequest(_attemptId--, engine.NowMicroseconds, long.MaxValue, false) { Key = job.Key };
            void Finish(string? error)
            {
                if (!_jobs.Contains(job) || lease != job.Lease) return;
                job.Lease++;
                job.Active = false;
                if (error == null) { _jobs.Remove(job); _metrics.Completed++; }
                else if (job.Deliveries >= settings.MaxDeliveries) { _jobs.Remove(job); _metrics.DeadLettered++; }
                else
                {
                    _metrics.Redeliveries++;
                    engine.Schedule(engine.NowMicroseconds + settings.RetryDelayMs * 1000L, SimulationEventPriority.Ordinary, _ =>
                    {
                        if (_jobs.Contains(job)) { _waiting.AddLast(job); Pump(); }
                    });
                }
                Pump();
            }
            engine.Schedule(engine.NowMicroseconds + settings.AcknowledgementTimeoutMs * 1000L, SimulationEventPriority.Deadline, _ =>
            {
                if (!_jobs.Contains(job) || lease != job.Lease) return;
                request.Finish(SimulationRequestOutcome.TimedOut, engine.NowMicroseconds);
                CancelAttempt(request);
                Finish("AcknowledgementTimeout");
            });
            Deliver(request, Finish);
        }
    }

    public void SetAvailable(bool available)
    {
        IsAvailable = available;
        if (!available)
        {
            _metrics.Lost += _jobs.Count;
            _jobs.Clear();
            _waiting.Clear();
        }
        else Pump();
    }

    public SimulationJobMetrics Snapshot() => new()
    {
        Accepted = _metrics.Accepted, Completed = _metrics.Completed, DeadLettered = _metrics.DeadLettered,
        Redeliveries = _metrics.Redeliveries, Rejected = _metrics.Rejected, Lost = _metrics.Lost,
        Active = _jobs.Count(j => j.Active), Waiting = _jobs.Count(j => !j.Active)
    };
}
