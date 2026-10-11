using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace CodexTokenMonitor
{
    internal static class SmokeTest
    {
        public static int Main(string[] args)
        {
            string fixture = null;
            if (args == null || args.Length == 0)
            {
                fixture = Path.Combine(Path.GetTempPath(), "CodexTokenMonitor-Smoke-" + Guid.NewGuid().ToString("N"));
                CreateFixture(fixture);
                args = new[] { "--codex-home", fixture };
            }
            try { return Run(args); }
            finally
            {
                if (fixture != null)
                {
                    try { Directory.Delete(fixture, true); }
                    catch { }
                }
            }
        }

        private static int Run(string[] args)
        {
            AnalyticsRegressionTest.Run();
            using (SessionEngine engine = new SessionEngine(args))
            {
                engine.Start();
                DateTime deadline = DateTime.UtcNow.AddSeconds(12);
                while (DateTime.UtcNow < deadline)
                {
                    MonitorSnapshot snapshot = engine.Latest;
                    if (snapshot != null && snapshot.Sessions.Count > 0 && snapshot.IndexedFiles >= 10 && snapshot.AvailableFiles > 0) break;
                    Thread.Sleep(100);
                }
                MonitorSnapshot result = engine.Latest;
                Console.WriteLine("home=" + result.CodexHome);
                Console.WriteLine("indexed=" + result.IndexedFiles + "/" + result.AvailableFiles + " target=" + result.IndexTarget);
                Console.WriteLine("sessions=" + result.Sessions.Count + " running=" + result.RunningSessions);
                Console.WriteLine("today=" + result.Today.Total + " input=" + result.Today.Input + " output=" + result.Today.Output + " cached=" + result.Today.Cached);
                Console.WriteLine("api_value=" + result.TodayApiValue.Value + " partial=" + result.TodayApiValue.IsPartial + " days=" + result.Days.Count);
                if (!String.IsNullOrEmpty(result.Error)) Console.WriteLine("error=" + result.Error);
                SessionView newest = result.Sessions.FirstOrDefault();
                foreach (SessionView active in result.Sessions.Where(s => s.IsRunning))
                    Console.WriteLine("active=" + active.Id + " last=" + active.LastActivityLocal.ToString("HH:mm:ss") + " title=" + active.Title + " repo=" + active.Repository);
                if (newest != null)
                    Console.WriteLine("newest=" + newest.Repository + " title=" + (!String.IsNullOrWhiteSpace(newest.Title)) + " session_total=" + newest.SessionTotal.Total + " task=" + (newest.CurrentOrLastTask == null ? 0 : newest.CurrentOrLastTask.Usage.Total));
                var controlled = new System.Collections.Generic.Dictionary<string, TokenUsage>(StringComparer.OrdinalIgnoreCase);
                controlled["gpt-5.6-sol"] = new TokenUsage { Input = 2000000, Cached = 1000000, Output = 500000, Total = 2500000 };
                CostEstimate controlledCost = PriceCatalog.Calculate(controlled);
                bool formulaOk = controlledCost.Value == 14.40m && !controlledCost.IsPartial;
                Console.WriteLine("formula=" + controlledCost.Value + " ok=" + formulaOk);
                FieldInfo limitField = typeof(SessionEngine).GetField("_indexLimit", BindingFlags.Instance | BindingFlags.NonPublic);
                int beforeLimit = (int)limitField.GetValue(engine);
                engine.LoadOlder();
                int afterLimit = (int)limitField.GetValue(engine);
                bool olderOk = beforeLimit >= result.AvailableFiles
                    ? afterLimit == beforeLimit
                    : afterLimit > beforeLimit && afterLimit <= result.AvailableFiles;
                Console.WriteLine("older_request=" + beforeLimit + "->" + afterLimit + " ok=" + olderOk);
                bool ok = result.Sessions.Count > 0 && newest != null && newest.SessionTotal.Total > 0 && result.Days.Count > 0 && formulaOk && olderOk && String.IsNullOrEmpty(result.Error);
                Console.WriteLine(ok ? "SMOKE_OK" : "SMOKE_FAILED");
                return ok ? 0 : 1;
            }
        }

        private static void CreateFixture(string root)
        {
            string folder = Path.Combine(root, "sessions", "2026", "08", "21");
            Directory.CreateDirectory(folder);
            string stamp = DateTime.UtcNow.ToString("o");
            Encoding utf8 = new UTF8Encoding(false);
            for (int i = 0; i < 12; i++)
            {
                string id = String.Format("00000000-0000-0000-0000-{0:000000000000}", i + 1);
                string body =
                    "{\"timestamp\":\"" + stamp + "\",\"type\":\"session_meta\",\"payload\":{\"id\":\"" + id + "\",\"cwd\":\"C:\\\\fixture\"}}\n" +
                    "{\"timestamp\":\"" + stamp + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"turn_started\",\"turn_id\":\"turn-1\"}}\n" +
                    "{\"timestamp\":\"" + stamp + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"user_message\",\"message\":\"fixture question\"}}\n" +
                    "{\"timestamp\":\"" + stamp + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"agent_message\",\"message\":\"fixture answer\"}}\n" +
                    "{\"timestamp\":\"" + stamp + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"total_token_usage\":{\"input_tokens\":120,\"cached_input_tokens\":20,\"output_tokens\":30,\"total_tokens\":150}}}\n" +
                    "{\"timestamp\":\"" + stamp + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"turn_completed\",\"turn_id\":\"turn-1\"}}\n";
                File.WriteAllText(Path.Combine(folder, "rollout-" + id + ".jsonl"), body, utf8);
            }
        }
    }
}
