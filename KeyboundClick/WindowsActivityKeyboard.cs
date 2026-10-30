using System.Runtime.InteropServices;

namespace KeyboundClick;

internal sealed class WindowsActivityKeyboard : IActivityKeyboard
{
    public bool IsGameForeground
    {
        get
        {
            var window = GetForegroundWindow();
            if (window == 0) return false;
            GetWindowThreadProcessId(window, out var pid);
            return pid == (uint)Environment.ProcessId;
        }
    }

    public bool HasUserInput(ushort ownedKey)
    {
        for (ushort key = 1; key < 255; key++)
        {
            if (key == ownedKey) continue;
            // Windows also exposes left/right modifiers through the generic key.
            if (key == 0x10 && ownedKey is 0xA0 or 0xA1) continue;
            if ((GetAsyncKeyState(key) & 0x8000) != 0) return true;
        }
        return false;
    }

    public bool Press(ushort key) => IsGameForeground && Send(key, false);
    public bool Release(ushort key) => Send(key, true);

    private static bool Send(ushort key, bool release)
    {
        var inputs = new[] { new Input { Type = 1, Keyboard = new KeyboardInput { Key = key, Flags = release ? 2u : 0u } } };
        return SendInput(1, inputs, Marshal.SizeOf<Input>()) == 1;
    }

    // Win64 INPUT: DWORD + alignment + 32-byte union. Plugin targets x64 only.
    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Key;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
}
