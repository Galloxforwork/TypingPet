using System.Windows.Forms;

namespace TypingPet;

internal sealed class PetWindow : Form, IMessageFilter
{
    private const int WsExTransparent = 0x00000020;
    private const int WsExLayered = 0x00080000;
    private const int WmMouseWheel = 0x020A;
    private const int BumpAmplitude = 8;
    private const int CanvasPadding = 30;
    private static readonly TimeSpan BumpDuration = TimeSpan.FromMilliseconds(110);
    private Image? _image;
    private string? _currentImagePath;
    private readonly Dictionary<string, ShapeFrameSet> _shapeFrameCache = new();
    private readonly Queue<string> _shapeCacheOrder = new();
    private ShapeFrameSet? _activeShapeFrames;
    private readonly CounterWindow _counter = new();
    private readonly ContextMenuStrip _menu = new();
    private bool _clickThrough;
    private bool _counterEnabled;
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
    private int _baseWidth = 160;
    private int _baseHeight = 160;
    private int _zoomPercent = 100;
    private Rectangle _imageBounds = new(CanvasPadding, CanvasPadding, 160, 160);
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
        FormClosed += (_, _) => { Application.RemoveMessageFilter(this); _bumpTimer.Dispose(); _counter.Close(); _menu.Dispose(); _image?.Dispose(); foreach (var frames in _shapeFrameCache.Values) frames.Dispose(); };
        _menu.Items.Add("重置桌宠大小", null, (_, _) => SetZoom(100));
        _menu.Items.Add("显示/隐藏数字", null, (_, _) => ToggleCounter());
        _menu.Items.Add("打开设置", null, (_, _) => OpenSettingsRequested?.Invoke());
        _menu.Items.Add("隐藏桌宠与数字", null, (_, _) => { HideCompanion(); CompanionHidden?.Invoke(); });
        _menu.Items.Add("完全退出程序", null, (_, _) => ExitRequested?.Invoke());
        MouseDown += BeginDrag;
        MouseMove += ContinueDrag;
        MouseUp += EndDrag;
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
        if (_counterEnabled && !_counter.Visible) _counter.Show();
        PlaceCounter();
    }

    public void HideCompanion() { Hide(); _counter.Hide(); }

    private void ToggleCounter()
    {
        _counterEnabled = !_counterEnabled;
        if (_counterEnabled && Visible) { _counter.Show(); PlaceCounter(); }
        else _counter.Hide();
        AppearanceChanged?.Invoke();
    }

    public void Bump()
    {
        if (_isDragging || !Visible) return;
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

    public void ConfigureMotion(bool shapeMode, int amplitude)
    {
        _shapeMode = shapeMode;
        _motionAmplitude = Math.Clamp(amplitude, 1, 30);
        _baseWidth = 160 * _zoomPercent / 100;
        _baseHeight = 160 * _zoomPercent / 100;
        ResetImageBounds();
        SelectShapeFrames();
        PaintPet(_canvasBounds);
    }

    public void SetCounter(long shownValue) { _counter.SetNumber(shownValue, _zoomPercent); PlaceCounter(); }

    public void SetClickThrough(bool enabled)
    {
        if (_clickThrough == enabled) return;
        _clickThrough = enabled;
        RecreateHandle();
        PaintPet(_canvasBounds);
        _counter.SetClickThrough(enabled);
    }

    public void UpdateState(int stage, string title, string? imagePath, bool capsOn, bool shiftDown)
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
            SelectShapeFrames();
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
        _bumpTimer.Stop(); _bumpClock.Stop(); _currentOffset = 0;
        ResetImageBounds();
        SelectShapeFrames();
        var workArea = Screen.FromPoint(new Point(oldCenterX, oldBottom)).WorkingArea;
        var width = _baseWidth + 2 * CanvasPadding;
        var height = _baseHeight + 2 * CanvasPadding;
        var left = Math.Clamp(oldCenterX - _baseWidth / 2 - CanvasPadding, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        var top = Math.Clamp(oldBottom - _baseHeight - CanvasPadding, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));
        PaintPet(new Rectangle(left, top, width, height));
        _restingLocation = _canvasBounds.Location;
        _counter.SetNumberFromCurrentScale(_zoomPercent);
        PlaceCounter();
        AppearanceChanged?.Invoke();
    }

    private void PlaceCounter()
    {
        if (_counter.IsDisposed) return;
        _counter.Location = new Point(_canvasBounds.X + CanvasPadding + (_baseWidth - _counter.Width) / 2, _canvasBounds.Y + CanvasPadding + _baseHeight + _imageBottomOffset + 2);
    }

    private void UpdateBump()
    {
        var progress = Math.Clamp(_bumpClock.Elapsed.TotalMilliseconds / BumpDuration.TotalMilliseconds, 0, 1);
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
        if (_shapeMode)
        {
            var squeeze = (int)Math.Round(Math.Abs(_currentOffset) * 0.6);
            var stretch = Math.Max(0, _currentOffset);
            _imageBounds = new Rectangle(CanvasPadding + squeeze / 2, CanvasPadding - stretch, _baseWidth - squeeze, _baseHeight + stretch);
            _imageBottomOffset = 0;
        }
        else
        {
            _imageBounds = new Rectangle(CanvasPadding, CanvasPadding + _currentOffset, _baseWidth, _baseHeight);
            _imageBottomOffset = _currentOffset;
        }
        PaintPet(_canvasBounds);
        PlaceCounter();
    }

    private void BeginDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
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
        if (_shapeMode && _activeShapeFrames is not null && _activeShapeFrames.CanvasSize == bounds.Size)
            LayeredWindowPainter.Paint(Handle, bounds, _activeShapeFrames.GetNearest(_currentOffset));
        else
            LayeredWindowPainter.Paint(Handle, bounds, _image, _imageBounds);
    }

    private void SelectShapeFrames()
    {
        _activeShapeFrames = null;
        if (!_shapeMode || _image is null || _currentImagePath is null) return;
        var key = $"{_currentImagePath}|{_baseWidth}|{_motionAmplitude}";
        if (!_shapeFrameCache.TryGetValue(key, out var frames))
        {
            frames = new ShapeFrameSet(_image, new Size(_baseWidth + 2 * CanvasPadding, _baseHeight + 2 * CanvasPadding), _baseWidth, _baseHeight, _motionAmplitude);
            _shapeFrameCache.Add(key, frames);
            _shapeCacheOrder.Enqueue(key);
            while (_shapeCacheOrder.Count > 2)
            {
                var oldest = _shapeCacheOrder.Dequeue();
                _shapeFrameCache.Remove(oldest, out var discarded);
                discarded?.Dispose();
            }
        }
        _activeShapeFrames = frames;
    }

    private void ResetImageBounds()
    {
        _imageBounds = new Rectangle(CanvasPadding, CanvasPadding, _baseWidth, _baseHeight);
        _imageBottomOffset = 0;
    }

    private sealed class ShapeFrameSet : IDisposable
    {
        private readonly SortedDictionary<int, Bitmap> _frames = new();
        public Size CanvasSize { get; }

        public ShapeFrameSet(Image source, Size canvasSize, int width, int height, int amplitude)
        {
            CanvasSize = canvasSize;
            var offsets = Enumerable.Range(0, 9).Select(i => (int)Math.Round(-amplitude + i * amplitude / 4.0))
                .Append(0).Append(amplitude).Distinct().OrderBy(value => value);
            try
            {
                foreach (var offset in offsets)
                {
                    var squeeze = (int)Math.Round(Math.Abs(offset) * 0.6);
                    var stretch = Math.Max(0, offset);
                    var imageBounds = new Rectangle(CanvasPadding + squeeze / 2, CanvasPadding - stretch, width - squeeze, height + stretch);
                    _frames.Add(offset, LayeredWindowPainter.PrepareFrame(canvasSize, source, imageBounds));
                }
            }
            catch { Dispose(); throw; }
        }

        public Bitmap GetNearest(int offset)
        {
            var nearest = _frames.Keys.MinBy(value => Math.Abs(value - offset));
            return _frames[nearest];
        }

        public void Dispose() { foreach (var frame in _frames.Values) frame.Dispose(); }
    }

}

internal sealed class CounterWindow : Form
{
    private readonly Label _number = new();
    private int _zoomPercent = 100;

    public CounterWindow()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta; TransparencyKey = Color.Magenta; TopMost = true;
        _number.AutoSize = true; _number.BackColor = Color.Transparent; _number.ForeColor = Color.FromArgb(35, 43, 56);
        Controls.Add(_number);
        SetNumber(0, 100);
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x08000000; if (_clickThrough) cp.ExStyle |= 0x00000020; return cp; }
    }

    private bool _clickThrough;
    public void SetClickThrough(bool enabled) { _clickThrough = enabled; RecreateHandle(); }
    public void SetNumber(long value, int zoom) { _number.Text = Math.Max(0, value).ToString(); SetNumberFromCurrentScale(zoom); }
    public void SetNumberFromCurrentScale(int zoom)
    {
        _zoomPercent = Math.Clamp(zoom, 40, 600);
        _number.Font = new Font("Segoe UI", 14F * _zoomPercent / 100, FontStyle.Bold, GraphicsUnit.Point);
        _number.Location = new Point(0, 0);
        ClientSize = new Size(_number.PreferredWidth + 2, _number.PreferredHeight + 2);
    }
}
