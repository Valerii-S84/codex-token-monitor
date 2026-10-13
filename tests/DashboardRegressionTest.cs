using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace CodexTokenMonitor
{
    internal static class DashboardRegressionTest
    {
        [STAThread]
        public static int Main()
        {
            try
            {
                TestDailyRefresh();
                TestPriceAndUnknownModels();
                TestContextEmptyAndThresholds();
                Console.WriteLine("DASHBOARD_REGRESSION_OK cases=9");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static MonitorSnapshot Snapshot(long oldTokens)
        {
            MonitorSnapshot snapshot = new MonitorSnapshot { RefreshedLocal = DateTime.Now };
            snapshot.Days.Add(new DayView { Date = DateTime.Today, Tasks = 1, Usage = new TokenUsage { Total = 100 }, ApiValue = new CostEstimate() });
            snapshot.Days.Add(new DayView { Date = DateTime.Today.AddDays(-1), Tasks = 2, Usage = new TokenUsage { Total = oldTokens }, ApiValue = new CostEstimate() });
            return snapshot;
        }

        private static void TestDailyRefresh()
        {
            MonitorSnapshot original = Snapshot(200);
            string baseline = AuroraUi.DaysSignature(original.Days);
            Assert(baseline == AuroraUi.DaysSignature(Snapshot(200).Days), "Unchanged days must have a stable signature.");
            MonitorSnapshot updated = Snapshot(300);
            Assert(baseline != AuroraUi.DaysSignature(updated.Days), "An older day's tokens were ignored.");
            MonitorSnapshot cached = Snapshot(200);
            cached.Days[1].Usage.Cached = 10;
            Assert(baseline != AuroraUi.DaysSignature(cached.Days), "Cache-only changes were ignored.");
            MonitorSnapshot metadata = Snapshot(200);
            metadata.Days[1].ApiValue.UnknownTokens = 20;
            Assert(baseline != AuroraUi.DaysSignature(metadata.Days), "Partial pricing changes were ignored.");
            string isolated = Path.Combine(Path.GetTempPath(), "CodexDashboard-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(isolated, "sessions"));
            try
            {
                using (MainForm form = new MainForm(new[] { "--codex-home", isolated }))
                {
                    MethodInfo apply = typeof(MainForm).GetMethod("ApplySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
                    MethodInfo view = typeof(MainForm).GetMethod("SetView", BindingFlags.Instance | BindingFlags.NonPublic);
                    apply.Invoke(form, new object[] { original });
                    view.Invoke(form, new object[] { 1 });
                    apply.Invoke(form, new object[] { updated });
                    DataGridView grid = (DataGridView)typeof(MainForm).GetField("_days", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                    Assert(grid.Rows.Count == 2 && ((DayView)grid.Rows[1].Tag).Usage.Total == 300,
                        "The visible analytics table retained an outdated older day.");
                }
            }
            finally { Directory.Delete(isolated, true); }
        }

        private static void TestPriceAndUnknownModels()
        {
            Dictionary<string, TokenUsage> usage = new Dictionary<string, TokenUsage>();
            usage["gpt-5.6-sol"] = new TokenUsage { Input = 2000000, Cached = 1000000, Output = 500000, Reasoning = 100000, Total = 2500000 };
            CostEstimate result = PriceCatalog.Calculate(usage);
            Assert(result.Value == 14.40m && !result.IsPartial, "Current Sol pricing or cached/reasoning accounting is incorrect.");
            usage.Clear();
            usage["gpt-5.6"] = new TokenUsage { Input = 1000000, Total = 1000000 };
            usage["not-a-published-model"] = new TokenUsage { Input = 50, Total = 50 };
            result = PriceCatalog.Calculate(usage);
            Assert(result.Value == 4m && result.IsPartial && result.UnknownTokens == 50,
                "Alias or unknown-model costs are misleading.");
            usage.Clear();
            usage["gpt-6-astra"] = new TokenUsage { Input = 2000000, Cached = 1000000, Output = 500000, Total = 2500000 };
            result = PriceCatalog.Calculate(usage);
            Assert(result.Value == 36m && !result.IsPartial, "Astra usage still has an unknown or incorrect tariff.");
            foreach (KeyValuePair<string, decimal> expected in new Dictionary<string, decimal> { { "gpt-6.1-sol", 7.10m }, { "gpt-6-luna", 0.36m } })
            {
                usage.Clear();
                usage[expected.Key] = new TokenUsage { Input = 2000000, Cached = 1000000, Output = 500000, Total = 2500000 };
                result = PriceCatalog.Calculate(usage);
                Assert(result.Value == expected.Value && !result.IsPartial, "Sol/Luna pricing is incorrect.");
            }
            Assert(PriceCatalog.RateDate == "2026-10-11", "Pricing date is stale.");
            foreach (KeyValuePair<string, decimal> expected in new Dictionary<string, decimal> {
                { "gpt-6-sol", 7.20m }, { "GPT-6-SOL", 7.20m },
                { "gpt-5.3-codex", 8.925m }, { "gpt-5.2-codex", 8.925m },
                { "gpt-5.1-codex-max", 6.375m }, { "gpt-5.1-codex-mini", 1.275m },
                { "gpt-5.1-codex", 6.375m }, { "gpt-5-codex", 6.375m },
                { "codex-mini-latest", 4.875m } })
            {
                usage.Clear();
                usage[expected.Key] = new TokenUsage { Input = 2000000, Cached = 1000000, Output = 500000, Total = 2500000 };
                result = PriceCatalog.Calculate(usage);
                Assert(result.Value == expected.Value && !result.IsPartial, "Published Codex model pricing is missing or incorrect: " + expected.Key);
            }
            foreach (KeyValuePair<string, decimal> expected in new Dictionary<string, decimal> {
                { "gpt-5.6-terra", 10.70m }, { "gpt-5.6-luna", 1.07m } })
            {
                usage.Clear();
                usage[expected.Key] = new TokenUsage { Input = 3000000, Cached = 1000000, CacheWrite = 1000000, Output = 500000, Total = 3500000 };
                result = PriceCatalog.Calculate(usage);
                Assert(result.Value == expected.Value && !result.IsPartial, "Published cache-write pricing was omitted: " + expected.Key);
            }
            usage.Clear();
            usage["gpt-5.2-pro"] = new TokenUsage { Input = 100, Cached = 100, Total = 100 };
            result = PriceCatalog.Calculate(usage);
            Assert(result.IsPartial && result.UnknownTokens == 100 && !result.HasKnownUsage && result.Value == 0m,
                "An unavailable cache-read tariff was treated as free known usage.");
            Assert(FormatUtil.ApiValue(result) == "≈?", "Fully unpriced cached usage was displayed as zero dollars.");
            usage["gpt-5.2-pro"] = new TokenUsage { Input = 1000100, Cached = 100, Total = 1000100 };
            result = PriceCatalog.Calculate(usage);
            Assert(result.IsPartial && result.UnknownTokens == 100 && result.KnownTokens == 1000000 && result.Value == 21m,
                "Partial cache pricing lost the known input charge.");
            Console.WriteLine("PRICING_REGRESSION_OK: GPT-6 Sol; Codex models; Terra/Luna cache writes; unavailable cache prices.");

            usage.Clear();
            usage["gpt-6.1-sol"] = new TokenUsage { Input = 3000000, Cached = 1000000, CacheWrite = 1000000, Output = 500000, Total = 3500000 };
            result = PriceCatalog.Calculate(usage);
            Assert(result.Value == 9.60m && !result.IsPartial, "Cache writes were charged as uncached input or counted twice.");
            usage.Clear();
            usage["gpt-5.4"] = new TokenUsage { Input = 100, CacheWrite = 100, Total = 100 };
            result = PriceCatalog.Calculate(usage);
            Assert(result.IsPartial && result.UnknownTokens == 100 && result.Value == 0m, "An unavailable cache-write tariff was silently guessed.");
        }

        private static void TestContextEmptyAndThresholds()
        {
            using (ContextChart chart = new ContextChart())
            using (Bitmap bitmap = new Bitmap(400, 160))
            {
                chart.Size = bitmap.Size;
                foreach (long maximum in new long[] { 0, 258400 })
                foreach (long used in new long[] { 0, 258400, 300000 })
                {
                    chart.Maximum = maximum;
                    chart.Used = used;
                    chart.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                }
            }
        }
    }
}
