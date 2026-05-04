using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Security;
using System.Windows.Forms;

namespace SPOVersionManagement.Services
{
    /// <summary>
    /// Custom PSHost that routes interactive prompts to WinForms MessageBox dialogs,
    /// allowing PowerShell scripts that call Read-Host, ShouldProcess, ConfirmImpact, etc.
    /// to work inside the GUI application.
    /// </summary>
    internal class GuiPSHost : PSHost
    {
        private readonly Guid _instanceId = Guid.NewGuid();
        private readonly GuiPSHostUserInterface _ui;

        public GuiPSHost(Action<string> outputCallback)
        {
            _ui = new GuiPSHostUserInterface(outputCallback);
        }

        public override CultureInfo CurrentCulture => CultureInfo.CurrentCulture;
        public override CultureInfo CurrentUICulture => CultureInfo.CurrentUICulture;
        public override Guid InstanceId => _instanceId;
        public override string Name => "SPOVersionManagement GUI Host";
        public override PSHostUserInterface UI => _ui;
        public override Version Version => new Version(1, 0);

        public override void EnterNestedPrompt() { }
        public override void ExitNestedPrompt() { }
        public override void NotifyBeginApplication() { }
        public override void NotifyEndApplication() { }
        public override void SetShouldExit(int exitCode) { }
    }

    /// <summary>
    /// PSHostUserInterface that intercepts prompts and shows MessageBox dialogs.
    /// </summary>
    internal class GuiPSHostUserInterface : PSHostUserInterface
    {
        private readonly Action<string> _outputCallback;
        private readonly GuiPSRawUserInterface _rawUI = new GuiPSRawUserInterface();

        public GuiPSHostUserInterface(Action<string> outputCallback)
        {
            _outputCallback = outputCallback;
        }

        public override PSHostRawUserInterface RawUI => _rawUI;

        public override string ReadLine()
        {
            string result = null;
            InvokeOnUIThread(() =>
            {
                using (var dlg = new InputDialog("PowerShell Input", "Enter value:"))
                {
                    if (dlg.ShowDialog() == DialogResult.OK)
                        result = dlg.InputValue;
                    else
                        result = string.Empty;
                }
            });
            return result;
        }

        public override SecureString ReadLineAsSecureString()
        {
            var secure = new SecureString();
            string plain = ReadLine();
            if (plain != null)
                foreach (char c in plain)
                    secure.AppendChar(c);
            return secure;
        }

        // Write/WriteLine are intentionally no-ops here.
        // Output is already captured by Streams.Information/Warning/Error handlers in RunScriptAsync.
        // Implementing these would cause duplicate lines since Write-Host feeds both the host UI AND Information stream.
        public override void Write(string value) { }
        public override void Write(ConsoleColor foregroundColor, ConsoleColor backgroundColor, string value) { }
        public override void WriteLine(string value) { }
        public override void WriteErrorLine(string value) { }
        public override void WriteWarningLine(string message) { }
        public override void WriteVerboseLine(string message) { }
        public override void WriteDebugLine(string message) { }
        public override void WriteProgress(long sourceId, ProgressRecord record) { }

        public override Dictionary<string, PSObject> Prompt(string caption, string message,
            Collection<FieldDescription> descriptions)
        {
            var results = new Dictionary<string, PSObject>();
            foreach (var field in descriptions)
            {
                string value = null;
                InvokeOnUIThread(() =>
                {
                    string prompt = string.IsNullOrEmpty(caption) ? field.Name : $"{caption}\n{message}\n\n{field.Name}:";
                    using (var dlg = new InputDialog("PowerShell Prompt", prompt))
                    {
                        if (dlg.ShowDialog() == DialogResult.OK)
                            value = dlg.InputValue;
                        else
                            value = string.Empty;
                    }
                });
                results[field.Name] = PSObject.AsPSObject(value);
            }
            return results;
        }

        public override int PromptForChoice(string caption, string message,
            Collection<ChoiceDescription> choices, int defaultChoice)
        {
            int result = defaultChoice;
            InvokeOnUIThread(() =>
            {
                string text = string.IsNullOrEmpty(caption)
                    ? message
                    : $"{caption}\n\n{message}";

                using (var dlg = new ChoiceDialog("SPO Version Management", text, choices, defaultChoice))
                {
                    dlg.ShowDialog();
                    result = dlg.SelectedIndex;
                }
            });
            return result;
        }

        public override PSCredential PromptForCredential(string caption, string message,
            string userName, string targetName)
        {
            return PromptForCredential(caption, message, userName, targetName,
                PSCredentialTypes.Default, PSCredentialUIOptions.Default);
        }

