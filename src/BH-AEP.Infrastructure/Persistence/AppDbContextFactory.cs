using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BH_AEP.Infrastructure.Persistence;

/// <summary>Factory used only at design-time by `dotnet ef` (avoids booting the Web host).</summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var baseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../BH-AEP"));
        var dataDir = Path.Combine(baseDir, "data");
        Directory.CreateDirectory(dataDir);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={Path.Combine(dataDir, "bh-aep.db")};Cache=Shared")
            .Options;

        return new AppDbContext(options);
    }
}
