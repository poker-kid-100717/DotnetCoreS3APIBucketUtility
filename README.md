# DotnetCoreS3Utility

[![CI](https://github.com/poker-kid-100717/DotnetCoreS3APIBucketUtility/actions/workflows/ci.yml/badge.svg)](https://github.com/poker-kid-100717/DotnetCoreS3APIBucketUtility/actions/workflows/ci.yml)

A small ASP.NET Core Web API that wraps the AWS SDK for S3 bucket and object management, laid out with the same Core/Infrastructure/API separation I use on larger projects: an interface-driven `Core` layer, an `Infrastructure` layer implementing those interfaces against the AWS SDK, and a thin `API` layer exposing them over HTTP.

## Applied in production, not just referenced here

Object storage isn't abstract for me — I've built against it under real load and failure conditions:

- **Global Holdings** — designed object-storage and queue-based processing for large-file ingestion and background workloads, including transient-failure handling, structured logging, and correlation IDs for tracing failures across the pipeline. The bucket/object interface split in this repo is the same shape I used there, just against AWS S3 instead of the cloud storage that job's infrastructure ran on.

## Endpoints

**Buckets** (`/api/bucket`)
- `POST /create/{bucketName}` — create an S3 bucket (rejects if it already exists)
- `GET /list` — list buckets
- `DELETE /delete/{bucketName}` — delete a bucket

**Files** (`/api/files`)
- `POST /{bucketName}/add` — upload objects to a bucket
- `GET /{bucketName}/list` — list objects in a bucket
- `GET /{bucketName}/download/{fileName}` — download an object
- `DELETE /{bucketName}/delete/{fileName}` — delete an object

**Catalog** (`/api/catalog`), backed by **MongoDB**
- `GET /?bucket=&q=` — search stored objects by bucket and key text (one document per object, upserted on every upload)
- `GET /stats` — object count and bytes per bucket and content type, computed with an aggregation pipeline

Every upload, JSON object and delete updates the catalog, so "what is stored, where, and how much" is a database query instead of a listing of every bucket. Without `DATABASE_URL` the catalog is kept in memory.

There's also an in-app **Architecture** page (served at `/`) covering the same layering and design decisions as this README, for anyone running the API rather than reading the repo.

## Structure

```
DotnetCoreS3Utility.API/              Controllers, Startup/Program, request/response wiring
DotnetCoreS3Utility.Core/             IBucketRepository / IFilesRepository interfaces, request/response contracts
DotnetCoreS3Utility.Infrastructure/   AWS SDK-backed implementations of the Core interfaces, MongoDB object catalog
DotnetCoreS3Utility.Integration.Tests/ xUnit integration tests exercising the API against LocalStack S3 and MongoDB
cloudflare/                           Worker + Container definition for Cloudflare hosting (objects in Cloudflare R2)
```

## Running it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and AWS credentials with S3 access (via the standard AWS SDK credential chain — environment variables, shared credentials file, or an IAM role). Without credentials, the API still runs and serves the architecture page; S3 calls return a clean JSON error instead of a stack trace.

```bash
dotnet restore
dotnet run --project DotnetCoreS3Utility.API
```

Any S3-compatible store works: set `Storage__ServiceUrl`, `Storage__AccessKeyId` and `Storage__SecretAccessKey` (for example Cloudflare R2 at `https://<account-id>.r2.cloudflarestorage.com`, or MinIO). Set `DATABASE_URL` to a MongoDB URL to keep the catalog in MongoDB.

## Deploy to Cloudflare

Deployment runs from GitHub Actions on every push to `master` (`.github/workflows/deploy-cloudflare.yml`): a Worker fronts the API running in a Cloudflare Container, objects are stored in **Cloudflare R2** through its S3-compatible API, and the catalog lives in **MongoDB** (for example a free MongoDB Atlas cluster). The script smoke-tests the result, including that writes without the API key are refused.

Repository **secrets**:

| Secret | Required | Purpose |
| --- | --- | --- |
| `CLOUDFLARE_API_TOKEN` | yes | Wrangler deploys |
| `CLOUDFLARE_ACCOUNT_ID` | yes | Wrangler deploys; also forms the R2 endpoint |
| `R2_ACCESS_KEY_ID` / `R2_SECRET_ACCESS_KEY` | yes | R2 API token with Object Read & Write, ideally scoped to the buckets the API may use |
| `DATABASE_URL` | recommended | MongoDB URL, e.g. `mongodb+srv://<user>:<password>@<cluster>.mongodb.net/s3utility`. Without it the catalog is in memory and resets on restart. |
| `API_KEY` | recommended | Any random string. Writes need it in an `X-Api-Key` header; without it the deployed API is read-only. |

Optional repository variable: `APP_HOST` for a custom hostname. Until the required secrets exist the deploy job skips cleanly. `scripts/cloudflare-deploy.sh` can also be run locally with the same environment variables.

## Tests

```bash
dotnet test DotnetCoreS3Utility.Integration.Tests
```

The tests run the real API in-process (`WebApplicationFactory`) against [LocalStack](https://www.localstack.cloud/)'s S3 emulator and MongoDB 8, both started in Docker by [Testcontainers](https://dotnet.testcontainers.org/). You need Docker running, but no AWS account or credentials. The image is pinned to `localstack/localstack:4.14.0`, the last release that runs without a LocalStack auth token. GitHub Actions runs the same suite on every push and pull request.
