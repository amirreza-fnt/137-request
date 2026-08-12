using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace RequestService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for `dotnet ef` commands. The connection string is read
/// from the API startup project's configuration (appsettings*.json) or from the
/// ConnectionStrings__Requests environment variable — never hardcoded here.
/// </summary>
public sealed class RequestDbContextFactory : IDesignTimeDbContextFactory<RequestDbContext>
{
    public RequestDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Requests")
            ?? throw new InvalidOperationException(
                "Connection string not found. Set ConnectionStrings__Requests or run from the startup project directory.");

        var optionsBuilder = new DbContextOptionsBuilder<RequestDbContext>();
        optionsBuilder.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            sql.CommandTimeout(30);
        });

        return new RequestDbContext(optionsBuilder.Options);
    }
}
