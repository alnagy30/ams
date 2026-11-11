<#
============================================================
  BH-AEP  -  سكربت النشر لنظام التشغيل Windows
============================================================
  يبني المشروع وينشره كخدمة Windows واحدة (API + واجهة React
  تُقدَّم من نفس العملية) مع عزل ملفات البيانات خارج مجلد
  Program Files.

  الاستخدام (من مجلد المشروع، بصلاحية مسؤول):
    powershell -ExecutionPolicy Bypass -File deploy\Deploy.ps1

  الخيارات الشائعة:
    -DataFolder  "C:\ProgramData\BH-AEP"   مجلد البيانات (الافتراضي)
    -Port        5000                      منفذ الاستماع
    -SelfContained                         نشر ذاتي دون الحاجة لتثبيت .NET
    -SkipService                           عدم التسجيل كخدمة (تشغيل يدوي)
    -RunAfterInstall                       تشغيل الخدمة فوراً

  المتطلبات:
    - Windows 10/11 أو Windows Server 2019+
    - تثبيت .NET SDK 8 على جهاز التطوير لبناء المشروع
    - إن لم يُستعمل -SelfContained فيجب تثبيت "ASP.NET Core Runtime 8"
      على خادم الإنتاج من https://dotnet.microsoft.com
    - صلاحية مسؤول للتسجيل كخدمة وفتح منفذ جدار الحماية
============================================================
#>

[CmdletBinding()]
param(
    [string]$DataFolder   = "C:\ProgramData\BH-AEP",
    [int]$Port            = 5000,
    [string]$ServiceName  = "BH-AEP",
    [string]$PublishRoot  = "$PSScriptRoot\..\publish",
    [switch]$SelfContained,
    [switch]$SkipService,
    [switch]$RunAfterInstall
)

$ErrorActionPreference = "Stop"
$repoRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$webProject = Join-Path $repoRoot "src\BH-AEP.Web\BH-AEP.Web.csproj"
$toolProject = Join-Path $repoRoot "tools\ResetAdminPassword\BH-AEP.ResetAdmin.csproj"
$appFolder = Join-Path $PublishRoot "app"
$dbFile = Join-Path $DataFolder "data\bh-aep.db"

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "  نشر منظومة BH-AEP" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

# 1) التحقق من أدوات البناء
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { throw "لم يتم العثور على .NET SDK. ثبّته من https://dotnet.microsoft.com ثم أعد المحاولة." }

# 2) البناء والنشر
$rid = if ($SelfContained) { "-r win-x64 --self-contained true" } else { "--self-contained false" }
Write-Host "`n[1/5] بناء ونشر تطبيق الويب..." -ForegroundColor Yellow
& $dotnet publish $webProject -c Release $rid -o $appFolder
if ($LASTEXITCODE -ne 0) { throw "فشل نشر تطبيق الويب." }

Write-Host "[2/5] بناء أداة إعادة تعيين كلمة مرور الأدمن..." -ForegroundColor Yellow
& $dotnet publish $toolProject -c Release $rid -o (Join-Path $PublishRoot "tools\ResetAdminPassword")
if ($LASTEXITCODE -ne 0) { throw "فشل نشر أداة إعادة التعيين." }

# 3) تجهيز مجلد البيانات (خارج Program Files)
Write-Host "[3/5] تجهيز مجلد البيانات في: $DataFolder" -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path (Join-Path $DataFolder "data") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataFolder "data\backups") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataFolder "data\attachments") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $DataFolder "data\logs") | Out-Null

# نقل نسخة قاعدة البيانات الحالية إن وُجدت عند أول نشر
if ((Test-Path $dbFile) -eq $false) {
    $seedDb = Join-Path $repoRoot "data\bh-aep.db"
    if (Test-Path $seedDb) {
        Copy-Item $seedDb $dbFile -Force
        Write-Host "  تم نسخ قاعدة البيانات الابتدائية." -ForegroundColor Green
    }
}

# ملف إعدادات يحدد مسار البيانات ومنفذ الاستماع
$runtimeJson = @{
    ConnectionStrings = @{ Default = "Data Source=$dbFile;Cache=Shared" }
    Storage = @{
        DataRoot   = (Join-Path $DataFolder "data")
        BackupRetentionDays = 30
    }
    Kestrel = @{
        Endpoints = @{
            Http = @{ Url = "http://0.0.0.0:$Port" }
        }
    }
} | ConvertTo-Json -Depth 6

$jsonPath = Join-Path $appFolder "appsettings.Production.json"
Set-Content -Path $jsonPath -Value $runtimeJson -Encoding UTF8
Write-Host "  تم إنشاء: appsettings.Production.json" -ForegroundColor Green

# 4) التسجيل كخدمة Windows
$exePath = Join-Path $appFolder "BH-AEP.Web.exe"
$args = "`"$exePath`" --environment Production"
$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existing) {
    Write-Host "[4/5] تحديث الخدمة $ServiceName ..." -ForegroundColor Yellow
    Stop-Service $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe config $ServiceName binPath= $args | Out-Null
    Start-Sleep -Seconds 2
    if ($LASTEXITCODE -ne 0) { throw "فشل تحديث إعدادات الخدمة." }
} else {
    if (-not $SkipService) {
        Write-Host "[4/5] تسجيل الخدمة $ServiceName ..." -ForegroundColor Yellow
        New-Service -Name $ServiceName -DisplayName "منظومة الأصول الثابتة BH-AEP" `
            -BinaryPathName $args -StartupType Automatic | Out-Null
    } else {
        Write-Host "[4/5] تم تخطي تسجيل الخدمة (-SkipService)." -ForegroundColor Yellow
    }
}

# 5) فتح منفذ جدار الحماية
Write-Host "[5/5] إعداد جدار الحماية للمنفذ $Port ..." -ForegroundColor Yellow
$ruleName = "BH-AEP Port $Port"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow `
        -Protocol TCP -LocalPort $Port | Out-Null
}

# التشغيل
if ($existing) { Start-Service $ServiceName; Write-Host "  تمت إعادة تشغيل الخدمة." -ForegroundColor Green }
if ($RunAfterInstall -and -not $existing -and -not $SkipService) {
    Start-Service $ServiceName
}

Write-Host "`n==============================================" -ForegroundColor Cyan
Write-Host "  تم النشر بنجاح" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "مسار التطبيق : $appFolder"
Write-Host "مجلد البيانات: $DataFolder"
Write-Host "رابط الدخول  : http://<ip-هذا-الجهاز>:$Port"
Write-Host "الدخول الافتراضي: admin / Admin@123456  (سيُطلب تغييرها)"
if (-not $SkipService) {
    Write-Host "إدارة الخدمة : Get-Service $ServiceName | Start-Service / Restart-Service / Stop-Service"
}
Write-Host "إعادة تعيين كلمة مرور الأدمن عند نسيانها:"
Write-Host "  $(Join-Path $PublishRoot 'tools\ResetAdminPassword\ResetAdminPassword.exe') --db `"$dbFile`" --username admin"
Write-Host "==============================================" -ForegroundColor Cyan
