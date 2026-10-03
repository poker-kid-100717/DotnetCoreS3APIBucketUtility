#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotnetCoreS3Utility.Core.Communication.Catalog;
using DotnetCoreS3Utility.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DotnetCoreS3Utility.API.Controllers
{
    [Route("api/catalog")]
    [ApiController]
    public class CatalogController : ControllerBase
    {
        private readonly IObjectCatalog _catalog;

        public CatalogController(IObjectCatalog catalog)
        {
            _catalog = catalog;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CatalogEntry>>> Search([FromQuery] string? bucket, [FromQuery] string? q, [FromQuery] int limit = 50)
        {
            var results = await _catalog.SearchAsync(
                string.IsNullOrWhiteSpace(bucket) ? null : bucket.Trim(),
                string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
                Math.Clamp(limit, 1, 200));
            return Ok(results);
        }

        [HttpGet]
        [Route("stats")]
        public async Task<ActionResult<IEnumerable<CatalogStats>>> Stats()
        {
            return Ok(await _catalog.StatsAsync());
        }
    }
}
