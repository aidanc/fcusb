// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.IO;
using System.Security.Cryptography;
using Usb2Xchange.FlexColorPatch;

namespace Usb2Xchange.FlexColorPatch.Tests
{
    internal static class Program
    {
        private static int passed;
        private static int failed;

        private static int Main()
        {
            Run("production manifest is pinned", TestProductionManifest);
            Run("status recognizes the exact original", TestOriginalInspection);
            Run("activate patches exact sites and creates backup", TestActivate);
            Run("deactivate restores the exact original", TestDeactivate);
            Run("activate and deactivate are idempotent", TestIdempotence);
            Run("unknown full-file hash is refused", TestWrongHash);
            Run("partial patch is refused", TestPartialPatch);
            Run("unexpected site bytes are refused", TestUnexpectedSite);
            Run("invalid existing backup is refused", TestInvalidBackup);
            Run("active image without backup remains reversible",
                TestActiveWithoutBackup);
            Run("protected path is read-only", TestProtectedPath);

            Console.WriteLine("Passed: {0}; Failed: {1}", passed, failed);
            return failed == 0 ? 0 : 1;
        }

        private static void TestProductionManifest()
        {
            PatchDefinition definition =
                FlexColorPatchEngine.ProductionDefinition();
            Equal(7602176L, definition.FileLength);
            Equal(
                "B49217BA2BBFF2E9A9DF0952CC9657CD197C10022E2B62E3B818719FB78C1E84",
                definition.OriginalSha256);
            Equal(
                "D250B6177D2B30FADD55E06DF612E7F534CB5D1A30E82AA9CC71A3F1924DD119",
                definition.PatchedSha256);
            Equal(4, definition.Sites.Length);
            Equal(0x0031C9D1, definition.Sites[0].FileOffset);
            Equal(5, CountChangedBytes(definition.Sites));
        }

        private static void TestOriginalInspection()
        {
            WithFixture(delegate(string path, PatchDefinition definition,
                byte[] original)
            {
                FlexColorPatchEngine engine = Engine(definition);
                PatchInspection inspection = engine.Inspect(path);
                Equal(PatchState.Original, inspection.State);
                Equal(BackupState.Missing, inspection.BackupState);
                Equal(definition.OriginalSha256, inspection.Sha256);
                True(inspection.PeMatches);
                BytesEqual(original, File.ReadAllBytes(path));
            });
        }

        private static void TestActivate()
        {
            WithFixture(delegate(string path, PatchDefinition definition,
                byte[] original)
            {
                FlexColorPatchEngine engine = Engine(definition);
                PatchActionResult result = engine.Activate(path);
                True(result.Changed);
                Equal(PatchState.Active, result.Inspection.State);
                Equal(BackupState.Original, result.Inspection.BackupState);
                byte[] actual = File.ReadAllBytes(path);
                BytesEqual(Apply(original, definition.Sites, true), actual);
                BytesEqual(original,
                    File.ReadAllBytes(path + FlexColorPatchEngine.BackupSuffix));
                Equal(0, Directory.GetFiles(Path.GetDirectoryName(path),
                    "*.usb2xchange-tmp-*").Length);
            });
        }

        private static void TestDeactivate()
        {
            WithFixture(delegate(string path, PatchDefinition definition,
                byte[] original)
            {
                FlexColorPatchEngine engine = Engine(definition);
                engine.Activate(path);
                PatchActionResult result = engine.Deactivate(path);
                True(result.Changed);
                Equal(PatchState.Original, result.Inspection.State);
                Equal(BackupState.Original, result.Inspection.BackupState);
                BytesEqual(original, File.ReadAllBytes(path));
            });
        }

        private static void TestIdempotence()
        {
            WithFixture(delegate(string path, PatchDefinition definition,
                byte[] original)
            {
                FlexColorPatchEngine engine = Engine(definition);
                True(engine.Activate(path).Changed);
                False(engine.Activate(path).Changed);
                True(engine.Deactivate(path).Changed);
                False(engine.Deactivate(path).Changed);
                BytesEqual(original, File.ReadAllBytes(path));
            });
        }

