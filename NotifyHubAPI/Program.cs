using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NotifyHubAPI.Middleware;
using NotifyHubAPI.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// 配置 Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/notifyhub-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// 配置服务
ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

// 配置HTTP管道
ConfigurePipeline(app);

Log.Information("NotifyHubAPI 启动完成: {Urls}", string.Join(", ", app.Urls));

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "应用程序启动失败");
}
finally
{
    Log.CloseAndFlush();
}

void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    // 基础服务
    services.AddControllers();
    services.AddEndpointsApiExplorer();

    // 配置请求大小限制
    services.Configure<IISServerOptions>(options =>
    {
        options.MaxRequestBodySize = configuration.GetValue<long>("Security:MaxRequestSizeBytes", 1024 * 1024);
    });

    services.Configure<KestrelServerOptions>(options =>
    {
        options.Limits.MaxRequestBodySize = configuration.GetValue<long>("Security:MaxRequestSizeBytes", 1024 * 1024);
    });

    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "NotifyHub API",
            Version = "v1.0",
            Description = "统一邮件通知API服务 - 为多个项目提供统一邮件通知",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "NotifyHub Team",
                Email = "admin@notify.origami7023.cn"
            }
        });

        // API Key认证Swagger配置
        c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "API Key认证 (Header: X-API-Key 或 Authorization: Bearer {key})",
            Name = "X-API-Key",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "ApiKeyScheme"
        });

        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    // SMTP配置 - 优先从环境变量获取配置值
    services.Configure<SmtpSettings>(options =>
    {
        var smtpHost = Environment.GetEnvironmentVariable("NOTIFYHUB_SMTP_HOST");
        if (!string.IsNullOrEmpty(smtpHost))
            options.Host = smtpHost;

        var smtpPort = Environment.GetEnvironmentVariable("NOTIFYHUB_SMTP_PORT");
        if (!string.IsNullOrEmpty(smtpPort) && int.TryParse(smtpPort, out var port))
            options.Port = port;

        var smtpUseSsl = Environment.GetEnvironmentVariable("NOTIFYHUB_SMTP_USESSL");
        if (!string.IsNullOrEmpty(smtpUseSsl) && bool.TryParse(smtpUseSsl, out var useSsl))
            options.UseSsl = useSsl;

        var smtpUsername = Environment.GetEnvironmentVariable("NOTIFYHUB_SMTP_USERNAME");
        if (!string.IsNullOrEmpty(smtpUsername))
            options.Username = smtpUsername;

        var smtpPassword = Environment.GetEnvironmentVariable("NOTIFYHUB_SMTP_PASSWORD");
        if (!string.IsNullOrEmpty(smtpPassword))
            options.Password = smtpPassword;

        var smtpFromEmail = Environment.GetEnvironmentVariable("NOTIFYHUB_SMTP_FROMEMAIL");
        if (!string.IsNullOrEmpty(smtpFromEmail))
            options.FromEmail = smtpFromEmail;

        var smtpFromName = Environment.GetEnvironmentVariable("NOTIFYHUB_SMTP_FROMNAME");
        if (!string.IsNullOrEmpty(smtpFromName))
            options.FromName = smtpFromName;
    });

    // 注册服务
    services.AddScoped<IEmailService, SimpleEmailService>();
    services.AddSingleton<IApiKeyService, ApiKeyService>();

    // 内存缓存和限流
    services.AddMemoryCache();
    services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
    services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));
    services.AddInMemoryRateLimiting();
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

    // 健康检查
    services.AddHealthChecks()
        .AddCheck("smtp", () =>
        {
            try
            {
                var smtpHost = Environment.GetEnvironmentVariable("NOTIFYHUB_SMTP_HOST");
                return !string.IsNullOrEmpty(smtpHost)
                    ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("SMTP配置正常")
                    : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("SMTP配置缺失");
            }
            catch (Exception ex)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"SMTP检查失败: {ex.Message}");
            }
        })
        .AddCheck("apikeys", () =>
        {
            try
            {
                var envVars = Environment.GetEnvironmentVariables()
                    .Cast<System.Collections.DictionaryEntry>()
                    .Count(kv => kv.Key.ToString()?.StartsWith("NOTIFYHUB_APIKEY_") == true);

                return envVars > 0
                    ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy($"已加载{envVars}个API密钥")
                    : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("未找到API密钥配置");
            }
            catch (Exception ex)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"API密钥检查失败: {ex.Message}");
            }
        });

    // CORS配置
    services.AddCors(options =>
    {
        options.AddPolicy("AllowOrigins", policy =>
        {
            var allowedOrigins = configuration.GetSection("Security:AllowedHosts").Get<string[]>()
                ?? new[] { "https://notify.origami7023.cn" };

            // 只允许HTTPS origins (生产环境)
            var httpsOrigins = allowedOrigins
                .Where(origin => !origin.StartsWith("localhost"))
                .Select(origin => origin.StartsWith("http") ? origin : $"https://{origin}")
                .ToArray();

            if (builder.Environment.IsDevelopment())
            {
                // 开发环境允许localhost
                httpsOrigins = httpsOrigins.Concat(new[] { "http://localhost:3000", "https://localhost:7000" }).ToArray();
            }

            policy.WithOrigins(httpsOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    // HTTPS重定向配置
    if (!builder.Environment.IsDevelopment())
    {
        services.AddHsts(options =>
        {
            options.Preload = true;
            options.IncludeSubDomains = true;
            options.MaxAge = TimeSpan.FromDays(365);
        });
    }
}

void ConfigurePipeline(WebApplication app)
{
    // 开发环境配置
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "NotifyHub API v1");
            c.RoutePrefix = string.Empty;
        });
    }
    else
    {
        // 生产环境强制HTTPS
        app.UseHsts();
    }

    // === 安全中间件层 ===
    // 1. 全局异常处理（必须最早）
    app.UseGlobalExceptionHandler();

    // 2. 安全头
    if (app.Configuration.GetValue<bool>("Security:EnableSecurityHeaders", true))
    {
        app.UseSecurityHeaders();
    }

    // 3. 主机过滤（阻止直接IP访问）
    if (app.Configuration.GetValue<bool>("Security:BlockDirectIpAccess", true))
    {
        app.UseCustomHostFiltering();
    }

    // 4. 请求验证（大小、内容类型、恶意内容）
    app.UseRequestValidation();

    // 5. HTTPS重定向
    if (app.Configuration.GetValue<bool>("Security:RequireHttps", true))
    {
        app.UseHttpsRedirection();
    }

    // 6. CORS
    app.UseCors("AllowOrigins");

    // 7. 限流（在认证之前）
    app.UseIpRateLimiting();

    // 8. API密钥认证
    app.UseApiKeyAuthentication();

    // === 应用程序中间件层 ===
    app.UseRouting();
    app.UseAuthorization();
    app.MapControllers();

    // === 健康检查和信息端点 ===
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });

    // 默认路由 - 仅开发环境显示Swagger
    if (app.Environment.IsDevelopment())
    {
        app.MapGet("/", () => Results.Redirect("/swagger"));
    }
    else
    {
        app.MapGet("/", () => Results.Json(new
        {
            message = "NotifyHub API",
            status = "运行中",
            timestamp = DateTime.UtcNow
        }));
    }

    // 状态信息端点
    app.MapGet("/info", () =>
    {
        try
        {
            return Results.Ok(new
            {
                service = "NotifyHubAPI",
                version = "1.0.0 (Secure)",
                environment = app.Environment.EnvironmentName,
                timestamp = DateTime.UtcNow,
                security = new
                {
                    httpsOnly = app.Configuration.GetValue<bool>("Security:RequireHttps", true),
                    hostFiltering = app.Configuration.GetValue<bool>("Security:BlockDirectIpAccess", true),
                    rateLimiting = true,
                    securityHeaders = app.Configuration.GetValue<bool>("Security:EnableSecurityHeaders", true),
                    requestValidation = true
                },
                features = new
                {
                    emailSending = true,
                    emailHistory = false,
                    retryMechanism = false,
                    persistence = false
                }
            });
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "获取服务信息失败");
            return Results.Problem("服务信息获取失败");
        }
    }).RequireRateLimiting("DefaultPolicy");
}