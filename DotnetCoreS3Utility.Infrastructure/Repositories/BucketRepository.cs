using Amazon.S3;
using Amazon.S3.Model;
using DotnetCoreS3Utility.Core.Communication.Bucket;
using DotnetCoreS3Utility.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotnetCoreS3Utility.Infrastructure.Repositories
{
    public class BucketRepository : IBucketRepository
    {
        private readonly IAmazonS3 _s3Client;

        public BucketRepository(IAmazonS3 s3Client)
        {
            _s3Client = s3Client;
        }

        public async Task<bool> DoesS3BucketExist(string bucketName)
        {
            return await _s3Client.DoesS3BucketExistAsync(bucketName);
        }

        public async Task<CreateBucketResponse> CreateBucket(string bucketName)
        {
            var putBucketRequest = new PutBucketRequest
            {
                BucketName = bucketName,
                UseClientRegion = true
            };

            var response = await _s3Client.PutBucketAsync(putBucketRequest);

            return new CreateBucketResponse
            {
                BucketName = bucketName,
                RequestId = response.ResponseMetadata.RequestId
            };
        }

        public async Task<IEnumerable<ListS3BucketsResponse>> ListBuckets()
        {
            var response = await _s3Client.ListBucketsAsync();

            return response.Buckets.Select(b => new ListS3BucketsResponse
            {
                BucketName = b.BucketName,
                CreationDate = b.CreationDate
            });
        }

        public async Task DeleteBucket(string bucketName)
        {
            await _s3Client.DeleteBucketAsync(bucketName);
        }
    }
}
