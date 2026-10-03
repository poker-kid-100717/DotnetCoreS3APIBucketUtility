#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotnetCoreS3Utility.Core.Communication.Catalog;
using DotnetCoreS3Utility.Core.Interfaces;

namespace DotnetCoreS3Utility.Infrastructure.Catalog
{
    /// <summary>Used when no DATABASE_URL is configured; the catalog is lost on restart.</summary>
    public class InMemoryObjectCatalog : IObjectCatalog
    {
        private readonly ConcurrentDictionary<(string Bucket, string Key), CatalogEntry> _entries = new();

        public string Mode => "In-memory";

        public Task RecordAsync(CatalogEntry entry)
        {
            _entries[(entry.BucketName, entry.Key)] = entry;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string bucketName, string key)
        {
            _entries.TryRemove((bucketName, key), out _);
            return Task.CompletedTask;
        }

        public Task RemoveBucketAsync(string bucketName)
        {
            foreach (var key in _entries.Keys.Where(k => k.Bucket == bucketName).ToList()) _entries.TryRemove(key, out _);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CatalogEntry>> SearchAsync(string? bucketName, string? text, int limit)
        {
            IReadOnlyList<CatalogEntry> result = _entries.Values
                .Where(e => bucketName == null || e.BucketName == bucketName)
                .Where(e => text == null || e.Key.Contains(text, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(e => e.UploadedAt).ThenBy(e => e.Key, StringComparer.Ordinal)
                .Take(limit).ToList();
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<CatalogStats>> StatsAsync()
        {
            IReadOnlyList<CatalogStats> result = _entries.Values
                .GroupBy(e => (e.BucketName, e.ContentType))
                .Select(g => new CatalogStats
                {
                    BucketName = g.Key.BucketName, ContentType = g.Key.ContentType, Objects = g.Count(),
                    TotalBytes = g.Sum(e => e.Size), LastUploadAt = g.Max(e => e.UploadedAt)
                })
                .OrderBy(s => s.BucketName, StringComparer.Ordinal).ThenByDescending(s => s.TotalBytes)
                .ToList();
            return Task.FromResult(result);
        }
    }
}
