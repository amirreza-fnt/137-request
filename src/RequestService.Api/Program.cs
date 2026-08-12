using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RequestService.Api.Middleware;
using RequestService.Infrastructure;
using RequestService.Infrastructure.Persistence;
using Serilog;

// ---------- Bootstrap logger (failed startup is still logged) ----------
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: context.Configuration["Logging:File"] ?? "/var/log/requestservice/app-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            flushToDiskInterval: TimeSpan.FromSeconds(1)));

    // ---------- Controllers ----------
    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
            // Serialize enums as strings (channel, status, ...) for a clean API contract.
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        });

    // Consistent error envelope for model-binding failures.
    builder.Services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var traceId = context.HttpContext.TraceIdentifier;
            var message = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault() ?? "Invalid request payload.";

            return new BadRequestObjectResult(new
            {
                code = "VALIDATION_ERROR",
                message,
                traceId
            });
        };
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddHttpContextAccessor();

    // ---------- Swagger ----------
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Request Service API (سامانه ۱۳۷ - ثبت درخواست)",
            Version = "v1",
            Description = "Microservice for registering citizen requests for the 137 municipality system. " +
                          "Citizen identity comes from sso-login-service; attachments are validated against the files service by id only."
        });

        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT issued by sso-login-service (citizen/operator)."
        });
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });

    // ---------- CORS (web clients only; mobile/desktop apps are not browsers) ----------
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
        {
            var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? Array.Empty<string>();
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });

        if (builder.Environment.IsDevelopment())
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        }
    });

    // ---------- Infrastructure (DB / repos / HTTP clients) ----------
    builder.Services.AddInfrastructure(builder.Configuration);

    // ---------- Health checks ----------
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<RequestDbContext>("database");

    var app = builder.Build();

    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
            | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto,
    });

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
    {
        app.UseSwagger();
        app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Request Service API v1"));
    }

    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseRouting();

    if (app.Environment.IsDevelopment())
    {
        app.UseCors("AllowAll");
    }
    else
    {
        app.UseCors("AllowFrontend");
    }

    app.MapControllers();

    // Health endpoints (Nginx/systemd probes). /api/health kept for parity with the files service.
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/api/health");

    Log.Information("Request Service started.");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Request Service failed to start.");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
