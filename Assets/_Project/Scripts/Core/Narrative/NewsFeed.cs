using System;
using System.Collections.Generic;
using System.Linq;

namespace AIGame.Core.Narrative
{
    /// <summary>Şehrin haber akışında bir satır: insanların olanı nasıl yorumladığı.</summary>
    public sealed class NewsItem
    {
        public NewsItem(double timeMinutes, string source, string headline)
        {
            TimeMinutes = timeMinutes;
            Source = source;
            Headline = headline;
        }

        /// <summary>Haberin yayımlanacağı şehir saati (dakika).</summary>
        public double TimeMinutes { get; }
        public string Source { get; }
        public string Headline { get; }
    }

    /// <summary>Haberleri zamanı gelince yayımlar. Operasyon sonuçları ileri tarihli haber bırakır.</summary>
    public sealed class NewsFeed
    {
        private readonly List<NewsItem> _pending = new List<NewsItem>();
        private readonly List<NewsItem> _published = new List<NewsItem>();

        public IReadOnlyList<NewsItem> Published => _published;
        public int PendingCount => _pending.Count;

        public void Schedule(NewsItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            _pending.Add(item);
        }

        /// <summary>Saati gelen haberleri yayımlar ve yeni yayımlananları döndürür.</summary>
        public IReadOnlyList<NewsItem> PublishDue(double nowMinutes)
        {
            var due = _pending.Where(n => n.TimeMinutes <= nowMinutes).OrderBy(n => n.TimeMinutes).ToList();
            if (due.Count == 0) return Array.Empty<NewsItem>();
            foreach (var item in due) _pending.Remove(item);
            _published.AddRange(due);
            return due;
        }
    }
}
