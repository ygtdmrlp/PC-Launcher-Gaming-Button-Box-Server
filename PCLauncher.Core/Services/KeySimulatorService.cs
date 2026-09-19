using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PCLauncher.Core.Services;

public record KeyPressResult(bool Success, string Message, string? ErrorDetail = null);

public record SimulatedKey(ushort VirtualKey, ushort ScanCode, bool IsExtended, bool IsModifier, string Name);

public interface IKeySimulatorService
{
    Task<KeyPressResult> SimulateKeySequenceAsync(string keySequence, int holdDurationMs = 100);
    IReadOnlyList<string> GetSupportedKeysList();
}

public class KeySimulatorService : IKeySimulatorService
{
    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_SCANCODE = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT
    {
        [FieldOffset(0)]
        public int type;
        [FieldOffset(8)]
        public KEYBDINPUT ki;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    public async Task<KeyPressResult> SimulateKeySequenceAsync(string keySequence, int holdDurationMs = 100)
    {
        if (string.IsNullOrWhiteSpace(keySequence))
        {
            return new KeyPressResult(false, "Boş tuş dizisi.");
        }

        try
        {
            // Ensure any running ETS2 / ATS or simulator window is brought to foreground if needed
            TryFocusGameWindow();

            var parts = keySequence.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var modifiers = new List<SimulatedKey>();
            var mainKeys = new List<SimulatedKey>();

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                var key = ResolveKey(trimmed);
                if (key == null)
                {
                    return new KeyPressResult(false, $"Tanınmayan tuş: '{trimmed}'");
                }

                if (key.IsModifier)
                {
                    modifiers.Add(key);
                }
                else
                {
                    mainKeys.Add(key);
                }
            }

            // 1. Press modifiers down
            foreach (var mod in modifiers)
            {
                SendSingleKey(mod, isKeyUp: false);
            }

            if (modifiers.Count > 0)
            {
                await Task.Delay(25);
            }

            // 2. Press main keys down
            foreach (var key in mainKeys)
            {
                SendSingleKey(key, isKeyUp: false);
            }

            // Realistic hold duration (DirectX / DirectInput requires the key down state
            // to persist across game frame / physics loop ticks - 80ms-120ms is ideal for ETS2)
            var actualHold = Math.Clamp(holdDurationMs, 40, 3000);
            await Task.Delay(actualHold);

            // 3. Release main keys up
            foreach (var key in mainKeys)
            {
                SendSingleKey(key, isKeyUp: true);
            }

            if (modifiers.Count > 0)
            {
                await Task.Delay(25);

                // 4. Release modifiers up (reverse order)
                for (int i = modifiers.Count - 1; i >= 0; i--)
                {
                    SendSingleKey(modifiers[i], isKeyUp: true);
                }
            }

            return new KeyPressResult(true, $"'{keySequence}' tuş komutu (DirectInput/ScanCode) başarıyla iletildi.");
        }
        catch (Exception ex)
        {
            return new KeyPressResult(false, "Tuş simülasyonu başarısız oldu.", ex.Message);
        }
    }

    private static void SendSingleKey(SimulatedKey key, bool isKeyUp)
    {
        uint flags = 0;

        if (isKeyUp)
        {
            flags |= KEYEVENTF_KEYUP;
        }

        if (key.IsExtended)
        {
            flags |= KEYEVENTF_EXTENDEDKEY;
        }

        var input = new INPUT
        {
            type = INPUT_KEYBOARD,
            ki = new KEYBDINPUT
            {
                time = 0,
                dwExtraInfo = IntPtr.Zero
            }
        };

        // If hardware scan code is present, use KEYEVENTF_SCANCODE for DirectX / DirectInput games (ETS2)
        if (key.ScanCode > 0)
        {
            flags |= KEYEVENTF_SCANCODE;
            input.ki.wScan = key.ScanCode;
            input.ki.wVk = 0; // Win32 specification: wVk is ignored when KEYEVENTF_SCANCODE is set
            input.ki.dwFlags = flags;
        }
        else
        {
            // Fallback for media keys or keys without physical scan codes
            input.ki.wVk = key.VirtualKey;
            input.ki.wScan = 0;
            input.ki.dwFlags = flags;
        }

        SendInput(1, new[] { input }, Marshal.SizeOf(typeof(INPUT)));
    }

