// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;

namespace Usb2Xchange.FlexColorPatch
{
    public enum PatchState
    {
        Original,
        Active,
        Partial,
        Unknown
    }

    public enum BackupState
    {
        Missing,
        Original,
        Invalid
    }

    public sealed class PatchSite
    {
        public PatchSite(int fileOffset, byte[] originalBytes,
            byte[] patchedBytes, string purpose)
        {
            if (fileOffset < 0)
            {
                throw new ArgumentOutOfRangeException("fileOffset");
            }
            if (originalBytes == null || patchedBytes == null ||
                originalBytes.Length == 0 ||
                originalBytes.Length != patchedBytes.Length)
            {
                throw new ArgumentException(
                    "Patch-site byte arrays must be nonempty and equal in length.");
            }

            FileOffset = fileOffset;
            OriginalBytes = (byte[])originalBytes.Clone();
            PatchedBytes = (byte[])patchedBytes.Clone();
            Purpose = purpose ?? string.Empty;
        }

        public int FileOffset { get; private set; }
        public byte[] OriginalBytes { get; private set; }
        public byte[] PatchedBytes { get; private set; }
        public string Purpose { get; private set; }
    }

    public sealed class PatchDefinition
    {
        public PatchDefinition(string name, long fileLength,
            string originalSha256, string patchedSha256, ushort machine,
            ushort optionalHeaderMagic, uint imageBase, PatchSite[] sites)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A patch name is required.", "name");
            }
            if (fileLength <= 0)
            {
                throw new ArgumentOutOfRangeException("fileLength");
            }
            if (!IsSha256(originalSha256) || !IsSha256(patchedSha256))
            {
                throw new ArgumentException(
                    "Original and patched SHA-256 values must contain 64 hex digits.");
            }
            if (sites == null || sites.Length == 0)
            {
                throw new ArgumentException("At least one patch site is required.",
                    "sites");
            }

