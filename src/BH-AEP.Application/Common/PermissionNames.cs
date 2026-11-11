namespace BH_AEP.Application.Common;

public static class PermissionNames
{
    public const string Dashboard = "Dashboard";
    public const string ManageUsers = "ManageUsers";
    public const string ManageEmployees = "ManageEmployees";
    public const string ReferenceData = "ReferenceData";
    public const string Notifications = "Notifications";
    public const string Backup = "Backup";
    public const string Import = "Import";
    public const string BulkTransfer = "BulkTransfer";
    public const string FinancialValue = "FinancialValue";
    public const string AssetEditLog = "AssetEditLog";
    public const string TransferHistory = "TransferHistory";
    public const string InventoryHistory = "InventoryHistory";
    public const string ScrapHistory = "ScrapHistory";
    public const string AuditLogs = "AuditLogs";
    public const string OrgStructureHistory = "OrgStructureHistory";

    public static readonly string[] AllAdminPermissions =
    {
        Dashboard, ManageUsers, ManageEmployees, ReferenceData, Notifications,
        Backup, Import, BulkTransfer, FinancialValue, AssetEditLog,
        TransferHistory, InventoryHistory, ScrapHistory, AuditLogs, OrgStructureHistory
    };
}
