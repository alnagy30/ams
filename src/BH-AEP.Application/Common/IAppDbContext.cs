using BH_AEP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace BH_AEP.Application.Common;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<TemporaryPermission> TemporaryPermissions { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Department> Departments { get; }
    DbSet<Category> Categories { get; }
    DbSet<Location> Locations { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<Asset> Assets { get; }
    DbSet<VehicleDetail> VehicleDetails { get; }
    DbSet<AssetAttachment> AssetAttachments { get; }
    DbSet<Accessory> Accessories { get; }
    DbSet<AssetAccessory> AssetAccessories { get; }
    DbSet<Custody> Custodies { get; }
    DbSet<Transfer> Transfers { get; }
    DbSet<InventoryCampaign> InventoryCampaigns { get; }
    DbSet<InventoryItem> InventoryItems { get; }
    DbSet<ScrapRequest> ScrapRequests { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<OrgStructureHistory> OrgStructureHistory { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<BackupLog> BackupLogs { get; }
    DbSet<ImportBatch> ImportBatches { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
