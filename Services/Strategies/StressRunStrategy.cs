using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebLoadTester.Domain;

namespace WebLoadTester.Services.Strategies
{
    public class StressRunStrategy : BaseRunStrategy, IRunStrategy
    {
        public async Task<List<RunResult>> ExecuteAsync(RunContext context, CancellationToken ct)
        {
            var results = new List<RunResult>();
            var max = Math.Clamp(context.Settings.Concurrency, 1, 50);
            var step = Math.Max(1, context.Settings.StressStep);
            var runsPerLevel = Math.Max(1, context.Settings.RunsPerLevel);
            var delay = Math.Max(0, context.Settings.StressPauseSeconds);

            if (context.Settings.StressStep <= 0)
            {
                context.Logger.Log("[Stress] Ramp step is 0; using 1 by default");
            }

            if (context.Settings.RunsPerLevel <= 0)
            {
                context.Logger.Log("[Stress] Runs per level is 0; using 1 by default");
            }

            var levels = new List<int>();
            for (var level = step; level <= max; level += step)
            {
                levels.Add(level);
            }

            if (levels.Count == 0 || levels[^1] != max)
            {
                levels.Add(max);
            }

            var totalRuns = levels.Count * runsPerLevel;
            var aggregateDone = 0;

            foreach (var level in levels)
            {
                context.Logger.Log($"[Stress] Уровень {level} VU: {runsPerLevel} прогонов");
                var levelResults = await RunWithQueueAsync(
                    context,
                    runsPerLevel,
                    level,
                    ct,
                    done =>
                    {
                        var current = Interlocked.Increment(ref aggregateDone);
                        context.Progress?.Invoke(current, totalRuns);
                    },
                    totalRuns);
                lock (results)
                {
                    results.AddRange(levelResults);
                }

                if (level != levels[^1] && delay > 0)
                {
                    context.Logger.Log($"[Stress] Пауза {delay} сек перед следующим уровнем");
                    await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                }
            }

            return results;
        }
    }
}
