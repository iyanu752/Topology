namespace TopologyServer;

public sealed class SimulationClient(SimulationEngine engine, SimulationWorkload workload)
{
    public List<SimulationRequest> Requests { get; } = [];
    public Action<SimulationRequest> OnRequest { get; set; } = _ => { };
    public Action<SimulationRequest> OnTimeout { get; set; } = _ => { };

    public void Start()
    {
        if (workload.RequestsPerSecond > 0) ScheduleArrival(0);
    }

    private void ScheduleArrival(long index)
    {
        var time = (long)Math.Round(index * 1_000_000.0 / workload.RequestsPerSecond!.Value, MidpointRounding.AwayFromZero);
        if (time >= engine.DurationMicroseconds) return;
        engine.Schedule(time, SimulationEventPriority.Ordinary, _ =>
        {
            if (!engine.TryRegisterRequest()) return;
            var request = new SimulationRequest(index, time,
                time + SimulationTime.FromMilliseconds(workload.ClientTimeoutMs),
                engine.Random.NextDouble() * 100 < workload.ReadPercentage);
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
