# Test-RetentionPolicies.ps1
# Diagnostic script: test retention policy exception add/remove operations
# Tests single URL and batch URL modes

param(
    [string]$AppId = "d623dce1-f285-47b2-a20d-0757b22ef14f",
    [string]$CertificateThumbprint = "7F96D2CCEBE96CB8CD904F9E699A08915D6C74BD",
    [string]$Organization = "contoso.onmicrosoft.com",

    # Test modes
    [switch]$TestAdd,          # Test adding a single URL to exception list
    [switch]$TestBatchAdd,     # Test batch adding multiple URLs
    [switch]$TestRemove,       # Test removing a single URL from exception list
    [switch]$TestBatchRemove,  # Test batch removing multiple URLs
    [switch]$TestRoundTrip,    # Test full add + verify + remove cycle
    [switch]$TestCleanup,      # Remove ALL orphaned exceptions from all policies

    # Target policy and URLs
    [string]$PolicyName,       # Policy name to test against (auto-selects first AllSites policy if empty)
    [string]$TestUrl,          # Single URL for single-mode tests
    [string[]]$TestUrls        # Array of URLs for batch-mode tests
)

Write-Host ""
Write-Host "=== RETENTION POLICY DIAGNOSTIC ===" -ForegroundColor Cyan
Write-Host ""

# Try app-based auth
Write-Host "Attempting app-based IPPS connection..." -ForegroundColor Yellow
Write-Host "  AppId:       $($AppId.Substring(0,8))..." -ForegroundColor Gray
Write-Host "  Thumbprint:  $($CertificateThumbprint.Substring(0,8))..." -ForegroundColor Gray
Write-Host "  Organization: $Organization" -ForegroundColor Gray
Write-Host ""

try {
    Disconnect-ExchangeOnline -Confirm:$false -ErrorAction SilentlyContinue
} catch {}

try {
    Connect-IPPSSession -AppId $AppId -CertificateThumbprint $CertificateThumbprint -Organization $Organization -ErrorAction Stop
    Write-Host "[OK] App-based IPPS connection successful!" -ForegroundColor Green
}
catch {
    Write-Host "[FAIL] App-based connection failed: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Falling back to interactive login..." -ForegroundColor Yellow
    try {
        Connect-IPPSSession -ErrorAction Stop
        Write-Host "[OK] Interactive IPPS connection successful" -ForegroundColor Green
    }
    catch {
        Write-Host "[ERROR] Interactive connection also failed: $_" -ForegroundColor Red
        exit 1
    }
}
Write-Host ""

# Check if connected
try {
    $policies = Get-RetentionCompliancePolicy -DistributionDetail -ErrorAction Stop
    Write-Host "[OK] Found $(@($policies).Count) retention policies." -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Cannot query policies: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Run Connect-IPPSSession first, then re-run this script." -ForegroundColor Yellow
    exit 1
}

Write-Host ""

