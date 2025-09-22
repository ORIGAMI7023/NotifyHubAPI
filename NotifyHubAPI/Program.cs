using NotifyHubAPI.Data;
using NotifyHubAPI.Services;
using NotifyHubAPI.Middleware;
using NotifyHubAPI.BackgroundServices;
using Microsoft.EntityFrameworkCore;
using Serilog;
using AspNetCoreRateLimit;

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

// 确保数据库已创建并初始化
await EnsureDatabaseInitialized(app);

Log.Information("NotifyHubAPI 服务启动: {Urls}", string.Join(", ", app.Urls));

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
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "NotifyHub API",
            Version = "v1.0",
            Description = "统一邮件通知API - 为多个项目提供统一邮件发送功能",
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

    // 数据库配置
    services.AddDbContext<NotificationDbContext>(options =>
    {
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"),
            sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null);
                sqlOptions.CommandTimeout(30);
            });
    });

    // SMTP配置
    services.Configure<SmtpSettings>(configuration.GetSection("SmtpSettings"));

    // 注册服务
    services.AddScoped<IEmailService, EmailService>();
    services.AddSingleton<IApiKeyService, ApiKeyService>();
    services.AddScoped<IDatabaseInitializationService, DatabaseInitializationService>();

    // 缓存和速率限制
    services.AddMemoryCache();
    services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
    services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));
    services.AddInMemoryRateLimiting();
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

    // 健康检查
    services.AddHealthChecks()
        .AddCheck("database", () =>
        {
            try
            {
                // 简化的数据库检查，避免使用 AddDbContextCheck
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                return !string.IsNullOrEmpty(connectionString)
                    ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("数据库配置正常")
                    : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("数据库连接字符串缺失");
            }
            catch (Exception ex)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"数据库检查失败: {ex.Message}");
            }
        })
        .AddCheck("smtp", () =>
        {
            try
            {
                var smtpSettings = configuration.GetSection("SmtpSettings").Get<SmtpSettings>();
                return !string.IsNullOrEmpty(smtpSettings?.Host)
                    ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("SMTP配置正常")
                    : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("SMTP配置缺失");
            }
            catch (Exception ex)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"SMTP配置检查失败: {ex.Message}");
            }
        })
        .AddCheck("apikeys", () =>
        {
            try
            {
                var apiKeysSection = configuration.GetSection("ApiKeys");
                var apiKeyCount = apiKeysSection.GetChildren().Count();
                return apiKeyCount > 0
                    ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy($"已配置{apiKeyCount}个API密钥")
                    : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("未配置API密钥");
            }
            catch (Exception ex)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"API密钥检查失败: {ex.Message}");
            }
        });

    // 后台服务
    services.AddHostedService<EmailRetryBackgroundService>();

    // CORS配置
    services.AddCors(options =>
    {
        options.AddPolicy("AllowOrigins", policy =>
        {
            var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                ?? new[] { "https://notify.origami7023.cn", "https://localhost:7000" };

            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });
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
            c.RoutePrefix = string.Empty; // 使Swagger为根路径
        });
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    // 核心中间件
    app.UseHttpsRedirection();
    app.UseCors("AllowOrigins");

    // 速率限制
    app.UseIpRateLimiting();

    // 自定义中间件
    app.UseApiKeyAuthentication();

    // 路由和控制器
    app.UseRouting();
    app.UseAuthorization();
    app.MapControllers();

    // 健康检查端点
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });

    // 默认路由
    app.MapGet("/", () => Results.Redirect("/swagger"));

    // 状态信息端点
    app.MapGet("/info", async (IServiceProvider serviceProvider) =>
    {
        using var scope = serviceProvider.CreateScope();
        var dbService = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationService>();
        var stats = await dbService.GetDatabaseStatsAsync();
        return Results.Ok(new
        {
            service = "NotifyHubAPI",
            version = "1.0.0",
            environment = app.Environment.EnvironmentName,
            timestamp = DateTime.UtcNow,
            stats = new
            {
                totalEmails = stats.TotalEmails,
                sentEmails = stats.SentEmails,
                failedEmails = stats.FailedEmails,
                pendingEmails = stats.PendingEmails
            }
        });
    });
}

async Task EnsureDatabaseInitialized(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbInitService = scope.ServiceProvider.GetRequiredService<IDatabaseInitializationService>();

    try
    {
        // 检查数据库连接
        var canConnect = await dbInitService.CheckDatabaseConnectionAsync();
        if (!canConnect)
        {
            Log.Error("无法连接到数据库，请检查连接字符串配置");
            throw new InvalidOperationException("数据库连接失败");
        }

        // 初始化数据库
        await dbInitService.InitializeDatabaseAsync();

        // 获取并记录统计信息
        var stats = await dbInitService.GetDatabaseStatsAsync();
        Log.Information("数据库统计: 总计{Total}封邮件, 成功{Sent}封, 失败{Failed}封, 待发送{Pending}封",
            stats.TotalEmails, stats.SentEmails, stats.FailedEmails, stats.PendingEmails);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "数据库初始化失败");
        throw;
    }
}