using BH_AEP.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace BH_AEP.Application.Services;

public interface IAssetCodeGenerator
{
    Task<string> NextAsync(CancellationToken ct = default);
}

public class AssetCodeGenerator : IAssetCodeGenerator
{
    private readonly IAppDbContext _db;

    public AssetCodeGenerator(IAppDbContext db)
    {
        _db = db;
    }

    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        var last = await _db.Assets
            .OrderByDescending(a => a.Id)
            .Select(a => a.AssetCode)
            .FirstOrDefaultAsync(ct);

        int sequence = 0;
        if (!string.IsNullOrEmpty(last) && last.StartsWith("AST-"))
        {
            if (int.TryParse(last[4..], out var parsed))
                sequence = parsed;
        }

        var next = sequence + 1;
        string candidate;
        do
        {
            candidate = $"AST-{next:D6}";
            next++;
        } while (await _db.Assets.AnyAsync(a => a.AssetCode == candidate, ct));

        return candidate;
    }
}