$idx = 0
foreach ($p in $policies) {
    $idx++
    Write-Host "--- Policy ${idx}: $($p.Name) ---" -ForegroundColor White
    Write-Host "  Guid:    $($p.Guid)" -ForegroundColor Gray
    Write-Host "  Enabled: $($p.Enabled)" -ForegroundColor Gray
    Write-Host "  Mode:    $($p.Mode)" -ForegroundColor Gray
    
    # SharePointLocation
    if ($p.SharePointLocation) {
        $spLocs = @($p.SharePointLocation)
        Write-Host "  SharePointLocation ($($spLocs.Count)):" -ForegroundColor Yellow
        foreach ($loc in $spLocs) {
            $name = if ($loc.Name) { $loc.Name } elseif ($loc.DisplayName) { $loc.DisplayName } else { $loc.ToString() }
            Write-Host "    Name='$name' DisplayName='$($loc.DisplayName)' Type=$($loc.GetType().Name)" -ForegroundColor DarkYellow
        }
    } else {
        Write-Host "  SharePointLocation: (empty/null)" -ForegroundColor DarkGray
    }
    
    # SharePointLocationException
    if ($p.SharePointLocationException) {
        $spExc = @($p.SharePointLocationException)
        Write-Host "  SharePointLocationException ($($spExc.Count)):" -ForegroundColor Yellow
        foreach ($exc in $spExc) {
            $name = if ($exc.Name) { $exc.Name } elseif ($exc.DisplayName) { $exc.DisplayName } else { $exc.ToString() }
            Write-Host "    - $name" -ForegroundColor DarkYellow
        }
    } else {
        Write-Host "  SharePointLocationException: (empty/null)" -ForegroundColor DarkGray
    }
    
    # ModernGroupLocation
    if ($p.ModernGroupLocation) {
        $mgLocs = @($p.ModernGroupLocation)
        Write-Host "  ModernGroupLocation ($($mgLocs.Count)):" -ForegroundColor Magenta
        foreach ($loc in $mgLocs) {
            $name = if ($loc.Name) { $loc.Name } elseif ($loc.DisplayName) { $loc.DisplayName } else { $loc.ToString() }
            Write-Host "    Name='$name' DisplayName='$($loc.DisplayName)' Type=$($loc.GetType().Name)" -ForegroundColor DarkMagenta
        }
    } else {
        Write-Host "  ModernGroupLocation: (empty/null)" -ForegroundColor DarkGray
    }
    
    # ModernGroupLocationException
    if ($p.ModernGroupLocationException) {
        $mgExc = @($p.ModernGroupLocationException)
        Write-Host "  ModernGroupLocationException ($($mgExc.Count)):" -ForegroundColor Magenta
        foreach ($exc in $mgExc) {
            $name = if ($exc.Name) { $exc.Name } elseif ($exc.DisplayName) { $exc.DisplayName } else { $exc.ToString() }
            Write-Host "    - $name" -ForegroundColor DarkMagenta
        }
    } else {
        Write-Host "  ModernGroupLocationException: (empty/null)" -ForegroundColor DarkGray
    }
    
    # Raw dump of all location-related properties
    Write-Host "  --- Raw property check ---" -ForegroundColor DarkGray
    $locationProps = $p.PSObject.Properties | Where-Object { $_.Name -match 'Location|SharePoint|ModernGroup|Teams|OneDrive' }
    foreach ($prop in $locationProps) {
        $val = if ($null -eq $prop.Value) { "(null)" } elseif ($prop.Value -is [System.Collections.IEnumerable] -and $prop.Value -isnot [string]) { "[$(@($prop.Value).Count)] $(@($prop.Value | ForEach-Object { if ($_.Name) {$_.Name} else {$_.ToString()} }) -join ', ')" } else { $prop.Value.ToString() }
        Write-Host "    $($prop.Name) = $val" -ForegroundColor DarkGray
    }
    Write-Host ""
}

Write-Host "=== END POLICY LISTING ===" -ForegroundColor Cyan
Write-Host ""

# === EXCEPTION ADD/REMOVE TESTS ===

# Auto-select first AllSites policy if no PolicyName specified
if (-not $PolicyName -and ($TestAdd -or $TestBatchAdd -or $TestRemove -or $TestBatchRemove -or $TestRoundTrip)) {
    foreach ($p in $policies) {
        $spLocs = @($p.SharePointLocation)
        foreach ($loc in $spLocs) {
            $name = if ($loc.Name) { $loc.Name } else { $loc.ToString() }
            if ($name -match "^All$") {
                $PolicyName = $p.Name
                Write-Host "[AUTO] Selected policy: '$PolicyName' (AllSites)" -ForegroundColor Cyan
                break
            }
        }
        if ($PolicyName) { break }
    }
    if (-not $PolicyName) {
        Write-Host "[ERROR] No AllSites policy found. Specify -PolicyName." -ForegroundColor Red
        exit 1
    }
}

