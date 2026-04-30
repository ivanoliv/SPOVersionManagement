using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Threading;
using System.Threading.Tasks;

namespace SPOVersionManagement.Services
{
    public class PowerShellHostService : IDisposable
    {
        private readonly string _rootPath;
        private RunspacePool _pool;
        private GuiPSHost _guiHost;
        private readonly SynchronizationContext _syncContext;

        public event Action<string> OnOutput;
        public event Action<string> OnError;
        public event Action<string> OnWarning;
        public event Action<int> OnProgress;

        public bool IsConnected { get; private set; }
        public string AdminUrl { get; private set; }
        public string PS51Path { get; private set; }  // Path to powershell.exe
        public string PS7Path { get; private set; }   // Path to pwsh.exe

        public PowerShellHostService(string rootPath)
        {
            _rootPath = rootPath;
            _syncContext = SynchronizationContext.Current;
            DetectPowerShellVersions();
        }

        /// <summary>
        /// Detects available PowerShell versions on the system.
        /// </summary>
        private void DetectPowerShellVersions()
        {
            // PS 5.1 (Windows PowerShell)
            PS51Path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!System.IO.File.Exists(PS51Path))
                PS51Path = null;

            // PS 7+ (PowerShell Core)
            try
            {
                var result = Process.Start(new ProcessStartInfo
                {
                    FileName = "pwsh",
                    Arguments = "-NoProfile -Command \"Write-Output $PSHOME\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                });
                result?.WaitForExit(1000);
                if (result?.ExitCode == 0)
                    PS7Path = "pwsh";
            }
            catch
            {
                PS7Path = null;
            }
        }

        /// <summary>
        /// Gets PowerShell versions available and their module info.
        /// </summary>
        public async Task<List<(string Version, bool Available, string Path)>> GetPowerShellVersionsAsync()
        {
            var versions = new List<(string, bool, string)>();

            if (PS51Path != null)
                versions.Add(("5.1", true, PS51Path));
            if (PS7Path != null)
                versions.Add(("7+", true, PS7Path));

            return await Task.FromResult(versions);
        }

        /// <summary>
        /// Checks if a module is available in a specific PowerShell version.
        /// </summary>
        public async Task<(bool Available, string Version)> CheckModuleInVersionAsync(
            string moduleName, string psVersion)
        {
            string psPath = psVersion == "5.1" ? PS51Path : PS7Path;
            if (string.IsNullOrEmpty(psPath))
                return (false, "PS version not available");

            try
            {
                return await Task.Run(() =>
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = psPath,
                        Arguments = $"-NoProfile -Command \"$m = Get-Module -ListAvailable '{moduleName}' -ErrorAction SilentlyContinue; if($m){{ Write-Output $m.Version }}else{{ exit 1 }}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using (var proc = Process.Start(psi))
                    {
                        string output = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit();
                        if (proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
                            return (true, output);
                        return (false, "Not installed");
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }


        /// <summary>
        /// Initializes the RunspacePool and imports required modules.
        /// </summary>
        public void Initialize()
        {
            var iss = InitialSessionState.CreateDefault();

            // Import modules
            string mainModule = System.IO.Path.Combine(_rootPath, "SPOVersionManagement.psm1");
            string filtersModule = System.IO.Path.Combine(_rootPath, "SPOSiteFilters.psm1");
            string retentionModule = System.IO.Path.Combine(_rootPath, "SPORetentionPolicyManager.psm1");

            if (System.IO.File.Exists(mainModule))
                iss.ImportPSModule(new[] { mainModule });
            if (System.IO.File.Exists(filtersModule))
                iss.ImportPSModule(new[] { filtersModule });
            if (System.IO.File.Exists(retentionModule))
                iss.ImportPSModule(new[] { retentionModule });

            // Create custom host that routes prompts to MessageBox
            _guiHost = new GuiPSHost(msg => PostToUI(() => OnOutput?.Invoke(msg)));

            _pool = RunspaceFactory.CreateRunspacePool(1, 3, iss, _guiHost);
            _pool.Open();
        }

        /// <summary>
        /// Runs a PowerShell script block asynchronously with output streaming.
        /// </summary>
        public async Task<PSDataCollection<PSObject>> RunScriptAsync(string script,
            Dictionary<string, object> parameters = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                using (var ps = PowerShell.Create())
                {
                    ps.RunspacePool = _pool;
                    ps.AddScript(script);

                    if (parameters != null)
                    {
                        foreach (var kv in parameters)
                            ps.AddParameter(kv.Key, kv.Value);
                    }

                    // Wire up streams
                    ps.Streams.Information.DataAdded += (s, e) =>
                    {
                        var info = ps.Streams.Information[e.Index];
                        PostToUI(() => OnOutput?.Invoke(info.MessageData?.ToString()));
                    };

                    ps.Streams.Warning.DataAdded += (s, e) =>
                    {
                        var warn = ps.Streams.Warning[e.Index];
                        PostToUI(() => OnWarning?.Invoke(warn.Message));
                    };

                    ps.Streams.Error.DataAdded += (s, e) =>
                    {
                        var err = ps.Streams.Error[e.Index];
                        PostToUI(() => OnError?.Invoke(err.Exception?.Message ?? err.ToString()));
                    };

                    ps.Streams.Progress.DataAdded += (s, e) =>
                    {
                        var prog = ps.Streams.Progress[e.Index];
                        if (prog.PercentComplete >= 0)
                            PostToUI(() => OnProgress?.Invoke(prog.PercentComplete));
                    };

                    var output = new PSDataCollection<PSObject>();

                    // Capture pipeline output (Write-Output) in real-time too
                    output.DataAdded += (sender, args) =>
                    {
                        var obj = output[args.Index];
                        if (obj != null)
                            PostToUI(() => OnOutput?.Invoke(obj.ToString()));
                    };

                    // Handle cancellation
                    cancellationToken.Register(() => ps.Stop());

                    ps.Invoke(null, output);
                    return output;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Launches Start-Dashboard.ps1 in a background runspace.
        /// </summary>
        public void LaunchDashboard(int port, string dataFolder)
        {
            string dashScript = System.IO.Path.Combine(_rootPath, "Start-Dashboard.ps1");
            if (!System.IO.File.Exists(dashScript))
            {
                PostToUI(() => OnError?.Invoke("Start-Dashboard.ps1 not found."));
                return;
            }

            Task.Run(() =>
            {
                using (var ps = PowerShell.Create())
                {
                    ps.RunspacePool = _pool;
                    ps.AddCommand(dashScript);
                    if (port > 0)
                        ps.AddParameter("Port", port);

                    try
                    {
                        ps.Invoke();
                    }
                    catch (Exception ex)
                    {
                        PostToUI(() => OnError?.Invoke($"Dashboard error: {ex.Message}"));
                    }
                }
            });
        }

        /// <summary>
        /// Starts the main SPO Version Management process.
        /// Supports ALL parameters from Start-SPOVersionManagement.ps1.
        /// </summary>
        public async Task StartVersionManagementAsync(
            string adminUrl, int majorVersionLimit, int majorWithMinorVersionsLimit,
            int maxConcurrentJobs, bool syncOnly, bool deleteOnly,
            bool manageRetention, bool useFileCache,
            CancellationToken cancellationToken = default,
            string inputSiteListCsv = null,
            string inputExclusionSiteListCsv = null,
            string graphReportCsv = null,
            string inputSiteSyncListCsv = null,
            int checkBatchSize = 10,
            int checkBatchDelaySeconds = 2,
            bool skipGraphConnection = false,
            bool openDashboard = false,
            bool resetDatabase = false,
            int deleteBeforeDays = 0)
        {
            AdminUrl = adminUrl;
            string script = System.IO.Path.Combine(_rootPath, "Start-SPOVersionManagement.ps1");

            var parameters = new Dictionary<string, object>
            {
                { "AdminUrl", adminUrl },
                { "MaxConcurrentJobs", maxConcurrentJobs },
                { "CheckBatchSize", checkBatchSize },
                { "CheckBatchDelaySeconds", checkBatchDelaySeconds }
            };

            // Delete mode: by age (days) or by version count (mutually exclusive)
            if (deleteBeforeDays > 0)
            {
                parameters["DeleteBeforeDays"] = deleteBeforeDays;
            }
            else
            {
                parameters["MajorVersionLimit"] = majorVersionLimit;
                parameters["MajorWithMinorVersionsLimit"] = majorWithMinorVersionsLimit;
            }

            // String parameters (only add if non-empty)
            if (!string.IsNullOrEmpty(inputSiteListCsv)) parameters["InputSiteListCSV"] = inputSiteListCsv;
            if (!string.IsNullOrEmpty(inputExclusionSiteListCsv)) parameters["InputExclusionSiteListCSV"] = inputExclusionSiteListCsv;
            if (!string.IsNullOrEmpty(graphReportCsv)) parameters["GraphReportCSV"] = graphReportCsv;
            if (!string.IsNullOrEmpty(inputSiteSyncListCsv)) parameters["InputSiteSyncListCSV"] = inputSiteSyncListCsv;

            // Switch parameters
            if (syncOnly) parameters["SyncOnly"] = true;
            if (deleteOnly) parameters["DeleteOnly"] = true;
            if (manageRetention) parameters["ManageRetentionPolicy"] = true;
            if (useFileCache) parameters["UseFileCache"] = true;
            if (skipGraphConnection) parameters["SkipGraphConnection"] = true;
            if (openDashboard) parameters["OpenDashboard"] = true;
            if (resetDatabase) parameters["ResetDatabase"] = true;

            string cmd = $"& '{script}'";
            foreach (var p in parameters)
            {
                if (p.Value is bool b && b)
                    cmd += $" -{p.Key}";
                else if (!(p.Value is bool))
                    cmd += $" -{p.Key} '{p.Value}'";
            }

            await RunScriptAsync(cmd, cancellationToken: cancellationToken);
        }

        public async Task StartArchiveSitesAsync(string adminUrl, string queueFile, CancellationToken cancellationToken = default)
        {
            string script = System.IO.Path.Combine(_rootPath, "Start-ArchiveWebsites.ps1");
            string cmd = $"& '{script}' -AdminUrl '{adminUrl}' -Unattended";

            if (!string.IsNullOrWhiteSpace(queueFile))
                cmd += $" -QueueFile '{queueFile}'";

            await RunScriptAsync(cmd, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Runs the file archive search on a specific site using Graph Search API.
        /// </summary>
        public async Task StartFileArchiveSearchAsync(string siteUrl, bool useInteractiveLogin,
            bool summaryOnly, CancellationToken cancellationToken = default,
            string clientId = null, string certThumbprint = null, string tenantId = null, string region = "NAM")
        {
            string script = System.IO.Path.Combine(_rootPath, "Start-FileArchiveSearch.ps1");
            string cmd = $"& '{script}' -SiteUrl '{siteUrl}'";

            if (useInteractiveLogin)
                cmd += " -UseInteractiveLogin";
            if (summaryOnly)
                cmd += " -SummaryOnly";
            if (!string.IsNullOrEmpty(clientId))
                cmd += $" -ClientId '{clientId}'";
            if (!string.IsNullOrEmpty(certThumbprint))
                cmd += $" -CertificateThumbprint '{certThumbprint}'";
            if (!string.IsNullOrEmpty(tenantId))
                cmd += $" -TenantId '{tenantId}'";
            if (!string.IsNullOrEmpty(region))
                cmd += $" -Region '{region}'";

            await RunScriptAsync(cmd, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Checks prerequisites across both PowerShell versions.
        /// Returns info about what's installed and where.
        /// </summary>
        public async Task<PSDataCollection<System.Management.Automation.PSObject>> CheckPrerequisitesAsync(CancellationToken cancellationToken = default)
        {
            // Check PS 5.1 modules
            var results = new List<Dictionary<string, object>>();

            // Check PowerShell versions first
            string ps51Status = PS51Path != null ? "Available" : "Not found";
            string ps7Status = PS7Path != null ? "Available" : "Not found";

            results.Add(new Dictionary<string, object>
            {
                { "Module", "PowerShell 5.1 (powershell.exe)" },
                { "Installed", PS51Path != null },
                { "Version", ps51Status },
                { "Required", "CSOM (SPO Mgmt Shell), Graph.Auth" }
            });

            results.Add(new Dictionary<string, object>
            {
                { "Module", "PowerShell 7.4+ (pwsh)" },
                { "Installed", PS7Path != null },
                { "Version", ps7Status },
                { "Required", "PnP.PowerShell" }
            });

            // Check modules in PS 5.1 if available
            if (PS51Path != null)
            {
                var (spoInstalled, spoVersion) = await CheckModuleInVersionAsync(
                    "Microsoft.Online.SharePoint.PowerShell", "5.1");
                results.Add(new Dictionary<string, object>
                {
                    { "Module", "SPO Mgmt Shell (PS 5.1)" },
                    { "Installed", spoInstalled },
                    { "Version", spoVersion },
                    { "Required", "CSOM operations, site/user management" }
                });

                var (graphAuthInstalled, graphAuthVersion) = await CheckModuleInVersionAsync(
                    "Microsoft.Graph.Authentication", "5.1");
                results.Add(new Dictionary<string, object>
                {
                    { "Module", "Microsoft.Graph (PS 5.1)" },
                    { "Installed", graphAuthInstalled },
                    { "Version", graphAuthVersion },
                    { "Required", "Graph reports, data sync v1.x" }
                });
            }

            // Check modules in PS 7 if available
            if (PS7Path != null)
            {
                var (pnpInstalled, pnpVersion) = await CheckModuleInVersionAsync(
                    "PnP.PowerShell", "7");
                results.Add(new Dictionary<string, object>
                {
                    { "Module", "PnP.PowerShell (PS 7+)" },
                    { "Installed", pnpInstalled },
                    { "Version", pnpVersion },
                    { "Required", "File archive (Set-PnPFileArchiveState)" }
                });
            }

            // Convert to PSObject collection
            var psResults = new PSDataCollection<System.Management.Automation.PSObject>();
            foreach (var dict in results)
            {
                var obj = new System.Management.Automation.PSObject();
                foreach (var kv in dict)
                    obj.Properties.Add(new PSNoteProperty(kv.Key, kv.Value));
                psResults.Add(obj);
            }

            return await Task.FromResult(psResults);
        }


        private void PostToUI(Action action)
        {
            if (_syncContext != null)
                _syncContext.Post(_ => action(), null);
            else
                action();
        }

        public void Dispose()
        {
            _pool?.Close();
            _pool?.Dispose();
        }
    }
}
