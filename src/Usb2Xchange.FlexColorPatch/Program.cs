// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;

namespace Usb2Xchange.FlexColorPatch
{
    internal static class Program
    {
        private static int Main(string[] arguments)
        {
            if (arguments.Length == 1 &&
                (arguments[0] == "--help" || arguments[0] == "-h" ||
                 arguments[0] == "/?"))
            {
                PrintUsage();
                return 0;
            }
            if (arguments.Length != 2)
            {
                PrintUsage();
                return 2;
            }

            var engine = FlexColorPatchEngine.CreateProduction();
            string action = arguments[0].ToLowerInvariant();
            try
            {
                if (action == "status")
                {
                    PrintInspection(engine.Inspect(arguments[1]));
                    return 0;
                }

                PatchActionResult result;
                if (action == "activate")
                {
                    result = engine.Activate(arguments[1]);
                }
                else if (action == "deactivate")
                {
                    result = engine.Deactivate(arguments[1]);
                }
                else
                {
                    PrintUsage();
                    return 2;
                }

                Console.WriteLine(result.Message);
                PrintInspection(result.Inspection);
                if (action == "activate")
                {
                    Console.WriteLine();
                    Console.WriteLine(
                        "Important: this only selects FlexColor's ASPI backend. " +
                        "It does not authorize hardware; the selected provider " +
                        "runtime and launcher approval remain separate.");
                }
                return 0;
            }
            catch (PatchException exception)
            {
                Console.Error.WriteLine("REFUSED: " + exception.Message);
                return 3;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("ERROR: " + exception.Message);
                return 1;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("USB2Xchange FlexColor ASPI patch utility");
            Console.WriteLine();
            Console.WriteLine(
                "Usage: flexcolor-aspi-patch <status|activate|deactivate> " +
                "<path-to-FlexColor.dll>");
            Console.WriteLine();
            Console.WriteLine(
                "Activate/deactivate refuse Program Files, Windows directories, " +
                "unknown hashes, partial patches, and reparse-point paths.");
        }

        private static void PrintInspection(PatchInspection inspection)
        {
            Console.WriteLine("Path:       {0}", inspection.Path);
            Console.WriteLine("State:      {0}", inspection.State);
            Console.WriteLine("SHA-256:    {0}", inspection.Sha256);
            Console.WriteLine("Length:     {0}", inspection.FileLength);
            Console.WriteLine("PE match:   {0}", inspection.PeMatches ? "yes" : "no");
            Console.WriteLine("Sites:      {0}", inspection.SiteProfile);
            Console.WriteLine("Backup:     {0} ({1})", inspection.BackupState,
                inspection.BackupPath);
            Console.WriteLine("Assessment: {0}", inspection.Reason);
        }
    }
}