    private static void TryFocusGameWindow()
    {
        try
        {
            // Check if popular simulator games are running (ETS2, ATS, etc.)
            string[] gameProcessNames = { "eurotrucks2", "amtrucks", "AssettoCorsa", "forzahorizon5", "forzahorizon4" };
            var fg = GetForegroundWindow();

            foreach (var procName in gameProcessNames)
            {
                var processes = Process.GetProcessesByName(procName);
                if (processes.Length > 0)
                {
                    var p = processes[0];
                    if (p.MainWindowHandle != IntPtr.Zero && p.MainWindowHandle != fg)
                    {
                        // Bring game to foreground so it definitely captures DirectInput
                        SetForegroundWindow(p.MainWindowHandle);
                    }
                    break;
                }
            }
        }
        catch
        {
            // Ignore focus failures silently
        }
    }

    public static SimulatedKey? ResolveKey(string keyName)
    {
        var k = keyName.Trim().ToUpperInvariant();

        // 1. DirectInput Set 1 Hardware Scan Code Lookup Table
        // DirectInput Key (DIK_*) values required by DirectX games like ETS2 / ATS
        switch (k)
        {
            // Modifiers
            case "CTRL":
            case "CONTROL":
            case "LCTRL":
                return new SimulatedKey(0x11, 0x1D, false, true, "Ctrl");
            case "RCTRL":
                return new SimulatedKey(0x11, 0x1D, true, true, "RCtrl");
            case "SHIFT":
            case "LSHIFT":
                return new SimulatedKey(0x10, 0x2A, false, true, "Shift");
            case "RSHIFT":
                return new SimulatedKey(0x10, 0x36, false, true, "RShift");
            case "ALT":
            case "LALT":
                return new SimulatedKey(0x12, 0x38, false, true, "Alt");
            case "RALT":
            case "ALTGR":
                return new SimulatedKey(0x12, 0x38, true, true, "RAlt");
            case "WIN":
            case "WINDOWS":
                return new SimulatedKey(0x5B, 0x5B, true, true, "Win");

            // Letters A-Z (DIK_A = 0x1E ..)
            case "A": return new SimulatedKey((ushort)'A', 0x1E, false, false, "A");
            case "B": return new SimulatedKey((ushort)'B', 0x30, false, false, "B");
            case "C": return new SimulatedKey((ushort)'C', 0x2E, false, false, "C");
            case "D": return new SimulatedKey((ushort)'D', 0x20, false, false, "D");
            case "E": return new SimulatedKey((ushort)'E', 0x12, false, false, "E"); // ETS2 Engine Start!
            case "F": return new SimulatedKey((ushort)'F', 0x21, false, false, "F");
            case "G": return new SimulatedKey((ushort)'G', 0x22, false, false, "G");
            case "H": return new SimulatedKey((ushort)'H', 0x23, false, false, "H"); // ETS2 Horn!
            case "I": return new SimulatedKey((ushort)'I', 0x17, false, false, "I"); // ETS2 Dashboard Display!
            case "J": return new SimulatedKey((ushort)'J', 0x24, false, false, "J"); // ETS2 Light Horn!
            case "K": return new SimulatedKey((ushort)'K', 0x25, false, false, "K"); // ETS2 High Beam!
            case "L": return new SimulatedKey((ushort)'L', 0x26, false, false, "L"); // ETS2 Lights!
            case "M": return new SimulatedKey((ushort)'M', 0x32, false, false, "M"); // ETS2 Map!
            case "N": return new SimulatedKey((ushort)'N', 0x31, false, false, "N"); // ETS2 Route Advisor Mode
            case "O": return new SimulatedKey((ushort)'O', 0x18, false, false, "O"); // ETS2 Beacon!
            case "P": return new SimulatedKey((ushort)'P', 0x19, false, false, "P"); // ETS2 Wipers!
            case "Q": return new SimulatedKey((ushort)'Q', 0x10, false, false, "Q");
            case "R": return new SimulatedKey((ushort)'R', 0x13, false, false, "R"); // ETS2 Radio!
            case "S": return new SimulatedKey((ushort)'S', 0x1F, false, false, "S");
            case "T": return new SimulatedKey((ushort)'T', 0x14, false, false, "T"); // ETS2 Trailer Hook!
            case "U": return new SimulatedKey((ushort)'U', 0x16, false, false, "U"); // ETS2 Axle Lift!
            case "V": return new SimulatedKey((ushort)'V', 0x2F, false, false, "V"); // ETS2 Differential Lock!
            case "W": return new SimulatedKey((ushort)'W', 0x11, false, false, "W");
            case "X": return new SimulatedKey((ushort)'X', 0x2D, false, false, "X"); // ETS2 Hazards / CB Radio
            case "Y": return new SimulatedKey((ushort)'Y', 0x15, false, false, "Y");
            case "Z": return new SimulatedKey((ushort)'Z', 0x2C, false, false, "Z");

            // Numbers 0-9 Top Row
            case "1": return new SimulatedKey((ushort)'1', 0x02, false, false, "1");
            case "2": return new SimulatedKey((ushort)'2', 0x03, false, false, "2");
            case "3": return new SimulatedKey((ushort)'3', 0x04, false, false, "3");
            case "4": return new SimulatedKey((ushort)'4', 0x05, false, false, "4");
            case "5": return new SimulatedKey((ushort)'5', 0x06, false, false, "5");
            case "6": return new SimulatedKey((ushort)'6', 0x07, false, false, "6");
            case "7": return new SimulatedKey((ushort)'7', 0x08, false, false, "7");
            case "8": return new SimulatedKey((ushort)'8', 0x09, false, false, "8");
            case "9": return new SimulatedKey((ushort)'9', 0x0A, false, false, "9");
            case "0": return new SimulatedKey((ushort)'0', 0x0B, false, false, "0");

            // Common Controls
            case "SPACE":
            case "BOŞLUK":
                return new SimulatedKey(0x20, 0x39, false, false, "Space"); // ETS2 Handbrake!
            case "ENTER":
            case "RETURN":
                return new SimulatedKey(0x0D, 0x1C, false, false, "Enter");
            case "ESC":
            case "ESCAPE":
                return new SimulatedKey(0x1B, 0x01, false, false, "Esc");
            case "TAB":
                return new SimulatedKey(0x09, 0x0F, false, false, "Tab");
            case "BACKSPACE":
            case "BACK":
                return new SimulatedKey(0x08, 0x0E, false, false, "Backspace");
            case "CAPSLOCK":
            case "CAPS":
                return new SimulatedKey(0x14, 0x3A, false, false, "CapsLock");

            // Brackets & Punctuation (Important for ETS2 Retarder!)
            case "[":
            case "LBRACKET":
                return new SimulatedKey(0xDB, 0x1A, false, false, "["); // ETS2 Retarder Decrease!
            case "]":
            case "RBRACKET":
                return new SimulatedKey(0xDD, 0x1B, false, false, "]"); // ETS2 Retarder Increase!
            case ";":
            case "SEMICOLON":
                return new SimulatedKey(0xBA, 0x27, false, false, ";");
            case "'":
            case "APOSTROPHE":
                return new SimulatedKey(0xDE, 0x28, false, false, "'");
            case "`":
            case "~":
            case "GRAVE":
                return new SimulatedKey(0xC0, 0x29, false, false, "`");
            case "-":
            case "MINUS":
                return new SimulatedKey(0xBD, 0x0C, false, false, "-");
            case "=":
            case "EQUALS":
                return new SimulatedKey(0xBB, 0x0D, false, false, "=");
            case ",":
            case "COMMA":
                return new SimulatedKey(0xBC, 0x33, false, false, ",");
            case ".":
            case "PERIOD":
                return new SimulatedKey(0xBE, 0x34, false, false, ".");
            case "/":
            case "SLASH":
                return new SimulatedKey(0xBF, 0x35, false, false, "/");
            case "\\":
            case "BACKSLASH":
                return new SimulatedKey(0xDC, 0x2B, false, false, "\\");

            // Function Keys F1-F12 (ETS2 Route advisor, mirrors, etc.)
            case "F1": return new SimulatedKey(0x70, 0x3B, false, false, "F1");
            case "F2": return new SimulatedKey(0x71, 0x3C, false, false, "F2"); // ETS2 Mirrors!
            case "F3": return new SimulatedKey(0x72, 0x3D, false, false, "F3"); // ETS2 GPS Mode!
            case "F4": return new SimulatedKey(0x73, 0x3E, false, false, "F4"); // ETS2 Light Controls!
            case "F5": return new SimulatedKey(0x74, 0x3F, false, false, "F5"); // ETS2 Route Advisor Zoom!
            case "F6": return new SimulatedKey(0x75, 0x40, false, false, "F6"); // ETS2 Job Info!
            case "F7": return new SimulatedKey(0x76, 0x41, false, false, "F7"); // ETS2 Damage / Service!
            case "F8": return new SimulatedKey(0x77, 0x42, false, false, "F8"); // ETS2 Messages!
            case "F9": return new SimulatedKey(0x78, 0x43, false, false, "F9");
            case "F10": return new SimulatedKey(0x79, 0x44, false, false, "F10");
            case "F11": return new SimulatedKey(0x7A, 0x57, false, false, "F11");
            case "F12": return new SimulatedKey(0x7B, 0x58, false, false, "F12"); // Steam Screenshot!

            // Arrow Keys (Extended = true)
            case "UP":
            case "YUKARI":
                return new SimulatedKey(0x26, 0x48, true, false, "Up");
            case "DOWN":
            case "AŞAĞI":
                return new SimulatedKey(0x28, 0x50, true, false, "Down");
            case "LEFT":
            case "SOL":
                return new SimulatedKey(0x25, 0x4B, true, false, "Left");
            case "RIGHT":
            case "SAĞ":
                return new SimulatedKey(0x27, 0x4D, true, false, "Right");

            // Navigation & Editing (Extended = true)
            case "INSERT":
            case "INS":
                return new SimulatedKey(0x2D, 0x52, true, false, "Insert");
            case "DELETE":
            case "DEL":
                return new SimulatedKey(0x2E, 0x53, true, false, "Delete");
            case "HOME":
                return new SimulatedKey(0x24, 0x47, true, false, "Home");
            case "END":
                return new SimulatedKey(0x23, 0x4F, true, false, "End");
            case "PAGEUP":
            case "PGUP":
                return new SimulatedKey(0x21, 0x49, true, false, "PageUp");
            case "PAGEDOWN":
            case "PGDN":
                return new SimulatedKey(0x22, 0x51, true, false, "PageDown");

            // NumPad
            case "NUM0":
            case "NUMPAD0":
                return new SimulatedKey(0x60, 0x52, false, false, "Num0");
            case "NUM1":
            case "NUMPAD1":
                return new SimulatedKey(0x61, 0x4F, false, false, "Num1");
            case "NUM2":
            case "NUMPAD2":
                return new SimulatedKey(0x62, 0x50, false, false, "Num2");
            case "NUM3":
            case "NUMPAD3":
                return new SimulatedKey(0x63, 0x51, false, false, "Num3");
            case "NUM4":
            case "NUMPAD4":
                return new SimulatedKey(0x64, 0x4B, false, false, "Num4");
            case "NUM5":
            case "NUMPAD5":
                return new SimulatedKey(0x65, 0x4C, false, false, "Num5");
            case "NUM6":
            case "NUMPAD6":
                return new SimulatedKey(0x66, 0x4D, false, false, "Num6");
            case "NUM7":
            case "NUMPAD7":
                return new SimulatedKey(0x67, 0x47, false, false, "Num7");
            case "NUM8":
            case "NUMPAD8":
                return new SimulatedKey(0x68, 0x48, false, false, "Num8");
            case "NUM9":
            case "NUMPAD9":
                return new SimulatedKey(0x69, 0x49, false, false, "Num9");
            case "NUM*":
            case "MULTIPLY":
                return new SimulatedKey(0x6A, 0x37, false, false, "Num*");
            case "NUM+":
            case "ADD":
                return new SimulatedKey(0x6B, 0x4E, false, false, "Num+");
            case "NUM-":
            case "SUBTRACT":
                return new SimulatedKey(0x6D, 0x4A, false, false, "Num-");
            case "NUM.":
            case "DECIMAL":
                return new SimulatedKey(0x6E, 0x53, false, false, "Num.");
            case "NUM/":
            case "DIVIDE":
                return new SimulatedKey(0x6F, 0x35, true, false, "Num/");
            case "NUMENTER":
                return new SimulatedKey(0x0D, 0x1C, true, false, "NumEnter");
            case "NUMLOCK":
                return new SimulatedKey(0x90, 0x45, false, false, "NumLock");
            case "SCROLLLOCK":
                return new SimulatedKey(0x91, 0x46, false, false, "ScrollLock");
            case "PRINTSCREEN":
            case "PRTSC":
            case "SNAPSHOT":
                return new SimulatedKey(0x2C, 0x37, true, false, "PrintScreen");

            // Media Keys (VK fallback, scan code = 0)
            case "MUTE":
            case "VOLUMEMUTE":
                return new SimulatedKey(0xAD, 0, true, false, "Mute");
            case "VOLUMEDOWN":
            case "VOLDOWN":
                return new SimulatedKey(0xAE, 0, true, false, "VolumeDown");
            case "VOLUMEUP":
            case "VOLUP":
                return new SimulatedKey(0xAF, 0, true, false, "VolumeUp");
            case "MEDIANEXT":
            case "NEXTTRACK":
                return new SimulatedKey(0xB0, 0, true, false, "MediaNext");
            case "MEDIAPREV":
            case "PREVTRACK":
                return new SimulatedKey(0xB1, 0, true, false, "MediaPrev");
            case "MEDIASTOP":
                return new SimulatedKey(0xB2, 0, true, false, "MediaStop");
            case "MEDIAPLAY":
            case "PLAYPAUSE":
                return new SimulatedKey(0xB3, 0, true, false, "PlayPause");

            default:
                break;
        }

        // 2. Generic F13 - F24 fallback
        if (k.StartsWith("F") && int.TryParse(k.Substring(1), out var fNum) && fNum is >= 13 and <= 24)
        {
            var vk = (ushort)(0x70 + (fNum - 1));
            var sc = (ushort)MapVirtualKey(vk, 0);
            return new SimulatedKey(vk, sc, false, false, k);
        }

        // 3. Fallback: MapVirtualKey
        var fallbackVk = ResolveVirtualKey(k);
        if (fallbackVk > 0)
        {
            var sc = (ushort)MapVirtualKey(fallbackVk, 0);
            var isExt = fallbackVk is 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28 or 0x2D or 0x2E;
            var isMod = fallbackVk is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C;
            return new SimulatedKey(fallbackVk, sc, isExt, isMod, k);
        }

        return null;
    }

