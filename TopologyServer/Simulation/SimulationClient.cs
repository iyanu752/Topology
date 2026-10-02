namespace TopologyServer;

public sealed class SimulationClient(SimulationEngine engine, SimulationWorkload workload)
{
    public List<SimulationRequest> Requests { get; } = [];
    public Action<SimulationRequest> OnRequest { get; set; } = _ => { };
    public Action<SimulationRequest> OnTimeout { get; set; } = _ => { };
    private long _generation;
    private long _origin;
    private int _rate;
    public bool IsAvailable { get; private set; } = true;

    public void SetAvailable(bool available)
    {
        IsAvailable = available;
        ChangeRate(_rate);
        if (!available)
            foreach (var request in Requests.Where(r => r.IsPending))
                if (request.Finish(SimulationRequestOutcome.Failed, engine.NowMicroseconds, "ClientUnavailable")) OnTimeout(request);
    }

    public void Start()
    {
        ChangeRate(workload.RequestsPerSecond ?? 0);
    }

    public void ChangeRate(int rate)
    {
        _generation++;
        _origin = engine.NowMicroseconds;
        _rate = rate;
        if (rate > 0 && IsAvailable) ScheduleArrival(0);
    }

    private void ScheduleArrival(long index)
    {
        var generation = _generation;
        var time = _origin + (long)Math.Round(index * 1_000_000.0 / _rate, MidpointRounding.AwayFromZero);
        if (time >= engine.DurationMicroseconds) return;
        engine.Schedule(time, SimulationEventPriority.Ordinary, _ =>
        {
            if (generation != _generation || !engine.TryRegisterRequest()) return;
            var request = new SimulationRequest(Requests.Count, time,
                time + SimulationTime.FromMilliseconds(workload.ClientTimeoutMs),
                engine.Random.NextDouble() * 100 < workload.ReadPercentage) { Key = (int)(engine.Random.NextDouble() * workload.KeySpaceSize) };
            Requests.Add(request);
            engine.Schedule(request.Deadline, SimulationEventPriority.Deadline, _ =>
            {
                if (request.Finish(SimulationRequestOutcome.TimedOut, engine.NowMicroseconds, "DeadlineExceeded")) OnTimeout(request);
            });
            OnRequest(request);
            ScheduleArrival(index + 1);
        });
    }

    public void Receive(SimulationRequest request, string? error)
    {
        if (engine.NowMicroseconds >= request.Deadline) return;
        request.Finish(error == null ? SimulationRequestOutcome.Succeeded : SimulationRequestOutcome.Failed,
            engine.NowMicroseconds, error);
    }
}
