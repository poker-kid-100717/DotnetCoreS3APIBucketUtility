using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DotnetCoreS3Utility.Core.Communication.Catalog;
using DotnetCoreS3Utility.Integration.Tests.Setup;
using Newtonsoft.Json;
using Xunit;

namespace DotnetCoreS3Utility.Integration.Tests.Scenarios
{
    /// <summary>Uploads and deletes keep the MongoDB catalog in step with the object store.</summary>
    [Collection("api")]
    public class CatalogControllerTests
    {
        private const string Bucket = TestContext.BucketName;

        private readonly HttpClient _httpClient;

        public CatalogControllerTests(TestContext context)
        {
            _httpClient = context.Client;
        }

        private async Task Upload(string fileName, string contentType, string body)
        {
            using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            using var formData = new MultipartFormDataContent { { fileContent, "formFiles", fileName } };
            var response = await _httpClient.PostAsync($"api/files/{Bucket}/add", formData);
            response.EnsureSuccessStatusCode();
        }

        private async Task<T> Get<T>(string url)
        {
            var response = await _httpClient.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync())!;
        }

        [Fact]
        public async Task Uploads_are_searchable_in_the_catalog_and_deletes_remove_them()
        {
            var name = $"catalog-{Guid.NewGuid():N}.csv";
            await Upload(name, "text/csv", "a,b\n1,2\n");

            var found = await Get<CatalogEntry[]>($"api/catalog?bucket={Bucket}&q={name[..16]}");
            var entry = Assert.Single(found, e => e.Key == name);
            Assert.Equal("text/csv", entry.ContentType);
            Assert.Equal(8, entry.Size);
            Assert.Equal("file", entry.Kind);

            var deleted = await _httpClient.DeleteAsync($"api/files/{Bucket}/delete/{name}");
            Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
            Assert.DoesNotContain(await Get<CatalogEntry[]>($"api/catalog?bucket={Bucket}&q={name}"), e => e.Key == name);
        }

        [Fact]
        public async Task Stats_aggregate_objects_and_bytes_per_bucket_and_content_type()
        {
            var tag = Guid.NewGuid().ToString("N");
            await Upload($"stats-{tag}-1.txt", "text/plain", "12345");
            await Upload($"stats-{tag}-2.txt", "text/plain", "1234567890");

            var stats = await Get<CatalogStats[]>("api/catalog/stats");
            var text = Assert.Single(stats, s => s.BucketName == Bucket && s.ContentType == "text/plain");
            Assert.True(text.Objects >= 2);
            Assert.True(text.TotalBytes >= 15);
        }

        [Fact]
        public async Task Health_reports_the_catalog_store()
        {
            var health = await _httpClient.GetStringAsync("health");
            Assert.Contains("MongoDB", health);
        }
    }
}