        public override PSCredential PromptForCredential(string caption, string message,
            string userName, string targetName, PSCredentialTypes allowedCredentialTypes,
            PSCredentialUIOptions options)
        {
            // Show a basic credential dialog
            PSCredential cred = null;
            InvokeOnUIThread(() =>
            {
                string user = userName;
                if (string.IsNullOrEmpty(user))
                {
                    using (var dlg = new InputDialog(caption ?? "Credentials", "Username:"))
                    {
                        if (dlg.ShowDialog() == DialogResult.OK)
                            user = dlg.InputValue;
                    }
                }

                if (!string.IsNullOrEmpty(user))
                {
                    using (var dlg = new InputDialog(caption ?? "Credentials", $"Password for {user}:", true))
                    {
                        if (dlg.ShowDialog() == DialogResult.OK)
                        {
                            var secure = new SecureString();
                            foreach (char c in dlg.InputValue)
                                secure.AppendChar(c);
                            cred = new PSCredential(user, secure);
                        }
                    }
                }
            });
            return cred;
        }

        private void InvokeOnUIThread(Action action)
        {
            if (Application.OpenForms.Count > 0)
            {
                var form = Application.OpenForms[0];
                if (form.InvokeRequired)
                    form.Invoke(action);
                else
                    action();
            }
            else
            {
                action();
            }
        }
    }

    /// <summary>
    /// Minimal PSHostRawUserInterface implementation.
    /// </summary>
    internal class GuiPSRawUserInterface : PSHostRawUserInterface
    {
        public override ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;
        public override ConsoleColor ForegroundColor { get; set; } = ConsoleColor.White;
        public override Size BufferSize { get; set; } = new Size(120, 50);
        public override Coordinates CursorPosition { get; set; } = new Coordinates(0, 0);
        public override int CursorSize { get; set; } = 1;
        public override bool KeyAvailable => false;
        public override Size MaxPhysicalWindowSize => new Size(200, 60);
        public override Size MaxWindowSize => new Size(200, 60);
        public override Coordinates WindowPosition { get; set; } = new Coordinates(0, 0);
        public override Size WindowSize { get; set; } = new Size(120, 50);
        public override string WindowTitle { get; set; } = "SPO Version Management";