            Name = name;
            FileLength = fileLength;
            OriginalSha256 = originalSha256.ToUpperInvariant();
            PatchedSha256 = patchedSha256.ToUpperInvariant();
            Machine = machine;
            OptionalHeaderMagic = optionalHeaderMagic;
            ImageBase = imageBase;
            Sites = (PatchSite[])sites.Clone();
            ValidateSites();
        }

        public string Name { get; private set; }
        public long FileLength { get; private set; }
        public string OriginalSha256 { get; private set; }
        public string PatchedSha256 { get; private set; }
        public ushort Machine { get; private set; }
        public ushort OptionalHeaderMagic { get; private set; }
        public uint ImageBase { get; private set; }
        public PatchSite[] Sites { get; private set; }

        private void ValidateSites()
        {
            var occupied = new HashSet<int>();
            foreach (PatchSite site in Sites)
            {
                if ((long)site.FileOffset + site.OriginalBytes.Length > FileLength)
                {
                    throw new ArgumentException("A patch site is outside the file.");
                }
                for (int i = 0; i < site.OriginalBytes.Length; ++i)
                {
                    if (!occupied.Add(site.FileOffset + i))
                    {
                        throw new ArgumentException("Patch sites overlap.");
                    }
                }
            }
        }

        private static bool IsSha256(string value)
        {
            if (value == null || value.Length != 64)
            {
                return false;
            }
            for (int i = 0; i < value.Length; ++i)
            {
                char c = value[i];
                if (!((c >= '0' && c <= '9') ||
                      (c >= 'a' && c <= 'f') ||
                      (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }
            return true;
        }
    }

    public sealed class PatchInspection
    {
        internal PatchInspection(string path, string sha256, long fileLength,
            PatchState state, string siteProfile, bool peMatches,
            string reason, string backupPath, BackupState backupState)
        {
            Path = path;
            Sha256 = sha256;
            FileLength = fileLength;
            State = state;
            SiteProfile = siteProfile;
            PeMatches = peMatches;
            Reason = reason;
            BackupPath = backupPath;
            BackupState = backupState;
        }

        public string Path { get; private set; }
        public string Sha256 { get; private set; }
        public long FileLength { get; private set; }
        public PatchState State { get; private set; }
        public string SiteProfile { get; private set; }
        public bool PeMatches { get; private set; }
        public string Reason { get; private set; }
        public string BackupPath { get; private set; }
        public BackupState BackupState { get; private set; }
    }

    public sealed class PatchActionResult
    {
        internal PatchActionResult(bool changed, string message,
            PatchInspection inspection)
        {
            Changed = changed;
            Message = message;
            Inspection = inspection;
        }

        public bool Changed { get; private set; }
        public string Message { get; private set; }
        public PatchInspection Inspection { get; private set; }
    }

    public sealed class PatchException : Exception
    {
        public PatchException(string message) : base(message)
        {
        }

        public PatchException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class FlexColorPatchEngine
    {
        public const string BackupSuffix = ".usb2xchange-original";

        private readonly PatchDefinition definition;
        private readonly string[] protectedRoots;

        public FlexColorPatchEngine(PatchDefinition definition,
            IEnumerable<string> protectedRoots)
        {
            if (definition == null)
            {
                throw new ArgumentNullException("definition");
            }
            this.definition = definition;

            var roots = new List<string>();
            if (protectedRoots != null)
            {
                foreach (string root in protectedRoots)
                {
                    if (!string.IsNullOrWhiteSpace(root))
                    {
                        roots.Add(NormalizePath(root));
                    }
                }
            }
            this.protectedRoots = roots.ToArray();
        }

        public PatchDefinition Definition
        {
            get { return definition; }
        }

        public static FlexColorPatchEngine CreateProduction()
        {
            var roots = new List<string>();
            AddEnvironmentRoot(roots, Environment.SpecialFolder.ProgramFiles);
            AddEnvironmentRoot(roots, Environment.SpecialFolder.ProgramFilesX86);
            string windows = Environment.GetFolderPath(
                Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(windows))
            {
                roots.Add(windows);
            }
            return new FlexColorPatchEngine(ProductionDefinition(), roots);
        }

        public static PatchDefinition ProductionDefinition()
        {
            return new PatchDefinition(
                "FlexColor 4.0.3 ASPI discovery patch",
                7602176,
                "B49217BA2BBFF2E9A9DF0952CC9657CD197C10022E2B62E3B818719FB78C1E84",
                "D250B6177D2B30FADD55E06DF612E7F534CB5D1A30E82AA9CC71A3F1924DD119",
                0x014c,
                0x010b,
                0x70000000,
                new PatchSite[]
                {
                    new PatchSite(0x0031C9D1,
                        new byte[] { 0x75, 0x58 },
                        new byte[] { 0x90, 0x90 },
                        "Enter direct backend enumeration"),
                    new PatchSite(0x0031C589,
                        new byte[] { 0x02 }, new byte[] { 0x01 },
                        "Select ASPI in focused enumeration"),
                    new PatchSite(0x0031C620,
                        new byte[] { 0x02 }, new byte[] { 0x01 },
                        "Select ASPI in main enumeration"),
                    new PatchSite(0x0031DC99,
                        new byte[] { 0x74 }, new byte[] { 0xEB },
                        "Load the ASPI provider on modern Windows")
                });
        }

        public PatchInspection Inspect(string path)
        {
            string target = RequireExistingFile(path);
            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(target);
            }
            catch (Exception exception)
            {
                throw new PatchException("Could not read " + target + ".",
                    exception);
            }

            BasicInspection basic = InspectBytes(bytes);
            string backupPath = target + BackupSuffix;
            BackupState backupState = InspectBackup(backupPath);
            return new PatchInspection(target, basic.Sha256, bytes.LongLength,
                basic.State, basic.SiteProfile, basic.PeMatches, basic.Reason,
                backupPath, backupState);
        }

        public PatchActionResult Activate(string path)
        {
            string target = RequireMutablePrivateFile(path);
            byte[] current = ReadAllBytes(target);
            BasicInspection inspection = InspectBytes(current);
            string backupPath = target + BackupSuffix;
            BackupState backupState = InspectBackup(backupPath);

            if (backupState == BackupState.Invalid)
            {
                throw new PatchException(
                    "The existing backup is not the exact supported original: " +
                    backupPath + ". Move it aside only after reviewing it.");
            }

            if (inspection.State == PatchState.Active)
            {
                if (backupState == BackupState.Missing)
                {
                    byte[] reconstructed = ApplySites(current, false);
                    RequireState(reconstructed, PatchState.Original,
                        "reconstructed original");
                    CreateVerifiedBackup(backupPath, reconstructed);
                }
                PatchInspection finalActive = Inspect(target);
                return new PatchActionResult(false,
                    "Patch is already active; no DLL bytes were changed.",
                    finalActive);
            }

            if (inspection.State != PatchState.Original)
            {
                throw RefusalForState(inspection);
            }

            if (backupState == BackupState.Missing)
            {
                CreateVerifiedBackup(backupPath, current);
            }

            byte[] candidate = ApplySites(current, true);
            RequireState(candidate, PatchState.Active, "patched candidate");
            ReplaceFromBytes(target, candidate, PatchState.Active);
            PatchInspection final = Inspect(target);
            return new PatchActionResult(true,
                "Patch activated. The exact original backup was retained at " +
                backupPath + ".", final);
        }

        public PatchActionResult Deactivate(string path)
        {
            string target = RequireMutablePrivateFile(path);
            byte[] current = ReadAllBytes(target);
            BasicInspection inspection = InspectBytes(current);
            string backupPath = target + BackupSuffix;
            BackupState backupState = InspectBackup(backupPath);

            if (backupState == BackupState.Invalid)
            {
                throw new PatchException(
                    "The existing backup is not the exact supported original: " +
                    backupPath + ". No file was changed.");
            }

            if (inspection.State == PatchState.Original)
            {
                PatchInspection finalOriginal = Inspect(target);
                return new PatchActionResult(false,
                    "Patch is already inactive; no DLL bytes were changed.",
                    finalOriginal);
            }

            if (inspection.State != PatchState.Active)
            {
                throw RefusalForState(inspection);
            }

            byte[] original;
            if (backupState == BackupState.Original)
            {
                original = ReadAllBytes(backupPath);
            }
            else
            {
                original = ApplySites(current, false);
            }
            RequireState(original, PatchState.Original, "restore candidate");
            ReplaceFromBytes(target, original, PatchState.Original);
            PatchInspection final = Inspect(target);
            return new PatchActionResult(true,
                "Patch deactivated. FlexColor.dll is byte-for-byte identical " +
                "to the supported original.", final);
        }

        private static void AddEnvironmentRoot(List<string> roots,
            Environment.SpecialFolder folder)
        {
            string value = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(value))
            {
                roots.Add(value);
            }
        }

        private PatchException RefusalForState(BasicInspection inspection)
        {
            return new PatchException(string.Format(CultureInfo.InvariantCulture,
                "Refusing to modify a {0} DLL (SHA-256 {1}). {2}",
                inspection.State.ToString().ToLowerInvariant(),
                inspection.Sha256, inspection.Reason));
        }

        private string RequireExistingFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new PatchException("An explicit FlexColor.dll path is required.");
            }
            string target = NormalizePath(path);
            if (!File.Exists(target))
            {
                throw new PatchException("File not found: " + target);
            }
            return target;
        }

        private string RequireMutablePrivateFile(string path)
        {
            string target = RequireExistingFile(path);
            foreach (string root in protectedRoots)
            {
                if (IsAtOrBelow(target, root))
                {
                    throw new PatchException(
                        "Refusing to modify an installed/system location: " +
                        target + ". Copy the complete FlexColor tree to a private " +
                        "writable directory and patch that copy.");
                }
            }
            RejectReparsePoints(target);
            return target;
        }

        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static bool IsAtOrBelow(string candidate, string root)
        {
            if (candidate.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            string prefix = root + Path.DirectorySeparatorChar;
            return candidate.StartsWith(prefix,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void RejectReparsePoints(string target)
        {
            string current = target;
            while (!string.IsNullOrEmpty(current))
            {
                FileAttributes attributes = File.GetAttributes(current);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new PatchException(
                        "Refusing a path containing a reparse point: " + current);
                }
                DirectoryInfo parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }
        }

        private BackupState InspectBackup(string backupPath)
        {
            if (!File.Exists(backupPath))
            {
                return BackupState.Missing;
            }
            try
            {
                return InspectBytes(File.ReadAllBytes(backupPath)).State ==
                    PatchState.Original ? BackupState.Original :
                    BackupState.Invalid;
            }
            catch (Exception)
            {
                return BackupState.Invalid;
            }
        }

        private void CreateVerifiedBackup(string backupPath, byte[] original)
        {
            RequireState(original, PatchState.Original, "backup candidate");
            string temporary = TemporaryPath(backupPath);
            try
            {
                WriteDurably(temporary, original);
                RequireState(ReadAllBytes(temporary), PatchState.Original,
                    "written backup");
                File.Move(temporary, backupPath);
                RequireState(ReadAllBytes(backupPath), PatchState.Original,
                    "saved backup");
            }
            catch (Exception exception)
            {
                SafeDeleteTemporary(temporary);
                if (exception is PatchException)
                {
                    throw;
                }
                throw new PatchException(
                    "Could not create the verified backup " + backupPath + ".",
                    exception);
            }
        }

        private void ReplaceFromBytes(string target, byte[] bytes,
            PatchState expectedState)
        {
            string temporary = TemporaryPath(target);
            try
            {
                WriteDurably(temporary, bytes);
                RequireState(ReadAllBytes(temporary), expectedState,
                    "written replacement");
                File.Replace(temporary, target, null);
                RequireState(ReadAllBytes(target), expectedState,
                    "installed replacement");
            }
            catch (Exception exception)
            {
                SafeDeleteTemporary(temporary);
                if (exception is PatchException)
                {
                    throw;
                }
                throw new PatchException(
                    "Atomic replacement failed; inspect the target and backup " +
                    "before retrying.", exception);
            }
        }

        private static void WriteDurably(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew,
                FileAccess.Write, FileShare.None, 64 * 1024,
                FileOptions.WriteThrough))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
        }

        private static string TemporaryPath(string target)
        {
            return target + ".usb2xchange-tmp-" +
                Guid.NewGuid().ToString("N");
        }

        private static void SafeDeleteTemporary(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
            }
        }

        private static byte[] ReadAllBytes(string path)
        {
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (Exception exception)
            {
                throw new PatchException("Could not read " + path + ".",
                    exception);
            }
        }

        private byte[] ApplySites(byte[] source, bool activate)
        {
            byte[] result = (byte[])source.Clone();
            foreach (PatchSite site in definition.Sites)
            {
                byte[] expected = activate ? site.OriginalBytes :
                    site.PatchedBytes;
                byte[] replacement = activate ? site.PatchedBytes :
                    site.OriginalBytes;
                if (!Matches(result, site.FileOffset, expected))
                {
                    throw new PatchException(string.Format(
                        CultureInfo.InvariantCulture,
                        "Patch site 0x{0:X8} does not contain the expected bytes.",
                        site.FileOffset));
                }
                Buffer.BlockCopy(replacement, 0, result, site.FileOffset,
                    replacement.Length);
            }
            return result;
        }

        private void RequireState(byte[] bytes, PatchState expected,
            string description)
        {
            BasicInspection inspection = InspectBytes(bytes);
            if (inspection.State != expected)
            {
                throw new PatchException(string.Format(
                    CultureInfo.InvariantCulture,
                    "The {0} failed exact verification: expected {1}, found {2} " +
                    "with SHA-256 {3}.", description, expected, inspection.State,
                    inspection.Sha256));
            }
        }

        private BasicInspection InspectBytes(byte[] bytes)
        {
            string sha256 = ComputeSha256(bytes);
            bool lengthMatches = bytes.LongLength == definition.FileLength;
            bool peMatches = lengthMatches && PeMatches(bytes);
            int originalSites = 0;
            int patchedSites = 0;
            int unexpectedSites = 0;

            foreach (PatchSite site in definition.Sites)
            {
                if (Matches(bytes, site.FileOffset, site.OriginalBytes))
                {
                    ++originalSites;
                }
                else if (Matches(bytes, site.FileOffset, site.PatchedBytes))
                {
                    ++patchedSites;
                }
                else
                {
                    ++unexpectedSites;
                }
            }

            string siteProfile = string.Format(CultureInfo.InvariantCulture,
                "original={0}, patched={1}, unexpected={2}", originalSites,
                patchedSites, unexpectedSites);
            PatchState state;
            string reason;
            if (peMatches && originalSites == definition.Sites.Length &&
                sha256 == definition.OriginalSha256)
            {
                state = PatchState.Original;
                reason = "Exact supported original.";
            }
            else if (peMatches && patchedSites == definition.Sites.Length &&
                sha256 == definition.PatchedSha256)
            {
                state = PatchState.Active;
                reason = "Exact supported patched image.";
            }
            else if (lengthMatches && unexpectedSites == 0 &&
                originalSites != 0 && patchedSites != 0)
            {
                state = PatchState.Partial;
                reason = "Only some patch sites are active.";
            }
            else
            {
                state = PatchState.Unknown;
                if (!lengthMatches)
                {
                    reason = "File length does not match the supported DLL.";
                }
                else if (!peMatches)
                {
                    reason = "PE identity does not match the supported DLL.";
                }
                else if (unexpectedSites != 0)
                {
                    reason = "One or more patch sites contain unexpected bytes.";
                }
                else
                {
                    reason = "Full-file SHA-256 does not match either exact image.";
                }
            }
            return new BasicInspection(state, sha256, peMatches, siteProfile,
                reason);
        }

        private bool PeMatches(byte[] bytes)
        {
            if (bytes.Length < 0x40 || bytes[0] != (byte)'M' ||
                bytes[1] != (byte)'Z')
            {
                return false;
            }
            int peOffset = ReadInt32(bytes, 0x3c);
            if (peOffset < 0 || (long)peOffset + 24 + 32 > bytes.LongLength)
            {
                return false;
            }
            if (bytes[peOffset] != (byte)'P' ||
                bytes[peOffset + 1] != (byte)'E' ||
                bytes[peOffset + 2] != 0 || bytes[peOffset + 3] != 0)
            {
                return false;
            }
            ushort machine = ReadUInt16(bytes, peOffset + 4);
            int optionalHeader = peOffset + 24;
            ushort magic = ReadUInt16(bytes, optionalHeader);
            uint imageBase = ReadUInt32(bytes, optionalHeader + 28);
            return machine == definition.Machine &&
                magic == definition.OptionalHeaderMagic &&
                imageBase == definition.ImageBase;
        }

        private static bool Matches(byte[] bytes, int offset, byte[] expected)
        {
            if (offset < 0 || (long)offset + expected.Length > bytes.LongLength)
            {
                return false;
            }
            for (int i = 0; i < expected.Length; ++i)
            {
                if (bytes[offset + i] != expected[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static ushort ReadUInt16(byte[] bytes, int offset)
        {
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static int ReadInt32(byte[] bytes, int offset)
        {
            return unchecked((int)ReadUInt32(bytes, offset));
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return (uint)bytes[offset] | ((uint)bytes[offset + 1] << 8) |
                ((uint)bytes[offset + 2] << 16) |
                ((uint)bytes[offset + 3] << 24);
        }

        private sealed class BasicInspection
        {
            internal BasicInspection(PatchState state, string sha256,
                bool peMatches, string siteProfile, string reason)
            {
                State = state;
                Sha256 = sha256;
                PeMatches = peMatches;
                SiteProfile = siteProfile;
                Reason = reason;
            }

            internal PatchState State { get; private set; }
            internal string Sha256 { get; private set; }
            internal bool PeMatches { get; private set; }
            internal string SiteProfile { get; private set; }
            internal string Reason { get; private set; }
        }
    }
}
