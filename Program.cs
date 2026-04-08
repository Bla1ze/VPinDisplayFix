using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace VPinDisplayFix;

class Program
{
    static readonly string Version = "1.0.0";

    // Default paths - override with command-line args
    static string PinUpPlayerIniPath = @"C:\vpinball\PinUPSystem\PinUpPlayer.ini";
    static string VpxRegistryPath = @"Software\Visual Pinball\VP10\Player";

    // Playfield is [INFO3] in PinUpPlayer.ini
    static string PlayfieldSection = "INFO3";

    // Target resolution to find
    static int TargetWidth = 3840;
    static int TargetHeight = 2160;

    static int Main(string[] args)
    {
        Console.Title = "VPin Display Fix";
        Console.WriteLine($"VPin Display Fix v{Version}");
        Console.WriteLine("Auto-assigns playfield to the 4K display");
        Console.WriteLine(new string('-', 55));

        var config = ParseArgs(args);
        if (config == null) return 1;
        if (config.ShowHelp) return 0;

        // Step 1: Enumerate displays
        var displays = DisplayEnumerator.GetAll();

        if (displays.Count == 0)
        {
            Console.WriteLine("ERROR: No displays found.");
            return 1;
        }

        Console.WriteLine($"\nFound {displays.Count} display(s):\n");
        for (int i = 0; i < displays.Count; i++)
        {
            var d = displays[i];
            string marker = Is4KDisplay(d) ? " << 4K PLAYFIELD" : "";
            Console.WriteLine($"  [{i}] {d.DeviceName}: {d.Width}x{d.Height} at ({d.Left},{d.Top}){marker}");
        }

        // Step 2: Find the 4K display
        var playfield = displays.FirstOrDefault(Is4KDisplay);

        if (playfield == null)
        {
            Console.WriteLine($"\nERROR: No {TargetWidth}x{TargetHeight} display found.");
            Console.WriteLine("Check your display settings and ensure the 4KP is connected.");
            return 1;
        }

        Console.WriteLine($"\n4K playfield display: {playfield.DeviceName}");
        Console.WriteLine($"  Position: ({playfield.Left},{playfield.Top})");
        Console.WriteLine($"  Size: {playfield.Width}x{playfield.Height}");
        int displayIndex = displays.IndexOf(playfield);
        Console.WriteLine($"  Index: {displayIndex}");

        bool anyChanges = false;

        // Step 3: Update PinUP Popper config
        if (config.UpdatePinUp)
        {
            Console.WriteLine($"\n--- PinUP Popper ({PinUpPlayerIniPath}) ---");
            anyChanges |= UpdatePinUpPopper(playfield, config.DryRun);
        }

        // Step 4: Update VPX registry
        if (config.UpdateVpx)
        {
            Console.WriteLine($"\n--- Visual Pinball X (Registry) ---");
            anyChanges |= UpdateVpxRegistry(displayIndex, config.DryRun);
        }

        // Summary
        Console.WriteLine($"\n{new string('-', 55)}");
        if (config.DryRun)
        {
            Console.WriteLine("DRY RUN - no changes were made.");
        }
        else if (anyChanges)
        {
            Console.WriteLine("Done. Restart PinUP Popper and VPX for changes to take effect.");
        }
        else
        {
            Console.WriteLine("Everything already correct. No changes needed.");
        }

        return 0;
    }

    static bool Is4KDisplay(DisplayInfo d)
    {
        // Handle both landscape (3840x2160) and portrait/rotated (2160x3840)
        return (d.Width == TargetWidth && d.Height == TargetHeight) ||
               (d.Width == TargetHeight && d.Height == TargetWidth);
    }

