using Currere_backend.Data;
using Currere_backend.Hubs;
using Currere_backend.Middlewares;
using Currere_backend.Services;
using FluentValidation;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

Console.OutputEncoding = Encoding.UTF8;
// serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/currere-log-.txt", rollingInterval: RollingInterval.Day) // Her g?n yeni txt
    .CreateLogger();

try
{
    Log.Information("Currere API aya?a kalk?yor...");
    var builder = WebApplication.CreateBuilder(args);

    // ══════════════════════════════════════════════════════════════
    // GÜVENLİK KONTROLÜ (HARDCODED SECRETS KORUMASI)
    // ══════════════════════════════════════════════════════════════
    var jwtSecret = builder.Configuration["JwtSettings:Secret"];
    var encSecret = builder.Configuration["Encryption:SecretKey"];
    
    if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret == "DO_NOT_PUT_SECRETS_HERE" ||
        string.IsNullOrWhiteSpace(encSecret) || encSecret == "DO_NOT_PUT_SECRETS_HERE")
    {
        throw new Exception("KRİTİK GÜVENLİK İHLALİ: JwtSettings:Secret veya Encryption:SecretKey güvenli değil! Lütfen şifreleri 'dotnet user-secrets' veya Environment Variables üzerinden sağlayın.");
    }

    // varsay?lan log
    builder.Host.UseSerilog();

    // Db
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // DI's
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
    builder.Services.AddSingleton<IExecutionQueueService, ExecutionQueueService>();
builder.Services.AddHostedService<ExecutionWorker>();
builder.Services.AddHostedService<SystemMaintenanceWorker>();
builder.Services.AddHostedService<KernelReaperWorker>(); // Zombi kernel avcısı
builder.Services.AddScoped<ICodePreProcessorService, CodePreProcessorService>();
builder.Services.AddScoped<ICodeExecutionService, CodeExecutionService>();
    builder.Services.AddScoped<IFileService, FileService>();
    builder.Services.AddScoped<IProfileJobService, ProfileJobService>(); // Hangfire BackgroundJob
    builder.Services.AddScoped<FileCleanupService>(); // Hangfire BackgroundJob
    builder.Services.AddScoped<IDatasetProfilerService, DatasetProfilerService>(); // dataset okuyarak baglam kurma, cikarim yapma
    builder.Services.AddValidatorsFromAssemblyContaining<Program>(); // validatr
    builder.Services.AddHttpClient<IAiService, GroqAiService>(); // groq
    builder.Services.AddScoped<INotebookConverterService, NotebookConverterService>(); // ipynb to py
    builder.Services.AddSingleton<IEncryptionService, EncryptionService>(); // api kriptotalama
    builder.Services.AddHttpClient<IKaggleService, KaggleService>(); // kaggle
    builder.Services.AddScoped<IWorkspaceSnapshotService, WorkspaceSnapshotService>(); // snapschot
    builder.Services.AddScoped<ISyntheticDataService, SyntheticDataService>(); // syntetic data dataset
    
    // ══════════════════════════════════════════════════════════════
    // SEMANTIC KERNEL DİNAMİK MOTOR (ENGINE) YAPILANDIRMASI
    // ══════════════════════════════════════════════════════════════
    builder.Services.AddKeyedScoped<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService, Currere_backend.Agents.Core.HybridChatCompletionService>("auto");

    builder.Services.AddKernel()
        .AddOpenAIChatCompletion(
            modelId: "llama-3.3-70b-versatile",
            apiKey: builder.Configuration["AiSettings:GroqApiKey"] ?? "IGNORE",
            endpoint: new Uri("https://api.groq.com/openai/v1"),
            serviceId: "groq"
        )
#pragma warning disable SKEXP0070 // Ollama is experimental in SK
        .AddOllamaChatCompletion(
            modelId: "llama3", 
            endpoint: new Uri("http://localhost:11434"),
            serviceId: "ollama"
        );
