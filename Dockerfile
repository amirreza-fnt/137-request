# Multi-stage build for Linux (AlmaLinux / Docker).
# Framework-dependent image — matches the systemd deployment (runtime installed on the host).

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/RequestService.Domain/RequestService.Domain.csproj src/RequestService.Domain/
COPY src/RequestService.Application/RequestService.Application.csproj src/RequestService.Application/
COPY src/RequestService.Infrastructure/RequestService.Infrastructure.csproj src/RequestService.Infrastructure/
COPY src/RequestService.Api/RequestService.Api.csproj src/RequestService.Api/
RUN dotnet restore src/RequestService.Api/RequestService.Api.csproj

COPY src/ src/
RUN dotnet publish src/RequestService.Api/RequestService.Api.csproj -c Release -o /out --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out .

# Development so Swagger UI is served out of the box (uses appsettings.Development.json).
ENV ASPNETCORE_ENVIRONMENT=Development
ENV ASPNETCORE_URLS=http://+:5006
EXPOSE 5006

ENTRYPOINT ["dotnet", "RequestService.Api.dll"]
