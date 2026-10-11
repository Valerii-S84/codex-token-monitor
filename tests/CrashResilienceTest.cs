using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace CodexTokenMonitor
{
    internal static class CrashResilienceTest
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static int Main(string[] args)
        {
            string root = Path.Combine(Path.GetTempPath(), "CodexTokenMonitor-Crash-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                FragmentedMalformedAndRotatedLog(root);
                OversizedLineIsBoundedAndResynchronizes(root);
                LockedLogDoesNotEscape(root);
                RepeatedFileChurnDoesNotEscape(root);
                MissingDeletedAndBinaryLogsDoNotEscape(root);
                Console.WriteLine("CRASH_RESILIENCE_OK cases=5");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("CRASH_RESILIENCE_FAILED " + ex);
                return 1;
            }
            finally
            {
                try { Directory.Delete(root, true); }
                catch { }
            }
        }

        private static void FragmentedMalformedAndRotatedLog(string root)
        {
            string path = Path.Combine(root, "rollout-11111111-1111-1111-1111-111111111111.jsonl");
            string meta = "{\"timestamp\":\"2026-08-21T12:00:00Z\",\"type\":\"session_meta\",\"payload\":{\"id\":\"11111111-1111-1111-1111-111111111111\",\"cwd\":\"C:\\\\repo\"}}\n";
            string partial = "{\"timestamp\":\"2026-08-21T12:00:01Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"turn_started\",\"turn_id\":\"turn-1\"";
            File.WriteAllText(path, meta + "{not-json\n" + partial, Utf8);

            FileCursor cursor = new FileCursor { Path = path };
            FileInfo info = new FileInfo(path);
            Assert(SessionLogParser.ParseAppend(cursor, info), "Initial fragmented parse was not observed.");
            Assert(cursor.Partial.Length > 0 && cursor.Session.Tasks.Count == 0, "Fragment was not retained safely.");

            File.AppendAllText(path,
                "}}\n"
                + "{\"timestamp\":\"2026-08-21T12:00:02Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\",\"total_token_usage\":{\"input_tokens\":120,\"cached_input_tokens\":20,\"output_tokens\":30,\"total_tokens\":150}}}\n"
                + "{\"timestamp\":\"2026-08-21T12:00:03Z\",\"type\":\"event_msg\",\"payload\":{\"type\":\"turn_completed\",\"turn_id\":\"turn-1\"}}\n", Utf8);
            info.Refresh();
            Assert(SessionLogParser.ParseAppend(cursor, info), "Completed fragment was not parsed.");
            Assert(cursor.Session.Tasks.Count == 1 && cursor.Session.Tasks[0].CompletedLocal.HasValue,
                "Fragmented task lifecycle was not recovered.");
            Assert(cursor.Session.EffectiveTotal.Total == 150, "Recovered token total is incorrect.");

            string rotated = "{\"timestamp\":\"2026-08-21T13:00:00Z\",\"type\":\"session_meta\",\"payload\":{\"id\":\"22222222-2222-2222-2222-222222222222\"}}\n";
            File.WriteAllText(path, rotated, Utf8);
            info.Refresh();
            Assert(SessionLogParser.ParseAppend(cursor, info), "Truncated log did not reset the cursor.");
            Assert(cursor.Session.Id == "22222222-2222-2222-2222-222222222222" && cursor.Session.Tasks.Count == 0,
                "Rotated log retained stale session state.");
        }

        private static void OversizedLineIsBoundedAndResynchronizes(string root)
        {
            string path = Path.Combine(root, "oversized.jsonl");
            byte[] block = new byte[64 * 1024];
            for (int i = 0; i < block.Length; i++) block[i] = (byte)'x';
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                for (int i = 0; i < 1024; i++) stream.Write(block, 0, block.Length);
            FileCursor cursor = new FileCursor { Path = path };
            long peakBefore = Process.GetCurrentProcess().PeakWorkingSet64;
            Stopwatch watch = Stopwatch.StartNew();
            SessionLogParser.ParseAppend(cursor, new FileInfo(path));
            watch.Stop();
            Assert(cursor.Partial.Length == 0, "Oversized incomplete line was retained in memory.");
            Assert(cursor.DiscardingOversizedLine, "Oversized line discard state was not retained.");
            long peakGrowth = Process.GetCurrentProcess().PeakWorkingSet64 - peakBefore;
            Assert(peakGrowth < 48L * 1024 * 1024, "Oversized line caused excessive peak memory growth: " + peakGrowth);
            Assert(watch.Elapsed < TimeSpan.FromSeconds(20), "Oversized line handling exceeded 20 seconds.");

            File.AppendAllText(path,
                "tail\n{\"type\":\"session_meta\",\"payload\":{\"id\":\"33333333-3333-3333-3333-333333333333\"}}\n", Utf8);
            SessionLogParser.ParseAppend(cursor, new FileInfo(path));
            Assert(!cursor.DiscardingOversizedLine && cursor.Session.Id == "33333333-3333-3333-3333-333333333333",
                "Parser did not resynchronize after an oversized fragmented line.");
        }

        private static void LockedLogDoesNotEscape(string root)
        {
            string path = Path.Combine(root, "locked.jsonl");
            File.WriteAllText(path, "{locked}\n", Utf8);
            FileCursor cursor = new FileCursor { Path = path };
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                SessionLogParser.ParseAppend(cursor, new FileInfo(path));
            }
            Assert(cursor.Session != null, "Locked-file recovery left the cursor invalid.");
        }

        private static void RepeatedFileChurnDoesNotEscape(string root)
        {
            string path = Path.Combine(root, "churn.jsonl");
            FileCursor cursor = new FileCursor { Path = path };
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < 250; i++)
            {
                if (i % 25 == 0)
                    File.WriteAllText(path, "{\"type\":\"session_meta\",\"payload\":{\"id\":\"churn-" + i + "\"}}\n", Utf8);
                else
                    File.AppendAllText(path, "{malformed-" + i + "\n", Utf8);
                FileInfo info = new FileInfo(path);
                info.Refresh();
                SessionLogParser.ParseAppend(cursor, info);
            }
            watch.Stop();
            Assert(watch.Elapsed < TimeSpan.FromSeconds(20), "Repeated file churn exceeded 20 seconds.");
        }

        private static void MissingDeletedAndBinaryLogsDoNotEscape(string root)
        {
            string path = Path.Combine(root, "race-and-binary.jsonl");
            FileCursor cursor = new FileCursor { Path = path };
            Assert(!SessionLogParser.ParseAppend(cursor, new FileInfo(path)),
                "A missing log was reported as changed.");

            File.WriteAllText(path, "{\"type\":\"session_meta\",\"payload\":{\"id\":\"stale\"}}\n", Utf8);
            FileInfo deleted = new FileInfo(path);
            deleted.Refresh();
            File.Delete(path);
            SessionLogParser.ParseAppend(cursor, deleted);

            byte[] garbage = new byte[] { 0xff, 0xfe, 0x00, 0xc3, 0x28, (byte)'\n' };
            File.WriteAllBytes(path, garbage);
            File.AppendAllText(path,
                "{\"type\":\"session_meta\",\"payload\":{\"id\":\"44444444-4444-4444-4444-444444444444\"}}\n", Utf8);
            Assert(SessionLogParser.ParseAppend(cursor, new FileInfo(path)),
                "Binary garbage prevented the parser from reaching a valid record.");
            Assert(cursor.Session.Id == "44444444-4444-4444-4444-444444444444",
                "Parser did not resynchronize after binary garbage.");
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
