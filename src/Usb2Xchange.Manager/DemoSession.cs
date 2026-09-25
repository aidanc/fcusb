// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Usb2Xchange.Manager
{
    // In-memory presentation model only. Never calls the live backend.
    internal static class DemoSession
    {
        internal static bool Enabled { get; private set; }
        internal static bool Installed { get; private set; }
        private static bool running;
        private static bool bound;
        private static bool registered;
        private static string pid;

        internal static void Begin()
        {
            Enabled = true;
            Installed = running = bound = registered = false;
            pid = "2002";
        }

        internal static void Decorate(Form form)
        {
            if (!Enabled) return;
            form.Text += " [DEMO]";
            int bottom = form.ClientSize.Height;
            form.ClientSize = new Size(form.ClientSize.Width, bottom + 34);
            Label banner = new Label();
            banner.Text = "DEMO - simulated hardware. No files, drivers or devices are changed.";
            banner.BackColor = Color.FromArgb(255, 239, 181);
            banner.TextAlign = ContentAlignment.MiddleCenter;
            banner.SetBounds(0, bottom, form.ClientSize.Width, 34);
            form.Controls.Add(banner);
        }

        internal static void SelectWinUsb()
        {
            if (!Enabled) throw new InvalidOperationException("Demo is not enabled.");
            bound = true;
        }

        internal static ScriptResult Run(string action)
        {
            if (!Enabled) throw new InvalidOperationException("Demo is not enabled.");
            switch (action)
            {
                case "Status":
                    return Ok(!Installed ? "Not installed. Choose Set up to try the walkthrough." :
                        "Installation: simulated private FlexColor copy\r\n" +
                        "Adapter: PID " + pid + (bound && registered ? " ready" : " needs setup") +
                        "\r\nSession: " + (running ? "running (simulated; FlexColor is not launched)" :
                        "stopped. " + (pid == "2003" && bound && registered ? "Choose Start FlexColor." : "Choose Adapter setup.")));
                case "DeviceStatus":
                    return Ok("Adapter state: " + (!bound ? "NeedsWinUsb" : !registered ? "NeedsInterfaceRegistration" : "Ready") +
                        "\r\nProduct ID: " + pid + " (simulated)\r\n" +
                        (!bound ? "Choose Simulate WinUSB selection. In real use, select Microsoft's inbox driver in Device Manager." :
                        !registered ? "Choose Register interface for this identity." :
                        pid == "2002" ? "Choose Initialize adapter. It will appear again as PID 2003." :
                        "Both identities are configured. Close this window and choose Start FlexColor."));
                case "Install": Installed = true; return Ok("Demo setup completed in memory. No licensed files are required or copied.");
                case "Uninstall": Begin(); return Ok("Demo reset. No files were removed.");
                case "OpenLogs": return Ok("Demo has no log files. Real sessions use the per-user Logs folder.");
                case "RegisterPresentInterface":
                    if (!Installed || !bound) return Fail("Set up first, then simulate WinUSB selection.");
                    registered = true; return Ok("Demo interface registered in memory. No UAC or registry changes.");
                case "InitializeAdapter":
                    if (!Installed || !bound || !registered) return Fail("Complete setup, WinUSB selection and registration first.");
                    if (pid == "2002") { pid = "2003"; bound = registered = false; }
                    return Ok("Demo PID 2003 is present. Select WinUSB and register its interface.");
                case "Start":
                    if (!Installed || pid != "2003" || !bound || !registered)
                        return Fail("Complete both adapter identities in Adapter setup before starting.");
                    running = true; return Ok("Simulated session started. No FlexColor process or scanner is opened.");
                case "Stop": running = false; return Ok("Simulated session stopped.");
                case "Repair": return Installed ? Ok("Demo repair completed. No files were read or changed.") : Fail("Set up first.");
                default: return Fail("Unknown demo action refused: " + action);
            }
        }

        private static ScriptResult Ok(string text) { return new ScriptResult(0, text); }
        private static ScriptResult Fail(string text) { return new ScriptResult(2, text); }

        internal static int SelfTest()
        {
            Begin();
            if (InstallationState.IsInstalled || ScriptRunner.Run("Start").ExitCode == 0 ||
                ScriptRunner.RunElevated("RegisterPresentInterface") == 0 ||
                ScriptRunner.Run("Unknown").ExitCode == 0) return 1;
            if (ScriptRunner.Run("Install").ExitCode != 0 || !InstallationState.IsInstalled) return 2;
            SelectWinUsb();
            if (ScriptRunner.RunElevated("RegisterPresentInterface") != 0 ||
                ScriptRunner.Run("InitializeAdapter").ExitCode != 0 || pid != "2003" ||
                ScriptRunner.Run("Start").ExitCode == 0) return 3;
            SelectWinUsb();
            if (ScriptRunner.RunElevated("RegisterPresentInterface") != 0 ||
                ScriptRunner.Run("Start").ExitCode != 0 || !running ||
                ScriptRunner.Run("Repair").ExitCode != 0 ||
                ScriptRunner.Run("OpenLogs").ExitCode != 0) return 4;
            if (ScriptRunner.Run("Stop").ExitCode != 0 || running) return 5;
            ScriptRunner.StartDetachedUninstall(0);
            return Installed || running || pid != "2002" ? 6 : 0;
        }

        // Documentation export renders these very same forms, not a recreated mockup.
        // This opt-in command writes only the requested PNGs and never enables live IO.
        internal static int ExportScreens(string directory)
        {
            Begin();
            Directory.CreateDirectory(directory);
            SaveForm(new MainForm(), directory, "01-manager.png");
            SaveForm(new SetupForm(), directory, "02-setup.png");
            Run("Install");
            SaveForm(new DeviceSetupForm(), directory, "03-adapter-loader.png");
            SelectWinUsb(); Run("RegisterPresentInterface"); Run("InitializeAdapter");
            SelectWinUsb(); Run("RegisterPresentInterface");
            SaveForm(new DeviceSetupForm(), directory, "04-adapter-ready.png");
            Run("Start");
            SaveForm(new MainForm(), directory, "05-session.png");
            return 0;
        }

        private static void SaveForm(Form form, string directory, string name)
        {
            using (form)
            {
                form.Show();
                Application.DoEvents();
                form.Refresh();
                using (Bitmap bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
                    using (FileStream stream = new FileStream(Path.Combine(directory, name), FileMode.CreateNew))
                        bitmap.Save(stream, ImageFormat.Png);
                }
                form.Close();
            }
        }
    }
}
