// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Usb2Xchange.WinUsb;

namespace Usb2Xchange.AspiShim
{
    internal static class TrustedFlexColorProcessGate
    {
        internal const string ExecutableSha256 =
            "4C5D402F3668F06BEAFC871B9F152D55BF5ACCA191C43082C6A8C5647916AF28";
        internal const string PatchedDllSha256 =
            "D250B6177D2B30FADD55E06DF612E7F534CB5D1A30E82AA9CC71A3F1924DD119";
        internal const string OriginalDllSha256 =
            "B49217BA2BBFF2E9A9DF0952CC9657CD197C10022E2B62E3B818719FB78C1E84";

        internal static bool ValidateCurrentProcess(IUsbLog log)
        {
            try
            {
                string executable;
                using (Process process = Process.GetCurrentProcess())
                {
                    executable = process.MainModule.FileName;
                }
                string root = Path.GetDirectoryName(executable);
                string dllDirectory = Path.Combine(root, "DLLS");
                string flexColorDll = Path.Combine(dllDirectory,
                    "FlexColor.dll");
                string originalBackup = flexColorDll +
                    ".usb2xchange-original";

                if (!IsPrivateRoot(root) || HasReparsePointInAncestors(root) ||
                    HasReparsePoint(dllDirectory) ||
                    HasReparsePoint(executable) ||
                    HasReparsePoint(flexColorDll) ||
                    HasReparsePoint(originalBackup))
                {
                    Warn(log, "trusted pass-through process path is not an " +
                        "ordinary private tree");
                    return false;
                }

                bool trusted = IsTrustedIdentity(Path.GetFileName(executable),
                    Sha256(executable), Sha256(flexColorDll),
                    Sha256(originalBackup));
                if (!trusted)
                {
                    Warn(log, "trusted pass-through executable, patched DLL, " +
                        "or original backup identity did not match");
                    return false;
                }
                if (log != null)
                {
                    log.Info("Trusted FlexColor process gate passed exact " +
                        "English 4.0.3 executable, active patch, and original " +
                        "backup identities.");
                }
                return true;
            }
            catch (Exception ex)
            {
                Warn(log, "trusted pass-through process validation failed: " +
                    ex.GetType().FullName + ": " + ex.Message);
                return false;
            }
        }

        internal static bool IsTrustedIdentity(string executableName,
            string executableSha256, string patchedDllSha256,
            string originalDllSha256)
        {
            return string.Equals(executableName, "FlexColor.exe",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(executableSha256, ExecutableSha256,
                    StringComparison.Ordinal) &&
                string.Equals(patchedDllSha256, PatchedDllSha256,
                    StringComparison.Ordinal) &&
                string.Equals(originalDllSha256, OriginalDllSha256,
                    StringComparison.Ordinal);
        }

        private static bool IsPrivateRoot(string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return false;
            }
            string fullRoot = Path.GetFullPath(root).TrimEnd('\\');
            string[] installedRoots = new string[]
            {
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(
                    Environment.SpecialFolder.Windows)
            };
            foreach (string installedRootValue in installedRoots)
            {
                if (string.IsNullOrWhiteSpace(installedRootValue))
                {
                    continue;
                }
                string installedRoot = Path.GetFullPath(installedRootValue).
                    TrimEnd('\\');
                if (fullRoot.Equals(installedRoot,
                        StringComparison.OrdinalIgnoreCase) ||
                    fullRoot.StartsWith(installedRoot + "\\",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool HasReparsePoint(string path)
        {
            return (!File.Exists(path) && !Directory.Exists(path)) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        private static bool HasReparsePointInAncestors(string path)
        {
            string current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                if (HasReparsePoint(current))
                {
                    return true;
                }
                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }
            return false;
        }

        private static string Sha256(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open,
                FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(stream)).
                    Replace("-", string.Empty);
            }
        }

        private static void Warn(IUsbLog log, string message)
        {
            if (log != null)
            {
                log.Warning("Trusted FlexColor process gate refused: " +
                    message + ".");
            }
        }
    }
}
