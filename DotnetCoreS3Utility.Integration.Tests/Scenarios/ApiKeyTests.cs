using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DotnetCoreS3Utility.Integration.Tests.Scenarios
{
    /// <summary>The hosted deployment sets Api:Key; writes without it never reach the object store.</summary>
    public class ApiKeyTests
    {
        private static HttpClient Client(string? key, bool requireKey = false) =>
            new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("Api:Key", key ?? "");
                builder.UseSetting("Api:RequireKey", requireKey.ToString());
            }).CreateClient();

        [Fact]
        public async Task Writes_without_the_key_are_rejected()
        {
            var client = Client("expected-key");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("api/bucket/create/any-bucket", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("api/bucket/delete/any-bucket")).StatusCode);

            var wrong = new HttpRequestMessage(HttpMethod.Post, "api/bucket/create/any-bucket");
            wrong.Headers.Add("X-Api-Key", "wrong-key");
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(wrong)).StatusCode);
        }

        [Fact]
        public async Task Writes_are_refused_when_a_key_is_required_but_not_configured()
        {
            var client = Client(null, requireKey: true);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("api/bucket/create/any-bucket", null)).StatusCode);
        }

        [Fact]
        public async Task Reads_do_not_need_the_key()
        {
            var client = Client("expected-key");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("health")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("api/catalog")).StatusCode);
        }
    }
}
