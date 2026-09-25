using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using DotnetCoreS3Utility.Core.Communication.Files;
using DotnetCoreS3Utility.Integration.Tests.Setup;
using Newtonsoft.Json;
using Xunit;

namespace DotnetCoreS3Utility.Integration.Tests.Scenarios
{
    [Collection("api")]
    public class FilesControllerTests
    {
        private const string Bucket = TestContext.BucketName;
        private const string FileName = "IntegrationTest.jpg";

        private readonly HttpClient _httpClient;

        public FilesControllerTests(TestContext context)
        {
            _httpClient = context.Client;
        }

        private async Task<HttpResponseMessage> UploadFileToS3Bucket()
        {
            using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("integration test file"));
            using var formData = new MultipartFormDataContent
            {
                { fileContent, "formFiles", FileName }
            };

            return await _httpClient.PostAsync($"api/files/{Bucket}/add", formData);
        }

        [Fact]
        public async Task When_AddFiles_endPoint_is_hit_we_are_returned_ok_status()
        {
            var response = await UploadFileToS3Bucket();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task When_ListFiles_endpoint_is_hit_the_uploaded_file_is_listed()
        {
            await UploadFileToS3Bucket();

            var response = await _httpClient.GetAsync($"api/files/{Bucket}/list");
            var result = JsonConvert.DeserializeObject<ListFilesResponse[]>(await response.Content.ReadAsStringAsync());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(result);
            Assert.Contains(result, file => file.Key == FileName);
        }

        [Fact]
        public async Task When_DownloadFiles_endpoint_is_hit_we_are_returned_ok_status()
        {
            await UploadFileToS3Bucket();

            var response = await _httpClient.GetAsync($"api/files/{Bucket}/download/{FileName}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task When_DeleteFile_endpoint_is_hit_we_are_returned_ok_status()
        {
            await UploadFileToS3Bucket();

            var response = await _httpClient.DeleteAsync($"api/files/{Bucket}/delete/{FileName}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task When_AddJsonObject_endpoint_is_hit_we_are_returned_ok_status()
        {
            var jsonObjectRequest = new AddJsonObjectRequest
            {
                Id = Guid.NewGuid(),
                Data = "Test-Data",
                TimeSent = DateTime.UtcNow
            };

            var response = await _httpClient.PostAsJsonAsync($"api/files/{Bucket}/addjsonobject/", jsonObjectRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
