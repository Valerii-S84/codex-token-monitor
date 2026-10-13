using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CodexTokenMonitor
{
    internal sealed class TokenUsage
    {
        public long Input;
        public long Cached;
        public long CacheWrite;
        public long Output;
        public long Reasoning;
        public long Total;

        public TokenUsage Clone()
        {
            return new TokenUsage
            {
                Input = Input,
                Cached = Cached,
                CacheWrite = CacheWrite,
                Output = Output,
                Reasoning = Reasoning,
                Total = Total
            };
        }

        public void Add(TokenUsage other)
        {
            if (other == null) return;
            Input += other.Input;
            Cached += other.Cached;
            CacheWrite += other.CacheWrite;
            Output += other.Output;
            Reasoning += other.Reasoning;
            Total += other.Total;
        }

        public static TokenUsage Difference(TokenUsage current, TokenUsage baseline)
        {
            if (current == null) return new TokenUsage();
            if (baseline == null) return current.Clone();
            return new TokenUsage
            {
                Input = Math.Max(0, current.Input - baseline.Input),
                Cached = Math.Max(0, current.Cached - baseline.Cached),
                CacheWrite = Math.Max(0, current.CacheWrite - baseline.CacheWrite),
                Output = Math.Max(0, current.Output - baseline.Output),
                Reasoning = Math.Max(0, current.Reasoning - baseline.Reasoning),
                Total = Math.Max(0, current.Total - baseline.Total)
            };
        }

        public static TokenUsage DeltaFromCounter(TokenUsage current, TokenUsage previous, bool hasPrevious)
        {
            if (!hasPrevious || previous == null) return current.Clone();
            return new TokenUsage
            {
                Input = current.Input >= previous.Input ? current.Input - previous.Input : current.Input,
                Cached = current.Cached >= previous.Cached ? current.Cached - previous.Cached : current.Cached,
                CacheWrite = current.CacheWrite >= previous.CacheWrite ? current.CacheWrite - previous.CacheWrite : current.CacheWrite,
                Output = current.Output >= previous.Output ? current.Output - previous.Output : current.Output,
                Reasoning = current.Reasoning >= previous.Reasoning ? current.Reasoning - previous.Reasoning : current.Reasoning,
                Total = current.Total >= previous.Total ? current.Total - previous.Total : current.Total
            };
        }
    }

    internal sealed class RateLimitView
    {
        public bool Available;
        public int UsedPercent;
        public int WindowMinutes;
        public DateTime? ResetsLocal;

        public RateLimitView Clone()
        {
            return new RateLimitView
            {
                Available = Available,
                UsedPercent = UsedPercent,
                WindowMinutes = WindowMinutes,
                ResetsLocal = ResetsLocal
            };
        }
    }

    internal sealed class TaskState
    {
        public string SessionId = "";
        public string TurnId = "";
        public DateTime StartedLocal;
        public DateTime? CompletedLocal;
        public TokenUsage Baseline = new TokenUsage();
        public TokenUsage Usage = new TokenUsage();
        public readonly Dictionary<string, TokenUsage> UsageByModel = new Dictionary<string, TokenUsage>(StringComparer.OrdinalIgnoreCase);
        public bool Interrupted;
    }

    internal sealed class SessionState
    {
        public string Id = "";
        public string Cwd = "";
        public string InlineTitle = "";
        public string CurrentModel = "";
        public string CurrentEffort = "";
        public DateTime FirstSeenLocal;
        public DateTime LastWriteLocal;
        public long ModelContextWindow;
        public TokenUsage CurrentContext = new TokenUsage();
        public RateLimitView PrimaryLimit = new RateLimitView();
        public RateLimitView SecondaryLimit = new RateLimitView();
        public TokenUsage RawCounter = new TokenUsage();
        public bool HasRawCounter;
        public TokenUsage EffectiveTotal = new TokenUsage();
        public readonly Dictionary<string, TokenUsage> UsageByModel = new Dictionary<string, TokenUsage>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<DateTime, TokenUsage> Daily = new Dictionary<DateTime, TokenUsage>();
        public readonly Dictionary<DateTime, Dictionary<string, TokenUsage>> DailyByModel = new Dictionary<DateTime, Dictionary<string, TokenUsage>>();
        public readonly Dictionary<DateTime, HashSet<string>> DailyTaskIds = new Dictionary<DateTime, HashSet<string>>();
        public readonly List<TaskState> Tasks = new List<TaskState>();

        public TaskState CurrentTask
        {
            get
            {
                for (int i = Tasks.Count - 1; i >= 0; i--)
                    if (!Tasks[i].CompletedLocal.HasValue) return Tasks[i];
                return null;
            }
        }
    }

    internal sealed class FileCursor
    {
        public bool DiscardingOversizedLine;
        public string Path = "";
        public long Offset;
        public long LastObservedLength;
        public long LastWriteUtcTicks;
        public byte[] Partial = new byte[0];
        public SessionState Session = new SessionState();
    }

    internal sealed class TaskView
    {
        public string SessionId = "";
        public string TurnId = "";
        public string Title = "";
        public string Repository = "";
        public bool IsFallbackTitle;
        public bool IsFallbackRepository;
        public string DisplayRepository { get { return IsFallbackRepository ? L10n.T("Engine.UnknownRepository") : Repository; } }
        public string DisplayTitle { get { return IsFallbackTitle ? (IsFallbackRepository || String.IsNullOrEmpty(Repository) ? L10n.T("Engine.Task") : L10n.F("Engine.RepositoryTask", Repository)) : Title; } }
        public string WorkingDirectory = "";
        public string LogPath = "";
        public DateTime StartedLocal;
        public DateTime? CompletedLocal;
        public TokenUsage Usage = new TokenUsage();
        public CostEstimate ApiValue = new CostEstimate();
        public string Models = "";
        public bool Interrupted;
    }

    internal sealed class SessionView
    {
        public string Id = "";
        public string Title = "";
        public string Repository = "";
        public bool IsFallbackTitle;
        public bool IsFallbackRepository;
        public string DisplayRepository { get { return IsFallbackRepository ? L10n.T("Engine.UnknownRepository") : Repository; } }
        public string DisplayTitle { get { return IsFallbackTitle ? (IsFallbackRepository || String.IsNullOrEmpty(Repository) ? L10n.T("Engine.Task") : L10n.F("Engine.RepositoryTask", Repository)) : Title; } }
        public string WorkingDirectory = "";
        public DateTime LastActivityLocal;
        public bool IsRunning;
        public bool IsStale;
        public string CurrentModel = "";
        public string CurrentEffort = "";
        public DateTime StartedLocal;
        public long ModelContextWindow;
        public TokenUsage CurrentContext = new TokenUsage();
        public TokenUsage SessionTotal = new TokenUsage();
        public CostEstimate ApiValue = new CostEstimate();
        public RateLimitView PrimaryLimit = new RateLimitView();
        public RateLimitView SecondaryLimit = new RateLimitView();
        public TaskView CurrentOrLastTask;
        public int TurnCount;
        public Color Accent;
    }

    internal sealed class DayView
    {
        public DateTime Date;
        public int Tasks;
        public TokenUsage Usage = new TokenUsage();
        public CostEstimate ApiValue = new CostEstimate();
        public string Models = "";
    }

    internal sealed class MonitorSnapshot
    {
        public string CodexHome = "";
        public readonly List<SessionView> Sessions = new List<SessionView>();
        public readonly List<DayView> Days = new List<DayView>();
        public TokenUsage Today = new TokenUsage();
        public CostEstimate TodayApiValue = new CostEstimate();
        public int TodayTasks;
        public int RunningSessions;
        public RateLimitView PrimaryLimit = new RateLimitView();
        public RateLimitView SecondaryLimit = new RateLimitView();
        public int IndexedFiles;
        public int AvailableFiles;
        public int IndexTarget;
        public string Error = "";
        public string DisplayError { get { return String.IsNullOrEmpty(Error) ? "" : Error == "CODEX_HOME not found" ? L10n.T("Engine.HomeMissing") : L10n.F("Engine.ReadError", Error); } }
        public DateTime RefreshedLocal = DateTime.Now;
    }

    internal static class FormatUtil
    {
        public static string Tokens(long value)
        {
            double abs = Math.Abs((double)value);
            if (abs >= 1000000000L) return (value / 1000000000d).ToString("0.##", L10n.Culture) + L10n.T("Format.Billion");
            if (abs >= 1000000L) return (value / 1000000d).ToString("0.##", L10n.Culture) + L10n.T("Format.Million");
            if (abs >= 1000L) return (value / 1000d).ToString("0.#", L10n.Culture) + L10n.T("Format.Thousand");
            return value.ToString("N0", L10n.Culture);
        }

        public static string Duration(DateTime started, DateTime? completed)
        {
            TimeSpan span = (completed ?? DateTime.Now) - started;
            if (span.TotalHours >= 1) return ((int)span.TotalHours).ToString(L10n.Culture) + L10n.T("Format.Hour") + " " + span.Minutes.ToString(L10n.Culture) + L10n.T("Format.Minute");
            if (span.TotalMinutes >= 1) return ((int)span.TotalMinutes).ToString(L10n.Culture) + L10n.T("Format.Minute") + " " + span.Seconds.ToString(L10n.Culture) + L10n.T("Format.Second");
            return Math.Max(0, (int)span.TotalSeconds).ToString(L10n.Culture) + L10n.T("Format.Second");
        }

        public static string ApiValue(CostEstimate estimate)
        {
            if (estimate == null) return "≈?";
            if (!estimate.HasKnownUsage && estimate.HasUnknownUsage) return "≈?";
            decimal value = estimate.Value;
            string amount;
            if (value > 0m && value < 0.01m) amount = "$" + value.ToString("0.000", L10n.Culture);
            else if (value >= 1000000m) amount = "$" + (value / 1000000m).ToString("0.##", L10n.Culture) + L10n.T("Format.Million");
            else if (value >= 1000m) amount = "$" + (value / 1000m).ToString("0.##", L10n.Culture) + L10n.T("Format.Thousand");
            else amount = "$" + value.ToString("0.00", L10n.Culture);
            return "≈" + amount + (estimate.IsPartial ? "*" : "");
        }

        public static int ContextPercent(TokenUsage usage, long window)
        {
            if (usage == null || window <= 0) return -1;
            return Math.Max(0, Math.Min(100, (int)Math.Round(usage.Total * 100d / window)));
        }

        public static int CacheHitPercent(TokenUsage usage)
        {
            if (usage == null || usage.Input <= 0) return 0;
            return Math.Max(0, Math.Min(100, (int)Math.Round(usage.Cached * 100d / usage.Input)));
        }

        public static string ModelName(string model)
        {
            return String.IsNullOrWhiteSpace(model) || model == UsageBuckets.UnknownModel ? L10n.T("Format.UnknownModel") : model;
        }

        public static string CompactModelName(string model)
        {
            if (String.IsNullOrWhiteSpace(model)) return "—";
            return ModelName(model);
        }

        public static string ModelList(string models)
        {
            if (String.IsNullOrWhiteSpace(models) || models == UsageBuckets.UnknownModel) return L10n.T("Format.UnknownModel");
            return String.Join(", ", models.Split(new[] { ", " }, StringSplitOptions.None).Select(ModelName));
        }

        public static string RelativeAgeShort(DateTime value)
        {
            TimeSpan age = DateTime.Now - value;
            if (age.TotalSeconds < 0) age = TimeSpan.Zero;
            if (age.TotalMinutes < 1) return Math.Max(0, (int)age.TotalSeconds).ToString(L10n.Culture) + L10n.T("Format.Second");
            if (age.TotalHours < 1) return ((int)age.TotalMinutes).ToString(L10n.Culture) + L10n.T("Format.Minute");
            if (age.TotalDays < 1) return ((int)age.TotalHours).ToString(L10n.Culture) + L10n.T("Format.Hour");
            return ((int)age.TotalDays).ToString(L10n.Culture) + L10n.T("Format.Day");
        }

        public static string RelativeAge(DateTime value)
        {
            return L10n.F("Format.Ago", RelativeAgeShort(value));
        }

        public static string OneLine(string value, int maxLength)
        {
            if (String.IsNullOrWhiteSpace(value)) return "";
            string clean = String.Join(" ", value.Replace('\r', ' ').Replace('\n', ' ').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            if (clean.Length <= maxLength) return clean;
            return clean.Substring(0, Math.Max(1, maxLength - 1)).TrimEnd() + "…";
        }
    }

    internal sealed class AppSettings
    {
        public int X = Int32.MinValue;
        public int Y = Int32.MinValue;
        public int Width = 420;
        public int Height = 620;
        public bool TopMost = true;

        public static string SettingsDirectory
        {
            get
            {
                string overridePath = Environment.GetEnvironmentVariable("CODEX_TOKEN_MONITOR_DATA_HOME");
                return String.IsNullOrWhiteSpace(overridePath)
                    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexTokenMonitor")
                    : Path.GetFullPath(overridePath);
            }
        }

        public static string SettingsPath
        {
            get { return Path.Combine(SettingsDirectory, "settings.ini"); }
        }

        public static string InstanceMutexName
        {
            get
            {
                const string standard = "Local\\CodexTokenMonitor-v2";
                if (String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CODEX_TOKEN_MONITOR_DATA_HOME"))) return standard;
                string directory = SettingsDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
                using (System.Security.Cryptography.SHA256 hash = System.Security.Cryptography.SHA256.Create())
                    return standard + "-" + BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(directory))).Replace("-", "");
            }
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                if (!File.Exists(SettingsPath)) return settings;
                foreach (string raw in File.ReadAllLines(SettingsPath))
                {
                    int split = raw.IndexOf('=');
                    if (split <= 0) continue;
                    string key = raw.Substring(0, split).Trim();
                    string value = raw.Substring(split + 1).Trim();
                    int number;
                    bool flag;
                    if (key == "x" && Int32.TryParse(value, out number)) settings.X = number;
                    else if (key == "y" && Int32.TryParse(value, out number)) settings.Y = number;
                    else if (key == "width" && Int32.TryParse(value, out number)) settings.Width = Math.Max(390, number);
                    else if (key == "height" && Int32.TryParse(value, out number)) settings.Height = Math.Max(540, number);
                    else if (key == "topmost" && Boolean.TryParse(value, out flag)) settings.TopMost = flag;
                }
            }
            catch { }
            return settings;
        }

        public void Apply(Form form)
        {
            Point anchor = X == Int32.MinValue || Y == Int32.MinValue
                ? Screen.PrimaryScreen.WorkingArea.Location
                : new Point(X, Y);
            Screen target = Screen.FromPoint(anchor) ?? Screen.PrimaryScreen;
            Rectangle work = target.WorkingArea;
            int safeWidth = Math.Min(Width, Math.Max(form.MinimumSize.Width, work.Width - 12));
            int safeHeight = Math.Min(Height, Math.Max(form.MinimumSize.Height, work.Height - 12));
            form.Size = new Size(safeWidth, safeHeight);
            if (X == Int32.MinValue || Y == Int32.MinValue) return;
            int safeX = Math.Max(work.Left, Math.Min(X, work.Right - safeWidth));
            int safeY = Math.Max(work.Top, Math.Min(Y, work.Bottom - safeHeight));
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(safeX, safeY);
        }

        public void Capture(Form form)
        {
            Rectangle bounds = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
            X = bounds.X;
            Y = bounds.Y;
            Width = bounds.Width;
            Height = bounds.Height;
            TopMost = form.TopMost;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                string content = "x=" + X + Environment.NewLine +
                                 "y=" + Y + Environment.NewLine +
                                 "width=" + Width + Environment.NewLine +
                                 "height=" + Height + Environment.NewLine +
                                 "topmost=" + TopMost + Environment.NewLine;
                File.WriteAllText(SettingsPath, content);
            }
            catch { }
        }
    }
}
