#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotnetCoreS3Utility.Core.Communication.Catalog;
using DotnetCoreS3Utility.Core.Interfaces;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace DotnetCoreS3Utility.Infrastructure.Catalog
{
    /// <summary>
    /// The object catalog in MongoDB (DATABASE_URL, for example a MongoDB Atlas mongodb+srv:// URL). One document per
    /// object, keyed by bucket and key so re-uploads replace it; stats come from an aggregation pipeline.
    /// </summary>
    public class MongoObjectCatalog : IObjectCatalog
    {
        public const string DefaultDatabase = "s3utility";

        private readonly IMongoCollection<CatalogDocument> _objects;

        public MongoObjectCatalog(string connectionString)
        {
            var url = MongoUrl.Create(connectionString);
            var client = new MongoClient(url);
            var database = client.GetDatabase(string.IsNullOrEmpty(url.DatabaseName) ? DefaultDatabase : url.DatabaseName);
            _objects = database.GetCollection<CatalogDocument>("objects");
            _objects.Indexes.CreateMany(new[]
            {
                new CreateIndexModel<CatalogDocument>(Builders<CatalogDocument>.IndexKeys.Ascending(d => d.BucketName).Descending(d => d.UploadedAt)),
                new CreateIndexModel<CatalogDocument>(Builders<CatalogDocument>.IndexKeys.Ascending(d => d.ContentType)),
            });
        }

        public string Mode => "MongoDB";

        public Task RecordAsync(CatalogEntry entry) =>
            _objects.ReplaceOneAsync(d => d.Id == DocumentId(entry.BucketName, entry.Key), new CatalogDocument
            {
                Id = DocumentId(entry.BucketName, entry.Key), BucketName = entry.BucketName, Key = entry.Key, Size = entry.Size,
                ContentType = entry.ContentType, Kind = entry.Kind, UploadedAt = entry.UploadedAt
            }, new ReplaceOptions { IsUpsert = true });

        public Task RemoveAsync(string bucketName, string key) => _objects.DeleteOneAsync(d => d.Id == DocumentId(bucketName, key));

        public Task RemoveBucketAsync(string bucketName) => _objects.DeleteManyAsync(d => d.BucketName == bucketName);

        public async Task<IReadOnlyList<CatalogEntry>> SearchAsync(string? bucketName, string? text, int limit)
        {
            var filter = Builders<CatalogDocument>.Filter.Empty;
            if (bucketName != null) filter &= Builders<CatalogDocument>.Filter.Eq(d => d.BucketName, bucketName);
            if (text != null) filter &= Builders<CatalogDocument>.Filter.Regex(d => d.Key, new BsonRegularExpression(Regex.Escape(text), "i"));
            var documents = await _objects.Find(filter).SortByDescending(d => d.UploadedAt).ThenBy(d => d.Key).Limit(limit).ToListAsync();
            return documents.Select(ToEntry).ToList();
        }

        public async Task<IReadOnlyList<CatalogStats>> StatsAsync()
        {
            var rows = await _objects.Aggregate()
                .Group(d => new { d.BucketName, d.ContentType }, g => new CatalogStats
                {
                    BucketName = g.Key.BucketName, ContentType = g.Key.ContentType, Objects = g.Count(),
                    TotalBytes = g.Sum(d => d.Size), LastUploadAt = g.Max(d => d.UploadedAt)
                })
                .SortBy(s => s.BucketName).ThenByDescending(s => s.TotalBytes)
                .ToListAsync();
            return rows;
        }

        private static string DocumentId(string bucketName, string key) => $"{bucketName}/{key}";

        private static CatalogEntry ToEntry(CatalogDocument d) => new()
        {
            BucketName = d.BucketName, Key = d.Key, Size = d.Size, ContentType = d.ContentType, Kind = d.Kind,
            UploadedAt = DateTime.SpecifyKind(d.UploadedAt, DateTimeKind.Utc)
        };

        public class CatalogDocument
        {
            [BsonId] public string Id { get; set; } = "";
            public string BucketName { get; set; } = "";
            public string Key { get; set; } = "";
            public long Size { get; set; }
            public string ContentType { get; set; } = "";
            public string Kind { get; set; } = "file";
            [BsonDateTimeOptions(Kind = DateTimeKind.Utc)] public DateTime UploadedAt { get; set; }
        }
    }
}
