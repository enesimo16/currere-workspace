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

    // varsay?lan log
    builder.Host.UseSerilog();

    // Db
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // DI's
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IWorkspaceService, WorkspaceService>();
    builder.Services.AddSingleton<IExecutionQueueService, ExecutionQueueService>();
// builder.Services.AddHostedService<ExecutionWorker>(); // Şimdilik Worker'ı susturduk (Sync Execution Test)
builder.Services.AddHostedService<SystemMaintenanceWorker>();
builder.Services.AddScoped<ICodePreProcessorService, CodePreProcessorService>();
builder.Services.AddScoped<ICodeExecutionService, CodeExecutionService>();
    builder.Services.AddScoped<IFileService, FileService>();
    builder.Services.AddScoped<IDatasetProfilerService, DatasetProfilerService>(); // dataset okuyarak baglam kurma, cikarim yapma
    builder.Services.AddValidatorsFromAssemblyContaining<Program>(); // validatr
    builder.Services.AddHttpClient<IAiService, GroqAiService>(); // groq
    builder.Services.AddScoped<INotebookConverterService, NotebookConverterService>(); // ipynb to py
    builder.Services.AddSingleton<IEncryptionService, EncryptionService>(); // api kriptotalama
    builder.Services.AddHttpClient<IKaggleService, KaggleService>(); // kaggle
    builder.Services.AddScoped<IWorkspaceSnapshotService, WorkspaceSnapshotService>(); // snapschot
    builder.Services.AddScoped<ISyntheticDataService, SyntheticDataService>(); // syntetic data dataset
    builder.Services.AddHttpClient<IHuggingFaceService, HuggingFaceService>(); // huggingface model



    builder.Services.AddSignalR(); // frontend signalR
    builder.Services.AddControllers();

    // CORS Configuration (Frontend & Extension Webview Support)
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll",
            corsBuilder => corsBuilder
                .SetIsOriginAllowed(origin => true) // SignalR with AllowCredentials requires this instead of AllowAnyOrigin
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
    });

    // RAate limiting
    builder.Services.AddRateLimiter(options =>
    {
        // ip tabanl? rate limiting dakkada 60 istek
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: partition => new FixedWindowRateLimiterOptions
                {
                    AutoReplenishment = true,
                    PermitLimit = 60,
                    QueueLimit = 2,
                    Window = TimeSpan.FromMinutes(1)
                }));

        // Sadece AI Endpointleri i?in ?ok kat? limit , dakikada 5 istek
        options.AddFixedWindowLimiter("AiStrictLimit", opt =>
        {
            opt.PermitLimit = 5;
            opt.Window = TimeSpan.FromMinutes(1);
            opt.QueueLimit = 0;
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests; // a??l?rsa -> 429
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

    // JWT Do?rulama
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
    
    // CORS'u UseRouting veya RateLimiter'dan hemen önce kullanıyoruz.
    app.UseCors("AllowAll");

    // ratelimiter before auth
    app.UseRateLimiter();

    app.UseMiddleware<ExceptionMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.MapHub<TerminalHub>("/terminalHub"); // frontend signalR

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
