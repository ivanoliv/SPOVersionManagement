# SPO Version Management - Entra ID App Registration Guide

This tool uses **two separate Entra ID app registrations** for security isolation:

| App | Purpose | Permissions Scope |
|-----|---------|-------------------|
| **SPO App** (EntraIdApp) | SharePoint site enumeration, version policy, version deletion | SharePoint + Graph |
| **Purview App** (PurviewApp) | Retention policy suspend/resume via Security & Compliance | Exchange/Purview |

---

## App 1: SPO App (Main Orchestrator)

This app connects to SharePoint Online Admin and Microsoft Graph for site enumeration,
version policy management, and batch version deletion.

### 1.1 Register the Application

1. Go to **Azure Portal** → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Fill in:
   - **Name**: `SPO Version Management`
   - **Supported account types**: `Accounts in this organizational directory only`
3. Click **Register**
4. Copy these values:
   - **Application (client) ID** → put in `AppPaths.json` → `EntraIdApp.ClientId`
   - **Directory (tenant) ID** → put in `AppPaths.json` → `EntraIdApp.TenantId`

### 1.2 Generate a Self-Signed Certificate

Open **PowerShell as Administrator** and run:

```powershell
# Generate certificate (valid 2 years)
$cert = New-SelfSignedCertificate `
    -Subject "CN=SPO Version Management" `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(2) `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -HashAlgorithm SHA256

# Display the thumbprint (save this!)
Write-Host "Certificate Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

# Export public key (.cer) for uploading to Entra ID
$cerPath = "C:\temp\SPO_VersionManagement.cer"
Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT
Write-Host "Public key exported to: $cerPath"

# (Optional) Export PFX with private key for backup or other machines
$pfxPassword = Read-Host -AsSecureString -Prompt "Enter PFX password"
$pfxPath = "C:\temp\SPO_VersionManagement.pfx"
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pfxPassword
Write-Host "PFX exported to: $pfxPath"
```

Copy the **Thumbprint** → put in `AppPaths.json` → `EntraIdApp.CertificateThumbprint`

### 1.3 Upload Certificate to Entra ID

1. In your app registration, go to **Certificates & secrets** → **Certificates** tab
2. Click **Upload certificate**
3. Select the `.cer` file exported above (`SPO_VersionManagement.cer`)
4. Click **Add**

### 1.4 Add API Permissions

Go to **API permissions** → **Add a permission**:

#### SharePoint (Application permissions)
| Permission | Required | Purpose |
|------------|----------|---------|
| `Sites.FullControl.All` | **Yes** | Set version policies, trigger batch delete jobs, read site storage |

#### Microsoft Graph (Application permissions)
| Permission | Required | Purpose |
|------------|----------|---------|
| `Reports.Read.All` | Recommended | Download SharePoint site usage reports (D180) |
| `Sites.Read.All` | Optional | Site enumeration via Graph (alternative to SPO cmdlets) |

> If you prefer not to grant `Reports.Read.All`, you can use the `-GraphReportCSV` parameter
> to manually provide the SharePoint site usage CSV.

### 1.5 Grant Admin Consent

1. On the **API permissions** page, click **Grant admin consent for [tenant]**
2. Confirm by clicking **Yes**
3. Verify all permissions show ✅ **Granted**

### 1.6 Configure AppPaths.json

Edit `Logs\AppPaths.json`:

```json
"EntraIdApp": {
    "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "CertificateThumbprint": "AABBCCDD1122334455..."
}
```

### 1.7 Install Certificate on the VM

If running on a different machine (VM), import the PFX:

```powershell
# Import PFX to CurrentUser\My store
$pfxPassword = Read-Host -AsSecureString -Prompt "Enter PFX password"
Import-PfxCertificate -FilePath "C:\path\to\SPO_VersionManagement.pfx" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -Password $pfxPassword

# Verify it's installed
Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*SPO Version*" } | Format-Table Thumbprint, Subject, NotAfter
```

---

## App 2: Purview App (Retention Policy Management)

This app connects to Security & Compliance Center (IPPS) to temporarily suspend
retention policies during version cleanup. It uses `Connect-IPPSSession` from the
ExchangeOnlineManagement module.

> **Why a separate app?** The SPO App uses SharePoint permissions. The Purview App
> needs Exchange/Compliance permissions. Separating them follows the principle of
> least privilege — the SPO operations don't need compliance access, and the
> compliance operations don't need SharePoint write access.

### 2.1 Register the Application

1. Go to **Azure Portal** → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Fill in:
   - **Name**: `SPO Version Management - Purview`
   - **Supported account types**: `Accounts in this organizational directory only`
3. Click **Register**
4. Copy the **Application (client) ID** → put in `AppPaths.json` → `PurviewApp.ClientId`

### 2.2 Generate a Self-Signed Certificate

```powershell
# Generate certificate (valid 2 years)
$cert = New-SelfSignedCertificate `
    -Subject "CN=SPO Version Management Purview" `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(2) `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -HashAlgorithm SHA256

# Display the thumbprint
Write-Host "Purview Certificate Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

# Export public key (.cer) for Entra ID
$cerPath = "C:\temp\SPO_Purview.cer"
Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT
Write-Host "Public key exported to: $cerPath"

# (Optional) Export PFX for backup/other machines
$pfxPassword = Read-Host -AsSecureString -Prompt "Enter PFX password"
$pfxPath = "C:\temp\SPO_Purview.pfx"
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pfxPassword
Write-Host "PFX exported to: $pfxPath"
```

Copy the **Thumbprint** → put in `AppPaths.json` → `PurviewApp.CertificateThumbprint`

### 2.3 Upload Certificate to Entra ID

