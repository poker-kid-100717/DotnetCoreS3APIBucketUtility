using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using DotnetCoreS3Utility.Core.Communication.Catalog;
using DotnetCoreS3Utility.Core.Communication.Files;
using DotnetCoreS3Utility.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotnetCoreS3Utility.Infrastructure.Repositories
{
    public class FilesRepository : IFilesRepository
    {

        private readonly IAmazonS3 _s3Client;
        private readonly IObjectCatalog _catalog;

        public FilesRepository(IAmazonS3 s3Client, IObjectCatalog catalog)
        {
            _s3Client = s3Client;
            _catalog = catalog;
        }

        public async Task<AddFileResponse> UploadFiles(string bucketName, IList<IFormFile> formFiles)
        {
            var response = new List<string>();

            foreach (var file in formFiles)
            {
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    InputStream = file.OpenReadStream(),
                    Key = file.FileName,
                    BucketName = bucketName,
                    CannedACL = S3CannedACL.NoACL
                };

                using (var fileTransferUtility = new TransferUtility(_s3Client))
                {
                    await fileTransferUtility.UploadAsync(uploadRequest);
                }

                await _catalog.RecordAsync(new CatalogEntry
                {
                    BucketName = bucketName, Key = file.FileName, Size = file.Length,
                    ContentType = string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType,
                    Kind = "file", UploadedAt = DateTime.UtcNow
                });

                var expiryUrlRequest = new GetPreSignedUrlRequest
                {
                    BucketName = bucketName,
                    Key = file.FileName,
                    Expires = DateTime.Now.AddDays(1)
                };

                var url = _s3Client.GetPreSignedURL(expiryUrlRequest);

                response.Add(url);
            }

            return new AddFileResponse
            {
                PreSignedUrl = response
            };
        }

        public async Task<IEnumerable<ListFilesResponse>> ListFiles(string bucketName)
        {
            var responses = await _s3Client.ListObjectsAsync(bucketName);

            return responses.S3Objects.Select(b => new ListFilesResponse
            {
                BucketName = b.BucketName,
                Key = b.Key,
                Owner = b.Owner.DisplayName,
                Size = b.Size ?? 0
            });
        }

        public async Task DownloadFile(string bucketName, string fileName)
        {
            var tempDirectory = Path.Combine(Path.GetTempPath(), "S3Temp");
            Directory.CreateDirectory(tempDirectory);
            var pathAndFileName = Path.Combine(tempDirectory, fileName);

            var downloadRequest = new TransferUtilityDownloadRequest
            {
                BucketName = bucketName,
                Key = fileName,
                FilePath = pathAndFileName
            };

            using (var transferUtility = new TransferUtility(_s3Client))
            {
                await transferUtility.DownloadAsync(downloadRequest);
            }
        }

        public async Task<DeleteFileResponse> DeleteFile(string bucketName, string fileName)
        {
            var multiObjectDeleteRequest = new DeleteObjectsRequest
            {
                BucketName = bucketName
            };

            multiObjectDeleteRequest.AddKey(fileName);

            var response = await _s3Client.DeleteObjectsAsync(multiObjectDeleteRequest);
            await _catalog.RemoveAsync(bucketName, fileName);

            return new DeleteFileResponse
            {
                NumberOfDeletedObjects = response.DeletedObjects.Count
            };
        }

        public async Task AddJsonObject(string bucketName, AddJsonObjectRequest request)
        {
            var createdOnUtc = DateTime.UtcNow;

            var s3Key = $"{createdOnUtc:yyyy}/{createdOnUtc:MM}/{createdOnUtc:dd}/{request.Id}";

            var body = JsonConvert.SerializeObject(request);
            var putObjectRequest = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = s3Key,
                ContentBody = body,
                ContentType = "application/json"
            };

            await _s3Client.PutObjectAsync(putObjectRequest);
            await _catalog.RecordAsync(new CatalogEntry
            {
                BucketName = bucketName, Key = s3Key, Size = Encoding.UTF8.GetByteCount(body),
                ContentType = "application/json", Kind = "json", UploadedAt = createdOnUtc
            });
        }

        public async Task<GetJsonObjectResponse> GetJsonObject(string bucketName, string fileName)
        {
            var request = new GetObjectRequest
            {
                BucketName = bucketName,
                Key = fileName
            };

            var response = await _s3Client.GetObjectAsync(request);

            using (var reader = new StreamReader(response.ResponseStream))
            {
                var contents = reader.ReadToEnd();
                return JsonConvert.DeserializeObject<GetJsonObjectResponse>(contents);
            }
        }
    }
}