    static bool UpdatePinUpPopper(DisplayInfo playfield, bool dryRun)
    {
        if (!File.Exists(PinUpPlayerIniPath))
        {
            Console.WriteLine($"  File not found: {PinUpPlayerIniPath}");
            Console.WriteLine("  Use --pinup-ini <path> to specify the correct location.");
            return false;
        }

        var ini = new IniFile(PinUpPlayerIniPath);
        bool changed = false;

        // Read current values from [INFO3]
        int currentX = ini.GetInt(PlayfieldSection, "ScreenXPos", -1);
        int currentY = ini.GetInt(PlayfieldSection, "ScreenYPos", -1);
        int currentW = ini.GetInt(PlayfieldSection, "ScreenWidth", -1);
        int currentH = ini.GetInt(PlayfieldSection, "ScreenHeight", -1);

        Console.WriteLine($"  Current: ScreenXPos={currentX}, ScreenYPos={currentY}, " +
                          $"ScreenWidth={currentW}, ScreenHeight={currentH}");

        // Determine correct values
        // PinUP uses the virtual desktop coordinates of the display
        int newX = playfield.Left;
        int newY = playfield.Top;
        int newW = playfield.Width;
        int newH = playfield.Height;

        Console.WriteLine($"  Correct: ScreenXPos={newX}, ScreenYPos={newY}, " +
                          $"ScreenWidth={newW}, ScreenHeight={newH}");

        if (currentX == newX && currentY == newY && currentW == newW && currentH == newH)
        {
            Console.WriteLine("  Already correct.");
            return false;
        }

        if (!dryRun)
        {
            // Backup the file first
            string backup = PinUpPlayerIniPath + $".bak.{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(PinUpPlayerIniPath, backup, overwrite: true);
            Console.WriteLine($"  Backup: {backup}");

            ini.Set(PlayfieldSection, "ScreenXPos", newX.ToString());
            ini.Set(PlayfieldSection, "ScreenYPos", newY.ToString());
            ini.Set(PlayfieldSection, "ScreenWidth", newW.ToString());
            ini.Set(PlayfieldSection, "ScreenHeight", newH.ToString());
            ini.Save();

            Console.WriteLine("  Updated PinUpPlayer.ini");
            changed = true;
        }
        else
        {
            Console.WriteLine("  Would update PinUpPlayer.ini (dry-run)");
            changed = true;
        }

        return changed;
    }

    static bool UpdateVpxRegistry(int displayIndex, bool dryRun)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(VpxRegistryPath, writable: !dryRun);
            if (key == null)
            {
                Console.WriteLine($"  Registry key not found: HKCU\\{VpxRegistryPath}");
                Console.WriteLine("  Is Visual Pinball X installed?");
                return false;
            }

            // VPX uses 0-based display index
            var currentValue = key.GetValue("Display");
            int currentDisplay = currentValue != null ? Convert.ToInt32(currentValue) : -1;

            Console.WriteLine($"  Current Display: {currentDisplay}");
            Console.WriteLine($"  Correct Display: {displayIndex}");

            if (currentDisplay == displayIndex)
            {
                Console.WriteLine("  Already correct.");
                return false;
            }

            if (!dryRun)
            {
                key.SetValue("Display", displayIndex, RegistryValueKind.DWord);
                Console.WriteLine($"  Updated registry: Display = {displayIndex}");
                return true;
            }
            else
            {
                Console.WriteLine($"  Would update registry (dry-run)");
                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ERROR: {ex.Message}");
            return false;
        }
    }

    static Config? ParseArgs(string[] args)
    {
        var config = new Config();

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--help" or "-h":
                    PrintUsage();
                    config.ShowHelp = true;
                    return config;
                case "--dry-run":
                    config.DryRun = true;
                    break;
                case "--pinup-ini":
                    if (i + 1 < args.Length) PinUpPlayerIniPath = args[++i];
                    break;
                case "--pinup-section":
                    if (i + 1 < args.Length) PlayfieldSection = args[++i];
                    break;
                case "--vpx-only":
                    config.UpdatePinUp = false;
                    break;
                case "--pinup-only":
                    config.UpdateVpx = false;
                    break;
                case "--width":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int w)) TargetWidth = w;
                    break;
                case "--height":
                    if (i + 1 < args.Length && int.TryParse(args[++i], out int h)) TargetHeight = h;
                    break;
            }
        }

        return config;
    }

    static void PrintUsage()
    {
        Console.WriteLine(@"
Usage: VPinDisplayFix [options]

Finds the 4K display and updates PinUP Popper and VPX to use it
as the playfield. Run this at startup before launching PinUP Popper.

Options:
  --dry-run                Show what would change without modifying anything
  --pinup-ini <path>       Path to PinUpPlayer.ini
                           (default: C:\vpinball\PinUPSystem\PinUpPlayer.ini)
  --pinup-section <name>   INI section for playfield (default: INFO3)
  --vpx-only               Only update VPX registry, skip PinUP Popper
  --pinup-only             Only update PinUP Popper, skip VPX
  --width <pixels>         Target display width (default: 3840)
  --height <pixels>        Target display height (default: 2160)
  -h, --help               Show this help

Examples:
  VPinDisplayFix                        Auto-fix both PinUP and VPX
  VPinDisplayFix --dry-run              Preview changes without applying
  VPinDisplayFix --pinup-only           Only fix PinUP Popper
  VPinDisplayFix --vpx-only             Only fix VPX
");
    }
}

