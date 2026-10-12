namespace TopologyServer;

public sealed class SimulationResilience(SimulationEngine engine, SimulationRetrySettings retries, SimulationCircuitBreakerSettings settings)
{
    private sealed class Circuit
    {
        public SimulationCircuitState State;
        public int Failures;
        public long ReopenAt;
        public int Generation;
    }

    private sealed class Call(SimulationRequest parent, Action<SimulationRequest> cancel)
    {
        public SimulationRequest Parent { get; } = parent;
        public Action<SimulationRequest> Cancel { get; } = cancel;
        public SimulationRequest? Attempt;
        public bool Stopped;
        public Action? ReleaseProbe;
    }

    private readonly Dictionary<string, Circuit> _circuits = [];
    private readonly Dictionary<long, Call> _calls = [];
    private readonly SimulationRetryMetrics _metrics = new();
    private long _nextId = long.MinValue;

    public void Execute(SimulationRequest parent, string scope,
        Action<SimulationRequest, Action<string?>> send, Action<SimulationRequest> cancel, Action<string?> done)
    {
        var call = new Call(parent, cancel);
        _calls.Add(parent.Id, call);
        var count = 0;
        void Finish(string? error)
        {
            if (call.Stopped) return;
            call.Stopped = true;
            _calls.Remove(parent.Id);
            done(error);
        }
        void Attempt()
        {
            if (call.Stopped || !parent.IsRunnable || engine.NowMicroseconds >= parent.Deadline) return;
            Circuit? circuit = null;
            if (settings.Enabled)
            {
                if (!_circuits.TryGetValue(scope, out circuit)) _circuits.Add(scope, circuit = new());
                if (circuit.State == SimulationCircuitState.HalfOpen || circuit.State == SimulationCircuitState.Open && engine.NowMicroseconds < circuit.ReopenAt)
                { _metrics.CircuitRejected++; Finish("CircuitOpen"); return; }
                if (circuit.State == SimulationCircuitState.Open) circuit.State = SimulationCircuitState.HalfOpen;
            }
            var generation = circuit?.Generation;
            call.ReleaseProbe = () =>
            {
                if (circuit?.State == SimulationCircuitState.HalfOpen && circuit.Generation == generation)
                { circuit.State = SimulationCircuitState.Open; circuit.ReopenAt = engine.NowMicroseconds; circuit.Generation++; }
            };
            count++;
            _metrics.Attempts++;
            if (count > 1) _metrics.RetryAttempts++;
            var request = new SimulationRequest(_nextId++, engine.NowMicroseconds, parent.Deadline, parent.IsRead) { Key = parent.Key };
            call.Attempt = request;
            send(request, error =>
            {
                if (call.Stopped || !parent.IsRunnable || call.Attempt != request) return;
                call.Attempt = null;
                call.ReleaseProbe = null;
                if (error != null) _metrics.FailuresByCause[error] = _metrics.FailuresByCause.GetValueOrDefault(error) + 1;
                if (circuit != null && circuit.Generation == generation)
                {
                    if (error == null || !Transient(error)) { circuit.Failures = 0; circuit.State = SimulationCircuitState.Closed; }
                    else if (++circuit.Failures >= settings.FailureThreshold || circuit.State == SimulationCircuitState.HalfOpen)
                    {
                        circuit.State = SimulationCircuitState.Open;
                        circuit.ReopenAt = engine.NowMicroseconds + SimulationTime.FromMilliseconds(settings.OpenDurationMs);
                        circuit.Generation++;
                        _metrics.CircuitOpened++;
                    }
                }
                parent.IsStaleRead = request.IsStaleRead;
                if (error == null || !Transient(error) || count >= retries.MaxAttempts || !parent.IsRead && !retries.RetryWrites)
                { Finish(error); return; }
                var delay = Math.Min(retries.MaxDelayMs, retries.BaseDelayMs * Math.Pow(2, count - 1));
                if (retries.JitterPercentage > 0) delay *= 1 - engine.Random.NextDouble() * retries.JitterPercentage / 100.0;
                var next = engine.NowMicroseconds + Math.Max(1, SimulationTime.FromMilliseconds(delay));
                if (next >= parent.Deadline) { Finish(error); return; }
                engine.Schedule(next, SimulationEventPriority.Ordinary, _ => Attempt());
            });
        }
        Attempt();
    }

    private static bool Transient(string error) => error.EndsWith("Unavailable", StringComparison.Ordinal) ||
        error.EndsWith("Overloaded", StringComparison.Ordinal) || error.EndsWith("Saturated", StringComparison.Ordinal) || error == "ExternalApiFailure";

    public void Cancel(SimulationRequest parent)
    {
        if (!_calls.Remove(parent.Id, out var call)) return;
        call.Stopped = true;
        call.ReleaseProbe?.Invoke();
        if (call.Attempt is { } request) { request.IsCancelled = true; call.Cancel(request); }
    }

    public SimulationRetryMetrics Snapshot() => new()
    {
        Attempts = _metrics.Attempts, RetryAttempts = _metrics.RetryAttempts, CircuitOpened = _metrics.CircuitOpened,
        CircuitRejected = _metrics.CircuitRejected, FailuresByCause = new(_metrics.FailuresByCause),
        Circuits = _circuits.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => new SimulationCircuitMetrics
        { Scope = p.Key, State = p.Value.State, ConsecutiveFailures = p.Value.Failures }).ToList()
    };
}
