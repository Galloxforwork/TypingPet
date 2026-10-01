using System.Drawing.Drawing2D;

namespace TypingPet;

internal sealed class CropImageForm : Form
{
    private readonly Bitmap _source;
    private readonly CropCanvas _canvas;

    public Rectangle SelectedCrop => _canvas.SelectedCrop;

    public CropImageForm(string path)
    {
        using var loaded = Image.FromFile(path);
        _source = new Bitmap(loaded);
        Text = "裁切图片 · 竖版 3:4";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 650);
        MinimumSize = new Size(560, 460);
        BackColor = Color.FromArgb(244, 247, 251);
        Font = new Font("Segoe UI", 9F);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(14) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        var instructions = new Label
        {
            Text = "固定竖版 3:4（宽:高） · 拖动白色取景框调整位置 · 滚轮或下方按钮调整范围",
            Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(57, 71, 93)
        };
        layout.Controls.Add(instructions, 0, 0);
        _canvas = new CropCanvas(_source) { Dock = DockStyle.Fill, BackColor = Color.FromArgb(31, 35, 45) };
        layout.Controls.Add(_canvas, 0, 1);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        var save = new Button { Text = "保存裁切", AutoSize = true, Height = 32, BackColor = Color.FromArgb(48, 93, 157), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        save.Click += (_, _) => { DialogResult = DialogResult.OK; Close(); };
        var cancel = new Button { Text = "取消", AutoSize = true, Height = 32 };
        cancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        var wider = new Button { Text = "扩大范围", AutoSize = true, Height = 32 };
        wider.Click += (_, _) => _canvas.ResizeSelection(1.12);
        var tighter = new Button { Text = "缩小范围", AutoSize = true, Height = 32 };
        tighter.Click += (_, _) => _canvas.ResizeSelection(1 / 1.12);
        buttons.Controls.AddRange([save, cancel, wider, tighter]);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
        FormClosed += (_, _) => _source.Dispose();
    }

    private sealed class CropCanvas : Control
    {
        private readonly Bitmap _image;
        private Rectangle _crop;
        private bool _dragging;
        private Point _dragOffset;

        public Rectangle SelectedCrop => _crop;

        public CropCanvas(Bitmap image)
        {
            _image = image;
            DoubleBuffered = true;
            TabStop = true;
            var maxWidth = Math.Min(image.Width / 3, image.Height / 4) * 3;
            if (maxWidth < 3) throw new ArgumentException("图片尺寸太小，无法裁切为 3:4。", nameof(image));
            var width = Math.Max(3, (int)Math.Round(maxWidth * 0.9 / 3) * 3);
            width = Math.Min(width, maxWidth);
            var height = width / 3 * 4;
            _crop = new Rectangle((image.Width - width) / 2, (image.Height - height) / 2, width, height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var imageArea = DisplayedImageBounds();
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(_image, imageArea);
            var selection = new Rectangle(
                imageArea.X + (int)Math.Round((double)_crop.X * imageArea.Width / _image.Width),
                imageArea.Y + (int)Math.Round((double)_crop.Y * imageArea.Height / _image.Height),
                Math.Max(1, (int)Math.Round((double)_crop.Width * imageArea.Width / _image.Width)),
                Math.Max(1, (int)Math.Round((double)_crop.Height * imageArea.Height / _image.Height)));
            using var shade = new SolidBrush(Color.FromArgb(145, 0, 0, 0));
            e.Graphics.FillRectangle(shade, imageArea.Left, imageArea.Top, imageArea.Width, Math.Max(0, selection.Top - imageArea.Top));
            e.Graphics.FillRectangle(shade, imageArea.Left, selection.Bottom, imageArea.Width, Math.Max(0, imageArea.Bottom - selection.Bottom));
            e.Graphics.FillRectangle(shade, imageArea.Left, selection.Top, Math.Max(0, selection.Left - imageArea.Left), selection.Height);
            e.Graphics.FillRectangle(shade, selection.Right, selection.Top, Math.Max(0, imageArea.Right - selection.Right), selection.Height);
            using var border = new Pen(Color.White, 2);
            e.Graphics.DrawRectangle(border, selection);
            using var grid = new Pen(Color.FromArgb(130, Color.White), 1);
            for (var i = 1; i < 3; i++)
            {
                var x = selection.Left + selection.Width * i / 3;
                var y = selection.Top + selection.Height * i / 3;
                e.Graphics.DrawLine(grid, x, selection.Top, x, selection.Bottom);
                e.Graphics.DrawLine(grid, selection.Left, y, selection.Right, y);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            var point = ScreenToImage(e.Location);
            if (!_crop.Contains(point)) MoveCropTo(point.X - _crop.Width / 2, point.Y - _crop.Height / 2);
            _dragOffset = new Point(point.X - _crop.X, point.Y - _crop.Y);
            _dragging = true;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                var point = ScreenToImage(e.Location);
                MoveCropTo(point.X - _dragOffset.X, point.Y - _dragOffset.Y);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging = false;
            Capture = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            ResizeSelection(e.Delta > 0 ? 1 / 1.12 : 1.12);
        }

        public void ResizeSelection(double factor)
        {
            var maxWidth = Math.Min(_image.Width / 3, _image.Height / 4) * 3;
            var width = Math.Clamp((int)Math.Round(_crop.Width * factor / 3) * 3, 3, maxWidth);
            if (width == _crop.Width) return;
            var centerX = _crop.Left + _crop.Width / 2;
            var centerY = _crop.Top + _crop.Height / 2;
            _crop.Size = new Size(width, width / 3 * 4);
            MoveCropTo(centerX - _crop.Width / 2, centerY - _crop.Height / 2);
        }

        private Rectangle DisplayedImageBounds()
        {
            var scale = Math.Min((double)Math.Max(1, Width - 24) / _image.Width, (double)Math.Max(1, Height - 24) / _image.Height);
            var width = Math.Max(1, (int)Math.Round(_image.Width * scale));
            var height = Math.Max(1, (int)Math.Round(_image.Height * scale));
            return new Rectangle((Width - width) / 2, (Height - height) / 2, width, height);
        }

        private Point ScreenToImage(Point point)
        {
            var area = DisplayedImageBounds();
            return new Point(
                Math.Clamp((int)Math.Round((double)(point.X - area.X) * _image.Width / area.Width), 0, _image.Width),
                Math.Clamp((int)Math.Round((double)(point.Y - area.Y) * _image.Height / area.Height), 0, _image.Height));
        }

        private void MoveCropTo(int x, int y)
        {
            _crop.Location = new Point(Math.Clamp(x, 0, _image.Width - _crop.Width), Math.Clamp(y, 0, _image.Height - _crop.Height));
            Invalidate();
        }
    }
}