#pragma warning restore SKEXP0070

    // Orchestrator ve Pluginler
    builder.Services.AddScoped<Currere_backend.Agents.Plugins.DockerExecutionPlugin>();
    builder.Services.AddScoped<Currere_backend.Agents.Core.AgentOrchestrator>();

    builder.Services.AddSingleton<KernelManagerService>(); // Jupyter stateful kernel yönetimi
    builder.Services.AddHttpClient<IHuggingFaceService, HuggingFaceService>(); // huggingface model



    builder.Services.AddMemoryCache();
    builder.Services.AddSignalR(); // frontend signalR
    builder.Services.AddControllers();

    // CORS Configuration — İkili Politika: AllowAll (Frontend) + ExtensionPolicy (VS Code & CLI)
    builder.Services.AddCors(options =>
    {
        // Genel frontend politikası (Next.js dev & production)
        options.AddPolicy("AllowAll",
            corsBuilder => corsBuilder
                .WithOrigins(
                    "http://localhost:3000",    // Next.js dev server
                    "http://localhost:5279",    // Backend self-reference
                    "https://currere.app"       // Production domain (ileride)
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());

        // VS Code Extension & CLI için esnek politika
        // vscode-webview:// origin ve localhost tüm portları kabul eder
        options.AddPolicy("ExtensionPolicy",
            corsBuilder => corsBuilder
                .SetIsOriginAllowed(origin =>
                {
                    if (string.IsNullOrEmpty(origin)) return false;
                    if (origin.StartsWith("vscode-webview://")) return true;
                    try { return new Uri(origin).Host == "localhost"; }
                    catch { return false; }
                })
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
    });

    // Rate limiting — Tüm kurallar tek GlobalLimiter içinde, bypass dahil
    var testBypassSecret = builder.Configuration["TestSettings:RateLimitBypassSecret"] ?? "";
    builder.Services.AddRateLimiter(options =>
    {
        // ══ EKLENDİ: Controller seviyesinde kullanılan "AiStrictLimit" politikası ══
        options.AddPolicy("AiStrictLimit", httpContext =>
        {
            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(
                $"AiStrictLimit-{ip}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            // ═══ TEST BYPASS — Doğru secret gelirse rate limit tamamen devre dışı ═══
            var headerSecret = httpContext.Request.Headers["X-Currere-Test-Secret"].FirstOrDefault();
            if (!string.IsNullOrEmpty(testBypassSecret) &&
                !string.IsNullOrEmpty(headerSecret) &&
                headerSecret == testBypassSecret)
            {
                return RateLimitPartition.GetNoLimiter<string>("test-bypass");
            }

            var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var path = httpContext.Request.Path.Value?.ToLower() ?? "";

            // Kernel execute — dakikada 20 istek
            if (path.Contains("/kernel/") && path.Contains("/execute"))
            {
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"kernel-{ip}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20, Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0, AutoReplenishment = true,
                        QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst
                    });
            }

            // Sentetik veri — 5 dakikada 3 istek
            if (path.Contains("/syntheticdata/"))
            {
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"synthetic-{ip}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 3, Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0, AutoReplenishment = true,
                        QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst
                    });
            }

            // HuggingFace — dakikada 5 istek
            if (path.Contains("/huggingface/"))
            {
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"hf-{ip}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5, Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0, AutoReplenishment = true,
                        QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst
                    });
            }

            // AI endpoint — dakikada 5 istek
            if (path.Contains("/ai/"))
            {
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"ai-{ip}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5, Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0, AutoReplenishment = true,
                        QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst
                    });
            }

            // Genel — dakikada 60 istek
            return RateLimitPartition.GetFixedWindowLimiter(
                ip,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60, Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0, AutoReplenishment = true,
                    QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst
                });
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // Swagger JWT
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Token'?n?z? buraya yap??t?r?n. ?rnek: Bearer {token}",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                new string[] {}
            }
        });

        // documentation
        var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename));
    });

    // JWT Doğrulama
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                    builder.Configuration.GetSection("JwtSettings:Secret").Value!)),
                ValidateIssuer = true,
                ValidIssuer = builder.Configuration.GetSection("JwtSettings:Issuer").Value,
                ValidateAudience = true,
                ValidAudience = builder.Configuration.GetSection("JwtSettings:Audience").Value,
                ValidateLifetime = true
            };

            // SignalR WebSocket JWT desteği: query string'den token okuma
            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;

                    if (!string.IsNullOrEmpty(accessToken) &&
                        (path.StartsWithSegments("/syncHub") || path.StartsWithSegments("/terminalHub")))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });

    // hangfire oto dosya silme
    builder.Services.AddHangfire(config => config.UseMemoryStorage());
    builder.Services.AddHangfireServer();

    var app = builder.Build();

    // hangfire ile expire s?resi dlanlar? sil
    // kapsam hatas? olustu scope kapsam?na ekledim
    using (var scope = app.Services.CreateScope())
    {
        var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<FileCleanupService>(
            "expired-file-cleanup",
            service => service.CleanupExpiredFilesAsync(),
            "*/10 * * * *"
        );
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    
    // Routing, RateLimiter'ın endpoint etiketlerini ([EnableRateLimiting]) okuyabilmesi için ÖNCE çağrılmalıdır!
    app.UseRouting();

    // CORS'u UseRouting veya RateLimiter'dan hemen önce kullanıyoruz.
    app.UseCors("AllowAll");

    // ratelimiter before auth
    app.UseRateLimiter();

    app.UseMiddleware<ExceptionMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapHub<TerminalHub>("/terminalHub").RequireCors("AllowAll"); // frontend signalR
    app.MapHub<SyncHub>("/syncHub").RequireCors("ExtensionPolicy"); // VS Code Sync SignalR

    app.Run();
}
catch (Exception ex) when (ex.GetType().Name is not "HostAbortedException" and not "StopTheHostException")
{
    // ??kerse
    Log.Fatal(ex, "API ba?lat?lamad?, kritik bir ??kme ya?and?!");
}
finally
{
    Log.CloseAndFlush();
}
