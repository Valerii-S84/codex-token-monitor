using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CodexTokenMonitor
{
    internal static class SessionLogParser
    {
        internal const int MaxLineBytes = 4 * 1024 * 1024;
        private static readonly Regex IdAtEnd = new Regex("([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", RegexOptions.Compiled);

        public static bool ParseAppend(FileCursor cursor, FileInfo info)
        {

            if (cursor == null || info == null || !info.Exists) return false;
            bool reset = cursor.Session == null || info.Length < cursor.Offset;
            if (reset)
            {
                cursor.Offset = 0;
                cursor.Partial = new byte[0];
                cursor.DiscardingOversizedLine = false;
                cursor.Session = new SessionState();
            }

            bool changed = false;
            try
            {
                using (FileStream stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    if (cursor.Offset > stream.Length)
                    {
                        cursor.Offset = 0;
                        cursor.Partial = new byte[0];
                        cursor.DiscardingOversizedLine = false;
                        cursor.Session = new SessionState();
                    }
                    stream.Seek(cursor.Offset, SeekOrigin.Begin);
                    int initialCapacity = cursor.Partial != null ? Math.Min(MaxLineBytes, cursor.Partial.Length) : 0;
                    using (MemoryStream line = new MemoryStream(Math.Max(4096, initialCapacity)))
                    {
                        bool discardLine = cursor.DiscardingOversizedLine;
                        if (!discardLine && cursor.Partial != null && cursor.Partial.Length > 0)
                            line.Write(cursor.Partial, 0, cursor.Partial.Length);

                        byte[] buffer = new byte[65536];
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            int segmentStart = 0;
                            for (int i = 0; i < read; i++)
                            {
                                if (buffer[i] != (byte)'\n') continue;
                                AppendSegment(line, buffer, segmentStart, i - segmentStart, ref discardLine);
                                int lineLength = (int)line.Length;
                                byte[] lineBuffer = line.GetBuffer();
                                if (lineLength > 0 && lineBuffer[lineLength - 1] == (byte)'\r') lineLength--;
                                if (!discardLine && lineLength > 0)
                                {
                                    ParseLine(cursor.Session, lineBuffer, lineLength, info.Name);
                                    changed = true;
                                }
                                line.SetLength(0);
                                discardLine = false;
                                segmentStart = i + 1;
                            }
                            AppendSegment(line, buffer, segmentStart, read - segmentStart, ref discardLine);
                        }
                        cursor.DiscardingOversizedLine = discardLine;
                        cursor.Partial = !discardLine && line.Length > 0 ? line.ToArray() : new byte[0];
                    }
                    cursor.Offset = stream.Position;
                }
                cursor.Path = info.FullName;
                cursor.LastObservedLength = info.Length;
                cursor.LastWriteUtcTicks = info.LastWriteTimeUtc.Ticks;
                if (info.LastWriteTime > cursor.Session.LastWriteLocal)
                    cursor.Session.LastWriteLocal = info.LastWriteTime;
                if (String.IsNullOrEmpty(cursor.Session.Id)) cursor.Session.Id = FallbackId(info.Name);
                if (cursor.Session.FirstSeenLocal == default(DateTime)) cursor.Session.FirstSeenLocal = info.CreationTime;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return changed || reset;
        }

        private static void AppendSegment(MemoryStream line, byte[] buffer, int offset, int count, ref bool discardLine)
        {
            if (discardLine || count <= 0) return;
            if (line.Length + count > MaxLineBytes)
            {
                line.SetLength(0);
                discardLine = true;
                return;
            }
            line.Write(buffer, offset, count);
        }

        private static void ParseLine(SessionState session, byte[] data, int length, string fileName)
        {
            string topType = JsonLite.GetString(data, length, "type", 0);
            // Windows may retain an old file timestamp while Codex keeps its writer open.
            DateTime activityTime = ParseTimestamp(JsonLite.GetString(data, length, "timestamp", 0), DateTime.MinValue);
            if (activityTime > session.LastWriteLocal) session.LastWriteLocal = activityTime;
            if (topType == "turn_context")
            {
                int contextPayload = JsonLite.FindObjectStart(data, length, "payload", 0);
                string contextModel = JsonLite.GetString(data, length, "model", contextPayload < 0 ? 0 : contextPayload);
                if (!String.IsNullOrWhiteSpace(contextModel)) session.CurrentModel = contextModel;
                string effort = JsonLite.GetString(data, length, "effort", contextPayload < 0 ? 0 : contextPayload);
                if (!String.IsNullOrWhiteSpace(effort)) session.CurrentEffort = effort;
                return;
            }
            if (topType == "session_meta")
            {
                int payload = JsonLite.FindObjectStart(data, length, "payload", 0);
                if (payload < 0) return;
                string id = JsonLite.GetString(data, length, "id", payload);
                if (String.IsNullOrEmpty(id)) id = JsonLite.GetString(data, length, "session_id", payload);
                string cwd = JsonLite.GetString(data, length, "cwd", payload);
                if (String.IsNullOrEmpty(cwd)) cwd = JsonLite.GetString(data, length, "working_directory", payload);
                if (!String.IsNullOrEmpty(id)) session.Id = id;
                if (!String.IsNullOrEmpty(cwd)) session.Cwd = cwd;
                string model = JsonLite.GetString(data, length, "model", payload);
                if (!String.IsNullOrWhiteSpace(model)) session.CurrentModel = model;
                DateTime timestamp = ParseTimestamp(JsonLite.GetString(data, length, "timestamp", 0), DateTime.Now);
                if (session.FirstSeenLocal == default(DateTime) || timestamp < session.FirstSeenLocal) session.FirstSeenLocal = timestamp;
                return;
            }

            if (topType != "event_msg" && topType != "event") return;
            int payloadStart = JsonLite.FindObjectStart(data, length, "payload", 0);
            if (payloadStart < 0) return;
            string eventType = JsonLite.GetString(data, length, "type", payloadStart);
            DateTime eventTime = ParseTimestamp(JsonLite.GetString(data, length, "timestamp", 0), DateTime.Now);

            if (eventType == "task_started" || eventType == "turn_started")
            {
                string turnId = JsonLite.GetString(data, length, "turn_id", payloadStart) ?? "";
                TaskState current = session.CurrentTask;
                if (current != null && current.TurnId == turnId) return;
                if (current != null)
                {
                    current.CompletedLocal = eventTime;
                    current.Interrupted = true;
                    current.Usage = TokenUsage.Difference(session.EffectiveTotal, current.Baseline);

                }
                TaskState task = new TaskState
                {
                    SessionId = String.IsNullOrEmpty(session.Id) ? FallbackId(fileName) : session.Id,
                    TurnId = turnId,
                    StartedLocal = eventTime,
                    Baseline = session.EffectiveTotal.Clone(),
                    Usage = new TokenUsage()
                };
                HashSet<string> dayTasks;
                if (!session.DailyTaskIds.TryGetValue(eventTime.Date, out dayTasks))
                    session.DailyTaskIds[eventTime.Date] = dayTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                dayTasks.Add(String.IsNullOrEmpty(turnId) ? eventTime.Ticks.ToString(CultureInfo.InvariantCulture) : turnId);
                session.Tasks.RemoveAll(t => t.CompletedLocal.HasValue);
                session.Tasks.Add(task);

                return;
            }

            if (eventType == "task_complete" || eventType == "turn_complete" || eventType == "turn_completed")
            {
                string turnId = JsonLite.GetString(data, length, "turn_id", payloadStart) ?? "";
                TaskState task = session.CurrentTask;
                if (task == null && !String.IsNullOrEmpty(turnId))
                    task = session.Tasks.LastOrDefault(t => t.TurnId == turnId && !t.CompletedLocal.HasValue);
                if (task != null)
                {
                    task.CompletedLocal = eventTime;
                    task.Usage = TokenUsage.Difference(session.EffectiveTotal, task.Baseline);

                }
                return;
            }

            if (eventType == "turn_aborted" || eventType == "task_aborted" ||
                eventType == "turn_cancelled" || eventType == "task_cancelled" ||
                eventType == "turn_failed" || eventType == "task_failed")
            {
                string turnId = JsonLite.GetString(data, length, "turn_id", payloadStart) ?? "";
                TaskState task = session.CurrentTask;
                if (task == null && !String.IsNullOrEmpty(turnId))
                    task = session.Tasks.LastOrDefault(t => t.TurnId == turnId && !t.CompletedLocal.HasValue);
                if (task != null)
                {
                    task.CompletedLocal = eventTime;
                    task.Interrupted = true;
                    task.Usage = TokenUsage.Difference(session.EffectiveTotal, task.Baseline);

                }
                return;
            }

            if (eventType == "token_count" || eventType == "token_usage")
            {
                int infoStart = JsonLite.FindObjectStart(data, length, "info", payloadStart);
                int infoEnd = infoStart < 0 ? length : JsonLite.FindObjectEnd(data, length, infoStart);
                if (infoEnd < 0) infoEnd = length;
                long contextWindow = JsonLite.GetLong(data, length, "model_context_window", infoStart < 0 ? payloadStart : infoStart, infoEnd, 0);
                if (contextWindow > 0) session.ModelContextWindow = contextWindow;
                int lastUsageStart = JsonLite.FindObjectStart(data, length, "last_token_usage", infoStart < 0 ? payloadStart : infoStart);
                if (lastUsageStart >= 0 && lastUsageStart < infoEnd)
                {
                    int lastUsageEnd = JsonLite.FindObjectEnd(data, length, lastUsageStart);
                    session.CurrentContext = ReadUsage(data, length, lastUsageStart, lastUsageEnd < 0 ? infoEnd : lastUsageEnd);
                }
                int limitsStart = JsonLite.FindObjectStart(data, length, "rate_limits", payloadStart);
                if (limitsStart >= 0)
                {
                    int limitsEnd = JsonLite.FindObjectEnd(data, length, limitsStart);
                    if (limitsEnd < 0) limitsEnd = length;
                    session.PrimaryLimit = ReadRateLimit(data, length, "primary", limitsStart, limitsEnd);
                    session.SecondaryLimit = ReadRateLimit(data, length, "secondary", limitsStart, limitsEnd);
                }
                int usageStart = JsonLite.FindObjectStart(data, length, "total_token_usage", payloadStart);
                if (usageStart < 0) usageStart = JsonLite.FindObjectStart(data, length, "session_usage", payloadStart);
                if (usageStart < 0) return;
                int usageEnd = JsonLite.FindObjectEnd(data, length, usageStart);
                if (usageEnd < 0) usageEnd = length;
                TokenUsage currentCounter = ReadUsage(data, length, usageStart, usageEnd);
                TokenUsage delta = TokenUsage.DeltaFromCounter(currentCounter, session.RawCounter, session.HasRawCounter);
                session.RawCounter = currentCounter;
                session.HasRawCounter = true;
                session.EffectiveTotal.Add(delta);
                UsageBuckets.Add(session.UsageByModel, session.CurrentModel, delta);
                DateTime day = eventTime.Date;
                TokenUsage daily;
                if (!session.Daily.TryGetValue(day, out daily))
                {
                    daily = new TokenUsage();
                    session.Daily[day] = daily;
                }
                daily.Add(delta);
                Dictionary<string, TokenUsage> dailyModels;
                if (!session.DailyByModel.TryGetValue(day, out dailyModels))
                {
                    dailyModels = new Dictionary<string, TokenUsage>(StringComparer.OrdinalIgnoreCase);
                    session.DailyByModel[day] = dailyModels;
                }
                UsageBuckets.Add(dailyModels, session.CurrentModel, delta);
                TaskState task = session.CurrentTask;
                if (task != null)
                {
                    task.Usage = TokenUsage.Difference(session.EffectiveTotal, task.Baseline);
                    UsageBuckets.Add(task.UsageByModel, session.CurrentModel, delta);
                }
                return;
            }

            if (eventType == "thread_name_updated" || eventType == "thread/name/updated")
            {
                string title = JsonLite.GetString(data, length, "thread_name", payloadStart);
                if (String.IsNullOrEmpty(title)) title = JsonLite.GetString(data, length, "name", payloadStart);
                if (!String.IsNullOrWhiteSpace(title)) session.InlineTitle = title;
            }
        }

        private static TokenUsage ReadUsage(byte[] data, int length, int start, int end)
        {
            TokenUsage usage = new TokenUsage();
            usage.Input = Math.Max(0, JsonLite.GetLong(data, length, "input_tokens", start, end, 0));
            usage.Cached = Math.Max(0, JsonLite.GetLong(data, length, "cached_input_tokens", start, end, 0));
            usage.CacheWrite = Math.Max(0, JsonLite.GetLong(data, length, "cache_write_input_tokens", start, end, 0));
            usage.Output = Math.Max(0, JsonLite.GetLong(data, length, "output_tokens", start, end, 0));
            usage.Reasoning = Math.Max(0, JsonLite.GetLong(data, length, "reasoning_output_tokens", start, end, 0));
            usage.Total = Math.Max(0, JsonLite.GetLong(data, length, "total_tokens", start, end, usage.Input + usage.Output));
            return usage;
        }

        private static RateLimitView ReadRateLimit(byte[] data, int length, string key, int start, int end)
        {
            int limitStart = JsonLite.FindObjectStart(data, length, key, start);
            if (limitStart < 0 || limitStart >= end) return new RateLimitView();
            int limitEnd = JsonLite.FindObjectEnd(data, length, limitStart);
            if (limitEnd < 0 || limitEnd > end) limitEnd = end;
            long resetSeconds = JsonLite.GetLong(data, length, "resets_at", limitStart, limitEnd, 0);
            DateTime? reset = null;
            if (resetSeconds > 0)
            {
                try { reset = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(resetSeconds).ToLocalTime(); }
                catch (ArgumentOutOfRangeException) { }
            }
            return new RateLimitView
            {
                Available = true,
                UsedPercent = (int)Math.Max(0, Math.Min(100, JsonLite.GetLong(data, length, "used_percent", limitStart, limitEnd, 0))),
                WindowMinutes = (int)Math.Max(0, Math.Min(Int32.MaxValue, JsonLite.GetLong(data, length, "window_minutes", limitStart, limitEnd, 0))),
                ResetsLocal = reset
            };
        }

        private static DateTime ParseTimestamp(string value, DateTime fallback)
        {
            DateTimeOffset timestamp;
            if (!String.IsNullOrEmpty(value) && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out timestamp))
                return timestamp.ToLocalTime().DateTime;
            return fallback;
        }

        private static string FallbackId(string fileName)
        {
            Match match = IdAtEnd.Match(fileName ?? "");
            return match.Success ? match.Groups[1].Value : Path.GetFileNameWithoutExtension(fileName ?? "unknown");
        }
    }

    internal sealed class SessionEngine : IDisposable
    {
        private const int InitialIndexLimit = 240;
        private const int OlderBatch = 120;
        private const int InitialBatch = 100;
        private const int LaterBatch = 100;
        private const int DiscoveryIntervalSeconds = 3;
        private const int CacheSaveIntervalSeconds = 45;
        private readonly string _codexHome;
        private readonly string _cachePath;
        private readonly Dictionary<string, FileCursor> _cursors = new Dictionary<string, FileCursor>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _repositoryCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Color> _colorCache = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        private List<FileInfo> _discovered = new List<FileInfo>();
        private Timer _timer;
        private int _busy;
        private bool _firstRefresh = true;
        private bool _dirty;
        private volatile bool _disposed;
        private DateTime _lastDiscoveryUtc = DateTime.MinValue;
        private DateTime _lastIndexWriteUtc = DateTime.MinValue;
        private DateTime _lastCacheSaveUtc = DateTime.MinValue;
        private DateTime _lastSnapshotUtc = DateTime.MinValue;
        private int _indexLimit = InitialIndexLimit;
        private MonitorSnapshot _latest;

        public event Action<MonitorSnapshot> SnapshotReady;
        public string CodexHome { get { return _codexHome; } }
        public MonitorSnapshot Latest { get { return _latest; } }

        public SessionEngine(string[] args)
        {
            _codexHome = DetectCodexHome(args);
            // v9 rebuilds parser state from logs; v8 offsets and totals are never mixed.
            _cachePath = Path.Combine(AppSettings.SettingsDirectory, "session-cache-v9.bin");
            _indexLimit = InitialIndexLimit;
            LoadCache();
            LoadTitles(true);
            _latest = BuildSnapshot("");
        }

        public void Start()
        {
            if (_timer != null) return;
            _timer = new Timer(RefreshCallback, null, 0, 1600);
        }

        public void RefreshNow()
        {
            ThreadPool.QueueUserWorkItem(delegate { RefreshCallback(true); });
        }

        public void LoadOlder()
        {
            List<FileInfo> discovered = _discovered;
            int available = discovered == null ? 0 : discovered.Count;
            MonitorSnapshot latest = _latest;
            int current = Math.Max(Thread.VolatileRead(ref _indexLimit), latest == null ? 0 : latest.IndexedFiles);
            int next = available <= 0
                ? current + OlderBatch
                : Math.Max(current, Math.Min(available, current + OlderBatch));
            Interlocked.Exchange(ref _indexLimit, next);
            RefreshNow();
        }

        private void RefreshCallback(object state)
        {
            if (_disposed || Interlocked.Exchange(ref _busy, 1) == 1) return;
            try
            {
                bool force = state is bool && (bool)state;
                bool changed = false;
                string error = "";
                if (String.IsNullOrEmpty(_codexHome) || !Directory.Exists(_codexHome))
                {
                    error = "CODEX_HOME not found";
                }
                else
                {
                    LoadTitles(false);
                    if (force || _firstRefresh || DateTime.UtcNow - _lastDiscoveryUtc > TimeSpan.FromSeconds(DiscoveryIntervalSeconds)) DiscoverFiles();
                    changed = ParseChangedAndNew();
                    if (_dirty && DateTime.UtcNow - _lastCacheSaveUtc > TimeSpan.FromSeconds(CacheSaveIntervalSeconds)) SaveCache();
                }
                string previousError = _latest == null ? "" : _latest.Error;
                bool publish = force || _firstRefresh || changed ||
                    !String.Equals(error, previousError, StringComparison.Ordinal) ||
                    DateTime.UtcNow - _lastSnapshotUtc > TimeSpan.FromSeconds(30);
                if (publish)
                {
                    _latest = BuildSnapshot(error);
                    _lastSnapshotUtc = DateTime.UtcNow;
                    Action<MonitorSnapshot> handler = SnapshotReady;
                    if (handler != null) handler(_latest);
                }
                _firstRefresh = false;
            }
            catch (Exception ex)
            {
                // Keep a stable diagnostic identifier; presentation adds the current-language prefix.
                _latest = BuildSnapshot(ex.GetType().Name);
                _lastSnapshotUtc = DateTime.UtcNow;
                Action<MonitorSnapshot> handler = SnapshotReady;
                if (handler != null) handler(_latest);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        private void DiscoverFiles()
        {
            List<FileInfo> files = new List<FileInfo>();
            foreach (string folderName in new[] { "sessions", "archived_sessions" })
            {
                string folder = Path.Combine(_codexHome, folderName);
                if (!Directory.Exists(folder)) continue;
                try
                {
                    foreach (string path in Directory.EnumerateFiles(folder, "*.jsonl", SearchOption.AllDirectories))
                    {
                        try { files.Add(new FileInfo(path)); }
                        catch { }
                    }
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }
            _discovered = files.OrderByDescending(f => f.LastWriteTimeUtc).ToList();
            _lastDiscoveryUtc = DateTime.UtcNow;
        }

        private bool ParseChangedAndNew()
        {
            int newBudget = _firstRefresh ? InitialBatch : LaterBatch;
            int added = 0;
            bool changed = false;
            int limit = Math.Max(InitialIndexLimit, Thread.VolatileRead(ref _indexLimit));
            List<FileInfo> candidates = _discovered.Take(limit).ToList();

            // Recently written logs are at the front. Checking a bounded hot set
            // avoids thousands of filesystem calls on every 1.6 second timer tick;
            // the periodic discovery promotes any resumed older session here.
            foreach (FileInfo info in candidates.Take(64))
            {
                try
                {
                    info.Refresh();
                    if (!info.Exists) continue;
                }
                catch (IOException) { continue; }
                catch (UnauthorizedAccessException) { continue; }
                FileCursor cursor;
                if (!_cursors.TryGetValue(info.FullName, out cursor)) continue;
                if (info.Length != cursor.Offset || info.LastWriteTimeUtc.Ticks != cursor.LastWriteUtcTicks)
                {
                    if (SessionLogParser.ParseAppend(cursor, info))
                    {
                        _dirty = true;
                        changed = true;
                    }
                }
            }

            foreach (FileInfo info in candidates)
            {
                if (added >= newBudget) break;
                if (_cursors.ContainsKey(info.FullName)) continue;
                FileCursor cursor = new FileCursor { Path = info.FullName };
                _cursors[info.FullName] = cursor;
                SessionLogParser.ParseAppend(cursor, info);
                _dirty = true;
                changed = true;
                added++;
            }
            return changed;
        }

        private void LoadTitles(bool force)
        {
            string indexPath = Path.Combine(_codexHome ?? "", "session_index.jsonl");
            if (!File.Exists(indexPath))
            {
                LoadLegacyTitles();
                return;
            }
            DateTime write = File.GetLastWriteTimeUtc(indexPath);
            if (!force && write == _lastIndexWriteUtc) return;
            try
            {
                Dictionary<string, string> next = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadLines(indexPath, Encoding.UTF8))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(line);
                    string id = JsonLite.GetString(bytes, bytes.Length, "id", 0);
                    string title = JsonLite.GetString(bytes, bytes.Length, "thread_name", 0);
                    if (!String.IsNullOrEmpty(id) && !String.IsNullOrWhiteSpace(title)) next[id] = title;
                }
                _titles.Clear();
                foreach (KeyValuePair<string, string> item in next) _titles[item.Key] = item.Value;
                _lastIndexWriteUtc = write;
            }
            catch (IOException) { }
        }

        private void LoadLegacyTitles()
        {
            string historyPath = Path.Combine(_codexHome ?? "", "history.jsonl");
            if (!File.Exists(historyPath)) return;
            try
            {
                foreach (string line in File.ReadLines(historyPath, Encoding.UTF8))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(line);
                    string id = JsonLite.GetString(bytes, bytes.Length, "session_id", 0);
                    string title = JsonLite.GetString(bytes, bytes.Length, "text", 0);
                    if (!String.IsNullOrEmpty(id) && !String.IsNullOrWhiteSpace(title) && !_titles.ContainsKey(id))
                        _titles[id] = FormatUtil.OneLine(title, 90);
                }
            }
            catch (IOException) { }
        }

        private MonitorSnapshot BuildSnapshot(string error)
        {
            int indexed = CountIndexed(_discovered);
            int available = _discovered.Count;
            MonitorSnapshot result = new MonitorSnapshot
            {
                CodexHome = _codexHome,
                AvailableFiles = available,
                IndexedFiles = indexed,
                IndexTarget = Math.Min(available, Math.Max(InitialIndexLimit, Thread.VolatileRead(ref _indexLimit))),
                Error = error ?? "",
                RefreshedLocal = DateTime.Now
            };

            DateTime today = DateTime.Today;
            Dictionary<string, SessionView> sessionUnique = new Dictionary<string, SessionView>(StringComparer.OrdinalIgnoreCase);
            Dictionary<DateTime, TokenUsage> usageByDay = new Dictionary<DateTime, TokenUsage>();
            Dictionary<DateTime, Dictionary<string, TokenUsage>> modelsByDay = new Dictionary<DateTime, Dictionary<string, TokenUsage>>();
            Dictionary<DateTime, HashSet<string>> tasksByDay = new Dictionary<DateTime, HashSet<string>>();

            // A session can briefly exist in both sessions and archived_sessions.
            // Keep the most complete copy so daily totals and API value are not doubled.
            List<FileCursor> uniqueCursors = _cursors.Values
                .Where(c => c.Session != null && !String.IsNullOrEmpty(c.Session.Id))
                .GroupBy(c => c.Session.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(c => c.Session.EffectiveTotal.Total)
                    .ThenByDescending(c => c.Session.LastWriteLocal)
                    .First())
                .ToList();

            foreach (FileCursor cursor in uniqueCursors)
            {
                SessionState state = cursor.Session;
                foreach (KeyValuePair<DateTime, TokenUsage> day in state.Daily)
                {
                    TokenUsage combined;
                    if (!usageByDay.TryGetValue(day.Key.Date, out combined))
                    {
                        combined = new TokenUsage();
                        usageByDay[day.Key.Date] = combined;
                    }
                    combined.Add(day.Value);

                    Dictionary<string, TokenUsage> combinedModels;
                    if (!modelsByDay.TryGetValue(day.Key.Date, out combinedModels))
                    {
                        combinedModels = new Dictionary<string, TokenUsage>(StringComparer.OrdinalIgnoreCase);
                        modelsByDay[day.Key.Date] = combinedModels;
                    }
                    Dictionary<string, TokenUsage> stateModels;
                    if (state.DailyByModel.TryGetValue(day.Key.Date, out stateModels))
                        UsageBuckets.AddMap(combinedModels, stateModels);
                    else
                        UsageBuckets.Add(combinedModels, UsageBuckets.UnknownModel, day.Value);
                }

                string repository = RepositoryFor(state.Cwd);
                string title = TitleFor(state, repository);
                bool fallbackTitle = String.IsNullOrEmpty(title);
                bool fallbackRepository = String.IsNullOrWhiteSpace(state.Cwd);
                Color accent = ColorFor(repository);
                TaskState current = state.CurrentTask;
                bool running = current != null && DateTime.Now - state.LastWriteLocal <= TimeSpan.FromMinutes(15);
                bool stale = current != null && !running;

                TaskState currentOrLast = current ?? state.Tasks.LastOrDefault();
                SessionView sessionView = new SessionView
                {
                    Id = state.Id,
                    Title = title,
                    Repository = repository,
                    IsFallbackTitle = fallbackTitle,
                    IsFallbackRepository = fallbackRepository,
                    WorkingDirectory = state.Cwd,
                    LastActivityLocal = state.LastWriteLocal,
                    IsRunning = running,
                    IsStale = stale,
                    CurrentModel = state.CurrentModel,
                    CurrentEffort = state.CurrentEffort,
                    StartedLocal = state.FirstSeenLocal,
                    ModelContextWindow = state.ModelContextWindow,
                    CurrentContext = state.CurrentContext.Clone(),
                    SessionTotal = state.EffectiveTotal.Clone(),
                    ApiValue = PriceCatalog.Calculate(state.UsageByModel),
                    PrimaryLimit = state.PrimaryLimit.Clone(),
                    SecondaryLimit = state.SecondaryLimit.Clone(),
                    CurrentOrLastTask = currentOrLast == null ? null : ToTaskView(currentOrLast, title, repository, state.Cwd, cursor.Path, fallbackTitle, fallbackRepository),
                    TurnCount = state.DailyTaskIds.Values.Sum(ids => ids.Count),
                    Accent = accent
                };
                SessionView existingSession;
                if (!sessionUnique.TryGetValue(state.Id, out existingSession) || existingSession.LastActivityLocal < sessionView.LastActivityLocal)
                    sessionUnique[state.Id] = sessionView;

                foreach (KeyValuePair<DateTime, HashSet<string>> entry in state.DailyTaskIds)
                {
                    HashSet<string> dayTasks;
                    if (!tasksByDay.TryGetValue(entry.Key, out dayTasks))
                        tasksByDay[entry.Key] = dayTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (string id in entry.Value) dayTasks.Add(state.Id + ":" + id);
                }
            }

            IEnumerable<DateTime> dates = usageByDay.Keys.Union(tasksByDay.Keys).Distinct().OrderByDescending(d => d);
            foreach (DateTime date in dates)
            {
                TokenUsage usage;
                if (!usageByDay.TryGetValue(date, out usage)) usage = new TokenUsage();
                Dictionary<string, TokenUsage> modelUsage;
                if (!modelsByDay.TryGetValue(date, out modelUsage))
                    modelUsage = new Dictionary<string, TokenUsage>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> dayTasks;
                int taskCount = tasksByDay.TryGetValue(date, out dayTasks) ? dayTasks.Count : 0;
                DayView dayView = new DayView
                {
                    Date = date,
                    Tasks = taskCount,
                    Usage = usage.Clone(),
                    ApiValue = PriceCatalog.Calculate(modelUsage),
                    Models = PriceCatalog.ModelList(modelUsage)
                };
                result.Days.Add(dayView);
                if (date == today)
                {
                    result.Today = dayView.Usage.Clone();
                    result.TodayTasks = dayView.Tasks;
                    result.TodayApiValue = dayView.ApiValue;
                }
            }

            result.Sessions.AddRange(sessionUnique.Values
                .OrderByDescending(s => s.IsRunning)
                .ThenByDescending(s => s.LastActivityLocal)
                .Take(60));
            result.RunningSessions = result.Sessions.Count(s => s.IsRunning);
            SessionView quotaSource = result.Sessions
                .Where(s => s.PrimaryLimit.Available || s.SecondaryLimit.Available)
                .OrderByDescending(s => s.LastActivityLocal)
                .FirstOrDefault();
            if (quotaSource != null)
            {
                result.PrimaryLimit = quotaSource.PrimaryLimit.Clone();
                result.SecondaryLimit = quotaSource.SecondaryLimit.Clone();
            }
            return result;
        }

        private int CountIndexed(List<FileInfo> discovered)
        {
            if (discovered == null || discovered.Count == 0) return 0;
            int count = 0;
            foreach (FileInfo file in discovered)
                if (_cursors.ContainsKey(file.FullName)) count++;
            return count;
        }

        private static TaskView ToTaskView(TaskState task, string title, string repository, string cwd, string logPath, bool fallbackTitle, bool fallbackRepository)
        {
            return new TaskView
            {
                SessionId = task.SessionId,
                TurnId = task.TurnId,
                Title = title,
                Repository = repository,
                IsFallbackTitle = fallbackTitle,
                IsFallbackRepository = fallbackRepository,
                WorkingDirectory = cwd,
                LogPath = logPath ?? "",
                StartedLocal = task.StartedLocal,
                CompletedLocal = task.CompletedLocal,
                Usage = task.Usage.Clone(),
                ApiValue = PriceCatalog.Calculate(task.UsageByModel),
                Models = PriceCatalog.ModelList(task.UsageByModel),
                Interrupted = task.Interrupted
            };
        }

        private static string RepositoryRootFor(string cwd)
        {
            if (String.IsNullOrWhiteSpace(cwd) || !Directory.Exists(cwd)) return "";
            try
            {
                DirectoryInfo directory = new DirectoryInfo(Path.GetFullPath(cwd));
                DirectoryInfo fallback = directory;
                while (directory != null)
                {
                    string marker = Path.Combine(directory.FullName, ".git");
                    if (Directory.Exists(marker) || File.Exists(marker)) return directory.FullName;
                    directory = directory.Parent;
                }
                return fallback.FullName;
            }
            catch { return ""; }
        }

        private string TitleFor(SessionState state, string repository)
        {
            string title;
            if (_titles.TryGetValue(state.Id, out title) && !String.IsNullOrWhiteSpace(title)) return FormatUtil.OneLine(title, 90);
            if (!String.IsNullOrWhiteSpace(state.InlineTitle)) return FormatUtil.OneLine(state.InlineTitle, 90);
            return "";
        }

        private string RepositoryFor(string cwd)
        {
            if (String.IsNullOrWhiteSpace(cwd)) return "";
            string cached;
            if (_repositoryCache.TryGetValue(cwd, out cached)) return cached;
            string result = "";
            try
            {
                DirectoryInfo current = new DirectoryInfo(cwd);
                DirectoryInfo fallback = current;
                while (current != null)
                {
                    if (Directory.Exists(Path.Combine(current.FullName, ".git")) || File.Exists(Path.Combine(current.FullName, ".git")))
                    {
                        result = current.Name;
                        break;
                    }
                    current = current.Parent;
                }
                if (String.IsNullOrEmpty(result)) result = fallback.Name;
            }
            catch
            {
                string trimmed = cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                result = Path.GetFileName(trimmed);
            }
            if (String.IsNullOrEmpty(result)) result = cwd;
            _repositoryCache[cwd] = result;
            return result;
        }

        private Color ColorFor(string key)
        {
            Color color;
            if (_colorCache.TryGetValue(key ?? "", out color)) return color;
            Color[] palette =
            {
                Color.FromArgb(92, 225, 230), Color.FromArgb(68, 182, 198),
                Color.FromArgb(255, 111, 166), Color.FromArgb(255, 184, 92),
                Color.FromArgb(112, 229, 150), Color.FromArgb(85, 156, 255)
            };
            int hash = 17;
            foreach (char c in key ?? "") hash = unchecked(hash * 31 + c);
            color = palette[(hash & Int32.MaxValue) % palette.Length];
            _colorCache[key ?? ""] = color;
            return color;
        }

        private static string DetectCodexHome(string[] args)
        {
            string explicitPath = null;
            for (int i = 0; args != null && i < args.Length; i++)
            {
                if (args[i].StartsWith("--codex-home=", StringComparison.OrdinalIgnoreCase))
                    explicitPath = args[i].Substring("--codex-home=".Length).Trim('"');
                else if (args[i].Equals("--codex-home", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    explicitPath = args[++i];
            }

            List<string> candidates = new List<string>();
            if (!String.IsNullOrWhiteSpace(explicitPath)) candidates.Add(explicitPath);
            string environment = Environment.GetEnvironmentVariable("CODEX_HOME");
            if (!String.IsNullOrWhiteSpace(environment)) candidates.Add(environment);
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!String.IsNullOrWhiteSpace(profile)) candidates.Add(Path.Combine(profile, ".codex"));
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!String.IsNullOrWhiteSpace(local)) candidates.Add(Path.Combine(local, "Codex"));
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!String.IsNullOrWhiteSpace(roaming)) candidates.Add(Path.Combine(roaming, "Codex"));

            foreach (string raw in candidates)
            {
                try
                {
                    string path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(raw));
                    if (Directory.Exists(Path.Combine(path, "sessions")) || File.Exists(Path.Combine(path, "session_index.jsonl"))) return path;
                }
                catch { }
            }
            return candidates.Count > 0 ? candidates[0] : "";
        }

        private void SaveCache()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath));
                string temp = _cachePath + ".tmp";
                List<FileCursor> entries = _cursors.Values
                    .Where(c => c.Session != null && !String.IsNullOrEmpty(c.Session.Id))
                    .OrderByDescending(c => c.Session.LastWriteLocal)
                    .Take(2000)
                    .ToList();
                using (FileStream output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
                using (BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8))
                {
                    writer.Write("CTM-CACHE-9");
                    writer.Write(entries.Count);
                    foreach (FileCursor cursor in entries) WriteCursor(writer, cursor);
                    writer.Flush();
                    output.Flush(true);
                }
                if (File.Exists(_cachePath)) File.Replace(temp, _cachePath, null, true);
                else File.Move(temp, _cachePath);
                _dirty = false;
                _lastCacheSaveUtc = DateTime.UtcNow;
            }
            catch { }
        }

        private void LoadCache()
        {
            if (!File.Exists(_cachePath)) return;
            try
            {
                using (BinaryReader reader = new BinaryReader(new FileStream(_cachePath, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.UTF8))
                {
                    if (reader.ReadString() != "CTM-CACHE-9") return;
                    int count = reader.ReadInt32();
                    if (count < 0 || count > 2000) return;
                    for (int i = 0; i < count; i++)
                    {
                        FileCursor cursor = ReadCursor(reader);
                        foreach (TaskState task in cursor.Session.Tasks)
                        {
                            DateTime activityTime = task.CompletedLocal ?? task.StartedLocal;
                            if (activityTime > cursor.Session.LastWriteLocal) cursor.Session.LastWriteLocal = activityTime;
                        }
                        if (IsCurrentHomePath(cursor.Path)) _cursors[cursor.Path] = cursor;
                    }
                    _indexLimit = Math.Max(InitialIndexLimit, _cursors.Count);
                }
            }
            catch
            {
                _cursors.Clear();
                _indexLimit = InitialIndexLimit;
            }
        }

        private bool IsCurrentHomePath(string path)
        {
            if (String.IsNullOrWhiteSpace(_codexHome) || String.IsNullOrWhiteSpace(path)) return false;
            try
            {
                string root = Path.GetFullPath(_codexHome).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                string candidate = Path.GetFullPath(path);
                return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static void WriteCursor(BinaryWriter writer, FileCursor cursor)
        {
            writer.Write(cursor.Path ?? "");
            writer.Write(cursor.Offset);
            writer.Write(cursor.LastObservedLength);
            writer.Write(cursor.LastWriteUtcTicks);
            byte[] partial = cursor.Partial != null && cursor.Partial.Length <= 1024 * 1024 ? cursor.Partial : new byte[0];
            writer.Write(partial.Length);
            writer.Write(partial);
            writer.Write(cursor.DiscardingOversizedLine);
            SessionState state = cursor.Session;
            writer.Write(state.Id ?? "");
            writer.Write(state.Cwd ?? "");
            writer.Write(state.InlineTitle ?? "");
            writer.Write(state.CurrentModel ?? "");
            writer.Write(state.CurrentEffort ?? "");
            writer.Write(state.ModelContextWindow);
            WriteUsage(writer, state.CurrentContext);
            WriteRateLimit(writer, state.PrimaryLimit);
            WriteRateLimit(writer, state.SecondaryLimit);
            writer.Write(state.FirstSeenLocal.ToBinary());
            writer.Write(state.LastWriteLocal.ToBinary());
            writer.Write(state.HasRawCounter);
            WriteUsage(writer, state.RawCounter);
            WriteUsage(writer, state.EffectiveTotal);
            WriteUsageMap(writer, state.UsageByModel);
            writer.Write(state.Daily.Count);
            foreach (KeyValuePair<DateTime, TokenUsage> day in state.Daily)
            {
                writer.Write(day.Key.ToBinary());
                WriteUsage(writer, day.Value);
                Dictionary<string, TokenUsage> dayModels;
                state.DailyByModel.TryGetValue(day.Key, out dayModels);
                WriteUsageMap(writer, dayModels);
            }
            writer.Write(state.DailyTaskIds.Count);
            foreach (KeyValuePair<DateTime, HashSet<string>> day in state.DailyTaskIds)
            {
                writer.Write(day.Key.ToBinary());
                writer.Write(day.Value.Count);
                foreach (string id in day.Value) writer.Write(id);
            }
            writer.Write(state.Tasks.Count);
            foreach (TaskState task in state.Tasks)
            {
                writer.Write(task.SessionId ?? "");
                writer.Write(task.TurnId ?? "");
                writer.Write(task.StartedLocal.ToBinary());
                writer.Write(task.CompletedLocal.HasValue);
                if (task.CompletedLocal.HasValue) writer.Write(task.CompletedLocal.Value.ToBinary());
                writer.Write(task.Interrupted);
                WriteUsage(writer, task.Baseline);
                WriteUsage(writer, task.Usage);
                WriteUsageMap(writer, task.UsageByModel);
            }
        }

        private static FileCursor ReadCursor(BinaryReader reader)
        {
            FileCursor cursor = new FileCursor();
            cursor.Path = reader.ReadString();
            cursor.Offset = reader.ReadInt64();
            cursor.LastObservedLength = reader.ReadInt64();
            cursor.LastWriteUtcTicks = reader.ReadInt64();
            int partialLength = reader.ReadInt32();
            if (partialLength < 0 || partialLength > 1024 * 1024) throw new InvalidDataException("Invalid cache");
            cursor.Partial = reader.ReadBytes(partialLength);
            cursor.DiscardingOversizedLine = reader.ReadBoolean();
            SessionState state = new SessionState();
            state.Id = reader.ReadString();
            state.Cwd = reader.ReadString();
            state.InlineTitle = reader.ReadString();
            state.CurrentModel = reader.ReadString();
            state.CurrentEffort = reader.ReadString();
            state.ModelContextWindow = reader.ReadInt64();
            state.CurrentContext = ReadUsage(reader);
            state.PrimaryLimit = ReadRateLimit(reader);
            state.SecondaryLimit = ReadRateLimit(reader);
            state.FirstSeenLocal = DateTime.FromBinary(reader.ReadInt64());
            state.LastWriteLocal = DateTime.FromBinary(reader.ReadInt64());
            state.HasRawCounter = reader.ReadBoolean();
            state.RawCounter = ReadUsage(reader);
            state.EffectiveTotal = ReadUsage(reader);
            ReadUsageMap(reader, state.UsageByModel);
            int days = reader.ReadInt32();
            if (days < 0 || days > 10000) throw new InvalidDataException("Invalid cache");
            for (int i = 0; i < days; i++)
            {
                DateTime day = DateTime.FromBinary(reader.ReadInt64());
                state.Daily[day] = ReadUsage(reader);
                Dictionary<string, TokenUsage> dayModels = new Dictionary<string, TokenUsage>(StringComparer.OrdinalIgnoreCase);
                ReadUsageMap(reader, dayModels);
                state.DailyByModel[day] = dayModels;
            }
            int taskDays = reader.ReadInt32();
            if (taskDays < 0 || taskDays > 100000) throw new InvalidDataException("Invalid task day count.");
            for (int i = 0; i < taskDays; i++)
            {
                DateTime day = DateTime.FromBinary(reader.ReadInt64());
                int count = reader.ReadInt32();
                if (count < 0 || count > 1000000) throw new InvalidDataException("Invalid task key count.");
                HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < count; j++) ids.Add(reader.ReadString());
                state.DailyTaskIds[day] = ids;
            }
            int tasks = reader.ReadInt32();
            if (tasks < 0 || tasks > 100000) throw new InvalidDataException("Invalid cache");
            for (int i = 0; i < tasks; i++)
            {
                TaskState task = new TaskState();
                task.SessionId = reader.ReadString();
                task.TurnId = reader.ReadString();
                task.StartedLocal = DateTime.FromBinary(reader.ReadInt64());
                if (reader.ReadBoolean()) task.CompletedLocal = DateTime.FromBinary(reader.ReadInt64());
                task.Interrupted = reader.ReadBoolean();
                task.Baseline = ReadUsage(reader);
                task.Usage = ReadUsage(reader);
                ReadUsageMap(reader, task.UsageByModel);
                state.Tasks.Add(task);
            }
            cursor.Session = state;
            return cursor;
        }

        private static void WriteUsage(BinaryWriter writer, TokenUsage usage)
        {
            usage = usage ?? new TokenUsage();
            writer.Write(usage.Input);
            writer.Write(usage.Cached);
            writer.Write(usage.CacheWrite);
            writer.Write(usage.Output);
            writer.Write(usage.Reasoning);
            writer.Write(usage.Total);
        }

        private static void WriteRateLimit(BinaryWriter writer, RateLimitView limit)
        {
            limit = limit ?? new RateLimitView();
            writer.Write(limit.Available);
            writer.Write(limit.UsedPercent);
            writer.Write(limit.WindowMinutes);
            writer.Write(limit.ResetsLocal.HasValue);
            if (limit.ResetsLocal.HasValue) writer.Write(limit.ResetsLocal.Value.ToBinary());
        }

        private static RateLimitView ReadRateLimit(BinaryReader reader)
        {
            RateLimitView limit = new RateLimitView
            {
                Available = reader.ReadBoolean(),
                UsedPercent = reader.ReadInt32(),
                WindowMinutes = reader.ReadInt32()
            };
            if (reader.ReadBoolean()) limit.ResetsLocal = DateTime.FromBinary(reader.ReadInt64());
            return limit;
        }

        private static TokenUsage ReadUsage(BinaryReader reader)
        {
            return new TokenUsage
            {
                Input = reader.ReadInt64(),
                Cached = reader.ReadInt64(),
                CacheWrite = reader.ReadInt64(),
                Output = reader.ReadInt64(),
                Reasoning = reader.ReadInt64(),
                Total = reader.ReadInt64()
            };
        }

        private static void WriteUsageMap(BinaryWriter writer, IDictionary<string, TokenUsage> map)
        {
            int count = map == null ? 0 : map.Count;
            writer.Write(count);
            if (map == null) return;
            foreach (KeyValuePair<string, TokenUsage> item in map)
            {
                writer.Write(item.Key ?? UsageBuckets.UnknownModel);
                WriteUsage(writer, item.Value);
            }
        }

        private static void ReadUsageMap(BinaryReader reader, IDictionary<string, TokenUsage> target)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 1000) throw new InvalidDataException("Invalid model usage cache");
            for (int i = 0; i < count; i++) target[reader.ReadString()] = ReadUsage(reader);
        }

        public void Dispose()
        {
            _disposed = true;
            Timer timer = _timer;
            _timer = null;
            if (timer != null) timer.Dispose();
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(800);
            while (Interlocked.CompareExchange(ref _busy, 1, 0) != 0 && DateTime.UtcNow < deadline) Thread.Sleep(20);
            try { if (_dirty) SaveCache(); }
            finally { Interlocked.Exchange(ref _busy, 0); }
        }

    }
}
