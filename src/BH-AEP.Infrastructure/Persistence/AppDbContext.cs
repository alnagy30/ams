using BH_AEP.Application.Common;
using BH_AEP.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BH_AEP.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<TemporaryPermission> TemporaryPermissions => Set<TemporaryPermission>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<VehicleDetail> VehicleDetails => Set<VehicleDetail>();
    public DbSet<AssetAttachment> AssetAttachments => Set<AssetAttachment>();
    public DbSet<Accessory> Accessories => Set<Accessory>();
    public DbSet<AssetAccessory> AssetAccessories => Set<AssetAccessory>();
    public DbSet<Custody> Custodies => Set<Custody>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<InventoryCampaign> InventoryCampaigns => Set<InventoryCampaign>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<ScrapRequest> ScrapRequests => Set<ScrapRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<OrgStructureHistory> OrgStructureHistory => Set<OrgStructureHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<BackupLog> BackupLogs => Set<BackupLog>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ApplyEnumConversions(modelBuilder);
        ApplyIndexes(modelBuilder);
        ApplyRelationships(modelBuilder);
    }

    private static void ApplyEnumConversions(ModelBuilder modelBuilder)
    {
        var converterType = typeof(EnumToStringConverter<>);
        var entityTypes = modelBuilder.Model.GetEntityTypes();
        foreach (var entityType in entityTypes)
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType.IsEnum)
                {
                    var converter = Activator.CreateInstance(
                        converterType.MakeGenericType(property.ClrType))!;
                    property.SetValueConverter((ValueConverter)converter);
                }
            }
        }
    }

    private static void ApplyIndexes(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Username).IsUnique().HasFilter("\"IsDeleted\" = 0");
        b.Entity<Employee>().HasIndex(e => e.NationalId).IsUnique().HasFilter("\"IsDeleted\" = 0");
        b.Entity<Asset>().HasIndex(a => a.AssetCode).IsUnique();
        b.Entity<Asset>().HasIndex(a => new { a.Status, a.IsDeleted });
        b.Entity<Asset>().HasIndex(a => a.DepartmentId);
        b.Entity<Asset>().HasIndex(a => a.LocationId);
        b.Entity<VehicleDetail>().HasIndex(v => v.PlateNumber).IsUnique();
        b.Entity<VehicleDetail>().HasIndex(v => v.ChassisNumber).IsUnique();
        b.Entity<VehicleDetail>().HasIndex(v => v.AssetId).IsUnique();
        b.Entity<AssetAttachment>().HasIndex(x => x.StoredName).IsUnique();
        b.Entity<AssetAttachment>().HasIndex(x => x.AssetId);
        b.Entity<Custody>().HasIndex(c => new { c.AssetId, c.ReturnDate });
        b.Entity<Custody>().HasIndex(c => new { c.CustodianId, c.ReturnDate });
        b.Entity<Transfer>().HasIndex(t => t.AssetId);
        b.Entity<Transfer>().HasIndex(t => new { t.FromDepartmentId, t.ToDepartmentId });
        b.Entity<InventoryItem>().HasIndex(i => new { i.CampaignId, i.AssetId }).IsUnique();
        b.Entity<ScrapRequest>().HasIndex(s => s.AssetId);
        b.Entity<Notification>().HasIndex(n => new { n.Type, n.Status });
        b.Entity<AuditLog>().HasIndex(l => new { l.EntityType, l.EntityId });
        b.Entity<AuditLog>().HasIndex(l => l.Timestamp);
        b.Entity<OrgStructureHistory>().HasIndex(h => h.DepartmentId);
    }

    private static void ApplyRelationships(ModelBuilder b)
    {
        var restrict = DeleteBehavior.Restrict;

        b.Entity<Asset>()
            .HasOne(a => a.Category).WithMany()
            .HasForeignKey(a => a.CategoryId).OnDelete(restrict);
        b.Entity<Asset>()
            .HasOne(a => a.Department).WithMany()
            .HasForeignKey(a => a.DepartmentId).OnDelete(restrict);
        b.Entity<Asset>()
            .HasOne(a => a.Location).WithMany()
            .HasForeignKey(a => a.LocationId).OnDelete(restrict);
        b.Entity<Asset>()
            .HasOne(a => a.Supplier).WithMany()
            .HasForeignKey(a => a.SupplierId).OnDelete(restrict);
        b.Entity<Asset>()
            .HasOne(a => a.Custodian).WithMany()
            .HasForeignKey(a => a.CustodianId).OnDelete(restrict);

        b.Entity<VehicleDetail>()
            .HasOne(v => v.Asset).WithOne(a => a.Vehicle)
            .HasForeignKey<VehicleDetail>(v => v.AssetId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<AssetAttachment>()
            .HasOne(x => x.Asset).WithMany(a => a.Attachments)
            .HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Employee>()
            .HasOne(e => e.Department).WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId).OnDelete(restrict);

        b.Entity<Department>()
            .HasOne(d => d.Parent).WithMany(d => d.Children)
            .HasForeignKey(d => d.ParentId).OnDelete(restrict);

        b.Entity<Category>()
            .HasOne(c => c.Parent).WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentId).OnDelete(restrict);

        b.Entity<Location>()
            .HasOne(l => l.Parent).WithMany(l => l.Children)
            .HasForeignKey(l => l.ParentId).OnDelete(restrict);

        b.Entity<Custody>()
            .HasOne(c => c.Asset).WithMany(a => a.Custodies)
            .HasForeignKey(c => c.AssetId).OnDelete(restrict);
        b.Entity<Custody>()
            .HasOne(c => c.Custodian).WithMany()
            .HasForeignKey(c => c.CustodianId).OnDelete(restrict);

        b.Entity<Transfer>()
            .HasOne(t => t.Asset).WithMany()
            .HasForeignKey(t => t.AssetId).OnDelete(restrict);

        b.Entity<InventoryCampaign>()
            .HasOne(c => c.Department).WithMany()
            .HasForeignKey(c => c.DepartmentId).OnDelete(restrict);

        b.Entity<InventoryItem>()
            .HasOne(i => i.Campaign).WithMany(c => c.Items)
            .HasForeignKey(i => i.CampaignId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<InventoryItem>()
            .HasOne(i => i.Asset).WithMany()
            .HasForeignKey(i => i.AssetId).OnDelete(restrict);

        b.Entity<ScrapRequest>()
            .HasOne(s => s.Asset).WithMany()
            .HasForeignKey(s => s.AssetId).OnDelete(restrict);

        b.Entity<AssetAccessory>()
            .HasOne(aa => aa.Asset).WithMany(a => a.AssetAccessories)
            .HasForeignKey(aa => aa.AssetId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<AssetAccessory>()
            .HasOne(aa => aa.Accessory).WithMany()
            .HasForeignKey(aa => aa.AccessoryId).OnDelete(restrict);
        b.Entity<AssetAccessory>().HasIndex(aa => new { aa.AssetId, aa.AccessoryId }).IsUnique();

        b.Entity<TemporaryPermission>()
            .HasOne(tp => tp.User).WithMany()
            .HasForeignKey(tp => tp.UserId).OnDelete(restrict);
    }
}
