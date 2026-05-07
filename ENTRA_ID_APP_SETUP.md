# SPO Version Management - Entra ID App Registration Guide

This tool uses **three separate Entra ID app registrations** for security isolation:

| App | Purpose | Permissions Scope |
|-----|---------|-------------------|
| **SPO App** (EntraIdApp) | SharePoint site enumeration, version policy, version deletion | SharePoint + Graph |
| **PnP App** (PnPApp) | File Archive Explorer — search files by extension via PnP.PowerShell | SharePoint (Sites.Read.All) |
| **Purview App** (PurviewApp) | Retention policy suspend/resume via Security & Compliance | Exchange/Purview |

---

## App 1: SPO App (Main Orchestrator)

This app connects to SharePoint Online Admin and Microsoft Graph for site enumeration,
version policy management, and batch version deletion.

### 1.1 Register the Application

1. Go to **Azure Portal** → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Fill in:
   - **Name**: `SPO Version Management`
   - **Supported account types**: `Single tenant only - [YourTenant]`
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

### 1.6 Configure Credentials

There are **3 ways** to enter the SPO app credentials:

#### Option A: Edit `config\AppPaths.json` directly

```json
"EntraIdApp": {
    "TenantId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "ClientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "CertificateThumbprint": "AABBCCDD1122334455..."
}
```

#### Option B: Dashboard (browser)

1. Open the Dashboard (`web\Dashboard.html` or via `Start-Dashboard.ps1`)
2. Go to **Settings** tab → **SPO App Registration** section
3. Fill in Tenant ID, Client ID, and Certificate Thumbprint
4. Click **Save**

#### Option C: GUI App (WinForms)

