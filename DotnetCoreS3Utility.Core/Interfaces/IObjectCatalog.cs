#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using DotnetCoreS3Utility.Core.Communication.Catalog;

namespace DotnetCoreS3Utility.Core.Interfaces
{
    /// <summary>
    /// Searchable metadata for stored objects. Object storage answers "give me this key"; the catalog answers
    /// "what is stored, where, and how much" without listing every bucket.
    /// </summary>
    public interface IObjectCatalog
    {
        /// <summary>"MongoDB" or "In-memory".</summary>
        string Mode { get; }
        Task RecordAsync(CatalogEntry entry);
        Task RemoveAsync(string bucketName, string key);
        Task RemoveBucketAsync(string bucketName);
        Task<IReadOnlyList<CatalogEntry>> SearchAsync(string? bucketName, string? text, int limit);
        Task<IReadOnlyList<CatalogStats>> StatsAsync();
    }
}
