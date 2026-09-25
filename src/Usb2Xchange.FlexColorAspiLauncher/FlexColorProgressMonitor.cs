// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal sealed class FlexColorProgressObservation
    {
        internal FlexColorProgressObservation(bool dialogVisible,
            bool sampleAvailable, int minimum, int maximum, int position,
            string source)
        {
            if (!dialogVisible && sampleAvailable)
            {
                throw new ArgumentException(
                    "A progress sample requires a visible progress dialog.",
                    "sampleAvailable");
            }
            if (sampleAvailable &&
                (minimum < 0 || maximum <= minimum || position < minimum ||
                 position > maximum))
            {
                throw new ArgumentOutOfRangeException("position",
                    "Progress range and position are inconsistent.");
            }
            DialogVisible = dialogVisible;
            SampleAvailable = sampleAvailable;
            Minimum = minimum;
            Maximum = maximum;
            Position = position;
            Source = source ?? string.Empty;
        }

        internal bool DialogVisible { get; private set; }
        internal bool SampleAvailable { get; private set; }
        internal int Minimum { get; private set; }
        internal int Maximum { get; private set; }
        internal int Position { get; private set; }
        internal string Source { get; private set; }
        internal bool AtMaximum
        {
            get { return SampleAvailable && Position == Maximum; }
        }
        internal int Percent
        {
            get
            {
                return SampleAvailable
                    ? checked((int)(((long)(Position - Minimum) * 100L) /
                        (Maximum - Minimum)))
                    : -1;
            }
        }
    }

    internal static class FlexColorProgressMonitor
    {
        private const uint ProgressGetRange = 0x0407;
        private const uint ProgressGetPosition = 0x0408;
        private const uint SendMessageAbortIfHung = 0x0002;
        private static readonly Regex PercentPattern = new Regex(
            "^\\s*([0-9]{1,3})\\s*%\\s*$",
            RegexOptions.CultureInvariant);

        internal static FlexColorProgressObservation TryRead(Process process)
        {
            if (process == null)
            {
                throw new ArgumentNullException("process");
            }
            if (process.HasExited)
            {
                return new FlexColorProgressObservation(false, false, 0, 0,
                    0, string.Empty);
            }

            var dialogs = new List<IntPtr>();
            NativeMethods.EnumWindows(delegate(IntPtr window, IntPtr value)
            {
                uint processId;
                NativeMethods.GetWindowThreadProcessId(window, out processId);
                if (processId == (uint)process.Id &&
                    NativeMethods.IsWindowVisible(window) &&
                    string.Equals(GetText(window), "Progress",
                        StringComparison.Ordinal))
                {
                    dialogs.Add(window);
                }
                return true;
            }, IntPtr.Zero);
            if (dialogs.Count == 0)
            {
                return new FlexColorProgressObservation(false, false, 0, 0,
                    0, string.Empty);
            }
            if (dialogs.Count != 1)
            {
                return new FlexColorProgressObservation(true, false, 0, 0,
                    0, "ambiguous-dialog");
            }

            var controls = new List<IntPtr>();
            var percentages = new List<int>();
            NativeMethods.EnumChildWindows(dialogs[0],
                delegate(IntPtr window, IntPtr value)
                {
                    if (!NativeMethods.IsWindowVisible(window))
                    {
                        return true;
                    }
                    string className = GetClassName(window);
                    if (string.Equals(className, "msctls_progress32",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        controls.Add(window);
                    }
                    else if (string.Equals(className, "Static",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        Match percent = PercentPattern.Match(GetText(window));
                        int parsed;
                        if (percent.Success && int.TryParse(
                                percent.Groups[1].Value,
                                NumberStyles.None, CultureInfo.InvariantCulture,
                                out parsed) && parsed >= 0 && parsed <= 100)
                        {
                            percentages.Add(parsed);
                        }
                    }
                    return true;
                }, IntPtr.Zero);

            if (controls.Count == 1)
            {
                int minimum;
                int maximum;
                int position;
                if (!TrySend(controls[0], ProgressGetRange, new IntPtr(1),
                        out minimum) ||
                    !TrySend(controls[0], ProgressGetRange, IntPtr.Zero,
                        out maximum) ||
                    !TrySend(controls[0], ProgressGetPosition, IntPtr.Zero,
                        out position))
                {
                    return new FlexColorProgressObservation(true, false, 0,
                        0, 0, "unresponsive-progress-control");
                }
                if (minimum >= 0 && maximum > minimum &&
                    position >= minimum && position <= maximum)
                {
                    return new FlexColorProgressObservation(true, true,
                        minimum, maximum, position, "msctls_progress32");
                }
                return new FlexColorProgressObservation(true, false, 0, 0,
                    0, "invalid-progress-control");
            }
            if (controls.Count > 1 || percentages.Count > 1)
            {
                return new FlexColorProgressObservation(true, false, 0, 0,
                    0, "ambiguous-control");
            }
            if (percentages.Count == 1)
            {
                return new FlexColorProgressObservation(true, true, 0, 100,
                    percentages[0], "static-percent");
            }
            return new FlexColorProgressObservation(true, false, 0, 0, 0,
                "no-readable-progress-control");
        }

        private static string GetText(IntPtr window)
        {
            var text = new StringBuilder(512);
            NativeMethods.GetWindowText(window, text, text.Capacity);
            return text.ToString();
        }

        private static bool TrySend(IntPtr window, uint message,
            IntPtr wParam, out int result)
        {
            IntPtr rawResult;
            IntPtr sent = NativeMethods.SendMessageTimeout(window, message,
                wParam, IntPtr.Zero, SendMessageAbortIfHung, 250,
                out rawResult);
            result = rawResult.ToInt32();
            return sent != IntPtr.Zero;
        }

        private static string GetClassName(IntPtr window)
        {
            var text = new StringBuilder(256);
            NativeMethods.GetClassName(window, text, text.Capacity);
            return text.ToString();
        }

        private static class NativeMethods
        {
            internal delegate bool EnumWindowCallback(IntPtr window,
                IntPtr value);

            [DllImport("user32.dll")]
            internal static extern bool EnumWindows(EnumWindowCallback callback,
                IntPtr value);

            [DllImport("user32.dll")]
            internal static extern bool EnumChildWindows(IntPtr parent,
                EnumWindowCallback callback, IntPtr value);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern int GetWindowText(IntPtr window,
                StringBuilder text, int capacity);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern int GetClassName(IntPtr window,
                StringBuilder text, int capacity);

            [DllImport("user32.dll")]
            internal static extern bool IsWindowVisible(IntPtr window);

            [DllImport("user32.dll")]
            internal static extern uint GetWindowThreadProcessId(
                IntPtr window, out uint processId);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern IntPtr SendMessageTimeout(IntPtr window,
                uint message, IntPtr wParam, IntPtr lParam, uint flags,
                uint timeoutMilliseconds, out IntPtr result);
        }
    }
}
