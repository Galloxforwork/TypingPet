using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace TypingPet;

internal sealed class PetWindow : Form, IMessageFilter
{
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const int WmMouseWheel = 0x020A;
    private const int BumpAmplitude = 8;
    private int CanvasPadding => Math.Max(30, Math.Max(
        Math.Max(_motionAmplitude + _scaleAmplitude, _swayAmplitude + _scaleAmplitude / 2) + 10,
        _counterInMainLayer && _counterEnabled ? _motionAmplitude + _counter.Height + Math.Max(0, _counterDistancePixels) + 10 : 0));
    private static readonly TimeSpan BumpDuration = TimeSpan.FromMilliseconds(110);
    private Image? _image;
    private Image? _backgroundImage;
    private Bitmap? _backgroundFrame;
    private string? _currentImagePath;
    private string? _currentBackgroundPath;
    private readonly CounterWindow _counter = new();
    private readonly ContextMenuStrip _menu = new();
    private bool _clickThrough;
    private bool _counterEnabled;
    private bool _counterInMainLayer;
    private bool _counterBorderEnabled;
    private int _counterSizePercent = 100;
    private int _counterDistancePixels = 2;
    private readonly System.Windows.Forms.Timer _bumpTimer = new() { Interval = 15 };
    private readonly System.Diagnostics.Stopwatch _bumpClock = new();
    private Point _restingLocation;
    private Rectangle _canvasBounds;
    private Point _dragStartCursor;
    private Point _dragStartLocation;
    private int _bumpStartOffset;
    private int _currentOffset;
    private bool _isDragging;
    private bool _shapeMode;
    private int _motionAmplitude = BumpAmplitude;
    private int _scaleAmplitude;
    private int _swayAmplitude;
    private decimal _animationSpeed = 1M;
    private int _baseWidth = 160;
    private int _baseHeight = 160;
    private int _zoomPercent = 100;
    private Rectangle _imageBounds = new(30, 30, 160, 160);
    private int _imageBottomOffset;

    public event Action? OpenSettingsRequested;
    public event Action? CompanionHidden;
    public event Action? ExitRequested;
    public event Action? AppearanceChanged;

    public int ZoomPercent => _zoomPercent;
    public Point RestingLocation => _restingLocation;
    public bool CounterEnabled => _counterEnabled;

