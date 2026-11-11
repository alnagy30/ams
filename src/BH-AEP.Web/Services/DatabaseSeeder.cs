using BH_AEP.Application.Common;
using BH_AEP.Domain.Entities;
using BH_AEP.Domain.Enums;
using BH_AEP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Web.Services;

public class DatabaseSeeder
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;

    public DatabaseSeeder(AppDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    public async Task SeedAsync()
    {
        var hasUsers = await _db.Users.AnyAsync(u => !u.IsDeleted);
        if (!hasUsers)
        {
            _db.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = _hasher.Hash("Admin@123456"),
                Role = UserRole.Admin,
                ForcePasswordChange = true,
                CreatedAt = DateTime.Now
            });
        }

        if (!await _db.Categories.AnyAsync())
        {
            string[] names =
            {
                "أجهزة ومعدات تقنية",
                "أثاث ومفروشات",
                "معدات مكتبية",
                "مركبات",
                "أجهزة ومعدات طبية",
                "أجهزة ومعدات معملية",
                "أدوات ومعدات صناعية",
                "مبانٍ ومنشآت",
                "بنية تحتية وشبكات"
            };

            foreach (var name in names)
            {
                _db.Categories.Add(new Category { Name = name, ParentId = null, IsDeleted = false });
            }
        }

        if (!await _db.Departments.AnyAsync())
        {
            _db.Departments.Add(new Department { Name = "الإدارة العامة", ParentId = null });
        }

        if (!await _db.Locations.AnyAsync())
        {
            _db.Locations.Add(new Location { Name = "الموقع الرئيسي", ParentId = null });
        }

        await _db.SaveChangesAsync();
    }
}
