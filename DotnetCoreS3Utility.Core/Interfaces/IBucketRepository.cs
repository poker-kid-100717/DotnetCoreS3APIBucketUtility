using DotnetCoreS3Utility.Core.Communication.Bucket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DotnetCoreS3Utility.Core.Interfaces
{
    public interface IBucketRepository
    {
        Task<bool> DoesS3BucketExist(string bucketName);
        Task<CreateBucketResponse> CreateBucket(string bucketName);
        Task<IEnumerable<ListS3BucketsResponse>> ListBuckets();
        Task DeleteBucket(string bucketName);
    }
}
