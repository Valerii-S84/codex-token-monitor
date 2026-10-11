using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CodexTokenMonitor
{
    internal static class AnalyticsRegressionTest
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        public static void Run()
        {
            string home = Path.Combine(Path.GetTempPath(), "CTM-analytics-" + Guid.NewGuid().ToString("N"));
            string sessions = Path.Combine(home, "sessions");
            Directory.CreateDirectory(sessions);
            try
            {
                string path = Path.Combine(sessions, "analytics.jsonl");
                StringBuilder log = new StringBuilder();
                DateTime yesterday = DateTime.Today.AddDays(-1).AddHours(12);
                DateTime today = DateTime.Today.AddHours(1);
                log.AppendLine("{\"type\":\"session_meta\",\"payload\":{\"id\":\"analytics\",\"model\":\"gpt-6.1-sol\"}}");
                for (int i = 0; i < 120; i++)
                {
                    DateTime at = (i < 60 ? yesterday : today).AddSeconds(i);
                    log.AppendLine(Event(at, "task_started", "\"turn_id\":\"turn-" + i + "\""));
                    log.AppendLine(Event(at, "token_count", "\"total_token_usage\":{\"input_tokens\":" + ((i + 1) * 100) + ",\"cache_write_input_tokens\":10,\"cached_input_tokens\":" + ((i + 1) * 20) + ",\"output_tokens\":" + ((i + 1) * 10) + ",\"total_tokens\":" + ((i + 1) * 110) + "}"));
                    log.AppendLine(Event(at, "task_complete", "\"turn_id\":\"turn-" + i + "\""));
                }
                File.WriteAllText(path, log.ToString(), new UTF8Encoding(false));
                FileCursor cursor = new FileCursor();
                SessionLogParser.ParseAppend(cursor, new FileInfo(path));
                Assert(cursor.Session.Tasks.Count == 1, "Completed task details are still accumulating.");
                Assert(cursor.Session.DailyTaskIds[yesterday.Date].Count == 60 && cursor.Session.DailyTaskIds[today.Date].Count == 60, "Daily task counts were lost during pruning.");
                Assert(cursor.Session.EffectiveTotal.Total == 13200 && cursor.Session.Daily[today.Date].Total == 6600, "Pruning changed token totals.");
                string cache = Path.Combine(home, "cache.bin");
                using (SessionEngine engine = new SessionEngine(new[] { "--codex-home", home }))
                {
                    Type type = typeof(SessionEngine);
                    type.GetField("_cachePath", Private).SetValue(engine, cache);
                    Dictionary<string, FileCursor> cursors = (Dictionary<string, FileCursor>)type.GetField("_cursors", Private).GetValue(engine);
                    cursors.Clear();
                    cursors[path] = cursor;
                    string archived = Path.Combine(home, "archived_sessions", "analytics.jsonl");
                    // Duplicate discovery of a moved log must not double daily usage.
                    cursors[archived] = cursor;
                    MethodInfo snapshot = type.GetMethod("BuildSnapshot", Private);
                    MonitorSnapshot before = (MonitorSnapshot)snapshot.Invoke(engine, new object[] { "" });
                    Assert(before.Today.Total == 6600 && before.TodayTasks == 60 && before.Days.Count == 2, "Duplicate logs changed analytics.");
                    cursors.Remove(archived);
                    type.GetMethod("SaveCache", Private).Invoke(engine, null);
                    cursors.Clear();
                    type.GetMethod("LoadCache", Private).Invoke(engine, null);
                    FileCursor restored = cursors[path];
                    Assert(restored.Offset == cursor.Offset && restored.Session.EffectiveTotal.CacheWrite == 10 && restored.Session.Tasks.Count == 1 && restored.Session.DailyTaskIds[today.Date].Count == 60, "Cache lost the parser offset or retained task state.");
                    File.AppendAllText(path, Event(today.AddMinutes(3), "task_started", "\"turn_id\":\"active\"") + "\n" + Event(today.AddMinutes(3), "token_count", "\"total_token_usage\":{\"input_tokens\":12100,\"cache_write_input_tokens\":10,\"cached_input_tokens\":2420,\"output_tokens\":1210,\"total_tokens\":13310}") + "\n");
                    SessionLogParser.ParseAppend(restored, new FileInfo(path));
                    Assert(restored.Session.EffectiveTotal.Total == 13310 && restored.Session.Daily[today.Date].Total == 6710 && restored.Session.CurrentTask.Usage.Total == 110, "Resuming the cache doubled usage or lost the active task.");
                    Assert(restored.Session.Tasks.Count == 1 && restored.Session.DailyTaskIds[today.Date].Count == 61, "Active task rollover lost daily counts.");
                    // An old cache must be rejected atomically before reading any cursor.
                    using (BinaryWriter writer = new BinaryWriter(File.Create(cache))) writer.Write("CTM-CACHE-8");
                    cursors.Clear();
                    type.GetMethod("LoadCache", Private).Invoke(engine, null);
                    Assert(cursors.Count == 0, "Old cache offsets were mixed into the new format.");
                }
                Console.WriteLine("ANALYTICS_OK: 8 assertions; 120 turns; duplicate logs; cache resume; v8 rejection.");
            }
            finally { Directory.Delete(home, true); }
        }

        private static string Event(DateTime at, string type, string fields)
        {
            return "{\"timestamp\":\"" + new DateTimeOffset(at).ToString("O") + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"" + type + "\"," + fields + "}}";
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
