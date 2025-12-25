using System;
using System.Collections.Generic;

namespace WebLoadTester.Domain;

public class TestPhase
{
    public string Name { get; set; } = string.Empty;
    public int Concurrency { get; set; }
    public int? Runs { get; set; }
    public TimeSpan? Duration { get; set; }
    public int PauseAfterSeconds { get; set; }
}

public class TestPlan
{
    public List<TestPhase> Phases { get; set; } = new();

    public int TotalRuns
    {
        get
        {
            var sum = 0;
            foreach (var phase in Phases)
            {
                if (phase.Runs.HasValue)
                {
                    sum += phase.Runs.Value;
                }
            }

            return sum;
        }
    }
}
