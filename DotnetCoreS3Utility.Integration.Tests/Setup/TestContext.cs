using System.Net.Http;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.LocalStack;
using Xunit;

namespace DotnetCoreS3Utility.Integration.Tests.Setup
{
    /// <summary>
    /// Starts LocalStack (S3 emulator) in Docker once per test run, points the
    /// API's IAmazonS3 at it, and creates the bucket the scenarios use.
    /// </summary>
    public sealed class TestContext : IAsyncLifetime
    {
        // Pinned: from March 2026 `localstack/localstack:latest` requires a
        // LOCALSTACK_AUTH_TOKEN. 4.14.0 is the last release that runs without one.
        private const string LocalStackImage = "localstack/localstack:4.14.0";

        // S3 bucket names must be lowercase.
        public const string BucketName = "integration-test-bucket";

        private readonly LocalStackContainer _localStack = new LocalStackBuilder(LocalStackImage).Build();
        private WebApplicationFactory<Program>? _factory;

        public HttpClient Client { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            await _localStack.StartAsync();

            var s3Client = new AmazonS3Client(
                new BasicAWSCredentials("test", "test"),
                new AmazonS3Config
                {
                    ServiceURL = _localStack.GetConnectionString(),
                    ForcePathStyle = true,
                    AuthenticationRegion = "us-east-1"
                });

            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IAmazonS3>();
                    services.AddSingleton<IAmazonS3>(s3Client);
                }));

            Client = _factory.CreateClient();

            var response = await Client.PostAsync($"api/bucket/create/{BucketName}", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task DisposeAsync()
        {
            Client?.Dispose();
            if (_factory != null)
            {
                await _factory.DisposeAsync();
            }
            await _localStack.DisposeAsync();
        }
    }
}
