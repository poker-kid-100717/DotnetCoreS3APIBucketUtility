# API image for Cloudflare Containers (also runs anywhere Docker does).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DotnetCoreS3Utility.Core/DotnetCoreS3Utility.Core.csproj DotnetCoreS3Utility.Core/
COPY DotnetCoreS3Utility.Infrastructure/DotnetCoreS3Utility.Infrastructure.csproj DotnetCoreS3Utility.Infrastructure/
COPY DotnetCoreS3Utility.API/DotnetCoreS3Utility.API.csproj DotnetCoreS3Utility.API/
RUN dotnet restore DotnetCoreS3Utility.API/DotnetCoreS3Utility.API.csproj
COPY DotnetCoreS3Utility.Core/ DotnetCoreS3Utility.Core/
COPY DotnetCoreS3Utility.Infrastructure/ DotnetCoreS3Utility.Infrastructure/
COPY DotnetCoreS3Utility.API/ DotnetCoreS3Utility.API/
RUN dotnet publish DotnetCoreS3Utility.API/DotnetCoreS3Utility.API.csproj -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "DotnetCoreS3Utility.API.dll"]