    public static ushort ResolveVirtualKey(string keyName)
    {
        var k = keyName.Trim().ToUpperInvariant();

        if (k is "CTRL" or "CONTROL") return 0x11;
        if (k is "ALT") return 0x12;
        if (k is "SHIFT") return 0x10;
        if (k is "WIN" or "WINDOWS") return 0x5B;

        if (k.Length == 1 && k[0] >= 'A' && k[0] <= 'Z') return (ushort)k[0];
        if (k.Length == 1 && k[0] >= '0' && k[0] <= '9') return (ushort)k[0];

        if (k.StartsWith("F") && int.TryParse(k.Substring(1), out var fNum) && fNum is >= 1 and <= 24)
        {
            return (ushort)(0x70 + (fNum - 1));
        }

        return k switch
        {
            "SPACE" or "BOŞLUK" => 0x20,
            "ENTER" or "RETURN" => 0x0D,
            "ESC" or "ESCAPE" => 0x1B,
            "TAB" => 0x09,
            "BACKSPACE" or "BACK" => 0x08,
            "INSERT" or "INS" => 0x2D,
            "DELETE" or "DEL" => 0x2E,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" or "PGUP" => 0x21,
            "PAGEDOWN" or "PGDN" => 0x22,
            "UP" or "YUKARI" => 0x26,
            "DOWN" or "AŞAĞI" => 0x28,
            "LEFT" or "SOL" => 0x25,
            "RIGHT" or "SAĞ" => 0x27,
            "CAPSLOCK" or "CAPS" => 0x14,
            "NUMLOCK" => 0x90,
            "SCROLLLOCK" => 0x91,
            "PRINTSCREEN" or "PRTSC" => 0x2C,
            "[" or "LBRACKET" => 0xDB,
            "]" or "RBRACKET" => 0xDD,
            _ => 0
        };
    }

    public IReadOnlyList<string> GetSupportedKeysList() => new[]
    {
        "Space", "Enter", "Esc", "Tab", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "W", "A", "S", "D", "E", "R", "F", "C", "X", "Z", "Q", "M", "L", "I", "J", "K", "H", "B", "N", "V", "T", "Y", "U", "O", "P",
        "[", "]",
        "1", "2", "3", "4", "5", "6", "7", "8", "9", "0",
        "Ctrl", "Shift", "Alt", "Up", "Down", "Left", "Right",
        "NumPad1", "NumPad2", "NumPad3", "NumPad4", "NumPad5", "NumPad6", "NumPad7", "NumPad8", "NumPad9", "NumPad0",
        "VolumeUp", "VolumeDown", "Mute", "PlayPause"
    };
}
