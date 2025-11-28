using NotifyHubAPI.Services;
using NotifyHubAPI.Middleware;
using Serilog;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);

// ����Serilog - ��ʹ�������ļ��������ظ�
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

builder.Host.UseSerilog();

// ���÷���
ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

// ����HTTP�ܵ�
ConfigurePipeline(app);

Log.Information("NotifyHubAPI ����������ɣ�������ַ: {Urls}", string.Join(", ", app.Urls));

try
{
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Ӧ�ó�������ʧ��");
}
finally
{
    Log.CloseAndFlush();
}

void ConfigureServices(IServiceCollection services, IConfiguration configuration)
{
    // ��������
    services.AddControllers();
    services.AddEndpointsApiExplorer();

    // �����С����
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
            Description = "ͳһ�ʼ�֪ͨAPI - Ϊ�����Ŀ�ṩͳһ�ʼ�֪ͨ����",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "NotifyHub Team",
                Email = "admin@notify.origami7023.cn"
            }
        });

        // API Key��֤Swagger����
        c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "API Key��֤ (Header: X-API-Key �� Authorization: Bearer {key})",
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

    // SMTP���� - ���ȼ����������� > �û����� > �����ļ�
    services.Configure<SmtpSettings>(options =>
    {
        // ���ȴ������ļ��������û����ܣ����ػ�������
        builder.Configuration.GetSection("SmtpSettings").Bind(options);

        // Ȼ���û����������ǣ�������ڣ�
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

        // ��¼������Դ�����ڵ��ԣ�
        var configSource = !string.IsNullOrEmpty(smtpHost) ? "��������" : "�����ļ�/�û�����";
        Log.Information("SMTP�����Ѽ��أ���Դ: {Source}", configSource);
    });

    // ע�����
    services.AddScoped<IEmailService, SimpleEmailService>();
    services.AddSingleton<IApiKeyService, ApiKeyService>();

    // �������ƺͻ���
    services.AddMemoryCache();
    services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
    services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));
    services.AddInMemoryRateLimiting();
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

    // �������
    services.AddHealthChecks()
    .AddCheck("smtp", () =>
    {
        try
        {
            // ���SMTP���� - ���ȼ����������� > �û����� > �����ļ�
            var smtpHost = Environment.GetEnvironmentVariable("NOTIFYHUB_SMTP_HOST")
                          ?? builder.Configuration["SmtpSettings:Host"];
            var smtpUsername = Environment.GetEnvironmentVariable("NOTIFYHUB_SMTP_USERNAME")
                              ?? builder.Configuration["SmtpSettings:Username"];
            var smtpPassword = Environment.GetEnvironmentVariable("NOTIFYHUB_SMTP_PASSWORD")
                              ?? builder.Configuration["SmtpSettings:Password"];

            if (!string.IsNullOrEmpty(smtpHost) &&
                !string.IsNullOrEmpty(smtpUsername) &&
                !string.IsNullOrEmpty(smtpPassword))
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy($"SMTP��������: {smtpHost}");
            }

            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("SMTP���ò�����");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"SMTP���ʧ��: {ex.Message}");
        }
    })
    .AddCheck("apikeys", () =>
    {
        try
        {
            // ���API��Կ - ���ȼ����������� > �û����� > �����ļ�
            var apiKeyCount = 0;

            // 1. ��黷������
            var envVars = Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .Count(kv => kv.Key.ToString()?.StartsWith("NOTIFYHUB_APIKEY_") == true);
            apiKeyCount += envVars;

            // 2. ����û����ܺ������ļ��е�ApiKeys����
            var apiKeysSection = builder.Configuration.GetSection("ApiKeys");
            if (apiKeysSection.Exists())
            {
                var configApiKeys = apiKeysSection.GetChildren()
                    .Where(kv => !string.IsNullOrEmpty(kv.Value))
                    .Count();
                apiKeyCount += configApiKeys;
            }

            if (apiKeyCount > 0)
            {
                // ������������ʾ������������������ʾ����
                var message = builder.Environment.IsDevelopment()
                    ? $"����{apiKeyCount}��API��Կ"
                    : "API��Կ���ó���";
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(message);
            }
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("δ��⵽API��Կ");
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"API��Կ���ʧ��: {ex.Message}");
        }
    });

    // CORS����
    services.AddCors(options =>
    {
        options.AddPolicy("AllowOrigins", policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                // ������������������Դ���������ԣ�
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            }
            else
            {
                // ������������ϸ�����������Դ
                var allowedOrigins = configuration.GetSection("Security:AllowedHosts").Get<string[]>()
                    ?? new[] { "https://notify.downf.cn" };

                var httpsOrigins = allowedOrigins
                    .Where(origin => !origin.StartsWith("localhost"))
                    .Select(origin => origin.StartsWith("http") ? origin : $"https://{origin}")
                    .ToArray();

                policy.WithOrigins(httpsOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            }
        });
    });

    // HTTPS�ض�������
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
    // ������������
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
        // ǿ��HTTPS
        app.UseHsts();
    }

    // === ��ȫ�м���� ===
    // 1. ȫ���쳣����������㣩
    app.UseGlobalExceptionHandler();

    // 2. ��ǿ��ȫ��⣨���ԭ���İ�ȫɨ���⣩
    app.UseEnhancedSecurity();

    // 3. ��ȫͷ����
    if (app.Configuration.GetValue<bool>("Security:EnableSecurityHeaders", true))
    {
        app.UseSecurityHeaders();
    }

    // 4. �������ˣ���ֱֹ��IP���ʣ�
    if (app.Configuration.GetValue<bool>("Security:BlockDirectIpAccess", true))
    {
        app.UseCustomHostFiltering();
    }

    // 5. ������֤����С������У�飩
    app.UseRequestValidation();

    // 6. HTTPS�ض���
    if (app.Configuration.GetValue<bool>("Security:RequireHttps", true))
    {
        app.UseHttpsRedirection();
    }

    // 7. CORS����
    app.UseCors("AllowOrigins");

    // 8. ��������֤֮ǰ��
    app.UseIpRateLimiting();

    // 9. API��Կ��֤
    app.UseApiKeyAuthentication();

    // === Ӧ�ó����м�� ===
    app.UseRouting();
    app.UseAuthorization();
    app.MapControllers();

    // === ��غ���Ϣ�˵� ===
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = async (context, report) =>
        {
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                status = report.Status.ToString(),
                checks = report.Entries.Select(e => new
                {
                    name = e.Key,
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration = e.Value.Duration.TotalMilliseconds
                }),
                totalDuration = report.TotalDuration.TotalMilliseconds
            });
            await context.Response.WriteAsync(result);
        }
    });
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });

    // Ĭ��·�� - ��ʾSwagger
    if (app.Environment.IsDevelopment())
    {
        app.MapGet("/", () => Results.Redirect("/swagger"));
    }
    else
    {
        app.MapGet("/", () => Results.Json(new
        {
            message = "NotifyHub API",
            status = "��������",
            timestamp = DateTime.UtcNow
        }));
    }

    // ����״̬��Ϣ�˵�
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
            app.Logger.LogError(ex, "��ȡ������Ϣʧ��");
            return Results.Problem("������Ϣ��ȡʧ��");
        }
    }).RequireRateLimiting("DefaultPolicy");
}