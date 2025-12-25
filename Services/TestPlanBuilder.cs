using System;
using System.Collections.Generic;
using WebLoadTester.Domain;

namespace WebLoadTester.Services;

public class TestPlanBuilder
{
    public TestPlan Build(RunSettings settings)
    {
        return settings.TestType switch
        {
            TestType.Stress => BuildStress(settings),
            TestType.Endurance => BuildEndurance(settings),
            _ => BuildSingle(settings)
        };
    }

    private TestPlan BuildSingle(RunSettings settings)
    {
        return new TestPlan
        {
            Phases =
            {
                new TestPhase
                {
                    Name = settings.TestType.ToString(),
                    Concurrency = settings.Concurrency,
                    Runs = settings.TotalRuns
                }
            }
        };
    }

    private TestPlan BuildStress(RunSettings settings)
    {
        var phases = new List<TestPhase>();
        var step = Math.Max(1, settings.StressStep);
        var max = Math.Max(step, settings.Concurrency);

        for (var level = step; level <= max; level += step)
        {
            phases.Add(new TestPhase
            {
                Name = $"Stress {level}",
                Concurrency = level,
                Runs = Math.Max(1, settings.RunsPerLevel),
                PauseAfterSeconds = Math.Max(0, settings.StressPauseSeconds)
            });
        }

        return new TestPlan { Phases = phases };
    }

    private TestPlan BuildEndurance(RunSettings settings)
    {
        return new TestPlan
        {
            Phases =
            {
                new TestPhase
                {
                    Name = "Endurance",
                    Concurrency = settings.Concurrency,
                    Duration = TimeSpan.FromMinutes(Math.Max(1, settings.EnduranceMinutes))
                }
            }
        };
    }
}