# Default test URLs
if (-not $TestUrl) { $TestUrl = "https://contoso.sharepoint.com/sites/copilotcommunity" }
if (-not $TestUrls) { $TestUrls = @("https://contoso.sharepoint.com/sites/copilotcommunity", "https://contoso.sharepoint.com/sites/Leadership") }

function Show-PolicyExceptions {
    param([string]$Label)
    Write-Host ""
    Write-Host "  [$Label] Current exceptions for '$PolicyName':" -ForegroundColor Gray
    $detail = Get-RetentionCompliancePolicy -Identity $PolicyName -DistributionDetail -ErrorAction Stop
    if ($detail.SharePointLocationException) {
        $exc = @($detail.SharePointLocationException)
        Write-Host "    SharePointLocationException ($($exc.Count)):" -ForegroundColor Yellow
        foreach ($e in $exc) {
            $n = if ($e.Name) { $e.Name } else { $e.ToString() }
            Write-Host "      - $n" -ForegroundColor DarkYellow
        }
    } else {
        Write-Host "    SharePointLocationException: (empty)" -ForegroundColor DarkGray
    }
    Write-Host ""
}

# --- TEST: Single Add ---
if ($TestAdd) {
    Write-Host "=== TEST: Single Add to Exception List ===" -ForegroundColor Magenta
    Write-Host "  Policy: $PolicyName" -ForegroundColor Gray
    Write-Host "  URL:    $TestUrl" -ForegroundColor Gray
    Write-Host ""

    Show-PolicyExceptions "BEFORE"

    Write-Host "  [RUN] Set-RetentionCompliancePolicy -Identity '$PolicyName' -AddSharePointLocationException '$TestUrl'" -ForegroundColor Cyan
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        Set-RetentionCompliancePolicy -Identity $PolicyName -AddSharePointLocationException $TestUrl -ErrorAction Stop
        $sw.Stop()
        Write-Host "  [OK] Command succeeded in $($sw.ElapsedMilliseconds)ms" -ForegroundColor Green
    }
    catch {
        $sw.Stop()
        Write-Host "  [FAIL] Error after $($sw.ElapsedMilliseconds)ms: $_" -ForegroundColor Red
        Write-Host "  [DETAIL] $($_.Exception.GetType().Name): $($_.Exception.Message)" -ForegroundColor DarkRed
    }

    Show-PolicyExceptions "AFTER"
}

# --- TEST: Batch Add ---
if ($TestBatchAdd) {
    Write-Host "=== TEST: Batch Add to Exception List ===" -ForegroundColor Magenta
    Write-Host "  Policy: $PolicyName" -ForegroundColor Gray
    Write-Host "  URLs ($($TestUrls.Count)):" -ForegroundColor Gray
    $TestUrls | ForEach-Object { Write-Host "    - $_" -ForegroundColor Gray }
    Write-Host ""

    Show-PolicyExceptions "BEFORE"

    Write-Host "  [RUN] Set-RetentionCompliancePolicy -Identity '$PolicyName' -AddSharePointLocationException @('$($TestUrls -join "','")')" -ForegroundColor Cyan
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        Set-RetentionCompliancePolicy -Identity $PolicyName -AddSharePointLocationException $TestUrls -ErrorAction Stop
        $sw.Stop()
        Write-Host "  [OK] Batch command succeeded in $($sw.ElapsedMilliseconds)ms" -ForegroundColor Green
    }
    catch {
        $sw.Stop()
        Write-Host "  [FAIL] Error after $($sw.ElapsedMilliseconds)ms: $_" -ForegroundColor Red
        Write-Host "  [DETAIL] $($_.Exception.GetType().Name): $($_.Exception.Message)" -ForegroundColor DarkRed
    }

    Show-PolicyExceptions "AFTER"
}

