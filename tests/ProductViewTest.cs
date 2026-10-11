using System;
using System.Collections.Generic;
using System.IO;

namespace CodexTokenMonitor
{
    internal static class ProductViewTest
    {
        internal static void Run()
        {
            string folder = Path.Combine(Path.GetTempPath(), "CodexView-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string path = Path.Combine(folder, "appearance.ini");
                ViewSettings saved = new ViewSettings { Mode = 2, Palette = 1, Intensity = 35, Economy = true, Motion = false };
                saved.Save(path);
                ViewSettings loaded = ViewSettings.Load(path);
                Check(loaded.Mode == 2 && loaded.Palette == 1 && loaded.Intensity == 35 && loaded.Economy && !loaded.Motion, "Appearance preferences did not survive restart.");
                saved.Mode = 0; saved.Save(path);
                Check(ViewSettings.Load(path).Mode == 0, "Atomic preference replacement failed.");
                File.WriteAllLines(path, new[] { "mode=99", "palette=-3", "intensity=999", "economy=garbage" });
                loaded = ViewSettings.Load(path);
                Check(loaded.Mode == 2 && loaded.Palette == 0 && loaded.Intensity == 100 && !loaded.Economy, "Invalid preferences were not bounded.");
            }
            finally { Directory.Delete(folder, true); }
            DateTime now = DateTime.Now;
            List<UsageHistorySample> samples = new List<UsageHistorySample> {
                new UsageHistorySample { At = now.AddSeconds(-20), TotalTokens = 1000 },
                new UsageHistorySample { At = now, TotalTokens = 1600 }
            };
            Check(Math.Abs(UsageSignals.TokensPerMinute(samples, now).Value - 1800) < .01, "Rate is not normalized by elapsed time.");
            Check(!UsageSignals.TokensPerMinute(samples, now.AddMinutes(1)).HasValue, "Stale telemetry was presented as a live rate.");
            samples[1].TotalTokens = 100;
            Check(!UsageSignals.TokensPerMinute(samples, now).HasValue, "Counter reset produced a misleading rate.");
            samples[1].TotalTokens = 1000;
            Check(UsageSignals.TokensPerMinute(samples, now) == 0, "Unchanged counters must yield zero rather than missing data.");
            TaskView current = new TaskView { SessionId = "live", WorkingDirectory = "project-a", Usage = new TokenUsage { Total = 150 } };
            TaskView peer = new TaskView { SessionId = "closed", WorkingDirectory = "project-a", CompletedLocal = now, Usage = new TokenUsage { Total = 100 } };
            TaskView other = new TaskView { SessionId = "other", WorkingDirectory = "project-b", CompletedLocal = now, Usage = new TokenUsage { Total = 90000 } };
            Check(UsageSignals.Comparison(current, new[] { peer, other }).StartsWith("+50%"), "Comparison mixed different projects.");
            peer.Interrupted = true;
            Check(!UsageSignals.Comparison(current, new[] { peer }).StartsWith("+"), "Interrupted work polluted the baseline.");
            Console.WriteLine("PRODUCT_VIEW_OK cases=9");
        }
        private static void Check(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); }
    }
}
