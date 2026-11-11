namespace BH_AEP.Domain.Enums;

public enum UserRole
{
    Admin,
    AssetManager,
    Viewer
}

public enum AssetStatus
{
    Active,
    UnderMaintenance,
    InStorage,
    Scrapped,
    Missing,
    Suspended
}

public enum CustodyType
{
    Individual,
    DepartmentGeneral
}

public enum AttachmentType
{
    Image,
    Invoice,
    Document
}

public enum InventoryCampaignStatus
{
    Planned,
    InProgress,
    Completed,
    Cancelled
}

public enum ScrapRequestStatus
{
    Pending,
    Approved,
    Rejected
}

public enum NotificationStatus
{
    Open,
    Resolved
}

public enum NotificationType
{
    VehicleLicenseExpiry,
    InventoryDeadline,
    FailedLogin,
    MissingData,
    InventoryMismatch,
    UserLockedOut,
    Generic
}