    public PetWindow(int zoomPercent, Point? savedLocation, bool counterEnabled)
    {
        _zoomPercent = Math.Clamp(zoomPercent, 40, 600);
        _baseWidth = 160 * _zoomPercent / 100;
        _baseHeight = 160 * _zoomPercent / 100;
        _counterEnabled = counterEnabled;
        ResetImageBounds();
        Text = "打字小伴侣";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(_baseWidth + 2 * CanvasPadding, _baseHeight + 2 * CanvasPadding);
        var fallback = new Point(Screen.PrimaryScreen!.WorkingArea.Right - Width - 25, Screen.PrimaryScreen.WorkingArea.Top + 12);
        var target = savedLocation ?? fallback;
        var workArea = Screen.FromPoint(target).WorkingArea;
        Location = new Point(
            Math.Clamp(target.X, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width)),
            Math.Clamp(target.Y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height)));
        TopMost = true;
        _restingLocation = Location;
        _canvasBounds = Bounds;
        _counter.SetNumberFromCurrentScale(_zoomPercent);
        _bumpTimer.Tick += (_, _) => UpdateBump();
        Application.AddMessageFilter(this);
        Shown += (_, _) => PaintPet(_canvasBounds);
        FormClosed += (_, _) => { Application.RemoveMessageFilter(this); _bumpTimer.Dispose(); _counter.Close(); _menu.Dispose(); _image?.Dispose(); _backgroundImage?.Dispose(); _backgroundFrame?.Dispose(); };
        _menu.Items.Add("重置桌宠大小", null, (_, _) => SetZoom(100));
        _menu.Items.Add("显示/隐藏数字", null, (_, _) => ToggleCounter());
        _menu.Items.Add("打开设置", null, (_, _) => OpenSettingsRequested?.Invoke());
        _menu.Items.Add("隐藏桌宠与数字", null, (_, _) => { HideCompanion(); CompanionHidden?.Invoke(); });
        _menu.Items.Add("完全退出程序", null, (_, _) => ExitRequested?.Invoke());
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, true);
        MouseDown += BeginDrag;
        MouseMove += ContinueDrag;
        MouseUp += EndDrag;
        MouseDoubleClick += (_, e) => { if (e.Button == MouseButtons.Left) OpenSettingsRequested?.Invoke(); };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | WsExLayered; // WS_EX_NOACTIVATE and per-pixel alpha
            if (_clickThrough) cp.ExStyle |= WsExTransparent;
            return cp;
        }
    }

    public void SetPinned(bool pinned) { TopMost = pinned; _counter.TopMost = pinned; }

    public void ShowCompanion()
    {
        if (!Visible) Show();
        PaintPet(_canvasBounds);
        if (_counterEnabled && !_counterInMainLayer && !_counter.Visible) _counter.Show();
        if (!_counterEnabled || _counterInMainLayer) _counter.Hide();
        PlaceCounter();
    }

    public void HideCompanion() { Hide(); _counter.Hide(); }

    private void ToggleCounter()
    {
        SetCounterEnabled(!_counterEnabled);
    }

    public void SetCounterEnabled(bool enabled)
    {
        if (_counterEnabled == enabled) return;
        _counterEnabled = enabled;
        if (_counterEnabled && Visible && !_counterInMainLayer) { _counter.Show(); PlaceCounter(); }
        else _counter.Hide();
        PaintPet(_canvasBounds);
        AppearanceChanged?.Invoke();
    }

    public void Bump()
    {
        if (_isDragging || !Visible || _animationSpeed <= 0 || (_motionAmplitude == 0 && _scaleAmplitude == 0 && _swayAmplitude == 0)) return;
        if (!_bumpTimer.Enabled)
        {
            _restingLocation = _canvasBounds.Location;
            _currentOffset = 0;
            ResetImageBounds();
        }
        _bumpStartOffset = _currentOffset;
        _bumpClock.Restart();
        _bumpTimer.Start();
    }

    public void ConfigureMotion(bool shapeMode, int amplitude, decimal speed = 1M, int scaleAmplitude = 0, int swayAmplitude = 0)
    {
        var oldBottom = _restingLocation.Y + CanvasPadding + _baseHeight;
        var oldCenterX = _restingLocation.X + CanvasPadding + _baseWidth / 2;
        _shapeMode = shapeMode;
        _motionAmplitude = Math.Clamp(amplitude, 0, 200);
        _scaleAmplitude = Math.Clamp(scaleAmplitude, 0, 200);
        _swayAmplitude = Math.Clamp(swayAmplitude, 0, 200);
        _animationSpeed = Math.Clamp(speed, 0M, 3M);
        _bumpTimer.Stop(); _bumpClock.Stop(); _currentOffset = 0; _bumpStartOffset = 0;
        _baseWidth = 160 * _zoomPercent / 100;
        _baseHeight = 160 * _zoomPercent / 100;
        var width = _baseWidth + 2 * CanvasPadding;
        var height = _baseHeight + 2 * CanvasPadding;
        _restingLocation = new Point(oldCenterX - _baseWidth / 2 - CanvasPadding, oldBottom - _baseHeight - CanvasPadding);
        Size = new Size(width, height);
        Location = _restingLocation;
        _canvasBounds = new Rectangle(_restingLocation, Size);
        ResetImageBounds();
        SelectBackgroundFrame();
        PaintPet(_canvasBounds);
    }

    public void SetCounter(long shownValue)
    {
        _counter.SetNumber(shownValue, _zoomPercent);
        if (_counterInMainLayer) PaintPet(_canvasBounds);
        PlaceCounter();
    }

    public void ConfigureCounterAppearance(bool showBorder, bool inMainLayer, int sizePercent, int distancePixels,
        string fontFamily, FontStyle fontStyle, Color textFill, Color textOutline, int textOutlineWidth,
        Color borderColor, Color borderFill, bool borderFillEnabled, int borderWidth, int opacityPercent)
    {
        var oldBottom = _restingLocation.Y + CanvasPadding + _baseHeight;
        var oldCenterX = _restingLocation.X + CanvasPadding + _baseWidth / 2;
        _counterBorderEnabled = showBorder;
        _counterInMainLayer = inMainLayer;
        _counterSizePercent = Math.Clamp(sizePercent, 30, 300);
        _counterDistancePixels = Math.Clamp(distancePixels, -200, 400);
        _counter.SetAppearance(_counterBorderEnabled, _counterSizePercent, fontFamily, fontStyle, textFill, textOutline,
            textOutlineWidth, borderColor, borderFill, borderFillEnabled, borderWidth, opacityPercent);
        var width = _baseWidth + 2 * CanvasPadding;
        var height = _baseHeight + 2 * CanvasPadding;
        _restingLocation = new Point(oldCenterX - _baseWidth / 2 - CanvasPadding, oldBottom - _baseHeight - CanvasPadding);
        Size = new Size(width, height);
        Location = _restingLocation;
        _canvasBounds = new Rectangle(_restingLocation, Size);
        SelectBackgroundFrame();
        if (_counterEnabled && Visible && !_counterInMainLayer) _counter.Show(); else _counter.Hide();
        PaintPet(_canvasBounds);
        PlaceCounter();
    }

    public void SetClickThrough(bool enabled)
    {
        if (_clickThrough == enabled) return;
        _clickThrough = enabled;
        RecreateHandle();
        PaintPet(_canvasBounds);
        _counter.SetClickThrough(enabled);
    }

    public void UpdateState(int stage, string title, string? imagePath, string? backgroundPath, bool capsOn, bool shiftDown)
    {
        if (_currentImagePath != imagePath)
        {
            var old = _image; _image = null; old?.Dispose();
            _currentImagePath = imagePath;
            if (imagePath is not null && File.Exists(imagePath))
            {
                try { using var source = Image.FromFile(imagePath); _image = new Bitmap(source); }
                catch { }
            }
        }
        if (_currentBackgroundPath != backgroundPath)
        {
            _backgroundImage?.Dispose();
            _backgroundImage = null;
            _currentBackgroundPath = backgroundPath;
            if (backgroundPath is not null && File.Exists(backgroundPath))
            {
                try { using var source = Image.FromFile(backgroundPath); _backgroundImage = new Bitmap(source); }
                catch { }
            }
            SelectBackgroundFrame();
        }
        PaintPet(_canvasBounds);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseWheel)
        {
            var delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
            SetZoom(_zoomPercent + (delta > 0 ? 10 : -10));
            return;
        }
        base.WndProc(ref m);
    }

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg != WmMouseWheel || !_canvasBounds.Contains(Cursor.Position)) return false;
        var delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
        SetZoom(_zoomPercent + (delta > 0 ? 10 : -10));
        return true;
    }

    private void SetZoom(int percent)
    {
        var oldBottom = _restingLocation.Y + CanvasPadding + _baseHeight;
        var oldCenterX = _restingLocation.X + CanvasPadding + _baseWidth / 2;
        _zoomPercent = Math.Clamp(percent, 40, 600);
        _baseWidth = 160 * _zoomPercent / 100;
        _baseHeight = 160 * _zoomPercent / 100;
        _counter.SetNumberFromCurrentScale(_zoomPercent);
        _bumpTimer.Stop(); _bumpClock.Stop(); _currentOffset = 0;
        ResetImageBounds();
        SelectBackgroundFrame();
        var workArea = Screen.FromPoint(new Point(oldCenterX, oldBottom)).WorkingArea;
        var width = _baseWidth + 2 * CanvasPadding;
        var height = _baseHeight + 2 * CanvasPadding;
        var left = Math.Clamp(oldCenterX - _baseWidth / 2 - CanvasPadding, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        var top = Math.Clamp(oldBottom - _baseHeight - CanvasPadding, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
        PaintPet(new Rectangle(left, top, width, height));
        _restingLocation = _canvasBounds.Location;
        PlaceCounter();
        AppearanceChanged?.Invoke();
    }

    private void PlaceCounter()
    {
        if (_counter.IsDisposed) return;
        if (!_counterEnabled || _counterInMainLayer || !Visible) { _counter.Hide(); return; }
        _counter.Location = new Point(_canvasBounds.X + CanvasPadding + (_baseWidth - _counter.Width) / 2, _canvasBounds.Y + CanvasPadding + _baseHeight + _imageBottomOffset + _counterDistancePixels);
    }

    private void UpdateBump()
    {
        var duration = BumpDuration.TotalMilliseconds / (double)Math.Max(0.1M, _animationSpeed);
        var progress = Math.Clamp(_bumpClock.Elapsed.TotalMilliseconds / duration, 0, 1);
        if (progress >= 1)
        {
            _bumpTimer.Stop();
            _bumpClock.Stop();
            _currentOffset = 0;
            ResetImageBounds();
            PaintPet(_canvasBounds);
            PlaceCounter();
            return;
        }

        var pulse = Math.Sin(progress * Math.PI * 4) * (1 - progress);
        _currentOffset = Math.Clamp((int)Math.Round(_bumpStartOffset * (1 - progress) + _motionAmplitude * pulse), -_motionAmplitude, _motionAmplitude);
        var scale = (int)Math.Round(_scaleAmplitude * Math.Abs(pulse));
        var sway = (int)Math.Round(_swayAmplitude * pulse);
        if (_shapeMode)
        {
            var squeeze = (int)Math.Round(Math.Abs(_currentOffset) * 0.6);
            var stretch = Math.Max(0, _currentOffset);
            var width = Math.Max(1, _baseWidth - squeeze + scale);
            var height = Math.Max(1, _baseHeight + stretch + scale);
            _imageBounds = new Rectangle(CanvasPadding + squeeze / 2 - scale / 2 + sway, CanvasPadding - stretch - scale, width, height);
            _imageBottomOffset = 0;
        }
        else
        {
            _imageBounds = new Rectangle(CanvasPadding - scale / 2 + sway, CanvasPadding + _currentOffset - scale, _baseWidth + scale, _baseHeight + scale);
            _imageBottomOffset = _currentOffset;
        }
        PaintPet(_canvasBounds);
        PlaceCounter();
    }

    private void BeginDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || e.Clicks > 1) return;
        _bumpTimer.Stop();
        _bumpClock.Stop();
        _currentOffset = 0;
        ResetImageBounds();
        PaintPet(_canvasBounds);
        _restingLocation = _canvasBounds.Location;
        _dragStartCursor = Cursor.Position;
        _dragStartLocation = _canvasBounds.Location;
        _isDragging = true;
    }

    private void ContinueDrag(object? sender, MouseEventArgs e)
    {
        if (!_isDragging || e.Button != MouseButtons.Left) return;
        var cursor = Cursor.Position;
        Location = new Point(_dragStartLocation.X + cursor.X - _dragStartCursor.X, _dragStartLocation.Y + cursor.Y - _dragStartCursor.Y);
        _canvasBounds.Location = Location;
        _restingLocation = _canvasBounds.Location;
        PlaceCounter();
    }

    private void EndDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) { _menu.Show(Cursor.Position); return; }
        if (e.Button != MouseButtons.Left) return;
        _isDragging = false;
        _restingLocation = _canvasBounds.Location;
        PlaceCounter();
        AppearanceChanged?.Invoke();
    }

    private void PaintPet(Rectangle bounds)
    {
        _canvasBounds = bounds;
        if (Bounds != bounds) Bounds = bounds;
        if (!IsHandleCreated) return;
        using var foreground = LayeredWindowPainter.PrepareFrame(bounds.Size, _image, _imageBounds);
        if ((_backgroundFrame is not null && _backgroundFrame.Size == bounds.Size) || (_counterEnabled && _counterInMainLayer))
        {
            using var composite = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(composite))
            {
                if (_backgroundFrame is not null && _backgroundFrame.Size == bounds.Size) graphics.DrawImageUnscaled(_backgroundFrame, 0, 0);
                if (foreground is not null) graphics.DrawImageUnscaled(foreground, 0, 0);
                if (_counterEnabled && _counterInMainLayer) DrawCounter(graphics);
            }
            LayeredWindowPainter.Paint(Handle, bounds, composite);
            return;
        }
        LayeredWindowPainter.Paint(Handle, bounds, foreground);
    }

    private void DrawCounter(Graphics graphics)
    {
        var centerX = _imageBounds.Left + _imageBounds.Width / 2F;
        var y = _imageBounds.Bottom + _counterDistancePixels;
        _counter.DrawInto(graphics, centerX, y);
    }

    private void SelectBackgroundFrame()
    {
        _backgroundFrame?.Dispose();
        _backgroundFrame = _backgroundImage is null ? null : LayeredWindowPainter.PrepareFrame(
            new Size(_baseWidth + 2 * CanvasPadding, _baseHeight + 2 * CanvasPadding),
            _backgroundImage,
            new Rectangle(CanvasPadding, CanvasPadding, _baseWidth, _baseHeight));
    }

    private void ResetImageBounds()
    {
        _imageBounds = new Rectangle(CanvasPadding, CanvasPadding, _baseWidth, _baseHeight);
        _imageBottomOffset = 0;
    }

}