        private static void TestWrongHash()
        {
            WithFixture(delegate(string path, PatchDefinition definition,
                byte[] original)
            {
                original[0x300] ^= 0x40;
                File.WriteAllBytes(path, original);
                FlexColorPatchEngine engine = Engine(definition);
                Equal(PatchState.Unknown, engine.Inspect(path).State);
                Throws<PatchException>(delegate { engine.Activate(path); });
                False(File.Exists(path + FlexColorPatchEngine.BackupSuffix));
                BytesEqual(original, File.ReadAllBytes(path));
            });
        }

        private static void TestPartialPatch()
        {
            WithFixture(delegate(string path, PatchDefinition definition,
                byte[] original)
            {
                PatchSite site = definition.Sites[0];
                Buffer.BlockCopy(site.PatchedBytes, 0, original,
                    site.FileOffset, site.PatchedBytes.Length);
                File.WriteAllBytes(path, original);
                FlexColorPatchEngine engine = Engine(definition);
                Equal(PatchState.Partial, engine.Inspect(path).State);
                Throws<PatchException>(delegate { engine.Activate(path); });
                Throws<PatchException>(delegate { engine.Deactivate(path); });
                BytesEqual(original, File.ReadAllBytes(path));
            });
        }

        private static void TestUnexpectedSite()
        {
            WithFixture(delegate(string path, PatchDefinition definition,
                byte[] original)
            {
                original[definition.Sites[0].FileOffset] = 0xAA;
                File.WriteAllBytes(path, original);
                FlexColorPatchEngine engine = Engine(definition);
                PatchInspection inspection = engine.Inspect(path);
                Equal(PatchState.Unknown, inspection.State);
                True(inspection.SiteProfile.Contains("unexpected=1"));
                Throws<PatchException>(delegate { engine.Activate(path); });
                BytesEqual(original, File.ReadAllBytes(path));
            });
        }

        private static void TestInvalidBackup()
        {
            WithFixture(delegate(string path, PatchDefinition definition,
                byte[] original)
            {
                File.WriteAllBytes(path + FlexColorPatchEngine.BackupSuffix,
                    new byte[] { 1, 2, 3 });
                FlexColorPatchEngine engine = Engine(definition);
                Equal(BackupState.Invalid, engine.Inspect(path).BackupState);
                Throws<PatchException>(delegate { engine.Activate(path); });
                BytesEqual(original, File.ReadAllBytes(path));
            });
        }

        private static void TestActiveWithoutBackup()
        {
            WithFixture(delegate(string path, PatchDefinition definition,
                byte[] original)
            {
                File.WriteAllBytes(path, Apply(original, definition.Sites, true));
                FlexColorPatchEngine engine = Engine(definition);
                PatchActionResult repeated = engine.Activate(path);
                False(repeated.Changed);
                Equal(BackupState.Original, repeated.Inspection.BackupState);
                BytesEqual(original,
                    File.ReadAllBytes(path + FlexColorPatchEngine.BackupSuffix));
                File.Delete(path + FlexColorPatchEngine.BackupSuffix);
                PatchActionResult restored = engine.Deactivate(path);
                True(restored.Changed);
                BytesEqual(original, File.ReadAllBytes(path));
            });
        }

        private static void TestProtectedPath()
        {
            WithFixture(delegate(string path, PatchDefinition definition,
                byte[] original)
            {
                string root = Path.GetDirectoryName(path);
                var engine = new FlexColorPatchEngine(definition,
                    new string[] { root });
                Equal(PatchState.Original, engine.Inspect(path).State);
                Throws<PatchException>(delegate { engine.Activate(path); });
                BytesEqual(original, File.ReadAllBytes(path));
                False(File.Exists(path + FlexColorPatchEngine.BackupSuffix));
            });
        }

        private static FlexColorPatchEngine Engine(PatchDefinition definition)
        {
            return new FlexColorPatchEngine(definition, new string[0]);
        }