# --- TEST: Single Remove ---
if ($TestRemove) {
    Write-Host "=== TEST: Single Remove from Exception List ===" -ForegroundColor Magenta
    Write-Host "  Policy: $PolicyName" -ForegroundColor Gray
    Write-Host "  URL:    $TestUrl" -ForegroundColor Gray
    Write-Host ""

    Show-PolicyExceptions "BEFORE"

    Write-Host "  [RUN] Set-RetentionCompliancePolicy -Identity '$PolicyName' -RemoveSharePointLocationException '$TestUrl'" -ForegroundColor Cyan
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        Set-RetentionCompliancePolicy -Identity $PolicyName -RemoveSharePointLocationException $TestUrl -ErrorAction Stop
        $sw.Stop()
        Write-Host "  [OK] Command succeeded in $($sw.ElapsedMilliseconds)ms" -ForegroundColor Green
    }
    catch {
        $sw.Stop()
        Write-Host "  [FAIL] Error after $($sw.ElapsedMilliseconds)ms: $_" -ForegroundColor Red
        Write-Host "  [DETAIL] $($_.Exception.GetType().Name): $($_.Exception.Message)" -ForegroundColor DarkRed
    }

    Show-PolicyExceptions "AFTER"
}

# --- TEST: Batch Remove ---
if ($TestBatchRemove) {
    Write-Host "=== TEST: Batch Remove from Exception List ===" -ForegroundColor Magenta
    Write-Host "  Policy: $PolicyName" -ForegroundColor Gray
    Write-Host "  URLs ($($TestUrls.Count)):" -ForegroundColor Gray
    $TestUrls | ForEach-Object { Write-Host "    - $_" -ForegroundColor Gray }
    Write-Host ""

    Show-PolicyExceptions "BEFORE"

    Write-Host "  [RUN] Set-RetentionCompliancePolicy -Identity '$PolicyName' -RemoveSharePointLocationException @('$($TestUrls -join "','")')" -ForegroundColor Cyan
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        Set-RetentionCompliancePolicy -Identity $PolicyName -RemoveSharePointLocationException $TestUrls -ErrorAction Stop
        $sw.Stop()
        Write-Host "  [OK] Batch command succeeded in $($sw.ElapsedMilliseconds)ms" -ForegroundColor Green
    }
    catch {
        $sw.Stop()
        Write-Host "  [FAIL] Error after $($sw.ElapsedMilliseconds)ms: $_" -ForegroundColor Red
        Write-Host "  [DETAIL] $($_.Exception.GetType().Name): $($_.Exception.Message)" -ForegroundColor DarkRed
    }

    Show-PolicyExceptions "AFTER"
}

