namespace TopologyServer.Tests;

public class SimulationEngineTests
{
    [Fact]
    public void Events_RunByTimeThenPriorityThenInsertionOrder()
    {
        var engine = new SimulationEngine(1);
        var trace = new List<string>();
        engine.Schedule(20, SimulationEventPriority.Ordinary, _ => trace.Add("later"));
        engine.Schedule(10, SimulationEventPriority.Ordinary, e =>
        {
            trace.Add("first");
            e.Schedule(10, SimulationEventPriority.Ordinary, _ => trace.Add("nested"));
        });
        engine.Schedule(10, SimulationEventPriority.Deadline, _ => trace.Add("deadline"));
        engine.Schedule(10, SimulationEventPriority.ScenarioChange, _ => trace.Add("failure"));
        engine.Schedule(10, SimulationEventPriority.Ordinary, _ => trace.Add("second"));

        var result = engine.Run();

        Assert.Equal(new[] { "failure", "deadline", "first", "second", "nested", "later" }, trace);
        Assert.Equal(6, result.ProcessedEvents);
        Assert.Equal(1_000_000, result.ElapsedMicroseconds);
        Assert.Equal(SimulationExecutionStatus.Completed, result.Status);
    }

    [Fact]
    public void SameSeed_RepeatsDynamicallyScheduledRun()
    {
        static (SimulationEngineRunResult Result, List<long> Trace) Execute(int seed)
        {
            var engine = new SimulationEngine(1, seed);
            var trace = new List<long>();
            void Step(SimulationEngine e)
            {
                trace.Add(e.NowMicroseconds);
                if (trace.Count < 20)
                    e.Schedule(e.NowMicroseconds + 1 + (long)(e.Random.NextDouble() * 1000), SimulationEventPriority.Ordinary, Step);
            }
            engine.Schedule(0, SimulationEventPriority.Ordinary, Step);
            return (engine.Run(), trace);
        }

        var first = Execute(42);
        var second = Execute(42);
        Assert.Equal(first.Result, second.Result);
        Assert.Equal(first.Trace, second.Trace);
        Assert.False(first.Trace.SequenceEqual(Execute(43).Trace));
    }

    [Fact]
    public void Random_HasStableKnownSequence()
    {
        var random = new SimulationRandom(0);
        Assert.Equal(0xE220A8397B1DCDAFUL, random.NextUInt64());
        Assert.Equal(0x6E789E6AA1B965F4UL, random.NextUInt64());
        for (var i = 0; i < 1000; i++) Assert.InRange(random.NextDouble(), 0, Math.BitDecrement(1.0));
    }

    [Fact]
    public void Horizon_IncludesBoundaryEventsButRejectsNewRequestsAndLaterEvents()
    {
        var engine = new SimulationEngine(1);
        var completed = false;
        Assert.False(engine.Schedule(1_000_001, SimulationEventPriority.Ordinary, _ => Assert.Fail("Beyond horizon")));
        engine.Schedule(1_000_000, SimulationEventPriority.Ordinary, e =>
        {
            completed = true;
            Assert.False(e.TryRegisterRequest());
        });
        Assert.Equal(SimulationStopReason.DurationReached, engine.Run().StopReason);
        Assert.True(completed);
    }

    [Fact]
    public void EmptyRun_AdvancesToHorizon()
    {
        var result = new SimulationEngine(2).Run();
        Assert.Equal(2_000_000, result.ElapsedMicroseconds);
        Assert.Equal(0, result.ProcessedEvents);
    }

    [Fact]
    public void ProcessedLimit_StopsZeroTimeLoop()
    {
        var engine = new SimulationEngine(limits: new() { MaxProcessedEvents = 3 });
        void Loop(SimulationEngine e) => e.Schedule(e.NowMicroseconds, SimulationEventPriority.Ordinary, Loop);
        engine.Schedule(10, SimulationEventPriority.Ordinary, Loop);
        var result = engine.Run();
        Assert.Equal(SimulationStopReason.ProcessedEventLimit, result.StopReason);
        Assert.Equal(SimulationExecutionStatus.Incomplete, result.Status);
        Assert.Equal(3, result.ProcessedEvents);
        Assert.Equal(10, result.ElapsedMicroseconds);
        Assert.Equal(1, result.PendingEvents);
        Assert.Equal(0, engine.PendingEvents);
    }

    [Fact]
    public void ExactlyEnoughBudget_CanComplete()
    {
        var engine = new SimulationEngine(limits: new() { MaxProcessedEvents = 1, MaxGeneratedRequests = 1, MaxRetainedRecords = 1 });
        engine.Schedule(0, SimulationEventPriority.Ordinary, e =>
        {
            Assert.True(e.TryRegisterRequest());
            Assert.True(e.TryRetainRecords());
        });
        Assert.Equal(SimulationStopReason.DurationReached, engine.Run().StopReason);
    }

