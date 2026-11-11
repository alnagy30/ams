using BH_AEP.Application.Common;
using BH_AEP.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BH_AEP.Application;

public static class ApplicationDependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IAssetCodeGenerator, AssetCodeGenerator>();

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationScanService, NotificationScanService>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<IAssetService, AssetService>();
        services.AddScoped<IAssetDocumentService, AssetDocumentService>();
        services.AddScoped<ICustodyService, CustodyService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IScrapService, ScrapService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IImportService, ImportService>();

        return services;
    }
}
