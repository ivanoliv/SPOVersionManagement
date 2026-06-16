<#
.SYNOPSIS
    Filters SAM (SharePoint Admin Center) inactive sites report by inactivity period and exports to CSV.
.DESCRIPTION
    Reads a SAM "Content Management Assessment" or "Inactive Policy" CSV report,
    filters sites inactive for more than the specified number of days,
    and exports a new CSV with dates in dd/MM/yyyy format (pt-BR).
.PARAMETER SAMReportPath
    Path to the SAM CSV report file.
.PARAMETER Days
    Minimum number of days of inactivity (default: 180).
.PARAMETER OutputPath
    Output CSV path (default: Logs\InactiveSites_D<days>.csv)
.EXAMPLE
    .\Export-InactiveSites.ps1 -SAMReportPath ".\Logs\Politica Simulao 2_20260608125935000.csv" -Days 180
.EXAMPLE
    .\Export-InactiveSites.ps1 -SAMReportPath ".\Logs\Politica Simulao 2_20260608125935000.csv" -Days 365
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SAMReportPath,
    
    [int]$Days = 180,
    
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $SAMReportPath)) {
    Write-Host "[ERROR] File not found: $SAMReportPath" -ForegroundColor Red
    exit 1
}

if (-not $OutputPath) {
    $dir = Split-Path $SAMReportPath -Parent
    $OutputPath = Join-Path $dir "InactiveSites_D$Days.csv"
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  EXPORT INACTIVE SITES (D$Days+)" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Source: $(Split-Path $SAMReportPath -Leaf)" -ForegroundColor Gray
Write-Host "  Filter: inactive > $Days days" -ForegroundColor Gray
Write-Host ""

# Load CSV
$samCsv = Import-Csv $SAMReportPath -Encoding UTF8
Write-Host "  Loaded: $($samCsv.Count) sites" -ForegroundColor White

# Calculate cutoff date
$now = Get-Date
$cutoff = $now.AddDays(-$Days)
Write-Host "  Cutoff date: $($cutoff.ToString('dd/MM/yyyy')) (sites inactive before this)" -ForegroundColor Yellow
Write-Host ""

# Filter by last activity date
$inactive = [System.Collections.ArrayList]::new()
$noDate = 0
$parseErrors = 0

foreach ($row in $samCsv) {
    $lastActivity = $row.'Last activity date (UTC)'
    if (-not $lastActivity) { $noDate++; continue }
    
    try {
        # Parse mm/dd/yyyy format (US locale from SAM export)
        $activityDate = [DateTime]::Parse($lastActivity, [System.Globalization.CultureInfo]::new("en-US"))
        
        if ($activityDate -lt $cutoff) {
            $daysSinceActivity = [math]::Round(($now - $activityDate).TotalDays)
            $null = $inactive.Add([PSCustomObject]@{
                'Nome do Site'         = $row.'Site name'
                'URL'                  = $row.URL
                'Template'             = $row.Template
                'Conectado ao Teams'   = if ($row.'Connected to Teams' -eq 'True') { 'Sim' } else { 'Nao' }
                'Label Sensibilidade'  = $row.'Sensitivity label'
                'Politica Retencao'    = $row.'Retention Policy'
                'Lock State'           = $row.'Site lock state'
                'Ultima Atividade'     = $activityDate.ToString("dd/MM/yyyy")
                'Dias Inativo'         = $daysSinceActivity
                'Data Criacao'         = if ($row.'Site creation date (UTC)') { 
                    try { ([DateTime]::Parse($row.'Site creation date (UTC)', [System.Globalization.CultureInfo]::new("en-US"))).ToString("dd/MM/yyyy") } catch { $row.'Site creation date (UTC)' }
                } else { "" }
                'Storage (GB)'         = $row.'Storage used (GB)'
                'Owners'               = $row.'Number of site owners'
                'Email Owners'         = $row.'Email address of site owners'
                'Admins'               = $row.'Number of site admins'
                'Email Admins'         = $row.'Email address of site admins'
            })
        }
    } catch {
        $parseErrors++
    }
}

# Sort by days inactive (most inactive first)
$sorted = $inactive | Sort-Object 'Dias Inativo' -Descending

# Export
$sorted | Export-Csv -Path $OutputPath -NoTypeInformation -Encoding UTF8 -Delimiter ";"
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  RESULTADO" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Total no relatorio: $($samCsv.Count)" -ForegroundColor White
Write-Host "  Inativos (D$Days+): $($sorted.Count)" -ForegroundColor Green
if ($noDate -gt 0) { Write-Host "  Sem data atividade: $noDate" -ForegroundColor DarkGray }
if ($parseErrors -gt 0) { Write-Host "  Erros de parse: $parseErrors" -ForegroundColor Yellow }
Write-Host ""

# Storage summary
$totalStorageGB = ($sorted | ForEach-Object { [double]($_.'Storage (GB)' -replace ',','.') } | Measure-Object -Sum).Sum
Write-Host "  Storage total (inativos): $([math]::Round($totalStorageGB, 2)) GB ($([math]::Round($totalStorageGB/1024, 2)) TB)" -ForegroundColor Yellow
Write-Host "  Custo mensal (R$ 1.14/GB): R$ $([math]::Round($totalStorageGB * 1.14, 2))" -ForegroundColor Green
Write-Host "  Custo anual:  R$ $([math]::Round($totalStorageGB * 1.14 * 12, 2))" -ForegroundColor Green
Write-Host ""
Write-Host "  Exportado: $OutputPath" -ForegroundColor Cyan
Write-Host "  Formato: CSV (delimitador ;) | Datas: dd/MM/yyyy" -ForegroundColor Gray
Write-Host ""