1. Launch `SPOVersionManagement.exe` (from `src\SPOVersionManagement\bin\`)
2. Go to the **Configuration** tab
3. Fill in **Tenant ID**, **Client ID**, and **Certificate Thumbprint** under the Entra ID App card
4. Click **Save**

> All three methods write to the same `config\AppPaths.json` file.

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
   - **Supported account types**: `Single tenant only - [YourTenant]`
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

> **💡 Alternative: Add permission via App Manifest**
>
> If the UI method still doesn't work after creating the service principal, you can
> add the permission directly in the app manifest:
>
> 1. In your Purview app registration, go to **Manifest**
> 2. Find the `"requiredResourceAccess"` array
> 3. Add the following entry (or append to the existing array):
>
> ```json
> {
>     "resourceAppId": "00000002-0000-0ff1-ce00-000000000000",
>     "resourceAccess": [
>         {
>             "id": "dc50a0fb-09a3-484d-be87-e023b12c6440",
>             "type": "Role"
>         }
>     ]
> }
> ```
>
> 4. Click **Save**
> 5. Go back to **API permissions** — you should see `Exchange.ManageAsApp` listed
> 6. Click **Grant admin consent for [tenant]**
>
> | GUID | Meaning |
> |------|---------|
> | `00000002-0000-0ff1-ce00-000000000000` | Office 365 Exchange Online service |
> | `dc50a0fb-09a3-484d-be87-e023b12c6440` | `Exchange.ManageAsApp` permission |

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

### 2.7 Configure Credentials

There are **3 ways** to enter the Purview app credentials:

#### Option A: Edit `config\AppPaths.json` directly

```json
"PurviewApp": {
    "ClientId": "yyyyyyyy-yyyy-yyyy-yyyy-yyyyyyyyyyyy",
    "CertificateThumbprint": "EEFF00112233445566...",
    "Organization": "contoso.onmicrosoft.com"
}
```

#### Option B: Dashboard (browser)

1. Open the Dashboard → **Settings** tab → **Purview App** section
2. Fill in Client ID, Certificate Thumbprint, and Organization
3. Click **Save**

#### Option C: GUI App (WinForms)

1. Launch `SPOVersionManagement.exe`
2. Go to the **Configuration** tab
3. Fill in **Client ID**, **Certificate Thumbprint**, and **Organization** under the Purview App card
4. Click **Save**

> All three methods write to the same `config\AppPaths.json` file.

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

## App 3: PnP App (File Archive Explorer)

This app is used exclusively by the **File Archive Explorer** feature, which uses
`PnP.PowerShell` to connect to individual SharePoint sites and search for files
by extension (e.g., `.mp4`, `.pst`, `.bak`).

> **Why a separate app?** PnP.PowerShell connects per-site using `Connect-PnPOnline`,
> which requires its own app registration with `Sites.Read.All`. The SPO App uses
> `Connect-SPOService` (admin-level) and `Sites.FullControl.All`, which is too broad
> for read-only file scanning. Separating them follows the principle of least privilege.

### 3.1 Register the Application

1. Go to **Azure Portal** → **Microsoft Entra ID** → **App registrations** → **New registration**
2. Fill in:
   - **Name**: `SPO Version Management - PnP`
   - **Supported account types**: `Single tenant only - [YourTenant]`
3. Click **Register**
4. Copy the **Application (client) ID** → put in `AppPaths.json` → `PnPApp.ClientId`

### 3.2 Generate a Self-Signed Certificate

```powershell
# Generate certificate (valid 2 years)
$cert = New-SelfSignedCertificate `
    -Subject "CN=SPO Version Management PnP" `
    -KeyAlgorithm RSA `
    -KeyLength 2048 `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -NotAfter (Get-Date).AddYears(2) `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -HashAlgorithm SHA256

# Display the thumbprint
Write-Host "PnP Certificate Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

# Export public key (.cer) for Entra ID
$cerPath = "C:\temp\SPO_PnP.cer"
Export-Certificate -Cert $cert -FilePath $cerPath -Type CERT
Write-Host "Public key exported to: $cerPath"

# (Optional) Export PFX for backup/other machines
$pfxPassword = Read-Host -AsSecureString -Prompt "Enter PFX password"
$pfxPath = "C:\temp\SPO_PnP.pfx"
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $pfxPassword
Write-Host "PFX exported to: $pfxPath"
```

Copy the **Thumbprint** → put in `AppPaths.json` → `PnPApp.CertificateThumbprint`

### 3.3 Upload Certificate to Entra ID

1. In the **PnP app** registration, go to **Certificates & secrets** → **Certificates** tab
2. Click **Upload certificate**
3. Select the `.cer` file (`SPO_PnP.cer`)
4. Click **Add**

### 3.4 Add API Permissions

Go to **API permissions** → **Add a permission**:

#### SharePoint (Application permissions)
| Permission | Required | Purpose |
|------------|----------|---------|
| `Sites.Read.All` | **Yes** | Read files and list items across all sites |

#### Microsoft Graph (Application permissions)
| Permission | Required | Purpose |
|------------|----------|---------|
| `Sites.Read.All` | Optional | Alternative site enumeration via Graph |

### 3.5 Grant Admin Consent

1. On the **API permissions** page, click **Grant admin consent for [tenant]**
2. Confirm by clicking **Yes**
3. Verify all permissions show ✅ **Granted**

### 3.6 Configure Credentials

#### Option A: Edit `config\AppPaths.json` directly

```json
"PnPApp": {
    "ClientId": "zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz",
    "CertificateThumbprint": "1122334455667788AABBCCDDEEFF001122334455"
}
```

#### Option B: GUI App (WinForms)

1. Launch `SPOVersionManagement.exe`
2. Go to the **Configuration** tab
3. Fill in **Client ID** and **Certificate Thumbprint** under the **PnP APP** card
4. Click **Save**

### 3.7 Requirements

- **PowerShell 7.4+** (`pwsh`) — PnP.PowerShell does NOT work on PowerShell 5.1
- **PnP.PowerShell module** — install with: `Install-Module PnP.PowerShell -Scope CurrentUser -Force`
- **Certificate installed** in `Cert:\CurrentUser\My` on the machine running the tool
- **TenantId** is shared with the main SPO App (from `EntraIdApp.TenantId` in config)

### 3.8 Limitations

- **Read-only access**: this app only needs `Sites.Read.All` — it cannot modify or delete files
- **Certificate auth only in unattended mode**: interactive mode uses browser-based login (no PnP app needed)
- **One site at a time**: `Connect-PnPOnline` connects to a single site per session
- **PowerShell 7+ only**: PnP.PowerShell is not compatible with Windows PowerShell 5.1
- **No secret-based auth**: only certificate-based authentication is supported for app-only scenarios
- **Large sites may time out**: sites with millions of items may exceed Graph Search API limits

### 3.8 Install Certificate on the VM

Same as other apps — import the PFX to `Cert:\CurrentUser\My`:

```powershell
$pfxPassword = Read-Host -AsSecureString -Prompt "Enter PFX password"
Import-PfxCertificate -FilePath "C:\path\to\SPO_PnP.pfx" `
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

# PnP.PowerShell (for File Archive Explorer — requires PowerShell 7+)
Install-Module PnP.PowerShell -Scope CurrentUser -Force

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
    "PnPApp": {
        "ClientId": "zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz",
        "CertificateThumbprint": "1122334455667788AABBCCDDEEFF001122334455"
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
# Full run with app auth (credentials auto-loaded from config\AppPaths.json)
.\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -Unattended

# With retention policy management (uses Purview app from AppPaths.json)
.\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -Unattended -ManageRetentionPolicy

# Delete-only mode (skip SyncListPolicy)
.\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -Unattended -DeleteOnly

# Sync data for dashboard only (no version changes)
.\Start-SPOVersionManagement.ps1 -AdminUrl "https://contoso-admin.sharepoint.com" -SyncOnly

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

### "Cannot bind argument to parameter 'ClientId' because it is an empty string"
- The PnP App credentials are not configured
- Go to Configuration → **PnP APP** section and enter the Client ID and Certificate Thumbprint
- Or switch to **Interactive (browser)** auth mode in File Archive Explorer

### "Connect-PnPOnline: The remote server returned an error: (403) Forbidden"
- Verify the PnP App has `Sites.Read.All` SharePoint Application permission
- Verify admin consent was granted for the PnP app
- Verify the certificate thumbprint matches the one uploaded to the PnP app registration

### Certificate expired
- Generate a new certificate following steps 1.2 or 2.2
- Upload the new `.cer` to the corresponding app registration
- Update the thumbprint in `AppPaths.json`
- Import the new PFX on the VM
