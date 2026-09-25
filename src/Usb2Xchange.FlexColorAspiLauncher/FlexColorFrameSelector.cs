// Copyright (c) 2026 fcusb contributors; SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Usb2Xchange.FlexColorAspiLauncher
{
    internal static class FlexColorFrameSelector
    {
        private const int FrameComboControlId = 1004;
        private const int FrameLabelControlId = 1311;
        private const uint ComboGetCount = 0x0146;
        private const uint ComboGetCurrentSelection = 0x0147;
        private const uint ComboGetItemText = 0x0148;
        private const uint ComboGetItemTextLength = 0x0149;
        private const uint ComboSetCurrentSelection = 0x014E;
        private const int ComboSelectionChanged = 1;
        private const uint WindowCommand = 0x0111;
        private const uint AbortIfHung = 0x0002;
        private const uint MessageTimeoutMilliseconds = 5000;

        internal static string GetExactLabel(string token)
        {
            switch (token)
            {
                case "24x36":
                case "60x60":
                case "60x70":
                    return token;
                case "4x5":
                    return "4\"x5\"";
                default:
                    throw new InvalidOperationException(
                        "Frame token must be one of 24x36, 60x60, 60x70, " +
                        "or 4x5.");
            }
        }

        internal static bool TrySelectExact(Process process,
            string requestedFrameLabel)
        {
            process.Refresh();
            IntPtr parent = process.MainWindowHandle;
            if (parent == IntPtr.Zero)
            {
                return false;
            }

            var frameLabels = new List<IntPtr>();
            var frameCombos = new List<IntPtr>();
            NativeMethods.EnumChildWindows(parent,
                delegate(IntPtr window, IntPtr parameter)
                {
                    uint processId;
                    NativeMethods.GetWindowThreadProcessId(window,
                        out processId);
                    if (processId != checked((uint)process.Id) ||
                        !NativeMethods.IsWindowVisible(window))
                    {
                        return true;
                    }

                    var text = new StringBuilder(64);
                    var className = new StringBuilder(64);
                    NativeMethods.GetWindowText(window, text, text.Capacity);
                    NativeMethods.GetClassName(window, className,
                        className.Capacity);
                    int controlId = NativeMethods.GetDlgCtrlID(window);
                    if (controlId == FrameLabelControlId &&
                        className.ToString() == "Static" &&
                        text.ToString() == "Frame")
                    {
                        frameLabels.Add(window);
                    }
                    if (controlId == FrameComboControlId &&
                        className.ToString() == "ComboBox" &&
                        NativeMethods.IsWindowEnabled(window))
                    {
                        frameCombos.Add(window);
                    }
                    return true;
                }, IntPtr.Zero);

            if (frameLabels.Count == 0)
            {
                return false;
            }
            if (frameLabels.Count != 1 || frameCombos.Count != 1)
            {
                throw new InvalidOperationException(
                    "The exact PID-owned FlexColor 4.0.3 Frame controls " +
                    "were ambiguous or incomplete.");
            }

            IntPtr combo = frameCombos[0];
            int count = SendComboInteger(combo, ComboGetCount, 0);
            if (count <= 0)
            {
                return false;
            }
            if (count > 256)
            {
                throw new InvalidOperationException(
                    "The Frame list has an implausible item count.");
            }

            string[] required = new string[]
            {
                "24x36", "60x60", "60x70", "4\"x5\""
            };
            var requiredCounts = new Dictionary<string, int>(
                StringComparer.Ordinal);
            foreach (string label in required)
            {
                requiredCounts[label] = 0;
            }

            int requestedIndex = -1;
            for (int index = 0; index < count; ++index)
            {
                string item = GetComboItem(combo, index);
                if (requiredCounts.ContainsKey(item))
                {
                    ++requiredCounts[item];
                }
                if (item == requestedFrameLabel)
                {
                    if (requestedIndex != -1)
                    {
                        throw new InvalidOperationException(
                            "The requested Frame item appears more than once.");
                    }
                    requestedIndex = index;
                }
            }
            foreach (KeyValuePair<string, int> pair in requiredCounts)
            {
                if (pair.Value != 1)
                {
                    throw new InvalidOperationException(
                        "The supported FlexColor 4.0.3 Frame list is missing " +
                        "or duplicates exact item " + pair.Key + ".");
                }
            }
            if (requestedIndex < 0)
            {
                throw new InvalidOperationException(
                    "The requested exact Frame item was not found.");
            }

            int selectedIndex = SendComboInteger(combo,
                ComboGetCurrentSelection, 0);
            if (selectedIndex != requestedIndex)
            {
                int selectionResult = SendComboInteger(combo,
                    ComboSetCurrentSelection, requestedIndex);
                if (selectionResult != requestedIndex)
                {
                    throw new InvalidOperationException(
                        "FlexColor rejected the exact Frame selection.");
                }

                IntPtr comboParent = NativeMethods.GetParent(combo);
                uint parentProcessId;
                NativeMethods.GetWindowThreadProcessId(comboParent,
                    out parentProcessId);
                if (comboParent == IntPtr.Zero ||
                    parentProcessId != checked((uint)process.Id))
                {
                    throw new InvalidOperationException(
                        "The Frame combo parent is not owned by FlexColor.");
                }
                int command = FrameComboControlId |
                    (ComboSelectionChanged << 16);
                SendWindowMessage(comboParent, WindowCommand, command, combo);
            }

            selectedIndex = SendComboInteger(combo,
                ComboGetCurrentSelection, 0);
            return selectedIndex == requestedIndex &&
                GetComboItem(combo, selectedIndex) == requestedFrameLabel;
        }

        private static int SendComboInteger(IntPtr combo, uint message,
            int parameter)
        {
            UIntPtr result;
            IntPtr sent = NativeMethods.SendMessageTimeoutInteger(combo,
                message, new UIntPtr(unchecked((uint)parameter)),
                IntPtr.Zero, AbortIfHung, MessageTimeoutMilliseconds,
                out result);
            if (sent == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "FlexColor did not answer a bounded Frame combo query.");
            }
            return unchecked((int)result.ToUInt64());
        }

        private static string GetComboItem(IntPtr combo, int index)
        {
            int length = SendComboInteger(combo, ComboGetItemTextLength,
                index);
            if (length < 0 || length > 255)
            {
                throw new InvalidOperationException(
                    "FlexColor returned an invalid Frame item length.");
            }
            var value = new StringBuilder(length + 1);
            UIntPtr result;
            IntPtr sent = NativeMethods.SendMessageTimeoutText(combo,
                ComboGetItemText, new UIntPtr(unchecked((uint)index)), value,
                AbortIfHung, MessageTimeoutMilliseconds, out result);
            if (sent == IntPtr.Zero ||
                unchecked((int)result.ToUInt64()) != length)
            {
                throw new InvalidOperationException(
                    "FlexColor did not return an exact Frame item.");
            }
            return value.ToString();
        }

        private static void SendWindowMessage(IntPtr window, uint message,
            int parameter, IntPtr source)
        {
            UIntPtr result;
            IntPtr sent = NativeMethods.SendMessageTimeoutInteger(window,
                message, new UIntPtr(unchecked((uint)parameter)), source,
                AbortIfHung, MessageTimeoutMilliseconds, out result);
            if (sent == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "FlexColor did not process the Frame selection notice.");
            }
        }

        private static class NativeMethods
        {
            internal delegate bool EnumChildProcedure(IntPtr window,
                IntPtr parameter);

            [DllImport("user32.dll")]
            internal static extern bool EnumChildWindows(IntPtr parent,
                EnumChildProcedure callback, IntPtr parameter);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern int GetWindowText(IntPtr window,
                StringBuilder text, int maximumCount);

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            internal static extern int GetClassName(IntPtr window,
                StringBuilder className, int maximumCount);

            [DllImport("user32.dll")]
            internal static extern bool IsWindowVisible(IntPtr window);

            [DllImport("user32.dll")]
            internal static extern bool IsWindowEnabled(IntPtr window);

            [DllImport("user32.dll", SetLastError = true)]
            internal static extern int GetDlgCtrlID(IntPtr window);

            [DllImport("user32.dll")]
            internal static extern IntPtr GetParent(IntPtr window);

            [DllImport("user32.dll", EntryPoint = "SendMessageTimeoutW",
                SetLastError = true)]
            internal static extern IntPtr SendMessageTimeoutInteger(
                IntPtr window, uint message, UIntPtr wParam, IntPtr lParam,
                uint flags, uint timeoutMilliseconds, out UIntPtr result);

            [DllImport("user32.dll", CharSet = CharSet.Unicode,
                EntryPoint = "SendMessageTimeoutW", SetLastError = true)]
            internal static extern IntPtr SendMessageTimeoutText(
                IntPtr window, uint message, UIntPtr wParam,
                StringBuilder lParam, uint flags, uint timeoutMilliseconds,
                out UIntPtr result);

            [DllImport("user32.dll")]
            internal static extern uint GetWindowThreadProcessId(
                IntPtr window, out uint processId);
        }
    }
}
