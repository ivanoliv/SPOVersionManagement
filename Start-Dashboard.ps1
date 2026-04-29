<#
.SYNOPSIS
    Local HTTP server for the monitoring Dashboard
.DESCRIPTION
    Starts a simple HTTP server on port 8080 to serve the dashboard
.EXAMPLE
    .\Start-Dashboard.ps1
#>

param(
    [int]$Port = 8080
)

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

# Load paths from AppPaths.json
$appPathsFile = Join-Path $scriptPath "config\AppPaths.json"
if (-not (Test-Path $appPathsFile)) {
    $appPathsFile = Join-Path $scriptPath "Logs\AppPaths.json"
}
if (-not (Test-Path $appPathsFile)) {
    $appPathsFile = Join-Path $scriptPath "AppPaths.json"
}

if (Test-Path $appPathsFile) {
    try {
        $appPaths = Get-Content $appPathsFile -Raw | ConvertFrom-Json
        $rootPath = Join-Path $appPaths.RootPath $appPaths.ApplicationFolder
        $configPath = if ($appPaths.Directories.Config) { Join-Path $rootPath $appPaths.Directories.Config } else { Join-Path $rootPath "config" }
        $webPath = if ($appPaths.Directories.Web) { Join-Path $rootPath $appPaths.Directories.Web } else { Join-Path $rootPath "web" }
        $logsPath = Join-Path $rootPath $appPaths.Directories.Logs
    }
    catch {
        $configPath = Join-Path $scriptPath "config"
        $webPath = Join-Path $scriptPath "web"
        $logsPath = Join-Path $scriptPath "Logs"
    }
} else {
    $configPath = Join-Path $scriptPath "config"
    $webPath = Join-Path $scriptPath "web"
    $logsPath = Join-Path $scriptPath "Logs"
}

# Read version from AppPaths.json
$appVersion = "unknown"
$appPathsPath = Join-Path $configPath "AppPaths.json"
if (Test-Path $appPathsPath) {
    try {
        $appPathsData = Get-Content $appPathsPath -Raw | ConvertFrom-Json
        if ($appPathsData.AppVersion) { $appVersion = $appPathsData.AppVersion }
    } catch { }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SPO Version Management Dashboard" -ForegroundColor Cyan
Write-Host "  Version: v$appVersion" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Starting server on port $Port..." -ForegroundColor Yellow
Write-Host ""
Write-Host "Open in browser:" -ForegroundColor Green
Write-Host "  http://localhost:$Port/Dashboard.html" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C to stop" -ForegroundColor Gray
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Criar listener HTTP
$listener = [System.Net.HttpListener]::new()
$listener.Prefixes.Add("http://localhost:$Port/")
$listener.Prefixes.Add("http://127.0.0.1:$Port/")

try {
    $listener.Start()
    
    # Abrir automaticamente no navegador
    Start-Process "http://localhost:$Port/Dashboard.html"
    
    while ($listener.IsListening) {
        try {
            $context = $listener.GetContext()
            $request = $context.Request
            $response = $context.Response
            
            $localPath = $request.Url.LocalPath
            if ($localPath -eq "/") { $localPath = "/Dashboard.html" }
            
            # Resolve file path: web/ for HTML/JS/CSS, config/ for JSON, Logs/ for CSV
            $requestedFile = $localPath.TrimStart('/')
            if ($requestedFile -match '\.(html|js|css)$') {
                $filePath = Join-Path $webPath $requestedFile
            } elseif ($requestedFile -match '\.json$') {
                $filePath = Join-Path $configPath $requestedFile
            } else {
                $filePath = Join-Path $logsPath $requestedFile
            }
            
            # Ignorar favicon
            if ($localPath -eq "/favicon.ico") {
                $response.StatusCode = 204
                $response.Close()
                continue
            }
            
            Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $($request.HttpMethod) $localPath" -ForegroundColor Gray
            
            try {
                # Handle POST requests - save JSON files to config folder
                if ($request.HttpMethod -eq 'POST') {
                    $allowedFiles = @('ArchiveQueue.json', 'DashboardConfig.json')
                    $fileName = $localPath.TrimStart('/')
                    if ($fileName -in $allowedFiles) {
                        $reader = [System.IO.StreamReader]::new($request.InputStream, $request.ContentEncoding)
                        $body = $reader.ReadToEnd()
                        $reader.Close()
                        $savePath = Join-Path $configPath $fileName
                        [System.IO.File]::WriteAllText($savePath, $body, [System.Text.Encoding]::UTF8)
                        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] SAVED $fileName ($($body.Length) bytes)" -ForegroundColor Green
                        $response.StatusCode = 200
                        $response.Headers.Add("Access-Control-Allow-Origin", "*")
                        $okMsg = [System.Text.Encoding]::UTF8.GetBytes('{"status":"ok"}')
                        $response.ContentType = 'application/json'
                        $response.ContentLength64 = $okMsg.Length
                        $response.OutputStream.Write($okMsg, 0, $okMsg.Length)
                    } else {
                        $response.StatusCode = 403
                        $errMsg = [System.Text.Encoding]::UTF8.GetBytes('{"error":"not allowed"}')
                        $response.ContentType = 'application/json'
                        $response.ContentLength64 = $errMsg.Length
                        $response.OutputStream.Write($errMsg, 0, $errMsg.Length)
                    }
                }
                elseif (Test-Path $filePath) {
                    $content = [System.IO.File]::ReadAllBytes($filePath)
                    
                    # Definir content-type
                    $contentType = switch -Regex ($filePath) {
                        '\.html$' { 'text/html; charset=utf-8' }
                        '\.json$' { 'application/json; charset=utf-8' }
                        '\.css$'  { 'text/css; charset=utf-8' }
                        '\.js$'   { 'application/javascript; charset=utf-8' }
                        default   { 'text/plain; charset=utf-8' }
                    }
                    
                    $response.ContentType = $contentType
                    $response.Headers.Add("Access-Control-Allow-Origin", "*")
                    $response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate")
                    $response.ContentLength64 = $content.Length
                    $response.OutputStream.Write($content, 0, $content.Length)
                }
                else {
                    $response.StatusCode = 404
                    $errorMsg = [System.Text.Encoding]::UTF8.GetBytes("File not found: $localPath")
                    $response.OutputStream.Write($errorMsg, 0, $errorMsg.Length)
                }
            }
            catch {
                # Ignorar erros de conexao fechada pelo cliente
            }
            finally {
                try { $response.Close() } catch { }
            }
        }
        catch [System.Net.HttpListenerException] {
            # Cliente desconectou - ignorar
        }
        catch {
            # Ignorar outros erros de conexao
            if ($_.Exception.Message -notlike "*network name*" -and 
                $_.Exception.Message -notlike "*forcibly closed*" -and
                $_.Exception.Message -notlike "*thread exit*") {
                Write-Warning "Erro: $($_.Exception.Message)"
            }
        }
    }
}
catch {
    if ($_.Exception.Message -notlike "*thread exit*" -and
        $_.Exception.Message -notlike "*Access is denied*") {
        Write-Error "Erro no servidor: $_"
    }
}
finally {
    try {
        $listener.Stop()
        $listener.Close()
    } catch { }
    Write-Host "`nServer stopped." -ForegroundColor Yellow
}