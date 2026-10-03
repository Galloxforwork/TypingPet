using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TypingPet;

internal sealed class RawKeyboardInput : NativeWindow, IDisposable
{
    private const int WmInput = 0x00FF;
    private const uint RidInput = 0x10000003;
    private const uint RimTypeKeyboard = 1;
    private const ushort RiKeyBreak = 0x0001;
    private const ushort VkShift = 0x10;
    private const ushort VkLShift = 0xA0;
    private const ushort VkRShift = 0xA1;
    private const ushort VkCapital = 0x14;

    public event Action<KeyboardAction, bool>? ActionReceived;
    private bool _capsDown;
    private bool _capsKeyDown;
    private readonly HashSet<ushort> _heldKeys = [];

    public RawKeyboardInput()
    {
        var cp = new CreateParams { Caption = "TypingPet.RawKeyboardInput", Parent = new IntPtr(-3) };
        CreateHandle(cp);
        _capsDown = Control.IsKeyLocked(Keys.CapsLock);

        var device = new RawInputDevice
        {
            UsagePage = 0x01,
            Usage = 0x06,
            Flags = 0x00000100, // RIDEV_INPUTSINK: receive input while the app is in the background
            Target = Handle
        };
        if (!RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            DestroyHandle();
            throw new InvalidOperationException("Windows 拒绝注册键盘 Raw Input。请重启应用后重试。");
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmInput)
            ReadInput(m.LParam);
        base.WndProc(ref m);
    }

    private void ReadInput(IntPtr rawHandle)
    {
        uint size = 0;
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        if (GetRawInputData(rawHandle, RidInput, IntPtr.Zero, ref size, headerSize) == uint.MaxValue || size < headerSize)
            return;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(rawHandle, RidInput, buffer, ref size, headerSize) == uint.MaxValue)
                return;

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.Type != RimTypeKeyboard)
                return;

            var key = Marshal.PtrToStructure<RawKeyboard>(IntPtr.Add(buffer, (int)headerSize));
            var isUp = (key.Flags & RiKeyBreak) != 0;
            if (key.VirtualKey == VkShift || key.VirtualKey == VkLShift || key.VirtualKey == VkRShift)
            {
                ActionReceived?.Invoke(isUp ? KeyboardAction.ShiftUp : KeyboardAction.ShiftDown, _capsDown);
                return;
            }

            if (key.VirtualKey == VkCapital)
            {
                if (!isUp)
                    _capsKeyDown = true;
                else if (_capsKeyDown)
                {
                    _capsKeyDown = false;
                    _capsDown = Control.IsKeyLocked(Keys.CapsLock);
                    ActionReceived?.Invoke(KeyboardAction.CapsChanged, _capsDown);
                }
                return;
            }
            var virtualKey = key.VirtualKey;
            if (isUp)
            {
                if (!_heldKeys.Remove(virtualKey)) return;
                if (virtualKey == (ushort)Keys.Enter) ActionReceived?.Invoke(KeyboardAction.EnterUp, _capsDown);
                else if (virtualKey == (ushort)Keys.Back) ActionReceived?.Invoke(KeyboardAction.BackspaceUp, _capsDown);
                return;
            }

            var firstDown = _heldKeys.Add(virtualKey);
            if (virtualKey == (ushort)Keys.Enter)
            {
                if (firstDown) ActionReceived?.Invoke(KeyboardAction.EnterDown, _capsDown);
                return;
            }
            if (virtualKey == (ushort)Keys.Back)
            {
                ActionReceived?.Invoke(KeyboardAction.BackspaceDown, _capsDown);
                return;
            }
            if (IsCharacterKey(virtualKey))
                ActionReceived?.Invoke(virtualKey == (ushort)Keys.Space ? KeyboardAction.Space : KeyboardAction.Typing, _capsDown);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool IsCharacterKey(ushort key) =>
        key is >= 0x30 and <= 0x39 or // top-row digits
        >= 0x41 and <= 0x5A or // letters
        0x20 or // space
        >= 0x60 and <= 0x69 or // numpad digits
        >= 0x6A and <= 0x6F or // numpad operators
        >= 0xBA and <= 0xC0 or // punctuation keys
        0xDB or 0xDC or 0xDD or 0xDE or 0xE2;

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr WParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawKeyboard
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VirtualKey;
        public uint Message;
        public uint ExtraInformation;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr rawInput, uint command, IntPtr data, ref uint size, uint headerSize);
}

internal enum KeyboardAction { Typing, Space, BackspaceDown, BackspaceUp, EnterDown, EnterUp, CapsChanged, ShiftDown, ShiftUp }
