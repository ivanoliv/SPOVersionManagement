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
    /// Simple input dialog for PowerShell prompts.
    /// </summary>
    internal class InputDialog : Form
    {
        private TextBox _textBox;
        public string InputValue => _textBox.Text;

        public InputDialog(string title, string prompt, bool isPassword = false)
        {
            Text = title;
            Size = new System.Drawing.Size(420, 180);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            var lbl = new Label
            {
                Text = prompt,
                Location = new System.Drawing.Point(12, 12),
                Size = new System.Drawing.Size(380, 60),
                AutoSize = false
            };
            Controls.Add(lbl);

            _textBox = new TextBox
            {
                Location = new System.Drawing.Point(12, 78),
                Size = new System.Drawing.Size(380, 22),
                UseSystemPasswordChar = isPassword
            };
            Controls.Add(_textBox);

            var btnOk = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new System.Drawing.Point(230, 110),
                Size = new System.Drawing.Size(75, 28)
            };
            Controls.Add(btnOk);

            var btnCancel = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new System.Drawing.Point(315, 110),
                Size = new System.Drawing.Size(75, 28)
            };
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }

    /// <summary>
    /// Dialog that shows each PowerShell choice as a clickable button.
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

            int btnWidth = 140;
            int btnHeight = 36;
            int btnGap = 8;
            int margin = 16;

            // Message label
            var lbl = new Label
            {
                Text = message,
                Location = new System.Drawing.Point(margin, margin),
                MaximumSize = new System.Drawing.Size(500, 0),
                AutoSize = true
            };
            Controls.Add(lbl);

            int btnY = lbl.PreferredHeight + margin + 20;

            // Calculate total width needed for buttons
            int totalBtnWidth = choices.Count * btnWidth + (choices.Count - 1) * btnGap;
            int formWidth = Math.Max(totalBtnWidth + margin * 2, lbl.PreferredWidth + margin * 2);
            formWidth = Math.Max(formWidth, 320);

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
                    FlatStyle = FlatStyle.Flat
                };

                if (i == defaultChoice)
                {
                    btn.Font = new System.Drawing.Font(btn.Font, System.Drawing.FontStyle.Bold);
                    btn.FlatAppearance.BorderColor = System.Drawing.Color.DodgerBlue;
                    btn.FlatAppearance.BorderSize = 2;
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