class Config
{
    public bool DryRun { get; set; }
    public bool UpdatePinUp { get; set; } = true;
    public bool UpdateVpx { get; set; } = true;
    public bool ShowHelp { get; set; }
}

#region Display Enumeration via Win32

class DisplayInfo
{
    public string DeviceName { get; set; } = "";
    public int Left { get; set; }
    public int Top { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }
}

static class DisplayEnumerator
{
    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip,
        MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfoEx lpmi);

    delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor,
        ref Rect lprcMonitor, IntPtr dwData);

    [StructLayout(LayoutKind.Sequential)]
    struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    struct MonitorInfoEx
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    const uint MONITORINFOF_PRIMARY = 1;

    public static List<DisplayInfo> GetAll()
    {
        var displays = new List<DisplayInfo>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor,
            ref Rect lprcMonitor, IntPtr dwData) =>
        {
            var info = new MonitorInfoEx { Size = Marshal.SizeOf<MonitorInfoEx>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                displays.Add(new DisplayInfo
                {
                    DeviceName = info.DeviceName,
                    Left = info.Monitor.Left,
                    Top = info.Monitor.Top,
                    Width = info.Monitor.Right - info.Monitor.Left,
                    Height = info.Monitor.Bottom - info.Monitor.Top,
                    IsPrimary = (info.Flags & MONITORINFOF_PRIMARY) != 0
                });
            }
            return true;
        }, IntPtr.Zero);

        // Sort by position (left to right) for consistent indexing
        displays.Sort((a, b) => a.Left != b.Left ? a.Left.CompareTo(b.Left) : a.Top.CompareTo(b.Top));

        return displays;
    }
}

#endregion

#region Simple INI File Reader/Writer

class IniFile
{
    private readonly string _path;
    private readonly List<IniLine> _lines = new();

    public IniFile(string path)
    {
        _path = path;
        Parse(File.ReadAllLines(path));
    }

    public int GetInt(string section, string key, int defaultValue)
    {
        var value = GetValue(section, key);
        return value != null && int.TryParse(value, out int result) ? result : defaultValue;
    }

    public string? GetValue(string section, string key)
    {
        bool inSection = false;
        foreach (var line in _lines)
        {
            if (line.Type == LineType.Section)
            {
                inSection = line.Section!.Equals(section, StringComparison.OrdinalIgnoreCase);
            }
            else if (inSection && line.Type == LineType.KeyValue &&
                     line.Key!.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return line.Value;
            }
        }
        return null;
    }

    public void Set(string section, string key, string value)
    {
        bool inSection = false;
        for (int i = 0; i < _lines.Count; i++)
        {
            if (_lines[i].Type == LineType.Section)
            {
                inSection = _lines[i].Section!.Equals(section, StringComparison.OrdinalIgnoreCase);
            }
            else if (inSection && _lines[i].Type == LineType.KeyValue &&
                     _lines[i].Key!.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                _lines[i] = new IniLine
                {
                    Type = LineType.KeyValue,
                    Key = key,
                    Value = value,
                    Raw = $"{key}={value}"
                };
                return;
            }
        }
    }

    public void Save()
    {
        File.WriteAllLines(_path, _lines.Select(l => l.Raw));
    }

    private void Parse(string[] lines)
    {
        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                _lines.Add(new IniLine
                {
                    Type = LineType.Section,
                    Section = trimmed[1..^1],
                    Raw = raw
                });
            }
            else if (trimmed.Contains('=') && !trimmed.StartsWith(';') && !trimmed.StartsWith('#'))
            {
                int eq = trimmed.IndexOf('=');
                _lines.Add(new IniLine
                {
                    Type = LineType.KeyValue,
                    Key = trimmed[..eq],
                    Value = trimmed[(eq + 1)..],
                    Raw = raw
                });
            }
            else
            {
                _lines.Add(new IniLine { Type = LineType.Other, Raw = raw });
            }
        }
    }

    enum LineType { Other, Section, KeyValue }

    class IniLine
    {
        public LineType Type { get; set; }
        public string? Section { get; set; }
        public string? Key { get; set; }
        public string? Value { get; set; }
        public string Raw { get; set; } = "";
    }
}

#endregion
