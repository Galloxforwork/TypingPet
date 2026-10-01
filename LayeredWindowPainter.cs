using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TypingPet;

// Paints an entire pet frame with per-pixel alpha, including its new screen bounds.
internal static class LayeredWindowPainter
{
    private const int DibRgbColors = 0;
    private const int UlwAlpha = 2;
    private const byte AcSrcAlpha = 1;

    public static Bitmap PrepareFrame(Size canvasSize, Image? image, Rectangle imageBounds)
    {
        var frame = new Bitmap(canvasSize.Width, canvasSize.Height, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(frame))
        {
            graphics.Clear(Color.Transparent);
            if (image is not null)
            {
                graphics.CompositingMode = CompositingMode.SourceOver;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                var scale = Math.Min((double)imageBounds.Width / image.Width, (double)imageBounds.Height / image.Height);
                var width = Math.Max(1, (int)Math.Round(image.Width * scale));
                var height = Math.Max(1, (int)Math.Round(image.Height * scale));
                using var attributes = new ImageAttributes();
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                graphics.DrawImage(image,
                    new Rectangle(imageBounds.X + (imageBounds.Width - width) / 2, imageBounds.Y + (imageBounds.Height - height) / 2, width, height),
                    0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }

        var area = new Rectangle(Point.Empty, canvasSize);
        var locked = frame.LockBits(area, ImageLockMode.ReadWrite, PixelFormat.Format32bppPArgb);
        try
        {
            var row = new byte[canvasSize.Width * 4];
            for (var y = 0; y < canvasSize.Height; y++)
            {
                var address = IntPtr.Add(locked.Scan0, y * locked.Stride);
                Marshal.Copy(address, row, 0, row.Length);
                // Pillow's reference pet thresholds alpha after resizing. Undo premultiplication
                // before setting retained edge pixels opaque, so they do not acquire dark RGB.
                for (var x = 0; x < row.Length; x += 4)
                {
                    var alpha = row[x + 3];
                    if (alpha < 128) { row[x] = row[x + 1] = row[x + 2] = row[x + 3] = 0; continue; }
                    row[x] = (byte)Math.Min(255, row[x] * 255 / alpha);
                    row[x + 1] = (byte)Math.Min(255, row[x + 1] * 255 / alpha);
                    row[x + 2] = (byte)Math.Min(255, row[x + 2] * 255 / alpha);
                    row[x + 3] = 255;
                }
                Marshal.Copy(row, 0, address, row.Length);
            }
        }
        finally { frame.UnlockBits(locked); }
        return frame;
    }

    public static void Paint(IntPtr window, Rectangle bounds, Image? image, Rectangle imageBounds)
    {
        if (window == IntPtr.Zero || bounds.Width <= 0 || bounds.Height <= 0) return;
        using var frame = PrepareFrame(bounds.Size, image, imageBounds);
        Paint(window, bounds, frame);
    }

    public static void Paint(IntPtr window, Rectangle bounds, Bitmap frame)
    {
        if (window == IntPtr.Zero || bounds.Width <= 0 || bounds.Height <= 0 || frame.Size != bounds.Size) return;
        var screenDc = GetDC(IntPtr.Zero);
        if (screenDc == IntPtr.Zero) return;
        var memoryDc = CreateCompatibleDC(screenDc);
        if (memoryDc == IntPtr.Zero) { ReleaseDC(IntPtr.Zero, screenDc); return; }
        var info = new BitmapInfo { Header = new BitmapInfoHeader
        {
            Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(), Width = bounds.Width, Height = -bounds.Height,
            Planes = 1, BitCount = 32, Compression = 0
        } };
        var dib = CreateDIBSection(screenDc, ref info, DibRgbColors, out var pixels, IntPtr.Zero, 0);
        if (dib == IntPtr.Zero || pixels == IntPtr.Zero)
        {
            DeleteDC(memoryDc); ReleaseDC(IntPtr.Zero, screenDc); return;
        }
        var previous = SelectObject(memoryDc, dib);
        try
        {
            var area = new Rectangle(Point.Empty, frame.Size);
            var locked = frame.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var row = new byte[bounds.Width * 4];
                for (var y = 0; y < bounds.Height; y++)
                {
                    Marshal.Copy(IntPtr.Add(locked.Scan0, y * locked.Stride), row, 0, row.Length);
                    Marshal.Copy(row, 0, IntPtr.Add(pixels, y * row.Length), row.Length);
                }
            }
            finally { frame.UnlockBits(locked); }

            var position = new NativePoint(bounds.X, bounds.Y);
            var size = new NativeSize(bounds.Width, bounds.Height);
            var origin = new NativePoint(0, 0);
            var blend = new BlendFunction(0, 0, 255, AcSrcAlpha);
            if (!UpdateLayeredWindow(window, screenDc, ref position, ref size, memoryDc, ref origin, 0, ref blend, UlwAlpha))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            SelectObject(memoryDc, previous);
            DeleteObject(dib);
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativePoint(int x, int y) { public int X = x; public int Y = y; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeSize(int width, int height) { public int Width = width; public int Height = height; }
    [StructLayout(LayoutKind.Sequential)] private struct BlendFunction(byte operation, byte flags, byte alpha, byte format)
    { public byte Operation = operation; public byte Flags = flags; public byte Alpha = alpha; public byte Format = format; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfoHeader
    {
        public uint Size; public int Width; public int Height; public ushort Planes; public ushort BitCount;
        public uint Compression; public uint SizeImage; public int XPelsPerMeter; public int YPelsPerMeter;
        public uint ClrUsed; public uint ClrImportant;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public BitmapInfoHeader Header; public uint Red; public uint Green; public uint Blue; }

    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, int usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr dstDc, ref NativePoint position, ref NativeSize size, IntPtr srcDc, ref NativePoint srcPoint, uint colorKey, ref BlendFunction blend, int flags);
}
