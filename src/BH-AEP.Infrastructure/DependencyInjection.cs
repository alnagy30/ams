using BH_AEP.Application.Common;
using BH_AEP.Infrastructure.Jobs;
using BH_AEP.Infrastructure.Persistence;
using BH_AEP.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BH_AEP.Infrastructure;

public class InfrastructureOptions
{
    public string ConnectionString { get; set; } = "";
    public string DataRoot { get; set; } = "data";
    public int BackupRetentionDays { get; set; } = 30;

    public string DatabasePath => Path.Combine(DataRoot, "bh-aep.db");
    public string AttachmentsDirectory => Path.Combine(DataRoot, "attachments");
    public string BackupsDirectory => Path.Combine(DataRoot, "backups");
    public string LogsDirectory => Path.Combine(DataRoot, "logs");
}

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        InfrastructureOptions options)
    {
        var dataRoot = Path.GetFullPath(options.DataRoot);
        Directory.CreateDirectory(dataRoot);
        Directory.CreateDirectory(Path.Combine(dataRoot, "attachments"));
        Directory.CreateDirectory(Path.Combine(dataRoot, "backups"));
        Directory.CreateDirectory(Path.Combine(dataRoot, "logs"));

        services.AddDbContext<AppDbContext>(db =>
            db.UseSqlite(options.ConnectionString, sqlite => sqlite.CommandTimeout(120)));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        var storagePaths = new StoragePaths
        {
            Root = dataRoot,
            DatabaseName = "bh-aep.db",
            BackupRetentionDays = options.BackupRetentionDays
        };
        services.AddSingleton(Options.Create(storagePaths));
        services.AddSingleton(storagePaths);
        services.AddSingleton(Options.Create(new FileStorageOptions
        {
            RootPath = Path.Combine(dataRoot, "attachments")
        }));
        services.AddSingleton(new FileStorageOptions { RootPath = Path.Combine(dataRoot, "attachments") });

        services.AddScoped<IFileStorage, FileStorage>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IQrService, QrService>();
        services.AddScoped<IExcelWriter, ClosedXmlExcelWriter>();
        services.AddScoped<IExcelImportParser, ClosedXmlImportParser>();
        services.AddScoped<IPdfExportService, QuestPdfService>();
        services.AddScoped<IAssetCardPdfService, QuestPdfService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<ArchivalService>();
        services.AddScoped<ScheduledJobs>();

        return services;
    }
}
