using System;

namespace DotnetCoreS3Utility.Core.Communication.Catalog
{
    /// <summary>Metadata about one stored object, kept in the catalog database alongside the object store.</summary>
    public class CatalogEntry
    {
        public string BucketName { get; set; } = "";
        public string Key { get; set; } = "";
        public long Size { get; set; }
        public string ContentType { get; set; } = "";
        /// <summary>"file" for uploads, "json" for objects written by addjsonobject.</summary>
        public string Kind { get; set; } = "file";
        public DateTime UploadedAt { get; set; }
    }

    /// <summary>Object count and bytes per bucket and content type.</summary>
    public class CatalogStats
    {
        public string BucketName { get; set; } = "";
        public string ContentType { get; set; } = "";
        public long Objects { get; set; }
        public long TotalBytes { get; set; }
        public DateTime LastUploadAt { get; set; }
    }
}
