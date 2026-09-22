# DotnetCoreS3Utility

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
- Upload, list, and remove objects within a bucket

## Structure

```
DotnetCoreS3Utility.API/              Controllers, Startup/Program, request/response wiring
DotnetCoreS3Utility.Core/             IBucketRepository / IFilesRepository interfaces, request/response contracts
DotnetCoreS3Utility.Infrastructure/   AWS SDK-backed implementations of the Core interfaces
DotnetCoreS3Utility.Integration.Tests/ xUnit integration tests exercising the API against a test AWS context
```

## Running it

Requires AWS credentials with S3 access (via the standard AWS SDK credential chain — environment variables, shared credentials file, or an IAM role).

```bash
dotnet restore
dotnet run --project DotnetCoreS3Utility.API
```

## Tests

```bash
dotnet test DotnetCoreS3Utility.Integration.Tests
```