        private static void WithFixture(
            Action<string, PatchDefinition, byte[]> action)
        {
            string directory = Path.Combine(Path.GetTempPath(),
                "usb2xchange-flexcolor-patch-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                byte[] original;
                PatchDefinition definition = FixtureDefinition(out original);
                string path = Path.Combine(directory, "FlexColor.dll");
                File.WriteAllBytes(path, original);
                action(path, definition, (byte[])original.Clone());
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        private static PatchDefinition FixtureDefinition(out byte[] original)
        {
            original = new byte[1024];
            for (int i = 0; i < original.Length; ++i)
            {
                original[i] = (byte)((i * 31 + 7) & 0xff);
            }
            original[0] = (byte)'M';
            original[1] = (byte)'Z';
            WriteUInt32(original, 0x3c, 0x80);
            original[0x80] = (byte)'P';
            original[0x81] = (byte)'E';
            original[0x82] = 0;
            original[0x83] = 0;
            WriteUInt16(original, 0x84, 0x014c);
            WriteUInt16(original, 0x98, 0x010b);
            WriteUInt32(original, 0xb4, 0x12340000);

            PatchSite[] sites = new PatchSite[]
            {
                new PatchSite(0x200, new byte[] { 0x75, 0x58 },
                    new byte[] { 0x90, 0x90 }, "branch"),
                new PatchSite(0x220, new byte[] { 0x02 },
                    new byte[] { 0x01 }, "selector"),
                new PatchSite(0x240, new byte[] { 0x74 },
                    new byte[] { 0xeb }, "loader")
            };
            foreach (PatchSite site in sites)
            {
                Buffer.BlockCopy(site.OriginalBytes, 0, original,
                    site.FileOffset, site.OriginalBytes.Length);
            }
            byte[] patched = Apply(original, sites, true);
            return new PatchDefinition("generated test fixture", original.Length,
                Sha256(original), Sha256(patched), 0x014c, 0x010b,
                0x12340000, sites);
        }

        private static byte[] Apply(byte[] source, PatchSite[] sites,
            bool activate)
        {
            byte[] result = (byte[])source.Clone();
            foreach (PatchSite site in sites)
            {
                byte[] value = activate ? site.PatchedBytes : site.OriginalBytes;
                Buffer.BlockCopy(value, 0, result, site.FileOffset, value.Length);
            }
            return result;
        }

        private static string Sha256(byte[] value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return BitConverter.ToString(sha256.ComputeHash(value)).
                    Replace("-", string.Empty);
            }
        }

        private static int CountChangedBytes(PatchSite[] sites)
        {
            int count = 0;
            foreach (PatchSite site in sites)
            {
                for (int i = 0; i < site.OriginalBytes.Length; ++i)
                {
                    if (site.OriginalBytes[i] != site.PatchedBytes[i])
                    {
                        ++count;
                    }
                }
            }
            return count;
        }

        private static void WriteUInt16(byte[] value, int offset, ushort number)
        {
            value[offset] = (byte)(number & 0xff);
            value[offset + 1] = (byte)((number >> 8) & 0xff);
        }

        private static void WriteUInt32(byte[] value, int offset, uint number)
        {
            value[offset] = (byte)(number & 0xff);
            value[offset + 1] = (byte)((number >> 8) & 0xff);
            value[offset + 2] = (byte)((number >> 16) & 0xff);
            value[offset + 3] = (byte)((number >> 24) & 0xff);
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                ++passed;
                Console.WriteLine("PASS {0}", name);
            }
            catch (Exception exception)
            {
                ++failed;
                Console.WriteLine("FAIL {0}: {1}", name, exception);
            }
        }

        private static void True(bool value)
        {
            if (!value)
            {
                throw new Exception("Expected true.");
            }
        }

        private static void False(bool value)
        {
            if (value)
            {
                throw new Exception("Expected false.");
            }
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual))
            {
                throw new Exception(string.Format("Expected {0}, found {1}.",
                    expected, actual));
            }
        }

        private static void BytesEqual(byte[] expected, byte[] actual)
        {
            if (expected.Length != actual.Length)
            {
                throw new Exception("Byte-array lengths differ.");
            }
            for (int i = 0; i < expected.Length; ++i)
            {
                if (expected[i] != actual[i])
                {
                    throw new Exception("Byte arrays differ at offset " + i + ".");
                }
            }
        }

        private static void Throws<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }
            throw new Exception("Expected " + typeof(T).Name + ".");
        }
    }
}
