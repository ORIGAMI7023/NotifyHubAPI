using NotifyHubAPI.Services;
using NotifyHubAPI.Middleware;
using Serilog;
using AspNetCoreRateLimit;

var builder = WebApplication.CreateBuilder(args);

// ���� Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/notifyhub-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ���÷���
ConfigureServices(builder.Services, builder.Configuration);

var app = builder.Build();

// ����HTTP�ܵ�
ConfigurePipeline(app);

Log.Information("NotifyHubAPI �������: {Urls}", string.Join(", ", app.Urls));

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
    services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title = "NotifyHub API",
            Version = "v1.0",
            Description = "ͳһ�ʼ�֪ͨAPI - Ϊ�����Ŀ�ṩͳһ�ʼ�֪ͨ",
            Contact = new Microsoft.OpenApi.Models.OpenApiContact
            {
                Name = "NotifyHub Team",
                Email = "admin@notify.origami7023.cn"
            }
        });

        // API Key��֤Swagger
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

    // SMTP����
    services.Configure<SmtpSettings>(configuration.GetSection("SmtpSettings"));

    // ע����� - ʹ�ü򻯰汾���ʼ�����
    services.AddScoped<IEmailService, SimpleEmailService>();
    services.AddSingleton<IApiKeyService, ApiKeyService>();

    // �ڴ滺��
    services.AddMemoryCache();
    services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
    services.Configure<IpRateLimitPolicies>(configuration.GetSection("IpRateLimitPolicies"));
    services.AddInMemoryRateLimiting();
    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

    // ������� - �Ƴ����ݿ���
    services.AddHealthChecks()
        .AddCheck("smtp", () =>
        {
            try
            {
                var smtpSettings = configuration.GetSection("SmtpSettings").Get<SmtpSettings>();
                return !string.IsNullOrEmpty(smtpSettings?.Host)
                    ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("SMTP��������")
                    : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("SMTP����ȱʧ");
            }
            catch (Exception ex)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"SMTP���ü��ʧ��: {ex.Message}");
            }
        })
        .AddCheck("apikeys", () =>
        {
            try
            {
                var apiKeysSection = configuration.GetSection("ApiKeys");
                var apiKeyCount = apiKeysSection.GetChildren().Count();
                return apiKeyCount > 0
                    ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy($"����{apiKeyCount}��API��Կ")
                    : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("δ����API��Կ");
            }
            catch (Exception ex)
            {
                return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy($"API��Կ���ʧ��: {ex.Message}");
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
    // ������������
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "NotifyHub API v1");
            c.RoutePrefix = string.Empty; // ʹSwagger��Ϊ��·��
        });
    }
    else
    {
        app.UseExceptionHandler("/Error");
        app.UseHsts();
    }

    // �м���ܵ�
    app.UseHttpsRedirection();
    app.UseCors("AllowOrigins");

    // ��������
    app.UseIpRateLimiting();

    // �Զ����м��
    app.UseApiKeyAuthentication();

    // ·�ɺͿ�����
    app.UseRouting();
    app.UseAuthorization();
    app.MapControllers();

    // �������˵�
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });

    // Ĭ��·��
    app.MapGet("/", () => Results.Redirect("/swagger"));

    // ״̬��Ϣ�˵� - �򻯰汾
    app.MapGet("/info", async (IServiceProvider serviceProvider) =>
    {
        return Results.Ok(new
        {
            service = "NotifyHubAPI",
            version = "1.0.0 (Stateless)",
            environment = app.Environment.EnvironmentName,
            timestamp = DateTime.UtcNow,
            mode = "�����ݿ�ģʽ",
            status = "��������",
            features = new
            {
                emailSending = true,
                emailHistory = false,
                retryMechanism = false,
                persistence = false
            },
            message = "�ʼ����͹����������������淢�ͼ�¼"
        });
    });
}