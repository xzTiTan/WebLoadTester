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
            var max = Math.Max(1, context.Settings.Concurrency);
            var step = Math.Max(1, context.Settings.Stress.RampStep);
            var runsPerLevel = Math.Max(1, context.Settings.Stress.RunsPerLevel);
            var delay = Math.Max(0, context.Settings.Stress.RampDelaySeconds);

            for (var level = Math.Min(step, max); level <= max; level += step)
            {
                context.Logger.Log($"[Stress] Уровень {level}: {runsPerLevel} прогонов");
                var levelResults = await RunWithQueueAsync(context, runsPerLevel, level, ct);
                lock (results)
                {
                    results.AddRange(levelResults);
                }

                if (level + step <= max && delay > 0)
                {
                    context.Logger.Log($"[Stress] Пауза {delay} сек перед следующим уровнем");
                    await Task.Delay(TimeSpan.FromSeconds(delay), ct);
                }
            }

            return results;
        }
    }
}