    [Fact]
    public void PendingLimit_BoundsQueueBeforeRun()
    {
        var engine = new SimulationEngine(limits: new() { MaxPendingEvents = 1 });
        Assert.True(engine.Schedule(0, SimulationEventPriority.Ordinary, _ => Assert.Fail("Should not execute")));
        Assert.False(engine.Schedule(0, SimulationEventPriority.Ordinary, _ => { }));
        var result = engine.Run();
        Assert.Equal(SimulationStopReason.PendingEventLimit, result.StopReason);
        Assert.Equal(0, result.ProcessedEvents);
        Assert.Equal(1, result.PendingEvents);
    }

    [Fact]
    public void RequestLimit_DoesNotCountRejectedReservation()
    {
        var engine = new SimulationEngine(limits: new() { MaxGeneratedRequests = 1 });
        engine.Schedule(10, SimulationEventPriority.Ordinary, e =>
        {
            Assert.True(e.TryRegisterRequest());
            Assert.False(e.TryRegisterRequest());
            Assert.False(e.TryRetainRecords());
        });
        var result = engine.Run();
        Assert.Equal(SimulationStopReason.GeneratedRequestLimit, result.StopReason);
        Assert.Equal(1, result.GeneratedRequests);
        Assert.Equal(0, result.RetainedRecords);
    }

    [Fact]
    public void OutputLimit_ReservesRecordGroupsAtomically()
    {
        var engine = new SimulationEngine(limits: new() { MaxRetainedRecords = 3 });
        engine.Schedule(10, SimulationEventPriority.Ordinary, e =>
        {
            Assert.True(e.TryRetainRecords(2));
            Assert.False(e.TryRetainRecords(2));
        });
        var result = engine.Run();
        Assert.Equal(SimulationStopReason.RetainedRecordLimit, result.StopReason);
        Assert.Equal(2, result.RetainedRecords);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Cancellation_StopsBeforeNextEvent(bool cancelBeforeRun)
    {
        using var cancellation = new CancellationTokenSource();
        var engine = new SimulationEngine();
        if (cancelBeforeRun) cancellation.Cancel();
        engine.Schedule(10, SimulationEventPriority.Ordinary, _ => cancellation.Cancel());
        engine.Schedule(20, SimulationEventPriority.Ordinary, _ => Assert.Fail("Canceled"));
        var result = engine.Run(cancellation.Token);
        Assert.Equal(SimulationStopReason.Cancelled, result.StopReason);
        Assert.Equal(cancelBeforeRun ? 0 : 10, result.ElapsedMicroseconds);
        Assert.Equal(cancelBeforeRun ? 0 : 1, result.ProcessedEvents);
    }

    [Fact]
    public void InvalidSchedulingAndReuse_AreRejected()
    {
        var engine = new SimulationEngine();
        engine.Schedule(10, SimulationEventPriority.Ordinary, e =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => e.Schedule(9, SimulationEventPriority.Ordinary, _ => { }));
            Assert.Throws<InvalidOperationException>(() => e.Schedule(10, SimulationEventPriority.Deadline, _ => { }));
            Assert.Throws<InvalidOperationException>(() => e.Run());
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.Schedule(0, (SimulationEventPriority)99, _ => { }));
        engine.Run();
        Assert.Throws<InvalidOperationException>(() => engine.Run());
        Assert.Throws<InvalidOperationException>(() => engine.Schedule(10, SimulationEventPriority.Ordinary, _ => { }));
        Assert.Throws<InvalidOperationException>(() => engine.TryRegisterRequest());
    }

    [Fact]
    public void CallbackException_PropagatesAndClosesEngine()
    {
        var engine = new SimulationEngine();
        engine.Schedule(0, SimulationEventPriority.Ordinary, _ => throw new FormatException("test"));
        Assert.Throws<FormatException>(() => engine.Run());
        Assert.Throws<InvalidOperationException>(() => engine.Run());
        Assert.Equal(0, engine.PendingEvents);
    }

    [Fact]
    public void Limits_AreValidatedAndCopied()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationEngine(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationEngine(3601));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationEngine(limits: new() { MaxProcessedEvents = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationEngine(limits: new() { MaxPendingEvents = int.MaxValue }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationEngine(limits: new() { MaxGeneratedRequests = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationEngine(limits: new() { MaxRetainedRecords = int.MaxValue }));
        var limits = new SimulationExecutionLimits { MaxRetainedRecords = 1 };
        var engine = new SimulationEngine(limits: limits);
        limits.MaxRetainedRecords = 100;
        Assert.False(engine.TryRetainRecords(2));
        Assert.Equal(SimulationStopReason.RetainedRecordLimit, engine.Run().StopReason);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0.0005, 1)]
    [InlineData(1.2345, 1235)]
    [InlineData(3600000, 3600000000L)]
    public void TimeConversion_RoundsHalfUp(double milliseconds, long expected)
        => Assert.Equal(expected, SimulationTime.FromMilliseconds(milliseconds));

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(3600001)]
    public void TimeConversion_RejectsInvalidValues(double milliseconds)
        => Assert.Throws<ArgumentOutOfRangeException>(() => SimulationTime.FromMilliseconds(milliseconds));
}