        public override void FlushInputBuffer() { }
        public override BufferCell[,] GetBufferContents(Rectangle rectangle) => new BufferCell[0, 0];
        public override KeyInfo ReadKey(ReadKeyOptions options) => new KeyInfo();
        public override void ScrollBufferContents(Rectangle source, Coordinates destination,
            Rectangle clip, BufferCell fill) { }
        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents) { }
        public override void SetBufferContents(Rectangle rectangle, BufferCell fill) { }
    }

    /// <summary>
    /// Simple input dialog for PowerShell prompts — dark themed.
    /// </summary>
    internal class InputDialog : Form
    {
        private TextBox _textBox;
        public string InputValue => _textBox.Text;

        public InputDialog(string title, string prompt, bool isPassword = false)
        {
            Text = title;
            Size = new System.Drawing.Size(480, 210);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = System.Drawing.Color.FromArgb(20, 24, 36);
            ForeColor = System.Drawing.Color.White;
            Font = new System.Drawing.Font("Segoe UI", 9.5f);

            var iconLabel = new Label
            {
                Text = isPassword ? "\uD83D\uDD12" : "\u2328",
                Font = new System.Drawing.Font("Segoe UI", 16f),
                ForeColor = System.Drawing.Color.FromArgb(0, 212, 255),
                Location = new System.Drawing.Point(16, 16),
                AutoSize = true,
                BackColor = System.Drawing.Color.Transparent
            };
            Controls.Add(iconLabel);

            var lbl = new Label
            {
                Text = prompt,
                Location = new System.Drawing.Point(52, 16),
                Size = new System.Drawing.Size(400, 60),
                AutoSize = false,
                ForeColor = System.Drawing.Color.FromArgb(200, 200, 200),
                BackColor = System.Drawing.Color.Transparent,
                Font = new System.Drawing.Font("Segoe UI", 9.5f)
            };
            Controls.Add(lbl);

            _textBox = new TextBox
            {
                Location = new System.Drawing.Point(16, 88),
                Size = new System.Drawing.Size(432, 28),
                UseSystemPasswordChar = isPassword,
                BackColor = System.Drawing.Color.FromArgb(30, 36, 52),
                ForeColor = System.Drawing.Color.FromArgb(0, 212, 255),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new System.Drawing.Font("Cascadia Code", 10f)
            };
            Controls.Add(_textBox);

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(280, 130),
                Size = new System.Drawing.Size(80, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.FromArgb(0, 230, 118),
                ForeColor = System.Drawing.Color.FromArgb(20, 24, 36),
                Font = new System.Drawing.Font("Segoe UI", 9f, System.Drawing.FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnOk.FlatAppearance.BorderSize = 0;
            Controls.Add(btnOk);

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(370, 130),
                Size = new System.Drawing.Size(80, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = System.Drawing.Color.Transparent,
                ForeColor = System.Drawing.Color.FromArgb(0, 212, 255),
                Font = new System.Drawing.Font("Segoe UI", 9f),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(60, 80, 120);
            btnCancel.FlatAppearance.BorderSize = 1;
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }

    /// <summary>
    /// Dialog that shows each PowerShell choice as a clickable button — dark themed.
    /// </summary>
    internal class ChoiceDialog : Form
    {
        public int SelectedIndex { get; private set; }

        public ChoiceDialog(string title, string message,
            Collection<ChoiceDescription> choices, int defaultChoice)
        {
            SelectedIndex = defaultChoice;
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            AutoSize = false;
            BackColor = System.Drawing.Color.FromArgb(20, 24, 36);
            ForeColor = System.Drawing.Color.White;
            Font = new System.Drawing.Font("Segoe UI", 9.5f);

            int btnWidth = 150;
            int btnHeight = 40;
            int btnGap = 10;
            int margin = 20;

            // Icon
            var iconLabel = new Label
            {
                Text = "\u2753",
                Font = new System.Drawing.Font("Segoe UI", 18f),
                ForeColor = System.Drawing.Color.FromArgb(255, 193, 7),
                Location = new System.Drawing.Point(margin, margin),
                AutoSize = true,
                BackColor = System.Drawing.Color.Transparent
            };
            Controls.Add(iconLabel);

            // Message label
            var lbl = new Label
            {
                Text = message,
                Location = new System.Drawing.Point(margin + 40, margin),
                MaximumSize = new System.Drawing.Size(500, 0),
                AutoSize = true,
                ForeColor = System.Drawing.Color.FromArgb(200, 200, 200),
                BackColor = System.Drawing.Color.Transparent,
                Font = new System.Drawing.Font("Segoe UI", 10f)
            };
            Controls.Add(lbl);

            int msgHeight = Math.Max(lbl.PreferredHeight, 30);
            int btnY = margin + msgHeight + 24;

            // Separator line
            var sep = new Panel
            {
                Location = new System.Drawing.Point(margin, btnY - 10),
                Size = new System.Drawing.Size(500, 1),
                BackColor = System.Drawing.Color.FromArgb(40, 50, 70)
            };
            Controls.Add(sep);

            // Calculate total width needed for buttons
            int totalBtnWidth = choices.Count * btnWidth + (choices.Count - 1) * btnGap;
            int formWidth = Math.Max(totalBtnWidth + margin * 2, lbl.PreferredWidth + margin * 2 + 40);
            formWidth = Math.Max(formWidth, 400);

            int btnStartX = (formWidth - totalBtnWidth) / 2;

            for (int i = 0; i < choices.Count; i++)
            {
                int idx = i;
                string label = choices[i].Label.Replace("&", "");
                string helpText = choices[i].HelpMessage;

                var btn = new Button
                {
                    Text = label,
                    Size = new System.Drawing.Size(btnWidth, btnHeight),
                    Location = new System.Drawing.Point(btnStartX + i * (btnWidth + btnGap), btnY),
                    FlatStyle = FlatStyle.Flat,
                    Cursor = Cursors.Hand,
                    Font = new System.Drawing.Font("Segoe UI", 9.5f, System.Drawing.FontStyle.Bold)
                };

                if (i == defaultChoice)
                {
                    btn.BackColor = System.Drawing.Color.FromArgb(0, 212, 255);
                    btn.ForeColor = System.Drawing.Color.FromArgb(20, 24, 36);
                    btn.FlatAppearance.BorderSize = 0;
                }
                else
                {
                    btn.BackColor = System.Drawing.Color.Transparent;
                    btn.ForeColor = System.Drawing.Color.FromArgb(0, 212, 255);
                    btn.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(0, 212, 255);
                    btn.FlatAppearance.BorderSize = 1;
                }

                if (!string.IsNullOrEmpty(helpText))
                {
                    var tip = new ToolTip();
                    tip.SetToolTip(btn, helpText);
                }

                btn.Click += (s, e) =>
                {
                    SelectedIndex = idx;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                Controls.Add(btn);
            }

            sep.Size = new System.Drawing.Size(formWidth - margin * 2, 1);
            ClientSize = new System.Drawing.Size(formWidth, btnY + btnHeight + margin);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (DialogResult != DialogResult.OK)
                DialogResult = DialogResult.OK; // always return a result (default)
            base.OnFormClosing(e);
        }
    }
}
