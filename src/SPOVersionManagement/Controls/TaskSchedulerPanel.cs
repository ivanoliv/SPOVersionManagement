using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows.Forms;
using Newtonsoft.Json;
using SPOVersionManagement.Services;
using SPOVersionManagement.Theme;

namespace SPOVersionManagement.Controls
{
    /// <summary>
    /// Task Scheduler panel — create, view, and manage Windows Scheduled Tasks for automated execution.
    /// </summary>
    public class TaskSchedulerPanel : UserControl
    {
        private ConfigurationService _config;
        private PowerShellHostService _psHost;

        private Panel _adminBanner;
        private DataGridView _tasksGrid;
        private FlatButton _btnCreate, _btnDelete, _btnRefresh, _btnEnable, _btnDisable, _btnRunNow;
        private Label _lblStatus;
        private bool _isAdmin;

        private const string TaskFolder = "\\SPOVersionManagement";
        private const string TaskPrefix = "SPOVersionMgmt";

        public event EventHandler<string> StatusMessage;

        public TaskSchedulerPanel()
        {
            Dock = DockStyle.Fill;
            BackColor = Color.Transparent;
            AutoScroll = true;
            Padding = new Padding(24, 20, 24, 20);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        public void Initialize(ConfigurationService config, PowerShellHostService psHost)
        {
            _config = config;
            _psHost = psHost;
            _isAdmin = IsRunningAsAdmin();
            BuildLayout();
            if (_isAdmin) RefreshTasks();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            AppTheme.PaintGradientBackground(e.Graphics, ClientRectangle);
        }

        private static bool IsRunningAsAdmin()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        private void BuildLayout()
        {
            Controls.Clear();
            int fullW = Math.Max(700, ClientSize.Width - Padding.Horizontal - 16);
            int y = 0;

            // ═══ HEADER ═══
            Controls.Add(new Label
            {
                Text = "Task Scheduler",
                Font = AppTheme.FontTitle,
                ForeColor = AppTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, y)
            });
            Controls.Add(new Label
            {
                Text = "Schedule unattended executions via Windows Task Scheduler.",
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(0, y + 28)
            });
            y += 56;

            // ═══ ADMIN BANNER ═══
            if (!_isAdmin)
            {
                _adminBanner = new Panel
                {
                    Location = new Point(0, y),
                    Size = new Size(fullW, 52),
                    BackColor = Color.FromArgb(40, 255, 193, 7)
                };
                Controls.Add(_adminBanner);

                _adminBanner.Controls.Add(new Label
                {
                    Text = "\u26A0  Administrator privileges required",
                    Font = new Font("Segoe UI", 10f, FontStyle.Bold),
                    ForeColor = AppTheme.AccentGold,
                    AutoSize = true,
                    Location = new Point(12, 4)
                });
                _adminBanner.Controls.Add(new Label
                {
                    Text = "Task Scheduler operations require elevated permissions. Restart the application as Administrator to create or modify scheduled tasks.",
                    Font = AppTheme.FontSmall,
                    ForeColor = AppTheme.TextSecondary,
                    AutoSize = false,
                    Size = new Size(fullW - 130, 20),
                    Location = new Point(12, 28)
                });

                var btnRelaunch = new FlatButton
                {
                    Text = "Run as Admin",
                    Size = new Size(110, 30),
                    Location = new Point(fullW - 120, 10)
                };
                btnRelaunch.SetAccentColor(AppTheme.AccentGold);
                btnRelaunch.Click += BtnRelaunchAsAdmin_Click;
                _adminBanner.Controls.Add(btnRelaunch);

                y += 60;
            }

            // ═══ TOOLBAR ═══
            int toolY = y;
            _btnRefresh = new FlatButton { Text = "\u21BB Refresh", Size = new Size(100, 32), Location = new Point(0, toolY) };
            _btnRefresh.SetGhostStyle();
            _btnRefresh.Click += (s, e) => RefreshTasks();
            _btnRefresh.Enabled = _isAdmin;
            Controls.Add(_btnRefresh);

            _btnCreate = new FlatButton { Text = "+ New Task", Size = new Size(110, 32), Location = new Point(108, toolY) };
            _btnCreate.SetAccentColor(AppTheme.AccentGreen);
            _btnCreate.Click += BtnCreate_Click;
            _btnCreate.Enabled = _isAdmin;
            Controls.Add(_btnCreate);

            _btnEnable = new FlatButton { Text = "\u25B6 Enable", Size = new Size(90, 32), Location = new Point(226, toolY) };
            _btnEnable.SetGhostStyle();
            _btnEnable.Enabled = false;
            _btnEnable.Click += BtnEnable_Click;
            Controls.Add(_btnEnable);

            _btnDisable = new FlatButton { Text = "\u25A0 Disable", Size = new Size(90, 32), Location = new Point(324, toolY) };
            _btnDisable.SetGhostStyle();
            _btnDisable.Enabled = false;
            _btnDisable.Click += BtnDisable_Click;
            Controls.Add(_btnDisable);

            _btnRunNow = new FlatButton { Text = "\u25B6 Run Now", Size = new Size(100, 32), Location = new Point(422, toolY) };
            _btnRunNow.SetGhostStyle();
            _btnRunNow.Enabled = false;
            _btnRunNow.Click += BtnRunNow_Click;
            Controls.Add(_btnRunNow);

            _btnDelete = new FlatButton { Text = "\u2716 Delete", Size = new Size(90, 32), Location = new Point(530, toolY) };
            _btnDelete.SetAccentColor(AppTheme.AccentRed);
            _btnDelete.Enabled = false;
            _btnDelete.Click += BtnDelete_Click;
            Controls.Add(_btnDelete);

            y = toolY + 42;

            // ═══ TASKS GRID ═══
            _tasksGrid = new DataGridView
            {
                Location = new Point(0, y),
                Size = new Size(fullW, 260),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Vertical
            };
            AppTheme.StyleDataGrid(_tasksGrid);
            _tasksGrid.SelectionChanged += TasksGrid_SelectionChanged;

            _tasksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Enabled", HeaderText = "Enabled", FillWeight = 10 });
            _tasksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "TaskName", HeaderText = "Task Name", FillWeight = 25 });
            _tasksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Schedule", HeaderText = "Schedule", FillWeight = 25 });
            _tasksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastRun", HeaderText = "Last Run", FillWeight = 18 });
            _tasksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "LastResult", HeaderText = "Result", FillWeight = 12 });
            _tasksGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "NextRun", HeaderText = "Next Run", FillWeight = 18 });
            Controls.Add(_tasksGrid);

            y += 270;

            // ═══ STATUS ═══
            _lblStatus = new Label
            {
                Location = new Point(0, y),
                AutoSize = true,
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextMuted,
                Text = _isAdmin ? "Ready" : "Elevate to manage tasks."
            };
            Controls.Add(_lblStatus);
        }

        private void TasksGrid_SelectionChanged(object sender, EventArgs e)
        {
            bool hasSelection = _isAdmin && _tasksGrid.SelectedRows.Count > 0;
            _btnEnable.Enabled = hasSelection;
            _btnDisable.Enabled = hasSelection;
            _btnDelete.Enabled = hasSelection;
            _btnRunNow.Enabled = hasSelection;
        }

        private void RefreshTasks()
        {
            _tasksGrid.Rows.Clear();
            if (!_isAdmin)
            {
                _lblStatus.Text = "Elevate to manage tasks.";
                return;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = $"/Query /TN \"{TaskFolder}\\\" /FO CSV /V",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(5000);

                    if (proc.ExitCode != 0)
                    {
                        // No tasks found is exit code 1
                        if (error.Contains("cannot find the") || error.Contains("does not exist"))
                        {
                            _lblStatus.Text = "No scheduled tasks found. Click '+ New Task' to create one.";
                            return;
                        }
                        _lblStatus.Text = $"schtasks query returned: {error.Trim()}";
                        return;
                    }

                    ParseScheduledTasks(output);
                }

                _lblStatus.Text = $"{_tasksGrid.Rows.Count} task(s) found.";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"Error querying tasks: {ex.Message}";
            }
        }

        private void ParseScheduledTasks(string csvOutput)
        {
            var lines = csvOutput.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) return;

            // CSV header: "HostName","TaskName","Next Run Time","Status","Logon Mode","Last Run Time","Last Result",...
            // We need: TaskName, Schedule info, Last Run, Last Result, Next Run
            string[] headers = ParseCsvLine(lines[0]);
            int iName = Array.FindIndex(headers, h => h.Contains("TaskName"));
            int iNextRun = Array.FindIndex(headers, h => h.Contains("Next Run"));
            int iStatus = Array.FindIndex(headers, h => h.Contains("Status"));
            int iLastRun = Array.FindIndex(headers, h => h.Contains("Last Run Time"));
            int iLastResult = Array.FindIndex(headers, h => h.Contains("Last Result"));
            int iSchedule = Array.FindIndex(headers, h => h.Contains("Schedule Type") || h.Contains("Scheduled Type"));
            // Fallback for schedule
            int iRepeat = Array.FindIndex(headers, h => h.Contains("Repeat"));

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] cols = ParseCsvLine(line);
                if (cols.Length <= Math.Max(iName, Math.Max(iLastRun, iLastResult))) continue;

                string name = iName >= 0 && iName < cols.Length ? cols[iName] : "?";
                // Strip folder prefix
                if (name.StartsWith(TaskFolder + "\\")) name = name.Substring(TaskFolder.Length + 1);

                string nextRun = iNextRun >= 0 && iNextRun < cols.Length ? cols[iNextRun] : "-";
                string status = iStatus >= 0 && iStatus < cols.Length ? cols[iStatus] : "-";
                string lastRun = iLastRun >= 0 && iLastRun < cols.Length ? cols[iLastRun] : "-";
                string lastResult = iLastResult >= 0 && iLastResult < cols.Length ? cols[iLastResult] : "-";
                string schedule = iSchedule >= 0 && iSchedule < cols.Length ? cols[iSchedule] : "-";

                bool enabled = !status.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
                string enabledText = enabled ? "\u2713" : "\u2717";

                int idx = _tasksGrid.Rows.Add(enabledText, name, schedule, lastRun, lastResult, nextRun);
                _tasksGrid.Rows[idx].Cells["Enabled"].Style.ForeColor = enabled ? AppTheme.AccentGreen : AppTheme.AccentRed;
                _tasksGrid.Rows[idx].Tag = (TaskFolder + "\\" + name, enabled);
            }
        }

        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var current = new System.Text.StringBuilder();
            foreach (char c in line)
            {
                if (c == '"') { inQuotes = !inQuotes; continue; }
                if (c == ',' && !inQuotes) { fields.Add(current.ToString().Trim()); current.Clear(); continue; }
                current.Append(c);
            }
            fields.Add(current.ToString().Trim());
            return fields.ToArray();
        }

        private void BtnCreate_Click(object sender, EventArgs e)
        {
            using (var dlg = new TaskCreateDialog(_config, $"{TaskPrefix}_Daily"))
            {
                if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
                {
                    CreateTask(dlg.TaskName, dlg.Trigger, dlg.TriggerTime, dlg.AdminUrl, dlg.ExtraArgs);
                    RefreshTasks();
                }
            }
        }

        private void CreateTask(string taskName, string trigger, string triggerTime, string adminUrl, string extraArgs)
        {
            try
            {
                string scriptPath = Path.Combine(_config.RootPath, "Start-SPOVersionManagement.ps1");
                string pwsh = "pwsh.exe";
                string args = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\" -AdminUrl \"{adminUrl}\" -Unattended";
                if (!string.IsNullOrWhiteSpace(extraArgs)) args += " " + extraArgs;

                string fullTaskName = $"{TaskFolder}\\{taskName}";

                // Build schtasks /Create command
                string schedArgs = $"/Create /TN \"{fullTaskName}\" /TR \"\\\"{pwsh}\\\" {args}\" " +
                                   $"/SC {trigger} /ST {triggerTime} /RL HIGHEST /F";

                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = schedArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(10000);

                    if (proc.ExitCode == 0)
                    {
                        _lblStatus.Text = $"Task '{taskName}' created successfully.";
                        StatusMessage?.Invoke(this, $"Scheduled task '{taskName}' created.");
                    }
                    else
                    {
                        MessageBox.Show($"Failed to create task:\n{error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating task:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_tasksGrid.SelectedRows.Count == 0) return;
            var (fullName, _) = ((string, bool))_tasksGrid.SelectedRows[0].Tag;

            if (MessageBox.Show($"Delete scheduled task?\n\n{fullName}", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            RunSchtasks($"/Delete /TN \"{fullName}\" /F", "deleted");
            RefreshTasks();
        }

        private void BtnEnable_Click(object sender, EventArgs e)
        {
            if (_tasksGrid.SelectedRows.Count == 0) return;
            var (fullName, _) = ((string, bool))_tasksGrid.SelectedRows[0].Tag;
            RunSchtasks($"/Change /TN \"{fullName}\" /ENABLE", "enabled");
            RefreshTasks();
        }

        private void BtnDisable_Click(object sender, EventArgs e)
        {
            if (_tasksGrid.SelectedRows.Count == 0) return;
            var (fullName, _) = ((string, bool))_tasksGrid.SelectedRows[0].Tag;
            RunSchtasks($"/Change /TN \"{fullName}\" /DISABLE", "disabled");
            RefreshTasks();
        }

        private void BtnRunNow_Click(object sender, EventArgs e)
        {
            if (_tasksGrid.SelectedRows.Count == 0) return;
            var (fullName, _) = ((string, bool))_tasksGrid.SelectedRows[0].Tag;
            RunSchtasks($"/Run /TN \"{fullName}\"", "triggered");
        }

        private void RunSchtasks(string arguments, string successVerb)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(10000);

                    if (proc.ExitCode == 0)
                    {
                        _lblStatus.Text = $"Task {successVerb} successfully.";
                        StatusMessage?.Invoke(this, $"Scheduled task {successVerb}.");
                    }
                    else
                    {
                        MessageBox.Show($"schtasks failed:\n{error}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnRelaunchAsAdmin_Click(object sender, EventArgs e)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--navigate=exec.scheduler",
                    Verb = "runas",
                    UseShellExecute = true
                };
                Process.Start(psi);
                Application.Exit();
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // User cancelled UAC
                _lblStatus.Text = "Elevation cancelled by user.";
            }
        }

        public void RefreshData() => RefreshTasks();
    }

    /// <summary>
    /// Dialog for creating a new scheduled task.
    /// </summary>
    internal class TaskCreateDialog : Form
    {
        public string TaskName { get; private set; }
        public string Trigger { get; private set; }
        public string TriggerTime { get; private set; }
        public string AdminUrl { get; private set; }
        public string ExtraArgs { get; private set; }

        public TaskCreateDialog(ConfigurationService config, string defaultTaskName)
        {
            Text = "Create Scheduled Task";
            Size = new Size(520, 560);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = AppTheme.BgDark;

            int y = 16;
            int ctrlX = 140;
            int ctrlW = 330;

            // Task Name
            AddLabel("Task Name:", 14, y);
            var txtName = AddTextBox(ctrlX, y, ctrlW, defaultTaskName);
            y += 30;

            // Admin URL
            string defaultUrl = config.AppConfig?.AdminUrl ?? "";
            AddLabel("Admin URL:", 14, y);
            var txtAdminUrl = AddTextBox(ctrlX, y, ctrlW, defaultUrl);
            y += 30;

            // Schedule
            AddLabel("Schedule:", 14, y);
            var cmbTrigger = new ComboBox
            {
                Location = new Point(ctrlX, y),
                Size = new Size(140, 24),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextPrimary
            };
            cmbTrigger.Items.AddRange(new object[] { "DAILY", "WEEKLY", "MONTHLY" });
            cmbTrigger.SelectedIndex = 0;
            Controls.Add(cmbTrigger);
            y += 30;

            // Time
            AddLabel("Start Time:", 14, y);
            var txtTime = AddTextBox(ctrlX, y, 80, "02:00");
            Controls.Add(new Label
            {
                Text = "(HH:mm)",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                Location = new Point(ctrlX + 90, y + 3)
            });
            y += 36;

            // ═══ EXECUTION OPTIONS ═══
            AddSectionLabel("EXECUTION OPTIONS", 14, y);
            y += 22;

            // DeleteOnly
            var chkDeleteOnly = AddCheckBox("Delete Only", ctrlX, y, true);
            y += 26;

            // SyncOnly
            var chkSyncOnly = AddCheckBox("Sync Only", ctrlX, y, false);
            y += 26;

            // Mutually exclusive: DeleteOnly / SyncOnly
            chkDeleteOnly.CheckedChanged += (s, e) => { if (chkDeleteOnly.Checked) chkSyncOnly.Checked = false; };
            chkSyncOnly.CheckedChanged += (s, e) => { if (chkSyncOnly.Checked) chkDeleteOnly.Checked = false; };

            // UseFileCache
            var chkUseFileCache = AddCheckBox("Use File Cache", ctrlX, y, true);
            y += 26;

            // ManageRetentionPolicy
            var chkRetention = AddCheckBox("Manage Retention Policy", ctrlX, y, false);
            y += 26;

            // SkipGraphConnection
            var chkSkipGraph = AddCheckBox("Skip Graph Connection", ctrlX, y, false);
            y += 30;

            // MaxConcurrentJobs (checkbox + value)
            var (chkMaxJobs, txtMaxJobs) = AddCheckBoxWithValue("Max Concurrent Jobs:", ctrlX, y, true, "10");
            y += 30;

            // CheckBatchSize (checkbox + value)
            var (chkBatchSize, txtBatchSize) = AddCheckBoxWithValue("Check Batch Size:", ctrlX, y, false, "10");
            y += 30;

            // CheckBatchDelaySeconds (checkbox + value)
            var (chkBatchDelay, txtBatchDelay) = AddCheckBoxWithValue("Batch Delay (sec):", ctrlX, y, false, "2");
            y += 36;

            // Info
            Controls.Add(new Label
            {
                Text = "Runs pwsh.exe with Start-SPOVersionManagement.ps1 -Unattended.\nRequires Entra ID App auth (TenantId/ClientId/Certificate in AppPaths.json).",
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextMuted,
                AutoSize = true,
                MaximumSize = new Size(ctrlW + 120, 0),
                Location = new Point(14, y)
            });
            y += 40;

            // Buttons
            var btnOk = new FlatButton { Text = "Create", Size = new Size(90, 30), Location = new Point(ctrlX + ctrlW - 192, y) };
            btnOk.SetAccentColor(AppTheme.AccentGreen);
            btnOk.Click += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtName.Text))
                { MessageBox.Show("Task name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
                if (string.IsNullOrWhiteSpace(txtAdminUrl.Text))
                { MessageBox.Show("Admin URL is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

                TaskName = txtName.Text.Trim();
                Trigger = cmbTrigger.SelectedItem.ToString();
                TriggerTime = txtTime.Text.Trim();
                AdminUrl = txtAdminUrl.Text.Trim();

                // Build extra args from checkboxes
                var args = new System.Text.StringBuilder();
                if (chkDeleteOnly.Checked) args.Append(" -DeleteOnly");
                if (chkSyncOnly.Checked) args.Append(" -SyncOnly");
                if (chkUseFileCache.Checked) args.Append(" -UseFileCache");
                if (chkRetention.Checked) args.Append(" -ManageRetentionPolicy");
                if (chkSkipGraph.Checked) args.Append(" -SkipGraphConnection");
                if (chkMaxJobs.Checked) args.Append($" -MaxConcurrentJobs {txtMaxJobs.Text.Trim()}");
                if (chkBatchSize.Checked) args.Append($" -CheckBatchSize {txtBatchSize.Text.Trim()}");
                if (chkBatchDelay.Checked) args.Append($" -CheckBatchDelaySeconds {txtBatchDelay.Text.Trim()}");
                ExtraArgs = args.ToString().Trim();

                DialogResult = DialogResult.OK;
            };
            Controls.Add(btnOk);

            var btnCancel = new FlatButton { Text = "Cancel", Size = new Size(90, 30), Location = new Point(ctrlX + ctrlW - 94, y) };
            btnCancel.SetGhostStyle();
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
            Controls.Add(btnCancel);
        }

        private void AddLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                Font = AppTheme.FontBody,
                ForeColor = AppTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(x, y + 3)
            });
        }

        private void AddSectionLabel(string text, int x, int y)
        {
            Controls.Add(new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 7f, FontStyle.Bold),
                ForeColor = AppTheme.AccentCyan,
                AutoSize = true,
                Location = new Point(x, y)
            });
        }

        private TextBox AddTextBox(int x, int y, int w, string defaultText)
        {
            var t = new TextBox
            {
                Location = new Point(x, y),
                Size = new Size(w, 24),
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.FontBody,
                Text = defaultText ?? ""
            };
            Controls.Add(t);
            return t;
        }

        private CheckBox AddCheckBox(string text, int x, int y, bool isChecked)
        {
            var chk = new CheckBox
            {
                Text = "  " + text,
                Font = AppTheme.FontSmall,
                ForeColor = AppTheme.TextPrimary,
                BackColor = Color.Transparent,
                AutoSize = true,
                Checked = isChecked,
                Location = new Point(x, y)
            };
            Controls.Add(chk);
            return chk;
        }

        private (CheckBox chk, TextBox txt) AddCheckBoxWithValue(string label, int x, int y, bool isChecked, string defaultValue)
        {
            AddLabel(label, 14, y);
            int chkX = x + 10;
            var chk = new CheckBox
            {
                Text = "",
                Size = new Size(18, 18),
                Checked = isChecked,
                BackColor = Color.Transparent,
                Location = new Point(chkX, y + 2)
            };
            Controls.Add(chk);

            var txt = new TextBox
            {
                Location = new Point(chkX + 24, y),
                Size = new Size(60, 22),
                BackColor = AppTheme.BgInput,
                ForeColor = AppTheme.TextPrimary,
                Font = AppTheme.FontBody,
                Text = defaultValue,
                Enabled = isChecked
            };
            Controls.Add(txt);

            chk.CheckedChanged += (s, e) => txt.Enabled = chk.Checked;
            return (chk, txt);
        }
    }
}
