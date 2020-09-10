using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DotnetCoreS3Utility.Core.Communication.Bucket;
using DotnetCoreS3Utility.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DotnetCoreS3Utility.API.Controllers
{
    [Route("api/bucket")]
    [ApiController]
    public class BucketController : ControllerBase
    {
        private readonly IBucketRepository _bucketRepository;

        public BucketController(IBucketRepository bucketRepository)
        {
            _bucketRepository = bucketRepository;
        }

        [HttpPost]
        [Route("create/{bucketName}")]
        public async Task<ActionResult<CreateBucketResponse>> CreateS3Bucket([FromRoute] string bucketName)
        {
            var bucketExists = await _bucketRepository.DoesS3BucketExist(bucketName);

            if (bucketExists)
            {
                return BadRequest("S3 bucket already exists");
            }

            var result = await _bucketRepository.CreateBucket(bucketName);

            if (result == null)
            {
                return BadRequest();
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("list")]
        public async Task<ActionResult<IEnumerable<ListS3BucketsResponse>>> ListS3Buckets()
        {
            var result = await _bucketRepository.ListBuckets();

            if (result == null)
            {
                return NotFound();
            }

            return Ok(result);
        }

        [HttpDelete]
        [Route("delete/{bucketName}")]
        public async Task<IActionResult> DeleteS3Bucket(string bucketName)
        {
            await _bucketRepository.DeleteBucket(bucketName);

            return Ok();
        }
    }
}