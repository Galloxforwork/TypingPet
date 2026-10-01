using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace TypingPet;

internal sealed class BackgroundRemovalForm : Form
{
    private readonly Bitmap _source;
    private readonly Bitmap _previewSource;
    private readonly PictureBox _before = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(225, 230, 235) };
    private readonly PictureBox _after = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(225, 230, 235) };
    private readonly TrackBar _tolerance = new() { Minimum = 0, Maximum = 120, Value = 35, TickFrequency = 10, Width = 260 };
    private readonly Label _sampleLabel = new() { AutoSize = true, Margin = new Padding(12, 13, 8, 0) };
    private Color _background;

    public Color BackgroundColor => _background;
    public int Tolerance => _tolerance.Value;

    public BackgroundRemovalForm(string path)
    {
        using var loaded = Image.FromFile(path);
        _source = new Bitmap(loaded);
        var scale = Math.Min(1.0, 480.0 / Math.Max(_source.Width, _source.Height));
        _previewSource = new Bitmap(_source, new Size(Math.Max(1, (int)(_source.Width * scale)), Math.Max(1, (int)(_source.Height * scale))));
        _background = _source.GetPixel(0, 0);

        Text = "去除纯色背景";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 540);
        ClientSize = new Size(960, 640);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(14) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 88));
        root.Controls.Add(new Label { Text = "点击左图选取背景颜色；调节容差后查看右图。仅适合纯色或近似纯色背景。", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);

        var previews = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        previews.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        previews.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        previews.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previews.Controls.Add(new Label { Text = "原图（点击取色）", Dock = DockStyle.Fill }, 0, 0);
        previews.Controls.Add(new Label { Text = "处理后预览", Dock = DockStyle.Fill }, 1, 0);
        previews.Controls.Add(_before, 0, 1);
        previews.Controls.Add(_after, 1, 1);
        root.Controls.Add(previews, 0, 1);

        var bottom = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true };
        bottom.Controls.Add(new Label { Text = "容差", AutoSize = true, Margin = new Padding(0, 14, 0, 0) });
        bottom.Controls.Add(_tolerance);
        bottom.Controls.Add(_sampleLabel);
        var apply = new Button { Text = "使用处理结果", Width = 130, Height = 34, Margin = new Padding(20, 5, 5, 0) };
        apply.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        bottom.Controls.Add(apply);
        bottom.Controls.Add(new Button { Text = "取消", Width = 76, Height = 34, Margin = new Padding(0, 5, 0, 0), DialogResult = DialogResult.Cancel });
        root.Controls.Add(bottom, 0, 2);
        Controls.Add(root);
        _before.Image = _previewSource;
        _before.MouseClick += SampleBackground;
        _tolerance.Scroll += (_, _) => RefreshPreview();
        FormClosed += (_, _) => { _after.Image?.Dispose(); _previewSource.Dispose(); _source.Dispose(); };
        RefreshPreview();
    }

    private void SampleBackground(object? sender, MouseEventArgs e)
    {
        var scale = Math.Min((double)_before.ClientSize.Width / _previewSource.Width, (double)_before.ClientSize.Height / _previewSource.Height);
        var shownWidth = _previewSource.Width * scale;
        var shownHeight = _previewSource.Height * scale;
        var x = (int)((e.X - (_before.ClientSize.Width - shownWidth) / 2) / scale);
        var y = (int)((e.Y - (_before.ClientSize.Height - shownHeight) / 2) / scale);
        if (x < 0 || y < 0 || x >= _previewSource.Width || y >= _previewSource.Height) return;
        _background = _previewSource.GetPixel(x, y);
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        var previous = _after.Image;
        _after.Image = RemoveBackground(_previewSource, _background, Tolerance);
        previous?.Dispose();
        _sampleLabel.Text = $"背景色 #{_background.R:X2}{_background.G:X2}{_background.B:X2} · 容差 {Tolerance}";
    }

    public static Bitmap RemoveBackground(Image source, Color background, int tolerance)
    {
        var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var bits = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var stride = bits.Stride;
            var bytes = new byte[stride * bitmap.Height];
            Marshal.Copy(bits.Scan0, bytes, 0, bytes.Length);
            var visited = new bool[bitmap.Width * bitmap.Height];
            var queue = new Queue<int>();
            var limit = 3 * (tolerance + 18) * (tolerance + 18);

            void Add(int x, int y)
            {
                var position = y * bitmap.Width + x;
                if (visited[position]) return;
                visited[position] = true;
                var offset = y * stride + x * 4;
                var red = bytes[offset + 2] - background.R;
                var green = bytes[offset + 1] - background.G;
                var blue = bytes[offset] - background.B;
                if (red * red + green * green + blue * blue <= limit) queue.Enqueue(position);
            }

            for (var x = 0; x < bitmap.Width; x++) { Add(x, 0); Add(x, bitmap.Height - 1); }
            for (var y = 0; y < bitmap.Height; y++) { Add(0, y); Add(bitmap.Width - 1, y); }
            while (queue.Count > 0)
            {
                var position = queue.Dequeue();
                var x = position % bitmap.Width;
                var y = position / bitmap.Width;
                var offset = y * stride + x * 4;
                var red = bytes[offset + 2] - background.R;
                var green = bytes[offset + 1] - background.G;
                var blue = bytes[offset] - background.B;
                var distance = Math.Sqrt((red * red + green * green + blue * blue) / 3.0);
                bytes[offset + 3] = (byte)(bytes[offset + 3] * Math.Clamp((distance - tolerance) / 18.0, 0, 1));
                if (x > 0) Add(x - 1, y);
                if (x + 1 < bitmap.Width) Add(x + 1, y);
                if (y > 0) Add(x, y - 1);
                if (y + 1 < bitmap.Height) Add(x, y + 1);
            }
            Marshal.Copy(bytes, 0, bits.Scan0, bytes.Length);
        }
        finally { bitmap.UnlockBits(bits); }
        return bitmap;
    }
}