internal sealed class CounterWindow : Form
{
    private string _displayText = "0";
    private int _zoomPercent = 100;
    private int _sizePercent = 100;
    private bool _showBorder;
    private string _fontFamily = "Segoe UI";
    private FontStyle _fontStyle = FontStyle.Bold;
    private Color _textFill = Color.FromArgb(35, 43, 56);
    private Color _textOutline = Color.Empty;
    private int _textOutlineWidth;
    private Color _borderColor = Color.FromArgb(85, 100, 122);
    private Color _borderFill = Color.Empty;
    private bool _borderFillEnabled;
    private int _borderWidth = 1;
    private int _opacityPercent = 100;
    private Bitmap? _frame;

    public string DisplayText => _displayText;

    public CounterWindow()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black; TopMost = true; ShowInTaskbar = false;
        Shown += (_, _) => PaintFrame();
        HandleCreated += (_, _) => PaintFrame();
        SetNumber(0, 100);
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000 | 0x00080000; if (_clickThrough) cp.ExStyle |= 0x00000020; return cp; }
    }

    private bool _clickThrough;
    public void SetClickThrough(bool enabled) { _clickThrough = enabled; RecreateHandle(); }
    public void SetNumber(long value, int zoom) { _displayText = Math.Max(0, value).ToString(); SetNumberFromCurrentScale(zoom); }
    public void SetAppearance(bool showBorder, int sizePercent, string fontFamily, FontStyle fontStyle,
        Color textFill, Color textOutline, int textOutlineWidth, Color borderColor, Color borderFill,
        bool borderFillEnabled, int borderWidth, int opacityPercent)
    {
        _showBorder = showBorder;
        _sizePercent = Math.Clamp(sizePercent, 30, 300);
        _fontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "Segoe UI" : fontFamily;
        _fontStyle = fontStyle;
        _textFill = textFill;
        _textOutline = textOutline;
        _textOutlineWidth = Math.Clamp(textOutlineWidth, 0, 12);
        _borderColor = borderColor;
        _borderFill = borderFill;
        _borderFillEnabled = borderFillEnabled;
        _borderWidth = Math.Clamp(borderWidth, 0, 12);
        _opacityPercent = Math.Clamp(opacityPercent, 0, 100);
        SetNumberFromCurrentScale(_zoomPercent);
    }

    public void SetNumberFromCurrentScale(int zoom)
    {
        _zoomPercent = Math.Clamp(zoom, 40, 600);
        RenderFrame();
        PaintFrame();
    }

    public void DrawInto(Graphics graphics, float centerX, float y)
    {
        if (string.IsNullOrEmpty(_displayText)) return;
        using var font = CreateDisplayFont();
        using var path = CreateTextPath(font);
        var glyphBounds = path.GetBounds();
        var contentMargin = ContentMargin;
        var dx = centerX - glyphBounds.Width / 2F - glyphBounds.X;
        var dy = y + contentMargin - glyphBounds.Y;
        DrawVisuals(graphics, path, glyphBounds, dx, dy, contentMargin);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        base.OnHandleDestroyed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _frame?.Dispose();
        base.Dispose(disposing);
    }

    private float ContentMargin => (_showBorder || _borderFillEnabled ? Math.Max(5, _borderWidth + 3) : 2) + Math.Max(2, _textOutlineWidth + 1);

    private Font CreateDisplayFont() => new(_fontFamily, 14F * _zoomPercent / 100 * _sizePercent / 100, _fontStyle, GraphicsUnit.Point);

    private GraphicsPath CreateTextPath(Font font)
    {
        var path = new GraphicsPath();
        path.AddString(_displayText, font.FontFamily, (int)font.Style, Math.Max(1, font.Height), PointF.Empty, StringFormat.GenericTypographic);
        return path;
    }

    private void RenderFrame()
    {
        using var font = CreateDisplayFont();
        using var path = CreateTextPath(font);
        var glyph = path.GetBounds();
        var margin = ContentMargin;
        var width = Math.Max(1, (int)Math.Ceiling(glyph.Width + margin * 2));
        var height = Math.Max(1, (int)Math.Ceiling(glyph.Height + margin * 2));
        _frame?.Dispose();
        _frame = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(_frame))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            DrawVisuals(graphics, path, glyph, margin - glyph.X, margin - glyph.Y, margin);
        }
        ClientSize = _frame.Size;
    }

    private void DrawVisuals(Graphics graphics, GraphicsPath path, RectangleF glyphBounds, float dx, float dy, float margin)
    {
        var state = graphics.Save();
        try
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            using var translated = (GraphicsPath)path.Clone();
            using var transform = new Matrix();
            transform.Translate(dx, dy);
            translated.Transform(transform);

            var textX = glyphBounds.X + dx;
            var textY = glyphBounds.Y + dy;
            var outer = new RectangleF(textX - margin, textY - margin, glyphBounds.Width + margin * 2, glyphBounds.Height + margin * 2);
            if (_borderFillEnabled && !_borderFill.IsEmpty)
                using (var fill = new SolidBrush(ApplyOpacity(_borderFill))) graphics.FillRectangle(fill, outer);
            if (_showBorder && _borderWidth > 0)
                using (var pen = new Pen(ApplyOpacity(_borderColor), _borderWidth)) graphics.DrawRectangle(pen, outer.X + _borderWidth / 2F, outer.Y + _borderWidth / 2F, Math.Max(0, outer.Width - _borderWidth), Math.Max(0, outer.Height - _borderWidth));
            if (_textOutlineWidth > 0 && !_textOutline.IsEmpty)
                using (var pen = new Pen(ApplyOpacity(_textOutline), _textOutlineWidth) { LineJoin = LineJoin.Round }) graphics.DrawPath(pen, translated);
            using (var brush = new SolidBrush(ApplyOpacity(_textFill))) graphics.FillPath(brush, translated);
        }
        finally { graphics.Restore(state); }
    }

    private Color ApplyOpacity(Color color) => Color.FromArgb(color.A * _opacityPercent / 100, color.R, color.G, color.B);

    private void PaintFrame()
    {
        if (_frame is not null && IsHandleCreated && !IsDisposed)
            LayeredWindowPainter.Paint(Handle, new Rectangle(Location, _frame.Size), _frame);
    }
}
