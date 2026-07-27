<#
.SYNOPSIS
    finara-mssql konteynerindeki FinaraDb veritabanının tam yedeğini (.bak) alır ve host'a kopyalar.

.EXAMPLE
    ./backup-db.ps1
    ./backup-db.ps1 -KeepLast 30
#>
param(
    [string]$ContainerName = "finara-mssql",
    [string]$Database = "FinaraDb",
    [string]$OutDir = "$PSScriptRoot/backups",
    [int]$KeepLast = 14
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $OutDir)) {
    New-Item -ItemType Directory -Path $OutDir | Out-Null
}

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$fileName = "${Database}_${timestamp}.bak"
$containerPath = "/var/opt/mssql/backup/$fileName"
$hostPath = Join-Path $OutDir $fileName

$saPassword = (Get-Content "$PSScriptRoot/.env" | Where-Object { $_ -match "^MSSQL_SA_PASSWORD=" }) -replace "^MSSQL_SA_PASSWORD=", ""
if (-not $saPassword) {
    throw "MSSQL_SA_PASSWORD, docker/.env icinde bulunamadi."
}

Write-Host "Konteyner icinde yedek aliniyor: $containerPath"
docker exec $ContainerName mkdir -p /var/opt/mssql/backup
docker exec $ContainerName /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$saPassword" -Q "BACKUP DATABASE [$Database] TO DISK = N'$containerPath' WITH FORMAT, INIT;"

Write-Host "Host'a kopyalaniyor: $hostPath"
docker cp "${ContainerName}:${containerPath}" $hostPath
docker exec $ContainerName rm -f $containerPath

Write-Host "Eski yedekler temizleniyor (son $KeepLast tanesi tutuluyor)..."
Get-ChildItem -Path $OutDir -Filter "${Database}_*.bak" | Sort-Object LastWriteTime -Descending | Select-Object -Skip $KeepLast | Remove-Item -Force

Write-Host "Tamamlandi: $hostPath"
