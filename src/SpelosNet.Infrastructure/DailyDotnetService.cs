using SpelosNet.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SpelosNet.Infrastructure
{
    public class DailyDotnetService : IDailyDotnetService
    {
        private readonly Random _rng;
        private readonly IEnumerable<string> _msdnPages;
        private readonly InMemoryCache<string> _dailyCache;

        public DailyDotnetService(Random rng, JsonStorage storage)
        {
            _rng = rng;
            _msdnPages = storage.Get<IEnumerable<string>>("docs-pages.json");
            _dailyCache = new InMemoryCache<string>(() => GetRandomElement(_msdnPages.Where(p => !p.Contains("/misc/cs") && !p.Contains("compiler-messages"))), TimeSpan.FromDays(1));
        }
        private string GetRandomElement(IEnumerable<string> collection) => collection.ElementAt(_rng.Next(0, collection.Count()));

        public string GetRandomUrl() => GetRandomElement(_msdnPages);

        public string GetTodaysUrl() => _dailyCache.GetValue();
    }
}