1. In the **Purview app** registration, go to **Certificates & secrets** → **Certificates** tab
2. Click **Upload certificate**
3. Select the `.cer` file (`SPO_Purview.cer`)
4. Click **Add**

### 2.4 Add API Permissions

Go to **API permissions** → **Add a permission**:

#### Office 365 Exchange Online (Application permissions)

> Click **APIs my organization uses** → search for `Office 365 Exchange Online`

> **⚠️ If "Office 365 Exchange Online" does not appear in the search**, the service
> principal hasn't been created in your tenant yet. Run these commands first:
>
> ```powershell
> # Install Microsoft.Graph module if needed
> Install-Module Microsoft.Graph.Applications -Scope CurrentUser
>
> # Connect to Graph with admin permissions
> Connect-MgGraph -Scopes "Application.ReadWrite.All"
>
> # Create the Office 365 Exchange Online service principal
> New-MgServicePrincipal -AppId "00000002-0000-0ff1-ce00-000000000000"
> ```
>
> After running these commands, go back to **API permissions** → **Add a permission** →
> **APIs my organization uses** and search again for `Office 365 Exchange Online`.
> It should now appear with Application permissions available.

| Permission | Required | Purpose |
|------------|----------|---------|
| `Exchange.ManageAsApp` | **Yes** | Connect to Security & Compliance PowerShell via `Connect-IPPSSession` |

### 2.5 Grant Admin Consent

1. On the **API permissions** page, click **Grant admin consent for [tenant]**
2. Confirm by clicking **Yes**
3. Verify the permission shows ✅ **Granted**

### 2.6 Assign Compliance Administrator Role

The app also needs an Entra ID **directory role** to manage retention policies:

1. Go to **Microsoft Entra ID** → **Roles and administrators**
2. Search for **Compliance Administrator** and click on it
3. Click **Add assignments** → **Select members**
4. Search for your Purview app name (`SPO Version Management - Purview`)
5. Select it and click **Next** → **Assign**

> **Alternative roles** (use one):
> - `Compliance Administrator` — recommended, full compliance management
> - `Compliance Data Administrator` — broader data compliance scope
> - Custom role with `microsoft.office365.complianceManager/allEntities/allTasks`

### 2.7 Configure AppPaths.json

Edit `Logs\AppPaths.json`:

```json
"PurviewApp": {
    "ClientId": "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
    "CertificateThumbprint": "EEFF00112233445566...",
    "Organization": "contoso.onmicrosoft.com"
}
```

> **Organization** is your tenant's `.onmicrosoft.com` domain. Find it in:
> Azure Portal → Microsoft Entra ID → Overview → **Primary domain**

### 2.8 Install Certificate on the VM

Same as App 1 — import the PFX to `Cert:\CurrentUser\My`:

```powershell
$pfxPassword = Read-Host -AsSecureString -Prompt "Enter PFX password"
Import-PfxCertificate -FilePath "C:\path\to\SPO_Purview.pfx" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -Password $pfxPassword
```

---

## Required PowerShell Modules

Install on the machine that will run the scripts:

```powershell
# SharePoint Online Management Shell (for SPO cmdlets)
Install-Module Microsoft.Online.SharePoint.PowerShell -Scope CurrentUser -Force

# Microsoft Graph SDK (for reports and site data)
Install-Module Microsoft.Graph.Authentication -Scope CurrentUser -Force
Install-Module Microsoft.Graph.Reports -Scope CurrentUser -Force

# Exchange Online Management (for Security & Compliance / Purview)
Install-Module ExchangeOnlineManagement -Scope CurrentUser -Force
```

---

## AppPaths.json Full Example

```json
{
    "EntraIdApp": {
        "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
        "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
        "CertificateThumbprint": "AABBCCDD1122334455667788AABBCCDD11223344"
    },
    "PurviewApp": {
        "ClientId": "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
        "CertificateThumbprint": "EEFF00112233445566778899EEFF001122334455",
        "Organization": "contoso.onmicrosoft.com"
    }
}
```

---

## Usage

```powershell
# Full run with app auth (SPO app auto-loaded from AppPaths.json)
.\Start-SPOVersionManagement_app.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -Unattended

# With retention policy management (uses Purview app from AppPaths.json)
.\Start-SPOVersionManagement_app.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -Unattended -ManageRetentionPolicy

# Delete-only mode (skip SyncListPolicy)
.\Start-SPOVersionManagement_app.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -Unattended -DeleteOnly

# Sync data for dashboard only (no version changes)
.\Start-SPOVersionManagement_app.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -SyncOnly

# Interactive mode (prompts for login if AppPaths not configured)
.\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -ManageRetentionPolicy
```

---

## Troubleshooting

### "Connect-IPPSSession: The remote server returned an error: (401) Unauthorized"
- Verify `Exchange.ManageAsApp` permission is granted with admin consent
- Verify the Purview app has the `Compliance Administrator` role assigned
- Verify the certificate thumbprint matches the one uploaded to Entra ID
- Verify the Organization is the correct `.onmicrosoft.com` domain

### "Set-RetentionCompliancePolicy: Access denied"
- The Purview app needs the `Compliance Administrator` role (not just the API permission)
- Go to Entra ID → Roles and administrators → verify the role assignment

### "Connect-SPOService: Could not connect"
- Verify the SPO App has `Sites.FullControl.All` SharePoint permission
- Verify admin consent was granted
- Verify the certificate is in the local machine's `Cert:\CurrentUser\My` store

### Certificate expired
- Generate a new certificate following steps 1.2 or 2.2
- Upload the new `.cer` to the corresponding app registration
- Update the thumbprint in `AppPaths.json`
- Import the new PFX on the VM
