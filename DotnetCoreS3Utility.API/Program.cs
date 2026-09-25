using DotnetCoreS3Utility.API;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

var startup = new Startup(builder.Configuration);
startup.ConfigureServices(builder.Services);

var app = builder.Build();
startup.Configure(app, app.Environment);

app.Run();

// Exposes the entry point to WebApplicationFactory<Program> in the integration tests.
public partial class Program { }
