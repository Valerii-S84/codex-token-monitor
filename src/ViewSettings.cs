using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CodexTokenMonitor
{
    internal sealed class ViewSettings
    {
        internal int Mode = 1;
        internal int Palette;
        internal int Intensity = 80;
        internal bool Economy;
        internal bool Motion = true;
        internal string Language = L10n.DefaultLanguage();
        internal static string FilePath { get { return Path.Combine(AppSettings.SettingsDirectory, "appearance.ini"); } }
        internal static ViewSettings Current = Load(FilePath);
        internal Color Accent { get { return Palette == 1 ? Theme.Yellow : Palette == 2 ? Theme.Orange : Theme.Cyan; } }
        internal Color High { get { return Palette == 1 ? Color.FromArgb(49, 43, 27) : Palette == 2 ? Color.FromArgb(50, 32, 27) : Color.FromArgb(27, 42, 52); } }
        internal Color Low { get { return Palette == 1 ? Color.FromArgb(29, 27, 19) : Palette == 2 ? Color.FromArgb(29, 21, 21) : Color.FromArgb(15, 27, 35); } }
        internal Color[] TokenColors { get { return Palette == 1 ? new[] { Theme.Yellow, Theme.Orange, Theme.Cyan, Theme.Green, Theme.Text, Theme.Yellow } : Palette == 2 ? new[] { Theme.Orange, Theme.Yellow, Theme.Cyan, Theme.Green, Theme.Text, Theme.Orange } : new[] { Theme.Cyan, SpectrumFx.Blue, Theme.Orange, Theme.Green, Theme.Yellow, Theme.Cyan }; } }
        internal Color Adapt(Color color)
        {
            return color == Theme.Teal || color == Theme.Cyan || color == SpectrumFx.Blue ? Accent : color;
        }
        internal static ViewSettings Load(string path)
        {
            ViewSettings value = new ViewSettings();
            try
            {
                // A pre-language installation may have only the window settings file.
                bool legacy = File.Exists(path) || File.Exists(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)), "settings.ini"));
                if (legacy) value.Language = "uk";
                if (!File.Exists(path)) return value;
                foreach (string line in File.ReadAllLines(path))
                {
                    int split = line.IndexOf('=');
                    if (split < 1) continue;
                    string key = line.Substring(0, split), raw = line.Substring(split + 1);
                    int number; bool flag;
                    if (key == "mode" && Int32.TryParse(raw, out number)) value.Mode = Math.Max(0, Math.Min(2, number));
                    else if (key == "palette" && Int32.TryParse(raw, out number)) value.Palette = Math.Max(0, Math.Min(2, number));
                    else if (key == "intensity" && Int32.TryParse(raw, out number)) value.Intensity = Math.Max(0, Math.Min(100, number));
                    else if (key == "economy" && Boolean.TryParse(raw, out flag)) value.Economy = flag;
                    else if (key == "motion" && Boolean.TryParse(raw, out flag)) value.Motion = flag;
                    else if (key.Trim() == "language") value.Language = L10n.IsValidLanguage(raw.Trim()) ? raw.Trim().ToLowerInvariant() : "uk";
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return value;
        }
        internal void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string pending = path + ".tmp";
            File.WriteAllLines(pending, new[] { "mode=" + Mode, "palette=" + Palette, "intensity=" + Intensity,
                "economy=" + Economy, "motion=" + Motion, "language=" + (L10n.IsValidLanguage(Language) ? Language.ToLowerInvariant() : "uk") });
            if (File.Exists(path)) File.Replace(pending, path, null); else File.Move(pending, path);
        }
        internal void Save()
        {
            try { Save(FilePath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static class UsageSignals
    {
        internal static double? TokensPerMinute(IEnumerable<UsageHistorySample> history, DateTime now)
        {
            List<UsageHistorySample> samples = (history ?? Enumerable.Empty<UsageHistorySample>())
                .Where(x => x != null && x.At <= now).OrderBy(x => x.At).ToList();
            if (samples.Count < 2) return null;
            UsageHistorySample last = samples[samples.Count - 1];
            if ((now - last.At).TotalSeconds > 30) return null;
            DateTime cutoff = last.At.AddMinutes(-1);
            UsageHistorySample first = samples.LastOrDefault(x => x.At <= cutoff)
                ?? samples[0];
            double minutes = (last.At - first.At).TotalMinutes;
            if (minutes < 5d / 60 || last.TotalTokens < first.TotalTokens) return null;
            return (last.TotalTokens - first.TotalTokens) / minutes;
        }
        internal static string Comparison(TaskView current, IEnumerable<TaskView> history)
        {
            if (current == null) return L10n.T("Usage.NoTask");
            List<TaskView> peers = (history ?? Enumerable.Empty<TaskView>()).Where(t => t != null
                && t.CompletedLocal.HasValue && !t.Interrupted && t.Usage != null && t.Usage.Total > 0
                && String.Equals(t.WorkingDirectory, current.WorkingDirectory, StringComparison.OrdinalIgnoreCase)
                && !(t.SessionId == current.SessionId && t.TurnId == current.TurnId))
                .OrderByDescending(t => t.CompletedLocal).Take(20).ToList();
            if (peers.Count == 0) return L10n.T("Usage.NoPeers");
            double average = peers.Average(t => (double)t.Usage.Total);
            double delta = ((current.Usage == null ? 0 : current.Usage.Total) / average - 1) * 100;
            return L10n.F("Usage.Comparison", delta.ToString("+0;-0;0", L10n.Culture), peers.Count);
        }
    }

    internal sealed class UsageOverview : Control
    {
        internal SessionView Session;
        internal MonitorSnapshot Snapshot = new MonitorSnapshot();
        internal double? Rate;
        internal float LayoutScale = 1;
        internal UsageOverview()
        {
            BackColor = Theme.Background;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width < 60 || Height < 30) return;
            TaskView task = Session == null ? null : Session.CurrentOrLastTask;
            string[] captions = { L10n.T("Usage.Task"), L10n.T("Usage.Session"), L10n.T("Usage.Today") };
            string[] values = { task == null ? "—" : FormatUtil.Tokens(task.Usage.Total), Session == null ? "—" : FormatUtil.Tokens(Session.SessionTotal.Total), FormatUtil.Tokens(Snapshot.Today.Total) };
            string[] details = { UsageSignals.Comparison(task, Snapshot.Sessions.Select(s => s.CurrentOrLastTask)),
                Rate.HasValue ? L10n.F("Usage.TokenRate", FormatUtil.Tokens((long)Math.Round(Rate.Value))) : L10n.T("Usage.CollectingRate"),
                L10n.F("Usage.TodayDetail", Snapshot.TodayTasks, FormatUtil.ApiValue(Snapshot.TodayApiValue)) };
            Color[] colors = { Theme.Orange, ViewSettings.Current.Accent, Theme.Green };
            int gap = Math.Max(8, (int)(8 * LayoutScale));
            int width = (Width - gap * 2) / 3;
            for (int i = 0; i < 3; i++)
            {
                Rectangle card = new Rectangle(i * (width + gap), 0, width, Height - 1);
                AuroraUi.Surface(e.Graphics, card, colors[i], 13);
                ContextSculpture.DrawToken(e.Graphics, new Rectangle(card.X + 10, 8, 23, 23), colors[i], i + SpectrumFx.Phase * .1);
                AuroraUi.Text(e.Graphics, captions[i], new Rectangle(card.X + 39, 7, width - 49, 22), 8.5f * LayoutScale, colors[i], FontStyle.Bold, TextFormatFlags.Left);
                AuroraUi.Text(e.Graphics, values[i], new Rectangle(card.X + 12, 30, width - 24, Math.Max(25, Height - 58)), 22 * LayoutScale, Theme.Text, FontStyle.Bold, TextFormatFlags.Left);
                AuroraUi.Text(e.Graphics, details[i], new Rectangle(card.X + 12, Height - 25, width - 24, 19), 7 * LayoutScale, Theme.Muted, FontStyle.Regular, TextFormatFlags.Left);
            }
        }
    }
}
