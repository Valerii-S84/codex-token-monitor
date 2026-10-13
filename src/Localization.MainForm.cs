using System.Collections.Generic;

namespace CodexTokenMonitor
{
    internal static partial class L10n
    {
        static partial void AddMainFormTranslations(Dictionary<string, string> uk, Dictionary<string, string> en)
        {
            Add(uk, en, "UI.ContextShort", "КОНТ.", "CTX");
            Add(uk, en, "UI.ContextNarrow", "КТ.", "CTX");
            Add(uk, en, "UI.ActiveCount", "● {0} АКТИВНІ", "● {0} ACTIVE");
            Add(uk, en, "UI.TodayTotal", "СЬОГОДНІ {0}", "TODAY {0}");
            Add(uk, en, "UI.LimitUnavailable", "Ліміти ще не передано • лише виміряні дані", "Limits not received yet • measured data only");
            Add(uk, en, "UI.LimitFiveHours", "5 ГОД", "5 HRS");
            Add(uk, en, "UI.LimitWeek", "ТИЖДЕНЬ", "WEEK");
            Add(uk, en, "UI.LimitMinutes", "{0} ХВ", "{0} MIN");
            Add(uk, en, "UI.ResetHours", " · скидання через {0}г {1}хв", " · resets in {0}h {1}min");
            Add(uk, en, "UI.ResetMinutes", " · скидання через {0}хв", " · resets in {0}min");
            Add(uk, en, "UI.ResetDate", " · до {0}", " · until {0}");
            Add(uk, en, "UI.LimitRemaining", "{0} {1}% ЛИШИЛОСЬ{2}", "{0} {1}% LEFT{2}");
            Add(uk, en, "UI.LimitFiveHoursShort", "5Г", "5H");
            Add(uk, en, "UI.LimitMinutesShort", "{0}ХВ", "{0}MIN");
            Add(uk, en, "UI.AgentNumber", "АГЕНТ {0}", "AGENT {0}");
            Add(uk, en, "UI.LastActivityShort", "ОСТ. {0}", "LAST {0}");
            Add(uk, en, "UI.AppearanceTip", "Налаштування вигляду. Ctrl+1 / Ctrl+2 / Ctrl+3 — режими; Ctrl+M — рух.", "Appearance settings. Ctrl+1 / Ctrl+2 / Ctrl+3 — modes; Ctrl+M — motion.");
            Add(uk, en, "UI.Searching", "Пошук сесій…", "Searching for sessions…");
            Add(uk, en, "UI.PinOn", "ЗВЕРХ", "ON TOP");
            Add(uk, en, "UI.PinOff", "ЗАКР.", "PIN");
            Add(uk, en, "UI.LiveTab", "ЗАРАЗ", "LIVE");
            Add(uk, en, "UI.AnalyticsTab", "АНАЛІТИКА", "ANALYTICS");
            Add(uk, en, "UI.LocalOnly", "Лише локально • без API-ключа", "Local only • no API key");
            Add(uk, en, "UI.Refreshing", "Оновлення…", "Refreshing…");
            Add(uk, en, "UI.RefreshTip", "Оновити зараз", "Refresh now");
            Add(uk, en, "UI.PinTip", "Завжди поверх інших вікон", "Always on top of other windows");
            Add(uk, en, "UI.MinimizeTip", "Згорнути", "Minimize");
            Add(uk, en, "UI.CloseTip", "Закрити", "Close");
            Add(uk, en, "UI.Waiting", "ОЧІКУВАННЯ", "WAITING");
            Add(uk, en, "UI.NoSessions", "Сесій Codex ще немає", "No Codex sessions yet");
            Add(uk, en, "UI.TaskTokens", "ТОКЕНИ ЗАВДАННЯ", "TASK TOKENS");
            Add(uk, en, "UI.TrendTip", "Δ5ХВ з’являється після накопичення п’яти хвилин локальних вимірювань.", "Δ5MIN appears after five minutes of local measurements.");
            Add(uk, en, "UI.ContextTip", "Історія заповнення контекстного вікна вибраного агента.", "Context window usage history for the selected agent.");
            Add(uk, en, "UI.DaysHistory", "ІСТОРІЯ ЗА ДНЯМИ", "DAILY HISTORY");
            Add(uk, en, "UI.Older", "ДАВНІШІ", "OLDER");
            Add(uk, en, "UI.DayColumn", "ДЕНЬ", "DAY");
            Add(uk, en, "UI.TasksColumn", "ЗАВД.", "TASKS");
            Add(uk, en, "UI.TokensColumn", "ТОКЕНИ", "TOKENS");
            Add(uk, en, "UI.ApiColumn", "API-ОЦІНКА", "API VALUE");
            Add(uk, en, "UI.ChooseDay", "Виберіть день", "Select a day");
            Add(uk, en, "UI.HomeMissing", "CODEX_HOME не знайдено", "CODEX_HOME not found");
            Add(uk, en, "UI.LoadingShort", "ЗАВАНТАЖЕННЯ", "LOADING");
            Add(uk, en, "UI.IndexingOlder", "Локальне індексування давнішої історії…", "Indexing older history locally…");
            Add(uk, en, "UI.Loading", "ЗАВАНТАЖЕННЯ", "LOADING");
            Add(uk, en, "UI.AllLoaded", "УСЕ ГОТОВО", "ALL LOADED");
            Add(uk, en, "UI.OlderTip", "Завантажити наступні 120 давніших локальних журналів сесій.", "Load the next 120 older local session logs.");
            Add(uk, en, "UI.AllLoadedTip", "Усі доступні локальні журнали сесій проіндексовано.", "All available local session logs have been indexed.");
            Add(uk, en, "UI.AwaitingLogs", "Очікуємо локальні журнали Codex", "Waiting for local Codex logs");
            Add(uk, en, "UI.CurrentRun", "ПОТОЧНИЙ ЗАПУСК", "CURRENT RUN");
            Add(uk, en, "UI.LastRun", "ОСТАННІЙ ЗАПУСК", "LAST RUN");
            Add(uk, en, "UI.AwaitingData", "Очікуємо локальні дані сесії.", "Waiting for local session data.");
            Add(uk, en, "UI.Trend", "Δ5ХВ ", "Δ5MIN ");
            Add(uk, en, "UI.Today", "СЬОГОДНІ", "TODAY");
            Add(uk, en, "UI.NoDays", "Немає проіндексованих днів", "No indexed days");
            Add(uk, en, "UI.TodaySubscription", "Сьогодні • підписка", "Today • subscription");
            Add(uk, en, "UI.SubscriptionSuffix", " • підписка", " • subscription");
            Add(uk, en, "UI.PartialNote", ". * Частково: тариф принаймні однієї моделі невідомий.", ". * Partial: the rate for at least one model is unknown.");
            Add(uk, en, "UI.SubscriptionTip", "Режим підписки: API-оцінка є орієнтиром, а не списанням. Оцінка Standard для короткого контексту; режим обробки не враховується. Тарифи станом на {0}. * = часткова оцінка через невідомий тариф моделі.", "Subscription mode: API value is a reference estimate, not a charge. Standard rates for short context; processing mode is excluded. Rates as of {0}. * = partial estimate because a model rate is unknown.");
            Add(uk, en, "UI.TodayTip", "Завдань сьогодні: {0}. API ≈ базова оцінка, не списання. Тарифи: {1}; без надбавок за довгий контекст, швидкість і інструменти.", "Tasks today: {0}. API ≈ base estimate, not a charge. Rates: {1}; excludes long-context, speed and tool surcharges.");
            Add(uk, en, "UI.ActiveAgents", "АКТИВНІ АГЕНТИ: {0}", "ACTIVE AGENTS: {0}");
            Add(uk, en, "UI.IndexingProgress", " • індексування {0}/{1}", " • indexing {0}/{1}");
            Add(uk, en, "UI.HistoryProgress", " • історія {0}/{1}", " • history {0}/{1}");
            Add(uk, en, "UI.Footer", "⚙ Вигляд • {0}{1}", "⚙ Appearance • {0}{1}");
            Add(uk, en, "UI.IndexingTip", "Індексування: {0} із {1} запитаних журналів (усього {2}).", "Indexing: {0} of {1} requested logs ({2} available).");
            Add(uk, en, "UI.AgentsActive", "АКТИВНИХ АГЕНТІВ: {0}", "ACTIVE AGENTS: {0}");
            Add(uk, en, "UI.RunningBadge", "● ПРАЦЮЄ  •  {0}  •  ост. активність: {1}", "● RUNNING  •  {0}  •  last activity: {1}");
            Add(uk, en, "UI.StaleBadge", "⚠ НЕАКТУАЛЬНО  •  {0}", "⚠ STALE  •  {0}");
            Add(uk, en, "UI.WaitingBadge", "ОЧІКУВАННЯ  •  {0}", "WAITING  •  {0}");
            Add(uk, en, "UI.TaskUsageTip", "Завдання: {0} токенів • Вхід: {1} • Вихід: {2} • Кеш: {3} • Міркування: {4}. Кеш входить у вхідні токени; міркування — у вихідні.", "Task: {0} tokens • Input: {1} • Output: {2} • Cache: {3} • Reasoning: {4}. Cache is included in input tokens; reasoning in output tokens.");
            Add(uk, en, "UI.ApiTip", " API ≈ базова оцінка, не списання. Тарифи: {0}; без надбавок за довгий контекст, швидкість і виклики інструментів.", " API ≈ base estimate, not a charge. Rates: {0}; excludes long-context, speed and tool-call surcharges.");
            Add(uk, en, "UI.ComparisonTip", " Порівняння: до 20 завершених завдань цього проєкту. Швидкість — середня за останній доступний інтервал, ціль — 1 хвилина. Дані дня охоплюють проіндексовані журнали.", " Comparison: up to 20 completed tasks in this project. Speed is an average over the latest available interval, targeting one minute. Daily data covers indexed logs.");
            Add(uk, en, "UI.TrendPositive", "Δ5ХВ +{0}", "Δ5MIN +{0}");
            Add(uk, en, "UI.DayUsage", "Завдань: {0}  •  Вхід {1}  •  Вихід {2}  •  Кеш {3}", "Tasks: {0}  •  Input {1}  •  Output {2}  •  Cache {3}");
            Add(uk, en, "UI.DayTip", "Лише API-еквівалент; підписка не тарифікується за токенами. Моделі: {0}", "API equivalent only; subscription is not billed by tokens. Models: {0}");
        }
    }
}
