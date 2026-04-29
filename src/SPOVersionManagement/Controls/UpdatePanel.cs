using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json.Linq;
using SPOVersionManagement.Models;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    public class UpdatePanel : UserControl
    {
        private ConfigurationService _config;
        private ExecutionHistoryService _history;
        private PowerShellHostService _psHost;
        private GitHubUpdateService _updateService;
        private GitHubRelease _latestRelease;

        private Label _lblCurrentVer;
        private Label _lblLatestVer;
        private Label _lblStatus;
        private TextBox _txtReleaseNotes;
        private TextBox _txtStats;
        private FlatButton _btnCheck;
        private FlatButton _btnDownload;
        private FlatButton _btnRefreshStats;
        private FlatButton _btnInstallScripts;
        private ProgressBar _progressBar;
        private Label _lblProgress;

        private bool _canDownload;
        private bool _resizeHooked;
        private Timer _resizeDebounceTimer;

        public event EventHandler<string> StatusMessage;

        public UpdatePanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            AutoScroll = true;
            Padding = new Padding(28, 20, 28, 20);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public void Initialize(ConfigurationService config, PowerShellHostService psHost)
        {
            _config = config;
            _history = new ExecutionHistoryService(config);
            _psHost = psHost;

            string repo = config.AppConfig.GitHubRepo ?? "ivanoliv/SPOVersionManagement";
            string ver = config.AppConfig.AppVersion ?? "0.0.0.0";
            _updateService = new GitHubUpdateService(repo, ver);

            BuildLayout();
            // Fire-and-forget async check with proper exception handling
            _ = CheckForUpdatesAsyncSafe();
            // Load stats asynchronously to avoid blocking UI
            _ = RefreshStatsViewAsync();

            if (!_resizeHooked)
            {
                _resizeHooked = true;
                _resizeDebounceTimer = new Timer { Interval = 300 };
                _resizeDebounceTimer.Tick += (s, e) =>
                {
                    _resizeDebounceTimer.Stop();
                    if (_config == null)
                    {
                        return;
                    }

                    BuildLayout();
                    _ = RefreshStatsViewAsync();
                    if (_latestRelease != null)
                    {
                        UI(() => _txtReleaseNotes.Text = FormatReleaseNotes(_latestRelease));
                    }
                };

                Resize += (s, e) =>
                {
                    _resizeDebounceTimer.Stop();
                    _resizeDebounceTimer.Start();
                };
            }
        }

        /// <summary>
        /// Safe wrapper for fire-and-forget async check with exception handling.
        /// </summary>
        private async Task CheckForUpdatesAsyncSafe()
        {
            try
            {
                await CheckForUpdatesAsync();
            }
            catch (Exception ex)
            {
                // Log silently to prevent unhandled exceptions
                System.Diagnostics.Debug.WriteLine($"Update check failed: {ex.Message}");
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);
        }

        private void BuildLayout()
        {
            Controls.Clear();
            int y = 0;
            int w = Math.Max(760, ClientSize.Width - Padding.Horizontal - 16);

            Controls.Add(MakeLabel("Update / Stats", AppTheme.FontTitle, AppTheme.TextPrimary, 0, y));
            Controls.Add(MakeLabel("Check versions, update scripts, and monitor operational database status.", AppTheme.FontBody, AppTheme.TextSecondary, 0, y + 28));
            y += 56;

            int leftW = (w * 2 / 3) - 8;
            int rightW = w - leftW - 12;

            var verCard = new GlassPanel
            {
                Location = new Point(0, y),
                Size = new Size(leftW, 130),
                AccentLeft = AppTheme.AccentCyan,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            Controls.Add(verCard);

            verCard.Controls.Add(new Label
            {
                Text = "VERSION STATUS",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.AccentCyan,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 8)
            });

            verCard.Controls.Add(new Label
            {
                Text = "Installed:",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 36)
            });
            _lblCurrentVer = new Label
            {
                Text = _config?.AppConfig?.AppVersion ?? "?",
                Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(90, 30)
            };
            verCard.Controls.Add(_lblCurrentVer);

            verCard.Controls.Add(new Label
            {
                Text = "Latest:",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 66)
            });
            _lblLatestVer = new Label
            {
                Text = "Checking...",
                Font = new Font("Segoe UI Semibold", 14f, FontStyle.Bold),
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(90, 60)
            };
            verCard.Controls.Add(_lblLatestVer);

            _lblStatus = new Label
            {
                Text = string.Empty,
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.AccentGreen,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 96)
            };
            verCard.Controls.Add(_lblStatus);

            var repoCard = new GlassPanel
            {
                Location = new Point(leftW + 12, y),
                Size = new Size(rightW, 130),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            Controls.Add(repoCard);

            repoCard.Controls.Add(new Label
            {
                Text = "REPOSITORY",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 8)
            });
            repoCard.Controls.Add(new Label
            {
                Text = _config?.AppConfig?.GitHubRepo ?? "Not configured",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.AccentCyan,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 32)
            });
            repoCard.Controls.Add(new Label
            {
                Text = "Source: GitHub Releases API",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(14, 54)
            });

            y += 140;

            _btnCheck = new FlatButton
            {
                Text = "\u21BB  Check Now",
                Size = new Size(140, 34),
                Location = new Point(0, y)
            };
            _btnCheck.SetAccentColor(AppTheme.AccentCyan);
            _btnCheck.Click += async (s, e) => await CheckForUpdatesAsync();
            Controls.Add(_btnCheck);

            _btnDownload = new FlatButton
            {
                Text = "\u2B07  Download & Install",
                Size = new Size(180, 34),
                Location = new Point(150, y),
                Enabled = _canDownload
            };
            _btnDownload.SetAccentColor(AppTheme.AccentGreen);
            _btnDownload.Click += BtnDownload_Click;
            Controls.Add(_btnDownload);

            _btnInstallScripts = new FlatButton
            {
                Text = "\u2699  Update Scripts Folder",
                Size = new Size(200, 34),
                Location = new Point(340, y)
            };
            _btnInstallScripts.SetAccentColor(AppTheme.AccentGold);
            _btnInstallScripts.Click += async (s, e) => await RunInstallScriptAsync();
            Controls.Add(_btnInstallScripts);

            _btnRefreshStats = new FlatButton
            {
                Text = "\u21BB  Refresh Stats",
                Size = new Size(150, 34),
                Location = new Point(550, y)
            };
            _btnRefreshStats.SetGhostStyle();
            _btnRefreshStats.Click += (s, e) => RefreshStatsView();
            Controls.Add(_btnRefreshStats);

            y += 44;

            _progressBar = new ProgressBar
            {
                Location = new Point(0, y),
                Size = new Size(w, 6),
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100,
                Visible = false
            };
            Controls.Add(_progressBar);

            _lblProgress = new Label
            {
                Text = string.Empty,
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, y + 8),
                Visible = false
            };
            Controls.Add(_lblProgress);
            y += 26;

            Controls.Add(new Label
            {
                Text = "DATABASE STATS",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.AccentCyan,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, y)
            });
            y += 18;

            _txtStats = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(0, y),
                Size = new Size(w, 190),
                Font = AppTheme.FontMono,
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextSecondary,
                BorderStyle = BorderStyle.FixedSingle,
                WordWrap = false,
                Text = "Loading stats..."
            };
            Controls.Add(_txtStats);
            y += 198;

            Controls.Add(new Label
            {
                Text = "RELEASE NOTES / INSTALL OUTPUT",
                Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                ForeColor = AppTheme.AccentGold,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(0, y)
            });
            y += 18;

            _txtReleaseNotes = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(0, y),
                Size = new Size(w, 250),
                Font = AppTheme.FontMono,
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextSecondary,
                BorderStyle = BorderStyle.FixedSingle,
                WordWrap = true,
                Text = "Checking for release notes..."
            };
            Controls.Add(_txtReleaseNotes);
            y += 260;

            AutoScrollMinSize = new Size(0, y + 24);
        }

        /// <summary>
        /// Asynchronously loads and displays stats without blocking the UI thread.
        /// Large JSON files are parsed on a background thread with progress updates.
        /// </summary>
        private async Task RefreshStatsViewAsync()
        {
            if (_txtStats == null || _config == null)
            {
                return;
            }

            // Show loading message
            UI(() => _txtStats.Text = FormatStatsLoadingText("", 0));

            // Run expensive file I/O and JSON parsing on background thread with progress
            string statsText = await Task.Run(() => GenerateStatsTextWithProgress((percentage, status) =>
            {
                // Update UI on main thread
                UI(() => _txtStats.Text = FormatStatsLoadingText(status, percentage));
            }));
            
            // Final update on UI thread
            UI(() =>
            {
                if (_txtStats != null)
                {
                    _txtStats.Text = statsText;
                }
            });
        }

        /// <summary>
        /// Generates the stats text by reading and parsing large JSON files with progress callback.
        /// Must be called on a background thread.
        /// </summary>
        private string GenerateStatsTextWithProgress(Action<int, string> progressCallback)
        {
            try
            {
                var logsPath = _config.LogsPath;
                var configPath = _config.ConfigPath;
                var lines = new List<string>
                {
                    $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                    $"ConfigPath: {configPath}",
                    $"LogsPath  : {logsPath}",
                    new string('-', 104),
                    "File                            Exists  Last Write           Rows/Items   Size",
                    new string('-', 104)
                };

                // Define all files to process
                var fileStats = new (string, Func<int>)[]
                {
                    ("AllSites.json", () => CountArrayFromJson(Path.Combine(configPath, "AllSites.json"))),
                    ("ArchiveQueue.json", () => CountArrayFromJson(Path.Combine(configPath, "ArchiveQueue.json"))),
                    ("ArchiveAnalysis.json", () => CountArrayFromJson(Path.Combine(configPath, "ArchiveAnalysis.json"))),
                    ("SiteExecutionHistory.json", () => CountArrayFromJson(Path.Combine(configPath, "SiteExecutionHistory.json"))),
                    ("SessionHistory.json", () => CountArrayFromJson(Path.Combine(configPath, "SessionHistory.json"))),
                    ("ExecutionHistory.csv", () => CountCsvRows(Path.Combine(logsPath, "ExecutionHistory.csv"))),
                    ("JobStatus.json", () => CountDictionaryKeys(Path.Combine(configPath, "JobStatus.json"))),
                    ("RetentionPolicyDatabase.json", () => CountJsonItems(Path.Combine(configPath, "RetentionPolicyDatabase.json"))),
                    ("RetentionPolicyLog.json", () => CountJsonItems(Path.Combine(configPath, "RetentionPolicyLog.json"))),
                    ("ExcludedSites.json", () => CountJsonItems(Path.Combine(configPath, "ExcludedSites.json"))),
                    ("TenantStorage.json", () => CountJsonItems(Path.Combine(configPath, "TenantStorage.json"))),
                    ("TenantStorageTimeline.json", () => CountArrayFromJson(Path.Combine(configPath, "TenantStorageTimeline.json"))),
                    ("SiteStorage.csv", () => CountCsvRows(Path.Combine(logsPath, "SiteStorage.csv"))),
                };

                // Process each file with progress updates
                for (int i = 0; i < fileStats.Length; i++)
                {
                    var (fileName, countFactory) = fileStats[i];
                    int percentage = (int)((i + 1) * 100 / fileStats.Length);
                    progressCallback?.Invoke(percentage, $"Processing: {fileName}");
                    string fileRoot = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? logsPath : configPath;
                    AddFileStats(lines, fileRoot, fileName, countFactory);
                }

                lines.Add(string.Empty);
                lines.Add("Execution Totals");
                lines.Add(new string('-', 104));

                progressCallback?.Invoke(95, "Loading execution history...");
                var executions = _history.LoadExecutionHistory();
                var success = executions.Count(x => string.Equals(x.Status, "Success", StringComparison.OrdinalIgnoreCase));
                var running = executions.Count(x => string.Equals(x.Status, "Running", StringComparison.OrdinalIgnoreCase));
                var failed = executions.Count(x => string.Equals(x.Status, "Failed", StringComparison.OrdinalIgnoreCase));
                var queued = executions.Count(x => string.Equals(x.Status, "Queued", StringComparison.OrdinalIgnoreCase));

                lines.Add($"Total executions: {executions.Count}");
                lines.Add($"Success         : {success}");
                lines.Add($"Running         : {running}");
                lines.Add($"Failed          : {failed}");
                lines.Add($"Queued          : {queued}");

                progressCallback?.Invoke(100, "Complete!");
                return string.Join(Environment.NewLine, lines);
            }
            catch (Exception ex)
            {
                return "Failed to load stats:" + Environment.NewLine + ex.Message;
            }
        }

        private static string FormatStatsLoadingText(string status, int percentage)
        {
            string newline = Environment.NewLine;
            string normalizedStatus = (status ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace("\r", "\n")
                .Replace("\n", newline)
                .Trim();

            var lines = new List<string>
            {
                "Loading database stats..."
            };

            if (!string.IsNullOrWhiteSpace(normalizedStatus))
            {
                lines.Add(string.Empty);
                lines.Add(normalizedStatus);
            }

            lines.Add(string.Empty);
            lines.Add($"Processing files: {percentage}%");

            return string.Join(newline, lines);
        }

        /// <summary>
        /// Legacy synchronous method for backwards compatibility. 
        /// Consider using RefreshStatsViewAsync() instead.
        /// </summary>
        private void RefreshStatsView()
        {
            _ = RefreshStatsViewAsync();
        }

        private void AddFileStats(List<string> lines, string root, string fileName, Func<int> countFactory)
        {
            var path = Path.Combine(root, fileName);
            if (!File.Exists(path))
            {
                lines.Add($"{fileName,-32} {"No",-6} {"-",-20} {"-",-11} {"-"}");
                return;
            }

            var info = new FileInfo(path);
            int count;
            try
            {
                count = countFactory();
            }
            catch
            {
                count = -1;
            }

            var countText = count >= 0 ? count.ToString() : "parse err";
            lines.Add($"{fileName,-32} {"Yes",-6} {info.LastWriteTime:yyyy-MM-dd HH:mm:ss} {countText,-11} {FormatBytes(info.Length)}");
        }

        private static int CountArrayFromJson(string path)
        {
            var text = File.ReadAllText(path);
            var token = JToken.Parse(text);
            if (token is JArray arr)
            {
                return arr.Count;
            }

            if (token is JObject obj)
            {
                foreach (var key in new[] { "Sites", "Items", "Data", "Results", "History", "Entries" })
                {
                    if (obj[key] is JArray arrValue)
                    {
                        return arrValue.Count;
                    }
                }
            }

            return 0;
        }

        private static int CountDictionaryKeys(string path)
        {
            var text = File.ReadAllText(path);
            var token = JToken.Parse(text);
            return token is JObject obj ? obj.Properties().Count() : 0;
        }

        private static int CountJsonItems(string path)
        {
            var text = File.ReadAllText(path);
            var token = JToken.Parse(text);
            if (token is JArray arr)
            {
                return arr.Count;
            }

            if (token is JObject obj)
            {
                return obj.Properties().Count();
            }

            return 0;
        }

        private static int CountCsvRows(string path)
        {
            var total = File.ReadLines(path).Count();
            return Math.Max(0, total - 1);
        }

        private static string FormatBytes(long bytes)
        {
            const double kb = 1024.0;
            const double mb = kb * 1024.0;
            const double gb = mb * 1024.0;

            if (bytes >= gb) return $"{bytes / gb:0.00} GB";
            if (bytes >= mb) return $"{bytes / mb:0.00} MB";
            if (bytes >= kb) return $"{bytes / kb:0.00} KB";
            return $"{bytes} B";
        }

        private async Task RunInstallScriptAsync()
        {
            if (_config == null)
            {
                SetStatus("Configuration not loaded", false);
                return;
            }

            if (_psHost == null)
            {
                SetStatus("PowerShell host is not available", false);
                return;
            }

            var destination = ShowInputDialog("Install destination path:", "Update Scripts Folder", _config.RootPath);
            if (destination == null)
            {
                return;
            }

            bool force = MessageBox.Show(
                this,
                "Force install even if target version already exists?",
                "Update Scripts Folder",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) == DialogResult.Yes;

            var scriptPath = Path.Combine(_config.RootPath, "Install-SPOVersionManagement.ps1");
            if (!File.Exists(scriptPath))
            {
                SetStatus("Installer script not found: Install-SPOVersionManagement.ps1", false);
                return;
            }

            var args = $"-DestinationPath '{EscapePs(destination)}'";
            if (force)
            {
                args += " -Force";
            }
            args += " -NonInteractive";

            Action<string> outHandler = msg => AppendInstallOutput(msg);
            Action<string> warnHandler = msg => AppendInstallOutput("[WARN] " + msg);
            Action<string> errHandler = msg => AppendInstallOutput("[ERROR] " + msg);
            Action<int> progressHandler = pct => UI(() =>
            {
                if (_progressBar == null || _lblProgress == null)
                {
                    return;
                }

                _progressBar.Value = Math.Max(_progressBar.Minimum, Math.Min(_progressBar.Maximum, pct));
                _lblProgress.Text = $"Installing... {pct}%";
            });

            try
            {
                ToggleBusy(true, "Running installer...");
                AppendInstallOutput($"> Running {Path.GetFileName(scriptPath)} {args}");

                _psHost.OnOutput += outHandler;
                _psHost.OnWarning += warnHandler;
                _psHost.OnError += errHandler;
                _psHost.OnProgress += progressHandler;

                string command = $"& '{EscapePs(scriptPath)}' {args}";
                await _psHost.RunScriptAsync(command);

                SetStatus("Installer completed", true);
                RefreshStatsView();
            }
            catch (Exception ex)
            {
                SetStatus("Installer failed", false);
                AppendInstallOutput("[ERROR] " + ex.Message);
            }
            finally
            {
                _psHost.OnOutput -= outHandler;
                _psHost.OnWarning -= warnHandler;
                _psHost.OnError -= errHandler;
                _psHost.OnProgress -= progressHandler;
                ToggleBusy(false);
            }
        }

        private void AppendInstallOutput(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            UI(() =>
            {
                if (_txtReleaseNotes == null)
                {
                    return;
                }

                _txtReleaseNotes.AppendText(line + Environment.NewLine);
                _txtReleaseNotes.SelectionStart = _txtReleaseNotes.TextLength;
                _txtReleaseNotes.ScrollToCaret();
            });
        }

        private void ToggleBusy(bool busy, string statusText = null)
        {
            UI(() =>
            {
                if (_btnCheck != null) _btnCheck.Enabled = !busy;
                if (_btnDownload != null) _btnDownload.Enabled = !busy && _canDownload;
                if (_btnInstallScripts != null) _btnInstallScripts.Enabled = !busy;
                if (_btnRefreshStats != null) _btnRefreshStats.Enabled = !busy;

                if (_progressBar != null)
                {
                    _progressBar.Visible = busy;
                    if (busy)
                    {
                        _progressBar.Value = 0;
                    }
                }

                if (_lblProgress != null)
                {
                    _lblProgress.Visible = busy || !string.IsNullOrWhiteSpace(statusText);
                    _lblProgress.Text = statusText ?? string.Empty;
                }
            });
        }

        private void SetStatus(string message, bool ok)
        {
            UI(() =>
            {
                if (_lblStatus == null)
                {
                    return;
                }

                _lblStatus.Text = message ?? string.Empty;
                _lblStatus.ForeColor = ok ? AppTheme.AccentGreen : AppTheme.AccentRed;
                StatusMessage?.Invoke(this, message ?? string.Empty);
            });
        }

        private void UI(Action action)
        {
            if (IsDisposed || action == null)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(action);
                }
                catch
                {
                }
            }
            else
            {
                action();
            }
        }

        private static string EscapePs(string value)
        {
            return (value ?? string.Empty).Replace("'", "''");
        }

        private static string ShowInputDialog(string label, string title, string defaultValue)
        {
            using (var form = new Form())
            using (var txt = new TextBox())
            using (var lbl = new Label())
            using (var btnOk = new Button())
            using (var btnCancel = new Button())
            {
                form.Text = title;
                form.StartPosition = FormStartPosition.CenterParent;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MinimizeBox = false;
                form.MaximizeBox = false;
                form.ClientSize = new Size(560, 120);

                lbl.Text = label;
                lbl.AutoSize = true;
                lbl.Location = new Point(12, 12);

                txt.Text = defaultValue ?? string.Empty;
                txt.Location = new Point(12, 34);
                txt.Size = new Size(536, 20);

                btnOk.Text = "OK";
                btnOk.DialogResult = DialogResult.OK;
                btnOk.Location = new Point(392, 78);
                btnOk.Size = new Size(75, 28);

                btnCancel.Text = "Cancel";
                btnCancel.DialogResult = DialogResult.Cancel;
                btnCancel.Location = new Point(473, 78);
                btnCancel.Size = new Size(75, 28);

                form.Controls.Add(lbl);
                form.Controls.Add(txt);
                form.Controls.Add(btnOk);
                form.Controls.Add(btnCancel);
                form.AcceptButton = btnOk;
                form.CancelButton = btnCancel;

                return form.ShowDialog() == DialogResult.OK ? txt.Text : null;
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            // Disable check button on UI thread
            UI(() =>
            {
                if (_btnCheck != null)
                {
                    _btnCheck.Enabled = false;
                }

                _canDownload = false;
                if (_btnDownload != null)
                {
                    _btnDownload.Enabled = false;
                }

                if (_lblLatestVer != null)
                {
                    _lblLatestVer.Text = "Checking...";
                    _lblLatestVer.ForeColor = AppTheme.TextSecondary;
                }

                if (_lblStatus != null)
                {
                    _lblStatus.Text = string.Empty;
                }
            });

            try
            {
                _latestRelease = await _updateService.GetLatestReleaseAsync();
                
                // Marshal result back to UI thread
                UI(() =>
                {
                    if (_latestRelease == null)
                    {
                        if (_lblLatestVer != null)
                        {
                            _lblLatestVer.Text = "Unable to check";
                            _lblLatestVer.ForeColor = AppTheme.AccentRed;
                        }
                        SetStatus("Could not reach GitHub. Check your network or repository config.", false);
                        if (_txtReleaseNotes != null)
                        {
                            _txtReleaseNotes.Text = "Could not retrieve release information.";
                        }
                        return;
                    }

                    if (_lblLatestVer != null)
                    {
                        _lblLatestVer.Text = _latestRelease.VersionNumber;
                    }

                    bool isNewer = _updateService.IsNewerVersion(_latestRelease);
                    if (isNewer)
                    {
                        if (_lblLatestVer != null)
                        {
                            _lblLatestVer.ForeColor = AppTheme.AccentGreen;
                        }

                        _canDownload = true;
                        SetStatus("Update available", true);
                        StatusMessage?.Invoke(this, $"Update available: v{_latestRelease.VersionNumber}");
                    }
                    else
                    {
                        if (_lblLatestVer != null)
                        {
                            _lblLatestVer.ForeColor = AppTheme.TextPrimary;
                        }

                        _canDownload = false;
                        SetStatus("You are up to date", true);
                    }

                    if (_btnDownload != null)
                    {
                        _btnDownload.Enabled = _canDownload;
                    }

                    if (_txtReleaseNotes != null)
                    {
                        _txtReleaseNotes.Text = FormatReleaseNotes(_latestRelease);
                    }
                });
            }
            catch (Exception ex)
            {
                // Marshal error to UI thread
                UI(() =>
                {
                    if (_lblLatestVer != null)
                    {
                        _lblLatestVer.Text = "Error";
                        _lblLatestVer.ForeColor = AppTheme.AccentRed;
                    }

                    SetStatus(ex.Message, false);
                });
            }
            finally
            {
                // Re-enable button on UI thread
                UI(() =>
                {
                    if (_btnCheck != null)
                    {
                        _btnCheck.Enabled = true;
                    }
                });
            }
        }

        private async void BtnDownload_Click(object sender, EventArgs e)
        {
            if (_latestRelease?.Assets == null || _latestRelease.Assets.Count == 0)
            {
                MessageBox.Show("No downloadable assets in this release.", "No Assets", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            GitHubAsset zipAsset = null;
            foreach (var a in _latestRelease.Assets)
            {
                if (a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    zipAsset = a;
                    break;
                }
            }

            if (zipAsset == null)
            {
                MessageBox.Show("No ZIP asset found in this release.", "No ZIP", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(
                $"Download and install v{_latestRelease.VersionNumber}?\n\nAsset: {zipAsset.Name}\nSize: {zipAsset.Size / 1024} KB\n\nA backup will be created first.",
                "Confirm Update", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
            {
                return;
            }

            ToggleBusy(true, "Downloading...");

            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), zipAsset.Name);
                var progress = new Progress<int>(pct =>
                {
                    if (_progressBar != null)
                    {
                        _progressBar.Value = Math.Max(0, Math.Min(100, pct));
                    }

                    if (_lblProgress != null)
                    {
                        _lblProgress.Text = $"Downloading... {pct}%";
                    }
                });

                bool success = await _updateService.DownloadAssetAsync(zipAsset, tempPath, progress);
                if (!success)
                {
                    MessageBox.Show("Download failed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    SetStatus("Update download failed", false);
                    return;
                }

                if (_lblProgress != null)
                {
                    _lblProgress.Text = "Installing...";
                }

                var installer = new UpdateInstallerService(_config.RootPath);
                installer.InstallUpdate(tempPath);

                SetStatus("Update installed. Restart required.", true);
                if (_lblProgress != null)
                {
                    _lblProgress.Text = "Update installed. Restart to apply.";
                }

                MessageBox.Show("Update installed successfully.\nPlease restart the application.", "Update Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetStatus("Update failed", false);
                if (_lblProgress != null)
                {
                    _lblProgress.Text = "Error: " + ex.Message;
                }

                MessageBox.Show("Update failed:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ToggleBusy(false);
            }
        }

        private string FormatReleaseNotes(GitHubRelease release)
        {
            if (release == null)
            {
                return string.Empty;
            }

            string notes = $"Version: {release.VersionNumber}\n";
            notes += $"Published: {release.PublishedAt:yyyy-MM-dd HH:mm}\n";
            notes += $"Tag: {release.TagName}\n";
            if (release.Prerelease)
            {
                notes += "[PRE-RELEASE]\n";
            }

            notes += "\n" + new string('-', 60) + "\n\n";
            notes += release.Body ?? "(No release notes)";
            return notes;
        }

        private Label MakeLabel(string text, Font font, Color color, int x, int y)
        {
            return new Label
            {
                Text = text,
                Font = font,
                ForeColor = color,
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(x, y)
            };
        }
    }
}
