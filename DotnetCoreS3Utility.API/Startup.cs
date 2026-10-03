#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using DotnetCoreS3Utility.Core.Interfaces;
using DotnetCoreS3Utility.Infrastructure.Catalog;
using DotnetCoreS3Utility.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;

namespace DotnetCoreS3Utility.API
{
    public class Startup
    {
        private readonly IConfiguration _configuration;

        public Startup (IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // Any S3-compatible store: set Storage:ServiceUrl (for example Cloudflare R2,
            // https://<account-id>.r2.cloudflarestorage.com) with its access keys; otherwise AWS S3 via the SDK's credential chain.
            var serviceUrl = _configuration["Storage:ServiceUrl"];
            if (!string.IsNullOrWhiteSpace(serviceUrl))
            {
                services.AddSingleton<IAmazonS3>(new AmazonS3Client(
                    new BasicAWSCredentials(_configuration["Storage:AccessKeyId"], _configuration["Storage:SecretAccessKey"]),
                    new AmazonS3Config
                    {
                        ServiceURL = serviceUrl,
                        ForcePathStyle = true,
                        AuthenticationRegion = _configuration["Storage:Region"] ?? "auto"
                    }));
            }
            else
            {
                services.AddAWSService<IAmazonS3>(_configuration.GetAWSOptions());
            }

            // Object metadata goes to MongoDB when DATABASE_URL is set (for example a MongoDB Atlas mongodb+srv:// URL).
            var databaseUrl = _configuration["DATABASE_URL"];
            if (!string.IsNullOrWhiteSpace(databaseUrl))
            {
                services.AddSingleton<IObjectCatalog>(_ => new MongoObjectCatalog(databaseUrl));
            }
            else
            {
                services.AddSingleton<IObjectCatalog, InMemoryObjectCatalog>();
            }

            services.AddSingleton<IBucketRepository, BucketRepository>();
            services.AddSingleton<IFilesRepository, FilesRepository>();

            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseExceptionHandler(a => a.Run(async context =>
            {
                var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
                var exception = exceptionHandlerPathFeature?.Error;

                var result = JsonConvert.SerializeObject(new { error = exception?.Message });
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(result);
            }));

            // When Api:Key is set (the hosted deployment sets it), writes need a matching X-Api-Key header.
            // With Api:RequireKey=true and no key configured, writes are refused outright.
            var apiKey = _configuration["Api:Key"];
            var requireKey = _configuration.GetValue("Api:RequireKey", false) || !string.IsNullOrEmpty(apiKey);
            app.Use(async (context, next) =>
            {
                var isWrite = !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method);
                if (requireKey && isWrite && context.Request.Path.StartsWithSegments("/api") && !KeyMatches(context, apiKey))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new { error = "Writes require a valid X-Api-Key header." }));
                    return;
                }
                await next();
            });

            app.UseDefaultFiles();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGet("/health", (IObjectCatalog catalog) => Results.Ok(new { status = "Healthy", catalog = catalog.Mode }));
            });
        }

        private static bool KeyMatches(HttpContext context, string? expected)
        {
            if (string.IsNullOrEmpty(expected)) return false;
            var supplied = context.Request.Headers["X-Api-Key"].ToString();
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(expected));
        }
    }
}
