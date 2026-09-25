// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("USB2Xchange for FlexColor")]
[assembly: AssemblyDescription(
    "Modern Windows manager for Adaptec USB2Xchange and FlexColor 4.0.3")]
[assembly: AssemblyCompany("USB2Xchange Community Project")]
[assembly: AssemblyProduct("USB2Xchange for FlexColor")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Usb2Xchange.Manager
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] arguments)
        {
            if (arguments.Length == 1 && arguments[0] == "--self-test")
            {
                return File.Exists(EndUserScriptPath) ? 0 : 1;
            }

            if (arguments.Length == 1 && arguments[0] == "--demo-self-test")
                return DemoSession.SelfTest();
            if (arguments.Length == 2 && arguments[0] == "--demo-export")
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                return DemoSession.ExportScreens(arguments[1]);
            }
            if (arguments.Length == 1 && arguments[0] == "--demo")
                DemoSession.Begin();
            else if (arguments.Length != 0 && !(arguments.Length == 1 && arguments[0] == "--start"))
                return 2;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (arguments.Length == 1 && arguments[0] == "--start")
            {
                if (!InstallationState.IsInstalled)
                {
                    MessageBox.Show(
                        "USB2Xchange has not been set up for this Windows user.",
                        "USB2Xchange", MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    Application.Run(new MainForm());
                    return 2;
                }
                ScriptResult result = ProgressDialog.Run(
                    "Starting FlexColor",
                    "Initializing the adapter and checking the scanner...",
                    delegate { return ScriptRunner.Run("Start"); });
                if (result.ExitCode != 0)
                {
                    ShowResult(result, "FlexColor started");
                }
                return result.ExitCode;
            }

            Application.Run(new MainForm());
            return 0;
        }

        internal static string EndUserScriptPath
        {
            get
            {
                string packaged = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "scripts", "Usb2Xchange-EndUser.ps1");
                if (File.Exists(packaged))
                {
                    return packaged;
                }
                return Path.GetFullPath(Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "..", "..",
                    "scripts", "Usb2Xchange-EndUser.ps1"));
            }
        }

        internal static void ShowResult(ScriptResult result, string successTitle)
        {
            string text = string.IsNullOrWhiteSpace(result.Output)
                ? (result.ExitCode == 0 ? "Completed successfully." :
                    "The operation did not complete.")
                : result.Output.Trim();
            MessageBox.Show(text,
                result.ExitCode == 0 ? successTitle : "USB2Xchange problem",
                MessageBoxButtons.OK,
                result.ExitCode == 0 ? MessageBoxIcon.Information :
                    MessageBoxIcon.Error);
        }
    }

    internal static class InstallationState
    {
        internal static string UserRoot
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                    "USB2Xchange");
            }
        }

        internal static string ConfigurationPath
        {
            get { return Path.Combine(UserRoot, "configuration.json"); }
        }

        internal static string InstalledManagerPath
        {
            get { return Path.Combine(UserRoot, "App", "USB2Xchange.exe"); }
        }

        internal static bool IsInstalled
        {
            get
            {
                if (DemoSession.Enabled) return DemoSession.Installed;
                return File.Exists(ConfigurationPath) &&
                    File.Exists(InstalledManagerPath);
            }
        }
    }

    internal static class ExperimentalRisk
    {
        internal const string Version = "fcusb-experimental-risk-v1";
        internal static bool RequiresAcknowledgement(string action)
        {
            return action == "Start" || action == "InitializeAdapter" ||
                action == "RegisterPresentInterface";
        }

        internal static bool EnsureAcknowledged()
        {
            string path = Path.Combine(InstallationState.UserRoot,
                "risk-acknowledgement.txt");
            try
            {
                if (File.Exists(path) && new FileInfo(path).Length == Version.Length &&
                    (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 &&
                    File.ReadAllText(path) == Version)
                {
                    return true;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }

            string warning = "EXPERIMENTAL SOFTWARE - USE AT YOUR OWN RISK.\r\n\r\n" +
                "Unofficial, unfinished interoperability software for advanced users and developers. " +
                "Not supported, approved, endorsed, or warranted by Hasselblad, Imacon, Adaptec, " +
                "Microsoft, or their successors. Tested only in the documented configurations.\r\n\r\n" +
                "This software communicates with legacy scanner hardware and loads firmware into " +
                "the USB2Xchange adapter and scanner. Defects, unsupported hardware, incorrect " +
                "installation, firmware problems, interrupted transfers, USB failures, or operator " +
                "error could cause failed scans, corrupted output, data loss, crashes, loss of device " +
                "access, malfunction, wasted film or time, and potentially hardware damage.\r\n\r\n" +
                "You are responsible for backups, VM snapshots, rollback, hash verification, " +
                "following instructions, protecting originals, and equipment suitability. Do not use " +
                "valuable originals or production equipment unless you understand and accept these risks.\r\n\r\n" +
                "There is no warranty, express or implied, or promise of support, recovery, " +
                "compatibility, or production fitness. To the maximum extent permitted by applicable " +
                "law, the owner and contributors are not liable for equipment damage, data loss, " +
                "failed scans, lost profits, business interruption, consequential loss, or other claims. " +
                "GPL version 3 warranty and liability provisions control; mandatory legal rights remain.\r\n\r\n" +
                "Read DISCLAIMER.md and the installation instructions. Accepting does not disable " +
                "technical safeguards or authorize dangerous development tests. Only a warning-version " +
                "marker is stored locally; nothing is sent.\r\n\r\n" +
                "Have you read and understood this warning and do you accept operation at your own risk?";
            if (MessageBox.Show(warning, "fcusb experimental hardware warning",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            {
                return false;
            }
            ScriptResult result = ScriptRunner.Run("AcknowledgeRisk");
            if (result.ExitCode != 0)
            {
                Program.ShowResult(result, "Risk acknowledgement");
                return false;
            }
            return true;
        }
    }

    internal sealed class ScriptResult
    {
        internal ScriptResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output ?? string.Empty;
        }

        internal int ExitCode { get; private set; }
        internal string Output { get; private set; }
    }

    internal static class ScriptRunner
    {
        internal static ScriptResult Run(string action,
            params KeyValuePair<string, string>[] options)
        {
            if (DemoSession.Enabled) return DemoSession.Run(action);
            if (ExperimentalRisk.RequiresAcknowledgement(action) &&
                !ExperimentalRisk.EnsureAcknowledged())
            {
                return new ScriptResult(2, "Hardware operation cancelled; risks were not acknowledged.");
            }
            if (!File.Exists(Program.EndUserScriptPath))
            {
                return new ScriptResult(1,
                    "The USB2Xchange end-user workflow is missing. " +
                    "Extract the complete package and try again.");
            }

            List<string> arguments = new List<string>();
            arguments.Add("-NoProfile");
            arguments.Add("-ExecutionPolicy");
            arguments.Add("Bypass");
            arguments.Add("-File");
            arguments.Add(Program.EndUserScriptPath);
            arguments.Add("-Action");
            arguments.Add(action);
            arguments.Add("-UserRoot");
            arguments.Add(InstallationState.UserRoot);
            foreach (KeyValuePair<string, string> option in options)
            {
                arguments.Add("-" + option.Key);
                if (option.Value != null)
                {
                    arguments.Add(option.Value);
                }
            }

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "powershell.exe";
            start.Arguments = JoinArguments(arguments);
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;

            try
            {
                using (Process process = Process.Start(start))
                {
                    StringBuilder output = new StringBuilder();
                    object outputLock = new object();
                    process.OutputDataReceived += delegate(object sender,
                        DataReceivedEventArgs eventArguments)
                    {
                        if (eventArguments.Data != null)
                        {
                            lock (outputLock)
                            {
                                output.AppendLine(eventArguments.Data);
                            }
                        }
                    };
                    process.ErrorDataReceived += delegate(object sender,
                        DataReceivedEventArgs eventArguments)
                    {
                        if (eventArguments.Data != null)
                        {
                            lock (outputLock)
                            {
                                output.AppendLine(eventArguments.Data);
                            }
                        }
                    };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                    return new ScriptResult(process.ExitCode,
                        output.ToString());
                }
            }
            catch (Exception exception)
            {
                return new ScriptResult(1, exception.Message);
            }
        }

        internal static int RunElevated(string action)
        {
            if (DemoSession.Enabled) return DemoSession.Run(action).ExitCode;
            if (ExperimentalRisk.RequiresAcknowledgement(action) &&
                !ExperimentalRisk.EnsureAcknowledged())
            {
                return 2;
            }
            List<string> arguments = new List<string>();
            arguments.Add("-NoProfile");
            arguments.Add("-ExecutionPolicy");
            arguments.Add("Bypass");
            arguments.Add("-File");
            arguments.Add(Program.EndUserScriptPath);
            arguments.Add("-Action");
            arguments.Add(action);
            arguments.Add("-UserRoot");
            arguments.Add(InstallationState.UserRoot);

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "powershell.exe";
            start.Arguments = JoinArguments(arguments);
            start.UseShellExecute = true;
            start.Verb = "runas";
            start.WindowStyle = ProcessWindowStyle.Hidden;
            using (Process process = Process.Start(start))
            {
                process.WaitForExit();
                return process.ExitCode;
            }
        }

        internal static void StartDetachedUninstall(int managerProcessId)
        {
            if (DemoSession.Enabled) { DemoSession.Run("Uninstall"); return; }
            List<string> arguments = new List<string>();
            arguments.Add("-NoProfile");
            arguments.Add("-ExecutionPolicy");
            arguments.Add("Bypass");
            arguments.Add("-File");
            arguments.Add(Program.EndUserScriptPath);
            arguments.Add("-Action");
            arguments.Add("Uninstall");
            arguments.Add("-WaitForProcessId");
            arguments.Add(managerProcessId.ToString());

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "powershell.exe";
            start.Arguments = JoinArguments(arguments);
            start.UseShellExecute = true;
            start.WindowStyle = ProcessWindowStyle.Normal;
            Process.Start(start);
        }

        private static string JoinArguments(IList<string> arguments)
        {
            StringBuilder result = new StringBuilder();
            for (int index = 0; index < arguments.Count; ++index)
            {
                if (index != 0)
                {
                    result.Append(' ');
                }
                result.Append(QuoteArgument(arguments[index]));
            }
            return result.ToString();
        }

        private static string QuoteArgument(string value)
        {
            if (value.Length != 0 && value.IndexOfAny(
                    new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
            {
                return value;
            }
            StringBuilder result = new StringBuilder();
            result.Append('"');
            int backslashes = 0;
            foreach (char character in value)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }
                if (character == '"')
                {
                    result.Append('\\', backslashes * 2 + 1);
                    result.Append('"');
                    backslashes = 0;
                    continue;
                }
                result.Append('\\', backslashes);
                backslashes = 0;
                result.Append(character);
            }
            result.Append('\\', backslashes * 2);
            result.Append('"');
            return result.ToString();
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly Label statusLabel;
        private readonly Button setupButton;
        private readonly Button startButton;
        private readonly Button stopButton;
        private readonly Button deviceButton;
        private readonly Button logsButton;
        private readonly Button uninstallButton;

        internal MainForm()
        {
            Text = "USB2Xchange for FlexColor";
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            ClientSize = new Size(610, 390);

            Label title = new Label();
            title.Text = "USB2Xchange for FlexColor";
            title.Font = new Font("Segoe UI Semibold", 18F);
            title.AutoSize = true;
            title.Location = new Point(24, 20);
            Controls.Add(title);

            Label description = new Label();
            description.Text =
                "Modern Windows 10/11 access for the Adaptec USB2Xchange " +
                "using inbox WinUSB.";
            description.AutoSize = true;
            description.Location = new Point(28, 62);
            Controls.Add(description);

            GroupBox statusGroup = new GroupBox();
            statusGroup.Text = "Current status";
            statusGroup.Location = new Point(24, 92);
            statusGroup.Size = new Size(562, 108);
            Controls.Add(statusGroup);

            statusLabel = new Label();
            statusLabel.Location = new Point(16, 26);
            statusLabel.Size = new Size(530, 68);
            statusLabel.Text = "Checking...";
            statusGroup.Controls.Add(statusLabel);

            startButton = NewButton("Start FlexColor", 24, 218, 174);
            startButton.Font = new Font("Segoe UI Semibold", 10F);
            startButton.Click += delegate { RunAction("Start",
                "Starting FlexColor",
                "Initializing the adapter and checking the scanner...",
                "FlexColor started"); };

            stopButton = NewButton("Stop FlexColor", 212, 218, 174);
            stopButton.Click += delegate { RunAction("Stop",
                "Stopping FlexColor", "Closing only the managed session...",
                "FlexColor stopped"); };

            setupButton = NewButton("Set up", 400, 218, 186);
            setupButton.Click += SetupClicked;

            deviceButton = NewButton("Adapter setup", 24, 272, 174);
            deviceButton.Click += delegate
            {
                using (DeviceSetupForm form = new DeviceSetupForm())
                {
                    form.ShowDialog(this);
                }
                RefreshStatus();
            };

            logsButton = NewButton("Open logs", 212, 272, 174);
            logsButton.Click += delegate
            {
                ScriptResult result = ScriptRunner.Run("OpenLogs");
                if (result.ExitCode != 0 || DemoSession.Enabled)
                {
                    Program.ShowResult(result, "Logs opened");
                }
            };

            uninstallButton = NewButton("Uninstall", 400, 272, 186);
            uninstallButton.Click += UninstallClicked;

            Button refreshButton = NewButton("Refresh status", 24, 334, 130);
            refreshButton.Click += delegate { RefreshStatus(); };
            Button guideButton = NewButton("User guide", 166, 334, 130);
            guideButton.Click += delegate {
                string guide = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "docs", "USER_GUIDE.html");
                if (!File.Exists(guide)) guide = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "docs", "USER_GUIDE.html"));
                if (File.Exists(guide)) Process.Start(new ProcessStartInfo(guide) { UseShellExecute = true });
                else MessageBox.Show("See docs/USER_GUIDE.md in the full package.");
            };
            Button demoButton = NewButton(DemoSession.Enabled ? "Reset demo" : "Try demo", 308, 334, 130);
            demoButton.Click += delegate {
                if (DemoSession.Enabled) { DemoSession.Begin(); RefreshStatus(); }
                else Process.Start(new ProcessStartInfo(Application.ExecutablePath, "--demo") { UseShellExecute = true });
            };
            Button closeButton = NewButton("Close", 456, 334, 130);
            closeButton.Click += delegate { Close(); };

            DemoSession.Decorate(this);
            Shown += delegate { RefreshStatus(); };
        }

        private Button NewButton(string text, int x, int y, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 38);
            Controls.Add(button);
            return button;
        }

        private void RefreshStatus()
        {
            ScriptResult result = ScriptRunner.Run("Status");
            statusLabel.Text = result.ExitCode == 0 ? result.Output.Trim() :
                "Status could not be read:\r\n" + result.Output.Trim();
            bool installed = InstallationState.IsInstalled;
            setupButton.Text = installed ? "Repair installation" : "Set up";
            startButton.Enabled = installed;
            stopButton.Enabled = installed;
            deviceButton.Enabled = installed;
            logsButton.Enabled = installed;
            uninstallButton.Enabled = installed;
        }

        private void SetupClicked(object sender, EventArgs e)
        {
            if (InstallationState.IsInstalled)
            {
                RunAction("Repair", "Repairing USB2Xchange",
                    "Verifying and rebuilding the private FlexColor copy...",
                    "Repair completed");
                return;
            }
            using (SetupForm form = new SetupForm())
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    RefreshStatus();
                    if (!DemoSession.Enabled && InstallationState.IsInstalled &&
                        !Path.GetFullPath(Application.ExecutablePath).Equals(
                            Path.GetFullPath(
                                InstallationState.InstalledManagerPath),
                            StringComparison.OrdinalIgnoreCase))
                    {
                        if (MessageBox.Show(
                            "Setup completed. Open the installed USB2Xchange " +
                            "Manager now?", "USB2Xchange",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question) == DialogResult.Yes)
                        {
                            Process.Start(
                                InstallationState.InstalledManagerPath);
                            Close();
                        }
                    }
                }
            }
        }

        private void RunAction(string action, string title,
            string message, string successTitle)
        {
            ScriptResult result = ProgressDialog.Run(title, message,
                delegate { return ScriptRunner.Run(action); });
            Program.ShowResult(result, successTitle);
            RefreshStatus();
        }

        private void UninstallClicked(object sender, EventArgs e)
        {
            if (MessageBox.Show(
                    "Remove the private FlexColor copy, USB2Xchange settings, " +
                    "and shortcuts for this user? The Microsoft WinUSB " +
                    "selection will be left intact.",
                    "Uninstall USB2Xchange", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }
            ScriptRunner.StartDetachedUninstall(Process.GetCurrentProcess().Id);
            Application.Exit();
        }
    }

    internal sealed class SetupForm : Form
    {
        private readonly TextBox sourceText;
        private readonly TextBox firmwareText;
        private readonly TextBox runtimeText;
        private readonly CheckBox desktopShortcut;

        internal SetupForm()
        {
            Text = "Set up USB2Xchange";
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(650, 370);

            Label heading = new Label();
            heading.Text = "One-time setup";
            heading.Font = new Font("Segoe UI Semibold", 16F);
            heading.AutoSize = true;
            heading.Location = new Point(22, 18);
            Controls.Add(heading);

            Label explanation = new Label();
            explanation.Text =
                "Select your licensed FlexColor 4.0.3 files and local " +
                "USB2Xchange firmware. These inputs remain private on this PC.";
            explanation.Location = new Point(26, 58);
            explanation.Size = new Size(596, 42);
            Controls.Add(explanation);

            sourceText = AddPathRow("FlexColor 4.0.3 folder", 108, true,
                @"C:\Program Files (x86)\Hasselblad\FlexColor English v4.0.3");
            firmwareText = AddPathRow("USB2Xchange firmware", 174, false,
                string.Empty);
            runtimeText = AddPathRow("VC++ 7.1 folder (optional)", 240, true,
                string.Empty);

            desktopShortcut = new CheckBox();
            desktopShortcut.Text = "Create a desktop shortcut";
            desktopShortcut.Checked = true;
            desktopShortcut.AutoSize = true;
            desktopShortcut.Location = new Point(26, 302);
            Controls.Add(desktopShortcut);

            Button install = new Button();
            install.Text = "Install";
            install.Location = new Point(438, 319);
            install.Size = new Size(90, 32);
            install.Click += InstallClicked;
            Controls.Add(install);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Location = new Point(536, 319);
            cancel.Size = new Size(90, 32);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);
            CancelButton = cancel;
            if (DemoSession.Enabled) {
                firmwareText.Text = @"C:\example-inputs\usb2xchange.fw";
                runtimeText.Text = @"C:\example-inputs\vc71";
            }
            DemoSession.Decorate(this);
        }

        private TextBox AddPathRow(string labelText, int y, bool folder,
            string initialValue)
        {
            Label label = new Label();
            label.Text = labelText;
            label.AutoSize = true;
            label.Location = new Point(26, y);
            Controls.Add(label);

            TextBox textBox = new TextBox();
            textBox.Text = initialValue;
            textBox.Location = new Point(26, y + 22);
            textBox.Size = new Size(500, 23);
            Controls.Add(textBox);

            Button browse = new Button();
            browse.Text = "Browse...";
            browse.Location = new Point(536, y + 20);
            browse.Size = new Size(90, 27);
            browse.Enabled = !DemoSession.Enabled;
            textBox.ReadOnly = DemoSession.Enabled;
            browse.Click += delegate
            {
                if (folder)
                {
                    using (FolderBrowserDialog dialog =
                        new FolderBrowserDialog())
                    {
                        dialog.Description = labelText;
                        if (Directory.Exists(textBox.Text))
                        {
                            dialog.SelectedPath = textBox.Text;
                        }
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            textBox.Text = dialog.SelectedPath;
                        }
                    }
                }
                else
                {
                    using (OpenFileDialog dialog = new OpenFileDialog())
                    {
                        dialog.Title = labelText;
                        dialog.Filter =
                            "USB2Xchange firmware (*.fw)|*.fw|All files (*.*)|*.*";
                        if (dialog.ShowDialog(this) == DialogResult.OK)
                        {
                            textBox.Text = dialog.FileName;
                        }
                    }
                }
            };
            Controls.Add(browse);
            return textBox;
        }

        private void InstallClicked(object sender, EventArgs e)
        {
            if (!DemoSession.Enabled && (!Directory.Exists(sourceText.Text) ||
                !File.Exists(firmwareText.Text)))
            {
                MessageBox.Show(
                    "Select the FlexColor folder and USB2Xchange firmware file.",
                    "Setup needs input", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            List<KeyValuePair<string, string>> options =
                new List<KeyValuePair<string, string>>();
            options.Add(new KeyValuePair<string, string>(
                "SourceRoot", sourceText.Text));
            options.Add(new KeyValuePair<string, string>(
                "AdapterFirmwarePath", firmwareText.Text));
            if (!string.IsNullOrWhiteSpace(runtimeText.Text))
            {
                options.Add(new KeyValuePair<string, string>(
                    "LegacyRuntimeRoot", runtimeText.Text));
            }
            if (!desktopShortcut.Checked)
            {
                options.Add(new KeyValuePair<string, string>(
                    "NoDesktopShortcut", null));
            }

            ScriptResult result = ProgressDialog.Run(
                "Installing USB2Xchange",
                "Validating the package and preparing FlexColor...",
                delegate
                {
                    return ScriptRunner.Run("Install", options.ToArray());
                });
            Program.ShowResult(result, "Setup completed");
            if (result.ExitCode == 0)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }

    internal sealed class DeviceSetupForm : Form
    {
        private readonly TextBox statusText;

        internal DeviceSetupForm()
        {
            Text = "USB2Xchange adapter setup";
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(660, 420);

            Label heading = new Label();
            heading.Text = "One-time adapter setup";
            heading.Font = new Font("Segoe UI Semibold", 16F);
            heading.AutoSize = true;
            heading.Location = new Point(22, 18);
            Controls.Add(heading);

            Label explanation = new Label();
            explanation.Text =
                "Windows must use its signed inbox WinUsb Device driver for " +
                "both adapter identities (PID 2002 and PID 2003). Follow the " +
                "current status below; no project kernel driver is installed.";
            explanation.Location = new Point(26, 58);
            explanation.Size = new Size(604, 56);
            Controls.Add(explanation);

            statusText = new TextBox();
            statusText.Multiline = true;
            statusText.ReadOnly = true;
            statusText.ScrollBars = ScrollBars.Vertical;
            statusText.Location = new Point(26, 120);
            statusText.Size = new Size(608, 130);
            Controls.Add(statusText);

            Button deviceManager = NewButton("Open Device Manager", 26, 270, 184);
            if (DemoSession.Enabled) deviceManager.Text = "Simulate WinUSB selection";
            deviceManager.Click += delegate
            {
                if (DemoSession.Enabled) { DemoSession.SelectWinUsb(); RefreshStatus(); return; }
                Process.Start("devmgmt.msc");
            };

            Button register = NewButton("Register interface", 224, 270, 184);
            register.Click += delegate
            {
                try
                {
                    int exitCode = ScriptRunner.RunElevated(
                        "RegisterPresentInterface");
                    MessageBox.Show(
                        exitCode == 0 ?
                            "The interface was registered. Refresh the status." :
                            "Interface registration did not complete.",
                        "USB2Xchange", MessageBoxButtons.OK,
                        exitCode == 0 ? MessageBoxIcon.Information :
                            MessageBoxIcon.Error);
                }
                catch (Win32Exception exception)
                {
                    MessageBox.Show(exception.Message, "USB2Xchange",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                RefreshStatus();
            };

            Button initialize = NewButton("Initialize adapter", 422, 270, 212);
            initialize.Click += delegate
            {
                ScriptResult result = ProgressDialog.Run(
                    "Initializing USB2Xchange",
                    "Uploading firmware and waiting for PID 2003...",
                    delegate { return ScriptRunner.Run("InitializeAdapter"); });
                Program.ShowResult(result, "Adapter initialized");
                RefreshStatus();
            };

            Button refresh = NewButton("Refresh", 26, 326, 130);
            refresh.Click += delegate { RefreshStatus(); };
            Button close = NewButton("Close", 504, 326, 130);
            close.Click += delegate { Close(); };

            DemoSession.Decorate(this);
            Shown += delegate { RefreshStatus(); };
        }

        private Button NewButton(string text, int x, int y, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Location = new Point(x, y);
            button.Size = new Size(width, 38);
            Controls.Add(button);
            return button;
        }

        private void RefreshStatus()
        {
            ScriptResult result = ScriptRunner.Run("DeviceStatus");
            statusText.Text = result.Output.Trim();
            if (result.ExitCode != 0)
            {
                statusText.Text = "Unable to read adapter status:\r\n" +
                    statusText.Text;
            }
        }
    }

    internal sealed class ProgressDialog : Form
    {
        private readonly Func<ScriptResult> operation;
        private readonly BackgroundWorker worker;
        private ScriptResult result;

        private ProgressDialog(string title, string message,
            Func<ScriptResult> operation)
        {
            this.operation = operation;
            Text = title;
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ControlBox = false;
            ClientSize = new Size(450, 125);

            Label label = new Label();
            label.Text = message;
            label.Location = new Point(20, 18);
            label.Size = new Size(410, 42);
            Controls.Add(label);

            ProgressBar progress = new ProgressBar();
            progress.Style = ProgressBarStyle.Marquee;
            progress.MarqueeAnimationSpeed = 28;
            progress.Location = new Point(20, 76);
            progress.Size = new Size(410, 20);
            Controls.Add(progress);

            worker = new BackgroundWorker();
            worker.DoWork += delegate
            {
                result = this.operation();
            };
            worker.RunWorkerCompleted += delegate { Close(); };
            Shown += delegate { worker.RunWorkerAsync(); };
        }

        internal static ScriptResult Run(string title, string message,
            Func<ScriptResult> operation)
        {
            using (ProgressDialog dialog =
                new ProgressDialog(title, message, operation))
            {
                dialog.ShowDialog();
                return dialog.result ?? new ScriptResult(1,
                    "The operation ended without a result.");
            }
        }
    }
}
