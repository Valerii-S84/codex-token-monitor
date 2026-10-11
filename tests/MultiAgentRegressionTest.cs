using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace CodexTokenMonitor
{
    internal static class MultiAgentRegressionTest
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [STAThread]
        public static int Main()
        {
            string root = Path.Combine(Path.GetTempPath(), "CTM-multi-agent-" + Guid.NewGuid().ToString("N"));
            MainForm form = null;
            SessionEngine engine = null;
            try
            {
                string sessions = Path.Combine(root, "sessions");
                Directory.CreateDirectory(sessions);
                string cwd = Directory.GetCurrentDirectory().Replace("\\", "\\\\").Replace("\"", "\\\"");
                WriteSession(sessions, cwd, 1);
                form = new MainForm(new[] { "--codex-home", root });
                engine = (SessionEngine)typeof(MainForm).GetField("_engine", Private).GetValue(form);
                typeof(SessionEngine).GetField("_cachePath", Private).SetValue(engine, Path.Combine(root, "cache.bin"));
                form.Show();
                WaitUntil(delegate { return engine.Latest.RunningSessions == 1 && Cards(form) == 1; }, "The first agent did not appear.");
                File.AppendAllText(Path.Combine(sessions, "agent-1.jsonl"), Event("task_complete", "turn-1") + Event("task_started", "turn-1-next"));
                File.SetLastWriteTime(Path.Combine(sessions, "agent-1.jsonl"), DateTime.Now.AddHours(-1));

                for (int i = 2; i <= 4; i++) WriteSession(sessions, cwd, i);
                WaitUntil(delegate { return engine.Latest.RunningSessions == 4 && Cards(form) == 4; },
                    "Four concurrent agents were not visible.");
                AssertIds(engine, 1, 2, 3, 4);

                for (int i = 4; i >= 1; i--)
                {
                    string path = Path.Combine(sessions, "agent-" + i + ".jsonl");
                    DateTime written = DateTime.Now;
                    File.AppendAllText(path, Event("agent_message", ""));
                    File.SetLastWriteTime(path, DateTime.Now.AddHours(-1));
                    string id = "agent-" + i;
                    WaitUntil(delegate
                    {
                        return engine.Latest.Sessions.Any(s => s.Id == id && s.LastActivityLocal >= written)
                            && engine.Latest.RunningSessions == 4 && Cards(form) == 4;
                    }, "An agent disappeared after another chat updated.");
                    AssertIds(engine, 1, 2, 3, 4);
                }

                File.AppendAllText(Path.Combine(sessions, "agent-2.jsonl"), Event("task_complete", "turn-2"));
                WaitUntil(delegate { return engine.Latest.RunningSessions == 3 && Cards(form) == 3; },
                    "A completed agent did not disappear.");
                AssertIds(engine, 1, 3, 4);
                Console.WriteLine("MULTI_AGENT_OK: 1 -> 4 -> 3; stale file timestamps; four staggered updates; task rollover remains active.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
            finally
            {
                if (engine != null) engine.Dispose();
                if (form != null) form.Dispose();
                string fullRoot = Path.GetFullPath(root);
                string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(fullRoot)) Directory.Delete(fullRoot, true);
            }
        }

        private static void WriteSession(string sessions, string cwd, int number)
        {
            string meta = "{\"timestamp\":\"" + DateTimeOffset.Now.ToString("O")
                + "\",\"type\":\"session_meta\",\"payload\":{\"id\":\"agent-" + number + "\",\"cwd\":\"" + cwd + "\"}}\n";
            File.WriteAllText(Path.Combine(sessions, "agent-" + number + ".jsonl"), meta + Event("task_started", "turn-" + number), new UTF8Encoding(false));
            File.SetLastWriteTime(Path.Combine(sessions, "agent-" + number + ".jsonl"), DateTime.Now.AddHours(-1));
        }

        private static string Event(string type, string turn)
        {
            return "{\"timestamp\":\"" + DateTimeOffset.Now.ToString("O") + "\",\"type\":\"event_msg\",\"payload\":{\"type\":\"" + type + "\",\"turn_id\":\"" + turn + "\"}}\n";
        }

        private static int Cards(MainForm form)
        {
            return ((IList)typeof(MainForm).GetField("_agentCards", Private).GetValue(form)).Count;
        }

        private static void AssertIds(SessionEngine engine, params int[] expected)
        {
            string actual = String.Join(",", engine.Latest.Sessions.Where(s => s.IsRunning).Select(s => s.Id).OrderBy(id => id));
            string wanted = String.Join(",", expected.Select(number => "agent-" + number).OrderBy(id => id));
            if (actual != wanted) throw new InvalidOperationException("Active agent identities changed: " + actual);
        }

        private static void WaitUntil(Func<bool> ready, string error)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(8);
            do
            {
                Application.DoEvents();
                if (ready()) return;
                Thread.Sleep(20);
            } while (DateTime.UtcNow < deadline);
            throw new InvalidOperationException(error);
        }
    }
}
