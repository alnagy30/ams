using System.Text;
using BH_AEP.Application;
using BH_AEP.Application.Common;
using BH_AEP.Infrastructure;
using BH_AEP.Infrastructure.Jobs;
using BH_AEP.Infrastructure.Persistence;
using BH_AEP.Infrastructure.Services;
using BH_AEP.Web.Middleware;
using BH_AEP.Web.Services;
using Hangfire;
using Hangfire.Storage.SQLite;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File(Path.Combine(builder.Configuration["Storage:DataRoot"] ?? "data", "logs", "app-.log"),
        rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();
builder.Host.UseSerilog();

var storageOptions = new InfrastructureOptions
{
    ConnectionString = builder.Configuration.GetConnectionString("Default")!,
    DataRoot = Path.GetFullPath(builder.Configuration["Storage:DataRoot"] ?? "data"),
    BackupRetentionDays = builder.Configuration.GetValue("Storage:BackupRetentionDays", 30)
};

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUserService>();
builder.Services.AddScoped<DatabaseSeeder>();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructure(storageOptions);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwt = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DictionaryKeyPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddCors(o => o.AddPolicy("Lan", p => p
    .AllowAnyOrigin()
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// Hangfire (jobs on the same process; SQLite storage)
var hangfireDb = Path.Combine(storageOptions.DataRoot, "hangfire.db");
builder.Services.AddHangfire(c => c.UseSQLiteStorage(hangfireDb));
builder.Services.AddHangfireServer();

builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BH-AEP API",
        Version = "v1",
        Description = "منظومة إدارة الأصول الثابتة"
    });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
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

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

// تقديم واجهة الويب (React) إن وُجدت ضمن wwwroot - يسمح بنشر عملية واحدة (API + UI)
var wwwrootIndex = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "index.html");
var hasSpa = File.Exists(wwwrootIndex);
if (hasSpa)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// تهيئة قاعدة البيانات تلقائيًا
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedAsync();
}

// جدولة المهام المتكررة
using (var scope = app.Services.CreateScope())
{
    var recurring = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurring.AddOrUpdate<ScheduledJobs>("daily-scans", j => j.DailyScansAsync(CancellationToken.None), Cron.Daily(3, 0));
    recurring.AddOrUpdate<ScheduledJobs>("weekly-backup", j => j.WeeklyBackupAsync(CancellationToken.None), Cron.Weekly(DayOfWeek.Friday, 22, 0));
    recurring.AddOrUpdate<ScheduledJobs>("monthly-archival", j => j.MonthlyArchivalAsync(CancellationToken.None), Cron.Monthly(1, 3, 0));
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Lan");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// توجيه مسارات الواجهة (غير مسارات API) إلى index.html لدعم التنقل المباشر في SPA
if (hasSpa)
    app.MapFallbackToFile("{*path:regex(^(?!api|hubs).*$)}", "index.html");

app.MapGet("/", () => Results.Ok(new { name = "BH-AEP API", status = "running", time = DateTime.Now }));

await app.RunAsync();

public partial class Program
{
}
