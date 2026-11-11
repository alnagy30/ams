export interface TreeNode {
  id: number
  name: string
  parentId: number | null
  isDeleted: boolean
  children: TreeNode[]
}

export interface AuthUser {
  id: number
  username: string
  role: string
  forcePasswordChange: boolean
  permissions: string[]
}

export interface Session {
  accessToken: string
  refreshToken: string
  accessTokenExpiresAt: string
  user: AuthUser
}

export interface Paged<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
}

export interface AssetListItem {
  id: number
  assetCode: string
  name: string
  categoryId: number
  categoryName: string
  subCategoryName?: string | null
  brand?: string | null
  model?: string | null
  serialNumber?: string | null
  status: string
  custodyType?: string | null
  custodianName?: string | null
  departmentName: string
  locationName: string
  purchaseValue?: number | null
  plateNumber?: string | null
  isVehicle: boolean
  createdAt: string
  updatedAt: string
}

export interface VehicleDetail {
  id?: number | null
  plateNumber?: string | null
  chassisNumber?: string | null
  engineNumber?: string | null
  manufactureYear?: number | null
  color?: string | null
  fuelType?: string | null
  licenseExpiryDate?: string | null
  conditionOnReceipt?: string | null
}

export interface Attachment {
  id: number
  attachmentType: string
  originalName: string
  fileSize: number
  mimeType: string
  uploadedAt: string
}

export interface AssetAccessory {
  accessoryId: number
  accessoryName: string
  quantity: number
  notes?: string | null
}

export interface AssetDetail {
  id: number
  assetCode: string
  name: string
  categoryId: number
  categoryName: string
  brand?: string | null
  model?: string | null
  serialNumber?: string | null
  purchaseDate?: string | null
  purchaseValue?: number | null
  supplierId?: number | null
  supplierName?: string | null
  locationId: number
  locationName: string
  departmentId: number
  departmentName: string
  custodianId?: number | null
  custodianName?: string | null
  custodyType?: string | null
  status: string
  notes?: string | null
  inspectionNumber?: string | null
  inspectionDate?: string | null
  isVehicle: boolean
  hasMissingData: boolean
  missingDataFields: string[]
  vehicle?: VehicleDetail | null
  attachments: Attachment[]
  accessories: AssetAccessory[]
}

export interface Employee {
  id: number
  fullName: string
  nationalId: string
  jobTitle?: string | null
  departmentId: number
  departmentName: string
  isDeleted: boolean
  createdAt: string
  heldAssetsCount: number
}

export interface CustodyRecord {
  id: number
  assetId: number
  assetCode: string
  assetName: string
  custodianId?: number | null
  custodianName?: string | null
  custodyType: string
  handoverDate: string
  returnDate?: string | null
  notes?: string | null
  createdByName: string
  isOpen: boolean
}

export interface TransferRecord {
  id: number
  assetId: number
  assetCode: string
  assetName: string
  fromDepartmentId?: number | null
  fromDepartmentName?: string | null
  toDepartmentId?: number | null
  toDepartmentName?: string | null
  fromLocationId?: number | null
  fromLocationName?: string | null
  toLocationId?: number | null
  toLocationName?: string | null
  transferDate: string
  reason?: string | null
  createdByName: string
  isBulk: boolean
}

export interface NotificationItem {
  id: number
  type: string
  relatedEntityType: string
  relatedEntityId: number
  message: string
  status: string
  createdAt: string
  resolvedBy?: number | null
  resolvedByName?: string | null
  resolvedAt?: string | null
}

export interface AuditEntry {
  id: number
  userId: number
  userName: string
  action: string
  entityType: string
  entityId?: number | null
  beforeData?: string | null
  afterData?: string | null
  timestamp: string
  ipAddress?: string | null
}

export interface Lookups {
  categories: TreeNode[]
  departments: TreeNode[]
  locations: TreeNode[]
  suppliers: { id: number; name: string; contactInfo?: string | null; isDeleted: boolean }[]
  accessories: { id: number; name: string; isLibraryItem: boolean }[]
}

export interface Supplier {
  id: number
  name: string
  contactInfo?: string | null
  isDeleted: boolean
}

export interface Accessory {
  id: number
  name: string
  isLibraryItem: boolean
}

export interface ImportBatch {
  id: number
  fileName: string
  totalRows: number
  successRows: number
  failedRows: number
  importedAt: string
  importedByName: string
}

export interface ReportField {
  key: string
  label: string
  dataType: string
  financial: boolean
}

export interface ReportTableDef {
  key: string
  label: string
  fields: ReportField[]
}

export interface ReportRow {
  values: (string | number | null)[]
}

export interface ReportResult {
  table: string
  columns: ReportField[]
  rows: ReportRow[]
}
