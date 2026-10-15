using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using backend.Data;
using backend.Features.Activity.DTOs;

namespace backend.Features.Activity
{
    // The community feed's first pages as logged-out visitors see them. A page stays valid until the app uses the
    // database again (DatabaseActivity.Version), since nothing can have changed without that. Logged-out visitors
    // and crawlers open the start page at any time, and reading the feed for them woke the paused database, which
    // uses part of its free monthly amount. Before the database goes idle, AnonymousCommunityRefresh reads the
    // pages again, so they stay valid while it sleeps.
    public sealed class AnonymousCommunityPages
    {
        // The start page and the activity page each use one combination of limit and types. The cap keeps
        // made-up combinations from filling memory or the refresh.
        private const int MaxPages = 8;

        private readonly DatabaseActivity _database;
        private readonly ConcurrentDictionary<string, Entry> _pages = new();

        public AnonymousCommunityPages(DatabaseActivity database)
        {
            _database = database;
        }

        public bool TryGet(int limit, IReadOnlySet<string>? types, [NotNullWhen(true)] out ActivityPageDto? page)
        {
            if (_pages.TryGetValue(Key(limit, types), out var entry) && entry.Version == _database.Version)
            {
                page = entry.Page;
                return true;
            }

            page = null;
            return false;
        }

        // Call right after reading the page from the database. A change saved by another request in the meantime
        // is picked up by the refresh before the database goes idle.
        public void Set(int limit, IReadOnlySet<string>? types, ActivityPageDto page)
        {
            var key = Key(limit, types);
            if (_pages.Count >= MaxPages && !_pages.ContainsKey(key))
            {
                return;
            }

            _pages[key] = new Entry(limit, types, page, _database.Version);
        }

        public async Task RefreshAsync(IActivityFeed feed)
        {
            foreach (var (key, entry) in _pages)
            {
                var page = await feed.GetCommunityAsync(null, null, entry.Limit, entry.Types);
                _pages[key] = entry with { Page = page, Version = _database.Version };
            }
        }

        public static string Key(int limit, IReadOnlySet<string>? types) =>
            $"{limit}:{(types == null ? "all" : string.Join(',', types.Order()))}";

        private sealed record Entry(int Limit, IReadOnlySet<string>? Types, ActivityPageDto Page, long Version);
    }
}
