using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace CodexTokenMonitor
{
    // Both catalogs are compiled into the executable. Protocol/data parsing stays invariant.
    internal static partial class L10n
    {
        private static readonly Dictionary<string, string> Ukrainian = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal);
        private static string _language = "uk";
        private static CultureInfo _culture = CultureInfo.GetCultureInfo("uk-UA");
        internal static event EventHandler LanguageChanged;
        internal static string Language { get { return _language; } }
        internal static CultureInfo Culture { get { return _culture; } }

        static L10n()
        {
            AddCoreTranslations(Ukrainian, English);
            AddMainFormTranslations(Ukrainian, English);
            string[] errors = ValidateCatalog();
            if (errors.Length > 0) throw new InvalidOperationException(String.Join("; ", errors));
        }

        static partial void AddMainFormTranslations(Dictionary<string, string> uk, Dictionary<string, string> en);
        private static void Add(Dictionary<string, string> uk, Dictionary<string, string> en, string key, string ukText, string enText)
        {
            uk.Add(key, ukText);
            en.Add(key, enText);
        }

        internal static bool IsValidLanguage(string value)
        {
            return String.Equals(value, "uk", StringComparison.OrdinalIgnoreCase) || String.Equals(value, "en", StringComparison.OrdinalIgnoreCase);
        }

        internal static string DefaultLanguage()
        {
            // Windows user display language includes language packs and remains stable
            // after SetLanguage changes this process's thread cultures.
            try { return DefaultLanguage(CultureInfo.GetCultureInfo((int)GetUserDefaultUILanguage())); }
            catch (DllNotFoundException) { return DefaultLanguage(CultureInfo.InstalledUICulture); }
            catch (EntryPointNotFoundException) { return DefaultLanguage(CultureInfo.InstalledUICulture); }
            catch (CultureNotFoundException) { return DefaultLanguage(CultureInfo.InstalledUICulture); }
        }
        [DllImport("kernel32.dll")]
        private static extern ushort GetUserDefaultUILanguage();
        internal static string DefaultLanguage(CultureInfo windowsUiCulture)
        {
            return windowsUiCulture != null && windowsUiCulture.TwoLetterISOLanguageName == "uk" ? "uk" : "en";
        }

        internal static void SetLanguage(string language)
        {
            string next = IsValidLanguage(language) ? language.ToLowerInvariant() : "uk";
            bool changed = _language != next;
            _culture = CultureInfo.GetCultureInfo(next == "uk" ? "uk-UA" : "en-US");
            _language = next;
            Thread.CurrentThread.CurrentCulture = _culture;
            Thread.CurrentThread.CurrentUICulture = _culture;
            CultureInfo.DefaultThreadCurrentCulture = _culture;
            CultureInfo.DefaultThreadCurrentUICulture = _culture;
            EventHandler handler = LanguageChanged;
            if (changed && handler != null) handler(null, EventArgs.Empty);
        }

        internal static string T(string key)
        {
            string value;
            if (!(_language == "uk" ? Ukrainian : English).TryGetValue(key, out value))
                throw new ArgumentException("Missing translation: " + key, "key");
            return value;
        }

        internal static string F(string key, params object[] args) { return String.Format(Culture, T(key), args); }

        internal static string[] ValidateCatalog()
        {
            List<string> errors = new List<string>();
            foreach (string key in Ukrainian.Keys.Union(English.Keys).OrderBy(x => x, StringComparer.Ordinal))
            {
                string uk, en;
                if (!Ukrainian.TryGetValue(key, out uk) || String.IsNullOrWhiteSpace(uk)) errors.Add("Missing Ukrainian: " + key);
                if (!English.TryGetValue(key, out en) || String.IsNullOrWhiteSpace(en)) errors.Add("Missing English: " + key);
                if (uk != null && en != null)
                {
                    string[] ukSlots = FormatSlots(uk), enSlots = FormatSlots(en);
                    if (!ukSlots.SequenceEqual(enSlots)) errors.Add("Placeholder mismatch: " + key);
                }
            }
            return errors.ToArray();
        }

        private static string[] FormatSlots(string value)
        {
            return System.Text.RegularExpressions.Regex.Matches(value, @"(?<!\{)\{(\d+)(?:[^{}]*)\}(?!\})")
                .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).OrderBy(x => x).ToArray();
        }

        private static void AddCoreTranslations(Dictionary<string, string> uk, Dictionary<string, string> en)
        {
            Add(uk, en, "Format.Billion", "млрд", "B");
            Add(uk, en, "Format.Million", "млн", "M");
            Add(uk, en, "Format.Thousand", "тис.", "K");
            Add(uk, en, "Format.Hour", "г", "h");
            Add(uk, en, "Format.Minute", "хв", "m");
            Add(uk, en, "Format.Second", "с", "s");
            Add(uk, en, "Format.Day", "д", "d");
            Add(uk, en, "Format.Ago", "{0} тому", "{0} ago");
            Add(uk, en, "Format.UnknownModel", "модель невідома", "model unavailable");
            Add(uk, en, "Usage.NoTask", "Завдання ще не вибране", "No task selected yet");
            Add(uk, en, "Usage.NoPeers", "Ще немає завдань для порівняння", "No tasks to compare yet");
            Add(uk, en, "Usage.Comparison", "{0}% до середнього · {1} завд.", "{0}% vs average · {1} tasks");
            Add(uk, en, "Usage.Task", "ЗАВДАННЯ", "TASK");
            Add(uk, en, "Usage.Session", "СЕСІЯ", "SESSION");
            Add(uk, en, "Usage.Today", "СЬОГОДНІ", "TODAY");
            Add(uk, en, "Usage.TokenRate", "{0} токенів / хв", "{0} tokens / min");
            Add(uk, en, "Usage.CollectingRate", "Збираємо дані швидкості…", "Collecting rate data…");
            Add(uk, en, "Usage.TodayDetail", "{0} завдань · API ≈ {1}", "{0} tasks · API ≈ {1}");
            Add(uk, en, "Context.Session", "КОНТЕКСТ СЕСІЇ", "SESSION CONTEXT");
            Add(uk, en, "Context.Free", "{0}% ВІЛЬНО", "{0}% FREE");
            Add(uk, en, "Context.NoDataUpper", "НЕМАЄ ДАНИХ", "NO DATA");
            Add(uk, en, "Context.NoData", "Немає даних", "No data");
            Add(uk, en, "Context.Used", "{0}% зайнято", "{0}% used");
            Add(uk, en, "Context.Tokens", "{0} / {1} токенів", "{0} / {1} tokens");
            Add(uk, en, "Context.Waiting", "Очікуємо вимірювання", "Waiting for measurement");
            Add(uk, en, "Context.Now", "ЗАРАЗ", "NOW");
            Add(uk, en, "Context.EmptyChart", "Графік з’явиться після першої події", "The chart appears after the first event");
            Add(uk, en, "Tokens.TaskDetail", "  /  ЗАВД. {0}", "  /  TASK {0}");
            Add(uk, en, "Tokens.Total", "УСЬОГО", "TOTAL");
            Add(uk, en, "Tokens.Input", "ВХІД", "INPUT");
            Add(uk, en, "Tokens.Output", "ВИХІД", "OUTPUT");
            Add(uk, en, "Tokens.InputShort", "ВХІД", "IN");
            Add(uk, en, "Tokens.OutputShort", "ВИХІД", "OUT");
            Add(uk, en, "Tokens.Cache", "КЕШ", "CACHE");
            Add(uk, en, "Tokens.Hit", "ВЛУЧ.", "HIT");
            Add(uk, en, "Tokens.Api", "API ≈", "API ≈");
            Add(uk, en, "Appearance.Motion", "Плавний рух · Ctrl+M", "Smooth motion · Ctrl+M");
            Add(uk, en, "Appearance.ContextHelp", "Рухайте курсором, щоб змінити ракурс. Ctrl+M або права кнопка — увімкнути/вимкнути плавний рух.", "Move the pointer to change the view. Ctrl+M or right-click toggles smooth motion.");
            Add(uk, en, "Appearance.Mode", "Режим вигляду", "Display mode");
            Add(uk, en, "Appearance.Compact", "Компактний · Ctrl+1", "Compact · Ctrl+1");
            Add(uk, en, "Appearance.Analytical", "Аналітичний · Ctrl+2", "Analytical · Ctrl+2");
            Add(uk, en, "Appearance.Presentation", "Презентаційний · Ctrl+3", "Presentation · Ctrl+3");
            Add(uk, en, "Appearance.Palette", "Палітра", "Palette");
            Add(uk, en, "Appearance.Ocean", "Океан · синій та бірюза", "Ocean · blue and turquoise");
            Add(uk, en, "Appearance.Sun", "Сонце · золото та зелень", "Sun · gold and green");
            Add(uk, en, "Appearance.Fire", "Вогонь · помаранчевий та червоний", "Fire · orange and red");
            Add(uk, en, "Appearance.Intensity", "Інтенсивність світла", "Light intensity");
            Add(uk, en, "Appearance.NoGlow", "Без сяйва", "No glow");
            Add(uk, en, "Appearance.Soft", "М'яке", "Soft");
            Add(uk, en, "Appearance.Vivid", "Виразне", "Vivid");
            Add(uk, en, "Appearance.Maximum", "Максимальне", "Maximum");
            Add(uk, en, "Appearance.Economy", "Ощадний режим · автоматично від батареї", "Economy mode · automatic on battery");
            Add(uk, en, "Appearance.Language", "Мова", "Language");
            Add(uk, en, "Appearance.Ukrainian", "Українська", "Українська");
            Add(uk, en, "Appearance.English", "English", "English");
            Add(uk, en, "Engine.HomeMissing", "CODEX_HOME не знайдено", "CODEX_HOME not found");
            Add(uk, en, "Engine.ReadError", "Не вдалося оновити журнали сесій ({0}).", "Could not refresh session logs ({0}).");
            Add(uk, en, "Engine.Task", "Завдання Codex", "Codex task");
            Add(uk, en, "Engine.RepositoryTask", "Завдання · {0}", "{0} task");
            Add(uk, en, "Engine.UnknownRepository", "Невідомий репозиторій", "Unknown repository");
            Add(uk, en, "Program.StartError", "Не вдалося запустити монітор. Подробиці збережено в локальному журналі помилок.", "The monitor could not start. Details were saved to the local error log.");
        }
    }
}
