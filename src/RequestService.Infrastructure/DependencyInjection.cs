using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RequestService.Application.Interfaces;
using RequestService.Application.Options;
using RequestService.Application.Services;
using RequestService.Application.Validation;
using RequestService.Infrastructure.Clients;
using RequestService.Infrastructure.Persistence;

namespace RequestService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ---------- Options ----------
        services.AddOptions<SsoOptions>()
            .Bind(configuration.GetSection(SsoOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "Sso:BaseUrl is required.")
            .ValidateOnStart();

        services.AddOptions<FilesOptions>()
            .Bind(configuration.GetSection(FilesOptions.SectionName))
            .Validate(o => !o.ValidationEnabled || !string.IsNullOrWhiteSpace(o.BaseUrl), "Files:BaseUrl is required when Files:ValidationEnabled.")
            .ValidateOnStart();

        services.Configure<InternalAuthOptions>(configuration.GetSection(InternalAuthOptions.SectionName));
        services.Configure<TelephonyOptions>(configuration.GetSection(TelephonyOptions.SectionName));

        // ---------- Validation ----------
        services.AddValidatorsFromAssemblyContaining<CreateRequestValidator>();

        // ---------- Database ----------
        var connectionString = configuration.GetConnectionString("Requests")
            ?? throw new InvalidOperationException("ConnectionStrings:Requests is missing.");

        services.AddDbContext<RequestDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
                sql.CommandTimeout(30);
            }));

        // ---------- Persistence ----------
        services.AddScoped<IRequestRepository, RequestRepository>();

        // ---------- Application services ----------
        services.AddScoped<ITrackingCodeGenerator, RequestService.Application.Services.TrackingCodeGenerator>();
        services.AddScoped<IRequestService, RequestService.Application.Services.RequestService>();

        // ---------- Downstream HTTP clients (retry + circuit breaker) ----------
        var sso = configuration.GetSection(SsoOptions.SectionName).Get<SsoOptions>() ?? new SsoOptions();
        services.AddHttpClient<ISsoAuthClient, SsoAuthClient>(client =>
        {
            client.BaseAddress = new Uri(EnsureTrailingSlash(sso.BaseUrl));
            client.Timeout = TimeSpan.FromSeconds(sso.TimeoutSeconds + 5);
        }).AddStandardResilienceHandler(opt =>
        {
            opt.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(sso.TimeoutSeconds + 2);
            opt.AttemptTimeout.Timeout = TimeSpan.FromSeconds(sso.TimeoutSeconds);
            opt.Retry.MaxRetryAttempts = sso.RetryCount;
            opt.Retry.Delay = TimeSpan.FromMilliseconds(300);
            opt.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            opt.CircuitBreaker.MinimumThroughput = sso.CircuitBreakerMinThroughput;
            opt.CircuitBreaker.FailureRatio = sso.CircuitBreakerFailureRatio / 100.0;
            opt.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        });

        var files = configuration.GetSection(FilesOptions.SectionName).Get<FilesOptions>() ?? new FilesOptions();
        var filesBuilder = services.AddHttpClient<IFileServiceClient, FileServiceClient>(client =>
        {
            if (!string.IsNullOrWhiteSpace(files.BaseUrl))
            {
                client.BaseAddress = new Uri(EnsureTrailingSlash(files.BaseUrl));
            }

            client.Timeout = TimeSpan.FromSeconds(files.TimeoutSeconds + 5);
        });

        if (files.AllowInvalidSslCertificate)
        {
            filesBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        }

        filesBuilder.AddStandardResilienceHandler(opt =>
        {
            opt.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(files.TimeoutSeconds + 2);
            opt.AttemptTimeout.Timeout = TimeSpan.FromSeconds(files.TimeoutSeconds);
            opt.Retry.MaxRetryAttempts = files.RetryCount;
            opt.Retry.Delay = TimeSpan.FromMilliseconds(300);
            opt.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            opt.CircuitBreaker.MinimumThroughput = 8;
            opt.CircuitBreaker.FailureRatio = 0.5;
            opt.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        });

        // ---------- IssabelBridge (live queue / answer / hangup) ----------
        var telephony = configuration.GetSection(TelephonyOptions.SectionName).Get<TelephonyOptions>()
            ?? new TelephonyOptions();
        var telephonyBuilder = services.AddHttpClient<ITelephonyBridgeClient, TelephonyBridgeClient>(client =>
        {
            if (!string.IsNullOrWhiteSpace(telephony.BridgeBaseUrl))
            {
                client.BaseAddress = new Uri(EnsureTrailingSlash(telephony.BridgeBaseUrl));
            }

            client.Timeout = TimeSpan.FromSeconds(telephony.TimeoutSeconds + 3);
        });

        if (telephony.AllowInvalidSslCertificate)
        {
            telephonyBuilder.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        }

        return services;
    }

    private static string EnsureTrailingSlash(string url)
        => url.EndsWith('/') ? url : url + "/";
}
