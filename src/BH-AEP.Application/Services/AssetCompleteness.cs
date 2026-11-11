using BH_AEP.Domain.Entities;

namespace BH_AEP.Application.Services;

public static class AssetCompleteness
{
    public static IReadOnlyList<string> EvaluateMissingFields(Asset asset, bool hasAnyAttachment, bool hasOpenCustody)
    {
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(asset.SerialNumber))
            missing.Add("السيريال");

        if (asset.SupplierId is null)
            missing.Add("المورد");

        if (asset.PurchaseValue is null)
            missing.Add("القيمة");

        if (!hasAnyAttachment)
            missing.Add("الصورة");

        if (!hasOpenCustody)
            missing.Add("العهدة");

        return missing;
    }
}
