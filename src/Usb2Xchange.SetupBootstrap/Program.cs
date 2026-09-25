// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows.Forms;

[assembly: AssemblyTitle("USB2Xchange Setup")]
[assembly: AssemblyDescription(
    "Setup bootstrap for USB2Xchange for FlexColor")]
[assembly: AssemblyCompany("USB2Xchange Community Project")]
[assembly: AssemblyProduct("USB2Xchange for FlexColor")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Usb2Xchange.SetupBootstrap
{
    internal static class Program
    {
        private const string PayloadName = "Usb2Xchange.Payload.zip";

        [STAThread]
        private static int Main(string[] arguments)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool selfTest = arguments.Length == 1 &&
                arguments[0] == "--self-test";
            try
            {
                if (selfTest)
                {
                    return RunPayloadSelfTest();
                }
                if (arguments.Length != 0)
                {
                    throw new InvalidOperationException(
                        "This setup executable does not accept command-line options.");
                }
                return RunSetup();
            }
            catch (Exception exception)
            {
                if (!selfTest)
                {
                    MessageBox.Show(exception.Message, "USB2Xchange Setup",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return 1;
            }
        }

        private static int RunPayloadSelfTest()
        {
            string temporaryRoot = Path.Combine(Path.GetTempPath(),
                "USB2Xchange-Setup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            int result;
            try
            {
                ValidatePayload();
                string manager = ExtractPayload(temporaryRoot);
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = manager;
                start.Arguments = "--self-test";
                start.WorkingDirectory = Path.GetDirectoryName(manager);
                start.UseShellExecute = false;
                start.CreateNoWindow = true;
                using (Process process = Process.Start(start))
                {
                    process.WaitForExit();
                    result = process.ExitCode;
                }
            }
            finally
            {
                DeleteTemporaryTree(temporaryRoot);
            }
            if (Directory.Exists(temporaryRoot))
            {
                throw new IOException(
                    "The setup self-test could not remove its temporary files.");
            }
            return result;
        }

        private static int RunSetup()
        {
            string temporaryRoot = Path.Combine(Path.GetTempPath(),
                "USB2Xchange-Setup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);
            try
            {
                string manager = ExtractPayload(temporaryRoot);
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = manager;
                start.WorkingDirectory = Path.GetDirectoryName(manager);
                start.UseShellExecute = true;
                using (Process process = Process.Start(start))
                {
                    process.WaitForExit();
                    return process.ExitCode;
                }
            }
            finally
            {
                DeleteTemporaryTree(temporaryRoot);
            }
        }

        private static void ValidatePayload()
        {
            using (Stream stream = OpenPayload())
            using (ZipArchive archive = new ZipArchive(stream,
                ZipArchiveMode.Read, false))
            {
                string root = GetSingleRoot(archive);
                string expectedManager = root + "/USB2Xchange.exe";
                bool managerFound = false;
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string path = NormalizeEntry(entry.FullName);
                    if (path.Equals(expectedManager,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        managerFound = entry.Length > 0;
                    }
                }
                if (!managerFound)
                {
                    throw new InvalidDataException(
                        "The embedded USB2Xchange manager is missing.");
                }
            }
        }

        private static string ExtractPayload(string destinationRoot)
        {
            string rootPrefix = Path.GetFullPath(destinationRoot).
                TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string packageRoot;
            using (Stream stream = OpenPayload())
            using (ZipArchive archive = new ZipArchive(stream,
                ZipArchiveMode.Read, false))
            {
                packageRoot = GetSingleRoot(archive);
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string relative = NormalizeEntry(entry.FullName);
                    string destination = Path.GetFullPath(Path.Combine(
                        destinationRoot,
                        relative.Replace('/', Path.DirectorySeparatorChar)));
                    if (!destination.StartsWith(rootPrefix,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "The embedded package contains an unsafe path.");
                    }
                    if (relative.EndsWith("/", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(destination);
                        continue;
                    }
                    string parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }
                    using (Stream input = entry.Open())
                    using (FileStream output = new FileStream(destination,
                        FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        input.CopyTo(output);
                    }
                }
            }

            string manager = Path.Combine(destinationRoot, packageRoot,
                "USB2Xchange.exe");
            if (!File.Exists(manager))
            {
                throw new InvalidDataException(
                    "The embedded USB2Xchange manager was not extracted.");
            }
            return manager;
        }

        private static Stream OpenPayload()
        {
            Stream stream = Assembly.GetExecutingAssembly().
                GetManifestResourceStream(PayloadName);
            if (stream == null)
            {
                throw new InvalidDataException(
                    "The embedded USB2Xchange package is missing.");
            }
            return stream;
        }

        private static string GetSingleRoot(ZipArchive archive)
        {
            HashSet<string> roots = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                string relative = NormalizeEntry(entry.FullName);
                int slash = relative.IndexOf('/');
                if (slash <= 0)
                {
                    throw new InvalidDataException(
                        "The embedded package has no single root directory.");
                }
                roots.Add(relative.Substring(0, slash));
            }
            if (roots.Count != 1)
            {
                throw new InvalidDataException(
                    "The embedded package has multiple root directories.");
            }
            foreach (string root in roots)
            {
                return root;
            }
            throw new InvalidDataException("The embedded package is empty.");
        }

        private static string NormalizeEntry(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(
                    "The embedded package contains an empty path.");
            }
            string relative = value.Replace('\\', '/');
            if (relative.StartsWith("/", StringComparison.Ordinal) ||
                relative.IndexOf(':') >= 0)
            {
                throw new InvalidDataException(
                    "The embedded package contains an absolute path.");
            }
            string[] parts = relative.Split('/');
            foreach (string part in parts)
            {
                if (part == "..")
                {
                    throw new InvalidDataException(
                        "The embedded package contains a parent path.");
                }
            }
            return relative;
        }

        private static void DeleteTemporaryTree(string path)
        {
            string full = Path.GetFullPath(path).TrimEnd(
                Path.DirectorySeparatorChar);
            string expectedParent = Path.GetFullPath(Path.GetTempPath()).
                TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!full.StartsWith(expectedParent + "USB2Xchange-Setup-",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            for (int attempt = 0; attempt != 20; ++attempt)
            {
                try
                {
                    if (Directory.Exists(full))
                    {
                        Directory.Delete(full, true);
                    }
                    return;
                }
                catch (IOException)
                {
                    System.Threading.Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
        }
    }
}
