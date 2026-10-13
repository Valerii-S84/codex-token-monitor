using System;
using System.Collections.Generic;
using System.Linq;

namespace CodexTokenMonitor
{
    internal sealed class CostEstimate
    {
        public decimal Value;
        public long KnownTokens;
        public long UnknownTokens;

        public bool HasKnownUsage { get { return KnownTokens > 0; } }
        public bool HasUnknownUsage { get { return UnknownTokens > 0; } }
        public bool IsPartial { get { return HasUnknownUsage; } }
    }

    internal sealed class ModelRate
    {
        public decimal Input;
        public decimal? CachedInput;
        public decimal CacheWrite;
        public decimal Output;

        public ModelRate(decimal input, decimal? cachedInput, decimal output)
            : this(input, cachedInput, output, 0m) { }

        public ModelRate(decimal input, decimal? cachedInput, decimal output, decimal cacheWrite)
        {
            CacheWrite = cacheWrite;
            Input = input;
            CachedInput = cachedInput;
            Output = output;
        }
    }

    internal static class UsageBuckets
    {
        public const string UnknownModel = "<unknown>";

        public static void Add(IDictionary<string, TokenUsage> target, string model, TokenUsage usage)
        {
            if (target == null || usage == null) return;
            string key = String.IsNullOrWhiteSpace(model) ? UnknownModel : model.Trim();
            TokenUsage bucket;
            if (!target.TryGetValue(key, out bucket))
            {
                bucket = new TokenUsage();
                target[key] = bucket;
            }
            bucket.Add(usage);
        }

        public static void AddMap(IDictionary<string, TokenUsage> target, IDictionary<string, TokenUsage> source)
        {
            if (source == null) return;
            foreach (KeyValuePair<string, TokenUsage> item in source) Add(target, item.Key, item.Value);
        }
    }

    internal static class PriceCatalog
    {
        public const string RateDate = "2026-10-11";
        // USD per 1M tokens: Standard, short context; verified against official OpenAI pricing.
        // https://developers.openai.com/api/docs/pricing
        private static readonly Dictionary<string, ModelRate> Rates = new Dictionary<string, ModelRate>(StringComparer.OrdinalIgnoreCase)
        {
            { "gpt-6-astra", new ModelRate(10.00m, 1.00m, 50.00m, 12.50m) },
            { "gpt-6.1-sol", new ModelRate(2.00m, 0.10m, 10.00m, 2.50m) },
            { "gpt-6-luna", new ModelRate(0.10m, 0.01m, 0.50m, 0.125m) },
            { "gpt-6-sol", new ModelRate(2.00m, 0.20m, 10.00m, 2.50m) },
            { "gpt-5.6-sol", new ModelRate(4.00m, 0.40m, 20.00m, 5.00m) },
            { "gpt-5.6-terra", new ModelRate(2.00m, 0.20m, 12.00m, 2.50m) },
            { "gpt-5.6-luna", new ModelRate(0.20m, 0.02m, 1.20m, 0.25m) },
            { "gpt-5.5", new ModelRate(5.00m, 0.50m, 30.00m, 0m) },
            { "gpt-5.5-pro", new ModelRate(30.00m, null, 180.00m, 0m) },
            { "gpt-5.4", new ModelRate(2.50m, 0.25m, 15.00m, 0m) },
            { "gpt-5.4-mini", new ModelRate(0.75m, 0.075m, 4.50m, 0m) },
            { "gpt-5.4-nano", new ModelRate(0.20m, 0.02m, 1.25m, 0m) },
            { "gpt-5.4-pro", new ModelRate(30.00m, null, 180.00m, 0m) },
            { "gpt-5.2", new ModelRate(1.75m, 0.175m, 14.00m, 0m) },
            { "gpt-5.2-pro", new ModelRate(21.00m, null, 168.00m, 0m) },
            { "gpt-5.1", new ModelRate(1.25m, 0.125m, 10.00m, 0m) },
            { "gpt-5", new ModelRate(1.25m, 0.125m, 10.00m, 0m) },
            { "gpt-5-mini", new ModelRate(0.25m, 0.025m, 2.00m, 0m) },
            { "gpt-5-nano", new ModelRate(0.05m, 0.005m, 0.40m, 0m) },
            { "gpt-5-pro", new ModelRate(15.00m, null, 120.00m, 0m) },
            { "gpt-4.1", new ModelRate(2.00m, 0.50m, 8.00m, 0m) },
            { "gpt-4.1-mini", new ModelRate(0.40m, 0.10m, 1.60m, 0m) },
            { "gpt-4.1-nano", new ModelRate(0.10m, 0.025m, 0.40m, 0m) },
            { "gpt-4o", new ModelRate(2.50m, 1.25m, 10.00m, 0m) },
            { "gpt-4o-2024-05-13", new ModelRate(5.00m, null, 15.00m, 0m) },
            { "gpt-4o-mini", new ModelRate(0.15m, 0.075m, 0.60m, 0m) },
            { "o1", new ModelRate(15.00m, 7.50m, 60.00m, 0m) },
            { "o1-pro", new ModelRate(150.00m, null, 600.00m, 0m) },
            { "o3-pro", new ModelRate(20.00m, null, 80.00m, 0m) },
            { "o3", new ModelRate(2.00m, 0.50m, 8.00m, 0m) },
            { "o4-mini", new ModelRate(1.10m, 0.275m, 4.40m, 0m) },
            { "o3-mini", new ModelRate(1.10m, 0.55m, 4.40m, 0m) },
            { "gpt-4-turbo-2024-04-09", new ModelRate(10.00m, null, 30.00m, 0m) },
            { "gpt-4-0613", new ModelRate(30.00m, null, 60.00m, 0m) },
            { "gpt-3.5-turbo", new ModelRate(0.50m, null, 1.50m, 0m) },
            { "gpt-3.5-turbo-0125", new ModelRate(0.50m, null, 1.50m, 0m) },
            { "gpt-3.5-turbo-1106", new ModelRate(1.00m, null, 2.00m, 0m) },
            { "gpt-3.5-turbo-instruct", new ModelRate(1.50m, null, 2.00m, 0m) },
            { "davinci-002", new ModelRate(2.00m, null, 2.00m, 0m) },
            { "babbage-002", new ModelRate(0.40m, null, 0.40m, 0m) },
            { "gpt-5.3-codex", new ModelRate(1.75m, 0.175m, 14.00m, 0m) },
            { "gpt-5.2-codex", new ModelRate(1.75m, 0.175m, 14.00m, 0m) },
            { "gpt-5.1-codex-max", new ModelRate(1.25m, 0.125m, 10.00m, 0m) },
            { "gpt-5.1-codex-mini", new ModelRate(0.25m, 0.025m, 2.00m, 0m) },
            { "gpt-5.1-codex", new ModelRate(1.25m, 0.125m, 10.00m, 0m) },
            { "gpt-5-codex", new ModelRate(1.25m, 0.125m, 10.00m, 0m) },
            { "codex-mini-latest", new ModelRate(1.50m, 0.375m, 6.00m, 0m) },
            { "gpt-5.6", new ModelRate(4.00m, 0.40m, 20.00m, 5.00m) },
            { "gpt-5.6-cyber", new ModelRate(12.50m, 1.25m, 75.00m, 15.625m) },
            { "gpt-5.5-cyber", new ModelRate(12.50m, 1.25m, 75.00m, 0m) },
            { "gpt-rosalind-research", new ModelRate(5.00m, 0.50m, 25.00m, 0m) },
            { "gpt-rosalind-discovery", new ModelRate(5.00m, 0.50m, 25.00m, 0m) }
        };

