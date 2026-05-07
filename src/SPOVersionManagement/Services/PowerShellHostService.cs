using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SPOVersionManagement.Services
{
    /// <summary>
    /// Represents a prompt request raised by a running PowerShell script (Read-Host).
    /// </summary>
    public class PromptRequest
    {
        public string PromptText { get; set; }
        /// <summary>Parsed option letters such as "Y","N" or "F","S","D".</summary>
        public string[] Options { get; set; }
        /// <summary>Recent output lines before the prompt (context for the user).</summary>
        public string[] ContextLines { get; set; }
        /// <summary>Descriptive option labels: key → full label, e.g. "C" → "[C] Continue".</summary>
        public Dictionary<string, string> DescriptiveOptions { get; set; }
        /// <summary>Extracted question text from context (the meaningful line before options).</summary>
        public string QuestionText { get; set; }
    }

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
        public event EventHandler<bool> ConnectionChanged;

        /// <summary>
        /// Raised when the running script prompts for input via Read-Host.
        /// Handler receives the prompt text + parsed option keys (e.g. "Y","N").
        /// Handler must return the chosen answer (single string) or null/empty to send empty line.
        /// </summary>
        public Func<PromptRequest, System.Threading.Tasks.Task<string>> OnPromptRequest;

        public bool IsConnected { get; private set; }
        public string AdminUrl { get; private set; }

        public void SetConnected(bool connected, string adminUrl = null)
        {
            IsConnected = connected;
            if (adminUrl != null) AdminUrl = adminUrl;
            ConnectionChanged?.Invoke(this, connected);
        }
        public string PS51Path { get; private set; }  // Path to powershell.exe
        public string PS7Path { get; private set; }   // Path to pwsh.exe
        public string PS7Version { get; private set; } // Detected version string

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
                var psi = new ProcessStartInfo
                {
                    FileName = "pwsh",
                    Arguments = "-NoProfile -NonInteractive -Command \"Write-Output $PSVersionTable.PSVersion.ToString()\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(5000);
                    if (proc.HasExited && proc.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        PS7Path = "pwsh";
                        PS7Version = output;
                    }
                    else
                        PS7Path = null;

                    if (!proc.HasExited)
                    {
                        try { proc.Kill(); } catch { }
                        PS7Path = null;
                    }
                }
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
                        Arguments = $"-NoProfile -Command \"if($PSVersionTable.PSVersion.Major -ge 7){{ $PSStyle.OutputRendering = 'PlainText' }}; $m = Get-Module -ListAvailable '{moduleName}' -ErrorAction SilentlyContinue | Select-Object -First 1; if($m){{ Write-Output $m.Version.ToString() }}else{{ exit 1 }}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    psi.Environment["NO_COLOR"] = "1";

                    using (var proc = Process.Start(psi))
                    {
                        string output = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit();
                        // Strip any ANSI escape codes that PS7 may emit
                        output = Regex.Replace(output, @"\x1B\[[0-9;]*m", "").Trim();
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
        public string InitializeError { get; private set; }

        public void Initialize()
        {
            // Create custom host that routes prompts to MessageBox
            _guiHost = new GuiPSHost(msg => PostToUI(() => OnOutput?.Invoke(msg)));

            // Attempt 1: Pool with module imports
            try
            {
                var iss = InitialSessionState.CreateDefault();
                iss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;
                string mainModule = System.IO.Path.Combine(_rootPath, "SPOVersionManagement.psm1");
                string filtersModule = System.IO.Path.Combine(_rootPath, "SPOSiteFilters.psm1");
                string retentionModule = System.IO.Path.Combine(_rootPath, "SPORetentionPolicyManager.psm1");

                if (System.IO.File.Exists(filtersModule))
                    iss.ImportPSModule(new[] { filtersModule });
                if (System.IO.File.Exists(retentionModule))
                    iss.ImportPSModule(new[] { retentionModule });
                if (System.IO.File.Exists(mainModule))
                    iss.ImportPSModule(new[] { mainModule });

                _pool = RunspaceFactory.CreateRunspacePool(1, 6, iss, _guiHost);
                _pool.Open();
                return; // Success
            }
            catch (Exception ex)
            {
                InitializeError = $"Attempt1: {ex.GetType().Name}: {ex.Message}";
                _pool?.Dispose();
                _pool = null;
            }

            // Attempt 2: Bare pool without any module pre-imports
            try
            {
                var fallbackIss = InitialSessionState.CreateDefault();
                fallbackIss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;
                _pool = RunspaceFactory.CreateRunspacePool(1, 6, fallbackIss, _guiHost);
                _pool.Open();
                return; // Success
            }
            catch (Exception ex)
            {
                InitializeError += $" | Attempt2: {ex.GetType().Name}: {ex.Message}";
                _pool?.Dispose();
                _pool = null;
            }

            // Attempt 3: Single runspace without custom host
            try
            {
                var minIss = InitialSessionState.CreateDefault();
                minIss.ExecutionPolicy = Microsoft.PowerShell.ExecutionPolicy.Bypass;
                _pool = RunspaceFactory.CreateRunspacePool(minIss);
                _pool.Open();
            }
            catch (Exception ex)
            {
                InitializeError += $" | Attempt3: {ex.GetType().Name}: {ex.Message}";
                _pool?.Dispose();
                _pool = null;
            }
        }

        /// <summary>
        /// Callback context for per-execution output routing.
        /// Panels can pass their own handlers so concurrent executions don't cross-talk.
        /// </summary>
        public class ExecutionContext
        {
            public Action<string> OnOutput { get; set; }
            public Action<string> OnWarning { get; set; }
            public Action<string> OnError { get; set; }
            public Action<int> OnProgress { get; set; }
        }

        /// <summary>
        /// Runs a PowerShell script with per-execution output routing.
        /// Each caller provides its own callbacks so concurrent runs don't interfere.
        /// </summary>
        public async Task<PSDataCollection<PSObject>> RunScriptIsolatedAsync(string script,
            ExecutionContext ctx,
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

                    // Route streams to the caller's context — NOT the global events
                    ps.Streams.Information.DataAdded += (s, e) =>
                    {
                        var info = ps.Streams.Information[e.Index];
                        var msg = info.MessageData?.ToString();
                        if (msg != null)
                        {
                            PostToUI(() => ctx.OnOutput?.Invoke(msg));
                            PostToUI(() => OnOutput?.Invoke(msg)); // also fire global
                        }
                    };

                    ps.Streams.Warning.DataAdded += (s, e) =>
                    {
                        var warn = ps.Streams.Warning[e.Index];
                        PostToUI(() => ctx.OnWarning?.Invoke(warn.Message));
                        PostToUI(() => OnWarning?.Invoke(warn.Message));
                    };

                    ps.Streams.Error.DataAdded += (s, e) =>
                    {
                        var err = ps.Streams.Error[e.Index];
                        var msg = err.Exception?.Message ?? err.ToString();
                        PostToUI(() => ctx.OnError?.Invoke(msg));
                        PostToUI(() => OnError?.Invoke(msg));
                    };

                    ps.Streams.Progress.DataAdded += (s, e) =>
                    {
                        var prog = ps.Streams.Progress[e.Index];
                        if (prog.PercentComplete >= 0)
                        {
                            PostToUI(() => ctx.OnProgress?.Invoke(prog.PercentComplete));
                            PostToUI(() => OnProgress?.Invoke(prog.PercentComplete));
                        }
                    };

                    var output = new PSDataCollection<PSObject>();
                    output.DataAdded += (sender, args) =>
                    {
                        var obj = output[args.Index];
                        if (obj != null)
                        {
                            PostToUI(() => ctx.OnOutput?.Invoke(obj.ToString()));
                            PostToUI(() => OnOutput?.Invoke(obj.ToString()));
                        }
                    };

                    cancellationToken.Register(() => ps.Stop());
                    ps.Invoke(null, output);
                    return output;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Runs a PowerShell script block asynchronously with output streaming.
        /// Falls back to external pwsh.exe/powershell.exe if runspace pool is unavailable.
        /// </summary>
        public async Task<PSDataCollection<PSObject>> RunScriptAsync(string script,
            Dictionary<string, object> parameters = null,
            CancellationToken cancellationToken = default)
        {
            if (_pool == null)
            {
                // Fall back to external process execution
                return await RunScriptExternalAsync(script, cancellationToken);
            }

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

            // Run via external powershell.exe with stdin/stdout bridged so Read-Host prompts
            // surface as UI dialogs (see RunWithPromptBridgeAsync). PS 5.1 with profile keeps
            // user PSModulePath visible (Microsoft.Graph.Reports, SPO Mgmt Shell, etc.).
            await RunWithPromptBridgeAsync(script, parameters, cancellationToken);
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
            string clientId = null, string certThumbprint = null, string tenantId = null, string region = "NAM",
            string pnpClientId = null)
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
            if (!string.IsNullOrEmpty(pnpClientId))
                cmd += $" -PnpClientId '{pnpClientId}'";

            // PnP.PowerShell requires PS 7 — always run via external process
            await RunScriptExternalAsync(cmd, cancellationToken);
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
            string ps7Status = PS7Path != null ? (PS7Version ?? "Available") : "Not found";

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

        /// <summary>
        /// Runs a script file as an external process with stdin/stdout redirection,
        /// intercepting Read-Host prompts via a wrapper that emits sentinel markers.
        /// When a prompt is detected, OnPromptRequest is invoked to obtain the answer
        /// from the UI, which is then written to stdin so the script can continue.
        /// </summary>
        public async Task RunWithPromptBridgeAsync(string scriptPath, Dictionary<string, object> parameters, CancellationToken cancellationToken)
        {
            string psExe = PS51Path ?? "powershell.exe";

            // Wrapper script: overrides Read-Host to emit a marker, reads answer from stdin.
            // Then dot-sources the real script with all parameters via splatting.
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Continue'");
            sb.AppendLine("function Read-Host { param([string]$Prompt, [switch]$AsSecureString)");
            sb.AppendLine("  Write-Host \"__PSPROMPT_START__$Prompt`__PSPROMPT_END__\"");
            sb.AppendLine("  [Console]::Out.Flush()");
            sb.AppendLine("  $line = [Console]::In.ReadLine()");
            sb.AppendLine("  return $line");
            sb.AppendLine("}");
            sb.Append("$splat = @{");
            foreach (var p in parameters)
            {
                if (p.Value is bool b)
                {
                    if (b) sb.Append($" '{p.Key}' = $true;");
                }
                else
                {
                    string val = (p.Value?.ToString() ?? string.Empty).Replace("'", "''");
                    sb.Append($" '{p.Key}' = '{val}';");
                }
            }
            sb.AppendLine(" }");
            sb.AppendLine($"& '{scriptPath}' @splat");

            string wrapperPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SPOPromptWrap_" + Guid.NewGuid().ToString("N") + ".ps1");
            System.IO.File.WriteAllText(wrapperPath, sb.ToString(), new System.Text.UTF8Encoding(false));

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = psExe,
                    // No -NoProfile: profile sets PSModulePath needed for Microsoft.Graph.Reports (OneDrive paths)
                    Arguments = $"-ExecutionPolicy Bypass -File \"{wrapperPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = true
                };
                psi.Environment["NO_COLOR"] = "1";

                using (var proc = Process.Start(psi))
                {
                    cancellationToken.Register(() => { try { if (!proc.HasExited) proc.Kill(); } catch { } });

                    var stdout = proc.StandardOutput;
                    var stdin = proc.StandardInput;

                    // Read stdout char-by-char so we can detect prompts that don't end with a newline.
                    var buffer = new System.Text.StringBuilder();
                    var lineBuffer = new System.Text.StringBuilder();
                    var promptRegex = new Regex(@"__PSPROMPT_START__(.*?)__PSPROMPT_END__", RegexOptions.Singleline);

                    // Rolling buffer of recent output lines to provide context for prompts.
                    var recentLines = new List<string>();
                    const int MaxContextLines = 10;

                    var readTask = Task.Run(async () =>
                    {
                        var charBuf = new char[1];
                        while (!proc.HasExited || !stdout.EndOfStream)
                        {
                            int read = await stdout.ReadAsync(charBuf, 0, 1).ConfigureAwait(false);
                            if (read <= 0) { await Task.Delay(20).ConfigureAwait(false); continue; }
                            char c = charBuf[0];
                            buffer.Append(c);

                            // Detect prompt marker as soon as it appears
                            string buf = buffer.ToString();
                            var m = promptRegex.Match(buf);
                            if (m.Success)
                            {
                                // Emit any text before the marker as a normal output line, then handle prompt
                                int markerStart = m.Index;
                                if (markerStart > 0)
                                {
                                    string pre = buf.Substring(0, markerStart);
                                    foreach (var ln in pre.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
                                    {
                                        if (!string.IsNullOrEmpty(ln))
                                        {
                                            string cleanLn = Regex.Replace(ln, @"\x1B\[[0-9;]*m", "");
                                            recentLines.Add(cleanLn);
                                            if (recentLines.Count > MaxContextLines) recentLines.RemoveAt(0);
                                            PostToUI(() => OnOutput?.Invoke(cleanLn));
                                        }
                                    }
                                }

                                string promptText = m.Groups[1].Value.Trim();
                                buffer.Clear();
                                buffer.Append(buf.Substring(m.Index + m.Length));

                                string answer = string.Empty;
                                if (OnPromptRequest != null)
                                {
                                    var ctx = recentLines.ToArray();
                                    var req = new PromptRequest
                                    {
                                        PromptText = promptText,
                                        Options = ParsePromptOptions(promptText),
                                        ContextLines = ctx,
                                        DescriptiveOptions = ParseDescriptiveOptions(ctx),
                                        QuestionText = ExtractQuestionFromContext(ctx)
                                    };
                                    try
                                    {
                                        answer = await OnPromptRequest(req).ConfigureAwait(false);
                                    }
                                    catch { answer = string.Empty; }
                                }
                                stdin.WriteLine(answer ?? string.Empty);
                                stdin.Flush();
                                continue;
                            }

                            if (c == '\n')
                            {
                                string line = buffer.ToString().TrimEnd('\r', '\n');
                                buffer.Clear();
                                if (!string.IsNullOrEmpty(line))
                                {
                                    string clean = Regex.Replace(line, @"\x1B\[[0-9;]*m", "");
                                    recentLines.Add(clean);
                                    if (recentLines.Count > MaxContextLines) recentLines.RemoveAt(0);
                                    PostToUI(() => OnOutput?.Invoke(clean));
                                }
                            }
                        }
                        // Flush any remaining buffered text
                        if (buffer.Length > 0)
                        {
                            string line = buffer.ToString().TrimEnd('\r', '\n');
                            if (!string.IsNullOrEmpty(line))
                                PostToUI(() => OnOutput?.Invoke(line));
                        }
                    });

                    var errTask = Task.Run(async () =>
                    {
                        string line;
                        while ((line = await proc.StandardError.ReadLineAsync().ConfigureAwait(false)) != null)
                        {
                            string clean = Regex.Replace(line, @"\x1B\[[0-9;]*m", "");
                            PostToUI(() => OnError?.Invoke(clean));
                        }
                    });

                    await Task.WhenAll(readTask, errTask).ConfigureAwait(false);
                    proc.WaitForExit();

                    if (proc.ExitCode != 0 && !cancellationToken.IsCancellationRequested)
                        throw new Exception($"Script exited with code {proc.ExitCode}");
                }
            }
            finally
            {
                try { System.IO.File.Delete(wrapperPath); } catch { }
            }
        }

        /// <summary>
        /// Parses prompt text like "Choose (Y/N)" or "Choose execution mode (F/S/D)" into ["Y","N"] or ["F","S","D"].
        /// Returns empty array if no choice list found (free-text prompt).
        /// </summary>
        private static string[] ParsePromptOptions(string promptText)
        {
            if (string.IsNullOrEmpty(promptText)) return Array.Empty<string>();
            var m = Regex.Match(promptText, @"[\(\[]([A-Za-z](?:\s*/\s*[A-Za-z])+)[\)\]]");
            if (!m.Success) return Array.Empty<string>();
            return m.Groups[1].Value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToUpperInvariant()).ToArray();
        }

        /// <summary>
        /// Extracts descriptive option labels from context lines like "[C] Continue | [R] Restart".
        /// Returns a dictionary mapping the key letter to the full label (e.g. "C" → "[C] Continue").
        /// </summary>
        private static Dictionary<string, string> ParseDescriptiveOptions(string[] contextLines)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (contextLines == null) return result;

            // Look for lines with pattern: [X] Label | [Y] Label ...
            var optLineRegex = new Regex(@"\[([A-Za-z])\]\s+\w+");
            foreach (var line in contextLines.Reverse())
            {
                var matches = optLineRegex.Matches(line);
                if (matches.Count >= 2)
                {
                    // Parse individual options separated by |
                    var parts = line.Split('|');
                    foreach (var part in parts)
                    {
                        var optMatch = Regex.Match(part.Trim(), @"\[([A-Za-z])\]\s+(.+)");
                        if (optMatch.Success)
                        {
                            string key = optMatch.Groups[1].Value.ToUpperInvariant();
                            string label = $"[{optMatch.Groups[1].Value}] {optMatch.Groups[2].Value.Trim()}";
                            result[key] = label;
                        }
                    }
                    break; // Use the first (most recent) matching line
                }
            }
            return result;
        }

        /// <summary>
        /// Finds the question/context line from recent output (the meaningful line before the options line).
        /// Only extracts if there IS a descriptive options line in context; otherwise returns null
        /// so the dialog can use the prompt text directly.
        /// </summary>
        internal static string ExtractQuestionFromContext(string[] contextLines)
        {
            if (contextLines == null || contextLines.Length == 0) return null;

            // Find the options line (contains [X] Label | [Y] Label)
            var optLineRegex = new Regex(@"\[([A-Za-z])\]\s+\w+.*\|.*\[([A-Za-z])\]\s+\w+");
            int optLineIdx = -1;
            for (int i = contextLines.Length - 1; i >= 0; i--)
            {
                if (optLineRegex.IsMatch(contextLines[i]))
                {
                    optLineIdx = i;
                    break;
                }
            }

            // Only extract question if we found a descriptive options line
            if (optLineIdx < 0) return null;

            // The question is the non-empty, non-decoration line just before the options line
            for (int i = optLineIdx - 1; i >= 0; i--)
            {
                string candidate = contextLines[i].Trim();
                if (!string.IsNullOrEmpty(candidate) && !IsDecorationLine(candidate))
                    return candidate;
            }

            return null;
        }

        /// <summary>
        /// Returns true if the line is purely decorative (separators, banners, box-drawing).
        /// </summary>
        private static bool IsDecorationLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return true;
            // Strip whitespace and check if remaining chars are all decoration
            string trimmed = line.Trim();
            return trimmed.All(c => c == '=' || c == '-' || c == '*' || c == '_' || c == '─' || c == '═' || c == '│' || c == '┌' || c == '┐' || c == '└' || c == '┘' || c == '█' || c == '▒');
        }

        /// <summary>
        /// Runs a script via external PowerShell process when the embedded pool is unavailable.
        /// Uses PS 7 only for PnP scripts (Start-FileArchiveSearch), PS 5.1 for everything else.
        /// Streams output line-by-line via OnOutput/OnError events.
        /// </summary>
        private async Task<PSDataCollection<PSObject>> RunScriptExternalAsync(string script, CancellationToken cancellationToken)
        {
            // Only Start-FileArchiveSearch uses PnP.PowerShell (requires PS 7.4+)
            bool needsPS7 = script.Contains("Start-FileArchiveSearch") || script.Contains("PnP");
            string psPath = needsPS7 ? (PS7Path ?? PS51Path) : (PS51Path ?? PS7Path);

            if (string.IsNullOrEmpty(psPath))
                throw new InvalidOperationException("No PowerShell installation found (neither pwsh nor powershell.exe).");

            var output = new PSDataCollection<PSObject>();

            await Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = psPath,
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                psi.Environment["NO_COLOR"] = "1";

                using (var proc = Process.Start(psi))
                {
                    cancellationToken.Register(() => { try { proc.Kill(); } catch { } });

                    // Read output line by line
                    proc.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            string line = System.Text.RegularExpressions.Regex.Replace(e.Data, @"\x1B\[[0-9;]*m", "");
                            PostToUI(() => OnOutput?.Invoke(line));
                            output.Add(PSObject.AsPSObject(line));
                        }
                    };
                    proc.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            string line = System.Text.RegularExpressions.Regex.Replace(e.Data, @"\x1B\[[0-9;]*m", "");
                            PostToUI(() => OnError?.Invoke(line));
                        }
                    };

                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit();

                    if (proc.ExitCode != 0 && !cancellationToken.IsCancellationRequested)
                        throw new Exception($"Script exited with code {proc.ExitCode}");
                }
            }, cancellationToken);

            return output;
        }

        public void Dispose()
        {
            _pool?.Close();
            _pool?.Dispose();
        }
    }
}