# --- TEST: Full Round Trip (Add → Verify → Wait with retry → Remove → Verify) ---
if ($TestRoundTrip) {
    Write-Host "=== TEST: Full Round Trip ===" -ForegroundColor Magenta
    Write-Host "  Policy: $PolicyName" -ForegroundColor Gray
    Write-Host "  URL:    $TestUrl" -ForegroundColor Gray
    Write-Host ""

    Show-PolicyExceptions "INITIAL STATE"

    # Step 1: Add
    Write-Host "  [STEP 1/4] Adding to exception list..." -ForegroundColor Yellow
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        Set-RetentionCompliancePolicy -Identity $PolicyName -AddSharePointLocationException $TestUrl -ErrorAction Stop
        $sw.Stop()
        Write-Host "  [OK] Add succeeded in $($sw.ElapsedMilliseconds)ms" -ForegroundColor Green
    }
    catch {
        $sw.Stop()
        Write-Host "  [FAIL] Add failed after $($sw.ElapsedMilliseconds)ms: $_" -ForegroundColor Red
        Write-Host "  Aborting round trip." -ForegroundColor Red
        Show-PolicyExceptions "AFTER FAILED ADD"
        return
    }

    # Step 2: Verify add
    Write-Host "  [STEP 2/4] Verifying exception was added..." -ForegroundColor Yellow
    Show-PolicyExceptions "AFTER ADD"

    # Step 3: Remove with retry (wait for policy lock to clear)
    Write-Host "  [STEP 3/4] Removing from exception list (with retry)..." -ForegroundColor Yellow
    $removeSuccess = $false
    $maxRetries = 6
    $retryCount = 0
    $totalWaitSeconds = 0

    while (-not $removeSuccess -and $retryCount -le $maxRetries) {
        if ($retryCount -gt 0) {
            $waitSec = 15 * $retryCount  # 15s, 30s, 45s, 60s, 75s, 90s
            $totalWaitSeconds += $waitSec
            Write-Host "    [WAIT] Policy still locked - waiting ${waitSec}s before retry $retryCount/$maxRetries (total waited: ${totalWaitSeconds}s)..." -ForegroundColor DarkYellow
            Start-Sleep -Seconds $waitSec
        } else {
            # First attempt: wait 30s for policy deployment
            Write-Host "    [WAIT] Waiting 30s for policy deployment..." -ForegroundColor Gray
            Start-Sleep -Seconds 30
            $totalWaitSeconds = 30
        }

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            Set-RetentionCompliancePolicy -Identity $PolicyName -RemoveSharePointLocationException $TestUrl -ErrorAction Stop
            $sw.Stop()
            Write-Host "  [OK] Remove succeeded in $($sw.ElapsedMilliseconds)ms (after ${totalWaitSeconds}s total wait)" -ForegroundColor Green
            $removeSuccess = $true
        }
        catch {
            $sw.Stop()
            $errorMsg = $_.Exception.Message
            if ($errorMsg -match "PolicyLockConflict|being deployed|active requests") {
                $retryCount++
                if ($retryCount -gt $maxRetries) {
                    Write-Host "  [FAIL] Remove failed after $maxRetries retries (${totalWaitSeconds}s total wait): $_" -ForegroundColor Red
                }
            } else {
                Write-Host "  [FAIL] Remove failed (non-lock error): $_" -ForegroundColor Red
                break
            }
        }
    }

    # Step 4: Verify remove
    Write-Host "  [STEP 4/4] Verifying exception was removed..." -ForegroundColor Yellow
    Show-PolicyExceptions "AFTER REMOVE"
    
    Write-Host ""
    Write-Host "  === TIMING SUMMARY ===" -ForegroundColor Cyan
    Write-Host "  Policy lock cleared after: ~${totalWaitSeconds}s" -ForegroundColor $(if ($removeSuccess) { "Green" } else { "Red" })
    Write-Host ""
}

