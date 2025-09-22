using NotifyHubAPI.Services;
using NotifyHubAPI.Middleware;
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
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "NotifyHub API",
            Version = "v1.0",
            Description = "统一邮件通知API - 为多个项目提供统一邮件通知",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "NotifyHub Team",
                Email = "admin@notify.origami7023.cn"
            }
        });

        // API Key认证Swagger
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

    // SMTP配置
    services.Configure<SmtpSettings>(configuration.GetSection("SmtpSettings"));

    // 注册服务 - 使用简化版本的邮件服务
    services.AddScoped<IEmailService, SimpleEmailService>();
    services.AddSingleton<IApiKeyService, ApiKeyService>();

    // 内存缓存
    services.AddMemoryCache();
    services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
    services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));
    services.AddInMemoryRateLimiting();
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

    // 健康检查 - 移除数据库检查
    services.AddHealthChecks()
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
                    ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy($"发现{apiKeyCount}个API密钥")
                    : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("未配置API密钥");
            }
            catch (Exception ex)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"API密钥检查失败: {ex.Message}");
            }
        });

    // CORS
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
            c.RoutePrefix = string.Empty; // 使Swagger成为根路径
        });
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    // 中间件管道
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

    // 状态信息端点 - 简化版本
    app.MapGet("/info", async (IServiceProvider serviceProvider) =>
    {
        return Results.Ok(new
        {
            service = "NotifyHubAPI",
            version = "1.0.0 (Stateless)",
            environment = app.Environment.EnvironmentName,
            timestamp = DateTime.UtcNow,
            mode = "无数据库模式",
            status = "运行正常",
            features = new
            {
                emailSending = true,
                emailHistory = false,
                retryMechanism = false,
                persistence = false
            },
            message = "邮件发送功能正常，但不保存发送记录"
        });
    });
}