        public static CostEstimate Calculate(IDictionary<string, TokenUsage> usageByModel)
        {
            CostEstimate result = new CostEstimate();
            if (usageByModel == null) return result;
            foreach (KeyValuePair<string, TokenUsage> item in usageByModel)
            {
                TokenUsage usage = item.Value ?? new TokenUsage();
                long tokens = Math.Max(0, usage.Total > 0 ? usage.Total : usage.Input + usage.Output);
                ModelRate rate;
                if (!Rates.TryGetValue(item.Key ?? "", out rate))
                {
                    result.UnknownTokens += tokens;
                    continue;
                }
                long cached = Math.Min(Math.Max(0, usage.Cached), Math.Max(0, usage.Input));
                long cacheWrite = Math.Min(Math.Max(0, usage.CacheWrite), Math.Max(0, usage.Input - cached));
                long uncached = Math.Max(0, usage.Input - cached - cacheWrite);
                long unpriced = 0;
                if (cacheWrite > 0 && rate.CacheWrite <= 0m) unpriced += cacheWrite;
                else result.Value += cacheWrite * rate.CacheWrite / 1000000m;
                result.Value += uncached * rate.Input / 1000000m;
                if (cached > 0 && !rate.CachedInput.HasValue) unpriced += cached;
                else result.Value += cached * rate.CachedInput.GetValueOrDefault() / 1000000m;
                result.Value += Math.Max(0, usage.Output) * rate.Output / 1000000m;
                result.UnknownTokens += unpriced;
                result.KnownTokens += Math.Max(0, tokens - unpriced);
            }
            return result;
        }

        public static string ModelList(IDictionary<string, TokenUsage> usageByModel)
        {
            if (usageByModel == null || usageByModel.Count == 0) return UsageBuckets.UnknownModel;
            return String.Join(", ", usageByModel
                .Where(x => x.Value != null && (x.Value.Total > 0 || x.Value.Input > 0 || x.Value.Output > 0))
                .OrderByDescending(x => x.Value.Total)
                .Select(x => x.Key));
        }
    }
}