# --- TEST: Cleanup all orphaned exceptions ---
if ($TestCleanup) {
    Write-Host "=== TEST: Cleanup Orphaned Exceptions ===" -ForegroundColor Magenta
    Write-Host ""

    foreach ($p in $policies) {
        $pName = $p.Name
        $spExc = @($p.SharePointLocationException)
        if ($spExc.Count -eq 0) {
            Write-Host "  [SKIP] '$pName' - no SP exceptions" -ForegroundColor Gray
            continue
        }

        $excNames = @($spExc | ForEach-Object { if ($_.Name) { $_.Name } else { $_.ToString() } })
        Write-Host "  [$pName] Has $($excNames.Count) SP exception(s):" -ForegroundColor Yellow
        $excNames | ForEach-Object { Write-Host "    - $_" -ForegroundColor DarkYellow }

        Write-Host "  Removing all $($excNames.Count) exception(s) in one call..." -ForegroundColor Cyan
        
        # Retry with backoff
        $removeSuccess = $false
        $maxRetries = 6
        $retryCount = 0

        while (-not $removeSuccess -and $retryCount -le $maxRetries) {
            if ($retryCount -gt 0) {
                $waitSec = 15 * $retryCount
                Write-Host "    [WAIT] Policy locked - waiting ${waitSec}s before retry $retryCount/$maxRetries..." -ForegroundColor DarkYellow
                Start-Sleep -Seconds $waitSec
            }

            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            try {
                Set-RetentionCompliancePolicy -Identity $pName -RemoveSharePointLocationException $excNames -ErrorAction Stop
                $sw.Stop()
                Write-Host "  [OK] Removed $($excNames.Count) exception(s) from '$pName' in $($sw.ElapsedMilliseconds)ms" -ForegroundColor Green
                $removeSuccess = $true
            }
            catch {
                $sw.Stop()
                $errorMsg = $_.Exception.Message
                if ($errorMsg -match "PolicyLockConflict|being deployed|active requests") {
                    $retryCount++
                    if ($retryCount -gt $maxRetries) {
                        Write-Host "  [FAIL] Could not clean '$pName' after $maxRetries retries: $_" -ForegroundColor Red
                    }
                } else {
                    Write-Host "  [FAIL] Non-lock error for '$pName': $_" -ForegroundColor Red
                    break
                }
            }
        }

        # Wait before next policy to avoid cross-policy locks
        Write-Host "    [WAIT] Waiting 10s before next policy..." -ForegroundColor DarkGray
        Start-Sleep -Seconds 10
    }

    # Also check M365 Group exceptions
    foreach ($p in $policies) {
        $pName = $p.Name
        $mgExc = @($p.ModernGroupLocationException)
        if ($mgExc.Count -eq 0) { continue }

        $excNames = @($mgExc | ForEach-Object { if ($_.Name) { $_.Name } else { $_.ToString() } })
        Write-Host "  [$pName] Has $($excNames.Count) M365 Group exception(s):" -ForegroundColor Magenta
        $excNames | ForEach-Object { Write-Host "    - $_" -ForegroundColor DarkMagenta }

        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        try {
            Set-RetentionCompliancePolicy -Identity $pName -RemoveModernGroupLocationException $excNames -ErrorAction Stop
            $sw.Stop()
            Write-Host "  [OK] Removed $($excNames.Count) M365 Group exception(s) from '$pName' in $($sw.ElapsedMilliseconds)ms" -ForegroundColor Green
        }
        catch {
            $sw.Stop()
            Write-Host "  [FAIL] '$pName' M365 cleanup: $_" -ForegroundColor Red
        }
    }

    Write-Host ""
    Write-Host "  Verifying cleanup..." -ForegroundColor Yellow
    $postPolicies = Get-RetentionCompliancePolicy -DistributionDetail -ErrorAction Stop
    foreach ($p in $postPolicies) {
        $spExcCount = @($p.SharePointLocationException).Count
        $mgExcCount = @($p.ModernGroupLocationException).Count
        $color = if ($spExcCount -eq 0 -and $mgExcCount -eq 0) { "Green" } else { "Yellow" }
        Write-Host "  $($p.Name): SP exceptions=$spExcCount | M365 Group exceptions=$mgExcCount" -ForegroundColor $color
    }
}

if (-not $TestAdd -and -not $TestBatchAdd -and -not $TestRemove -and -not $TestBatchRemove -and -not $TestRoundTrip -and -not $TestCleanup) {
    Write-Host ""
    Write-Host "No test mode selected. Available switches:" -ForegroundColor Yellow
    Write-Host "  -TestAdd          Single URL add to exception" -ForegroundColor Gray
    Write-Host "  -TestBatchAdd     Batch URL add to exception" -ForegroundColor Gray
    Write-Host "  -TestRemove       Single URL remove from exception" -ForegroundColor Gray
    Write-Host "  -TestBatchRemove  Batch URL remove from exception" -ForegroundColor Gray
    Write-Host "  -TestRoundTrip    Full add-verify-wait-remove-verify cycle" -ForegroundColor Gray
    Write-Host "  -TestCleanup      Remove ALL orphaned exceptions from all policies" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Cyan
    Write-Host '  .\Test-RetentionPolicies.ps1 -TestRoundTrip' -ForegroundColor White
    Write-Host '  .\Test-RetentionPolicies.ps1 -TestCleanup' -ForegroundColor White
    Write-Host '  .\Test-RetentionPolicies.ps1 -TestBatchAdd -TestUrls "https://site1","https://site2"' -ForegroundColor White
}

Write-Host ""
Write-Host "=== END DIAGNOSTIC ===" -ForegroundColor Cyan
