<#
.SYNOPSIS
    Verilen .bak dosyasini finara-mssql konteynerindeki FinaraDb veritabanina geri yukler (WITH REPLACE).
    DIKKAT: Hedef veritabaninin uzerine yazar.

.EXAMPLE
    ./restore-db.ps1 -BakFile ./backups/FinaraDb_20260727_153000.bak
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$BakFile,
    [string]$ContainerName = "finara-mssql",
    [string]$Database = "FinaraDb"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BakFile)) {
    throw "Dosya bulunamadi: $BakFile"
}

$saPassword = (Get-Content "$PSScriptRoot/.env" | Where-Object { $_ -match "^MSSQL_SA_PASSWORD=" }) -replace "^MSSQL_SA_PASSWORD=", ""
if (-not $saPassword) {
    throw "MSSQL_SA_PASSWORD, docker/.env icinde bulunamadi."
}

$fileName = Split-Path $BakFile -Leaf
$containerPath = "/var/opt/mssql/backup/$fileName"

Write-Host "Bu islem '$Database' veritabaninin UZERINE YAZACAK. Devam etmek icin Enter'a basin, iptal icin Ctrl+C."
Read-Host | Out-Null

docker exec $ContainerName mkdir -p /var/opt/mssql/backup
Write-Host "Yedek konteynere kopyalaniyor..."
docker cp $BakFile "${ContainerName}:${containerPath}"

$sql = @"
ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [$Database] FROM DISK = N'$containerPath' WITH REPLACE;
ALTER DATABASE [$Database] SET MULTI_USER;
"@

Write-Host "Geri yukleniyor..."
docker exec $ContainerName /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$saPassword" -Q "$sql"
docker exec $ContainerName rm -f $containerPath

Write-Host "Tamamlandi: $Database, $fileName dosyasindan geri yuklendi."
