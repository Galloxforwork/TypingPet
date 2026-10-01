using System.Text.Json;
using System.Windows.Forms;

namespace TypingPet;

internal sealed class MainForm : Form
{
    private const int InitialIdleMs = 800;
    private const int InitialHoldMs = 650;
    private static readonly string[] RuntimeStageNames = ["输入中", "待机中", "回退按下", "回车按下", "回退抬起", "回车抬起", "暂停", "大小写切换"];
    private static readonly string[] ImageGroupNames = ["待机", "暂停", "输入中", "回车系", "回退系", "大小写切换"];
    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico"];
    private const string ImageClipboardFormat = "TypingPet.ImageSelection";
    private readonly string _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TypingPetPrototype");
    private readonly string _assetsDir;
    private readonly Dictionary<int, List<string>> _images = Enumerable.Range(0, 6).ToDictionary(i => i, _ => new List<string>());
    private readonly Dictionary<int, bool> _carouselModes = Enumerable.Range(0, 6).ToDictionary(i => i, _ => false);
    private readonly Dictionary<int, int> _imageIndices = Enumerable.Range(0, 6).ToDictionary(i => i, _ => 0);
    private readonly Dictionary<int, bool> _hasTriggered = Enumerable.Range(0, 6).ToDictionary(i => i, _ => false);
    private readonly Label _previewText = new();
    private readonly PictureBox _previewImage = new();
    private readonly Label _selectedLabel = new();
    private readonly Label _hintLabel = new();
    private readonly Label _runtimeStatusLabel = new();
    private readonly NumericUpDown _idleDelay = new() { Minimum = 100, Maximum = 5000, Width = 86 };
    private readonly NumericUpDown _holdDelay = new() { Minimum = 100, Maximum = 5000, Width = 86 };
    private readonly NumericUpDown _inputHold = new() { Minimum = 80, Maximum = 3000, Increment = 20, Value = 240, Width = 78 };
    private readonly NumericUpDown _pauseDuration = new() { Minimum = 100, Maximum = 30000, Increment = 100, Value = 1000, Width = 80 };
    private readonly NumericUpDown _backspaceHold = new() { Minimum = 100, Maximum = 5000, Increment = 100, Value = 650, Width = 70 };
    private readonly NumericUpDown _enterHold = new() { Minimum = 100, Maximum = 5000, Increment = 100, Value = 650, Width = 70 };
    private readonly NumericUpDown _counterFactor = new() { Minimum = 0.2M, Maximum = 10M, DecimalPlaces = 1, Increment = 0.1M, Value = 1M, Width = 60 };
    private readonly NumericUpDown _motionAmplitude = new() { Minimum = 1, Maximum = 30, Value = 8, Width = 52 };
    private readonly ComboBox _motionStyle = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 100 };
    private readonly CheckBox _stageCarousel = new() { Text = "触发时轮播", AutoSize = true, Margin = new Padding(3, 5, 4, 0) };
    private readonly NumericUpDown _profileThreshold = new() { Minimum = 0, Maximum = 10000000, Increment = 100, Width = 78 };
    private readonly CheckBox _advancedMode = new() { Text = "高级", AutoSize = true };
    private readonly ComboBox _stateMode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly CheckBox _thresholdSwitch = new() { Text = "按显示计数阈值自动切换设置组", AutoSize = true, Checked = true };
    private readonly Button[] _profileButtons = new Button[5];
    private readonly List<Button> _imageGroupButtons = new();
    private readonly ComboBox _profileImageGroup = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
    private readonly FlowLayoutPanel _imageItems = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AllowDrop = true, Padding = new Padding(6) };
    private readonly List<ImageGroup> _imageGroups = new();
    private int _editingImageGroup;
    private Button? _resetCounterButton;
    private Button? _applySettingsButton;
    private int _selectedImageIndex = -1;
    private ImageClipboardEntry? _imageClipboard;
    private readonly List<PetProfile> _appliedProfiles = new();
    private readonly List<ImageGroup> _appliedImageGroups = new();
    private readonly RuntimeSettings _appliedRuntime = new();
    private bool _appliedThresholdSwitch = true;
    private int _persistedEditingProfile;
    private bool _allowExit;
    private NotifyIcon? _trayIcon;
    private DateTime _inputPulseUntil = DateTime.MinValue;
    private Panel? _pageBody;
    private bool _imagePageMode;
    private readonly CheckBox _pinPet = new() { Text = "小伴侣置顶", AutoSize = true, Checked = true };
    private readonly CheckBox _clickThrough = new() { Text = "鼠标穿透", AutoSize = true };
    private readonly Button _togglePet = new() { Text = "隐藏小伴侣", AutoSize = true };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 80 };
    private RawKeyboardInput? _keyboard;
    private PetWindow? _petWindow;
    private DateTime _lastKeyAt = DateTime.MinValue;
    private DateTime _transientUntil = DateTime.MinValue;
    private int _currentStage = 1;
    private int _previewStage = 1;
    private bool _capsOn;
    private bool _shiftDown;
    private int _loadedIdleDelay = InitialIdleMs;
    private int _loadedHoldDelay = InitialHoldMs;
    private bool _loadedPetTopmost = true;
    private bool _loadedClickThrough;
    private int _loadedPetZoom = 200;
    private Point? _loadedPetLocation;
    private bool _loadedCounterEnabled = true;
    private string? _settingsLoadError;
    private long _inputCount;
    private bool _countDirty;
    private readonly List<PetProfile> _profiles = Enumerable.Range(0, 5).Select(i => new PetProfile { Name = $"设置组 {(char)('A' + i)}", Threshold = new long[] { 0, 100, 300, 500, 1000 }[i], ImageGroupId = i }).ToList();
    private int _editingProfile;
    private int _runtimeProfile;
    private bool _globalShapeMode;
    private bool _switchingProfile;
    private bool _switchingStage;

    public MainForm()
    {
        _assetsDir = Path.Combine(_dataDir, "assets");
        Directory.CreateDirectory(_assetsDir);
        LoadSettings();
        while (_imageGroups.Count < 5) _imageGroups.Add(new ImageGroup { Name = $"图片组 {(char)('A' + _imageGroups.Count)}" });
        BuildWindow();
        if (_settingsLoadError is not null)
            _hintLabel.Text = "读取已有设置失败，已保留原设置文件且暂停写入：" + _settingsLoadError;
        _editingProfile = _persistedEditingProfile;
        LoadEditingProfile(_editingProfile);
        UpdateProfileButtons();
        CopyDraftToRuntime();
        RenderPage();
        RenderSelectedPreview();
        if (_appliedThresholdSwitch) SelectProfileForCount(); else _runtimeProfile = _editingProfile;
        UpdateRuntimeStatus();
        _timer.Tick += (_, _) => UpdateState();
        _timer.Start();
    }

    private void BuildWindow()
    {
        Text = "打字小伴侣 · 设置";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(920, 700);
        MinimumSize = new Size(820, 620);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        TopMost = false;
        BackColor = Color.FromArgb(244, 247, 251);
        Font = new Font("Segoe UI", 9F);
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = BackColor, Padding = new Padding(18, 12, 18, 10) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        var heading = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        var title = new Label { Text = "Typing Pet", Font = new Font(Font.FontFamily, 18, FontStyle.Bold), ForeColor = Color.FromArgb(31, 41, 55), AutoSize = true, Location = new Point(0, 0) };
        _runtimeStatusLabel.AutoSize = true;
        _runtimeStatusLabel.ForeColor = Color.FromArgb(81, 94, 116);
        _runtimeStatusLabel.Location = new Point(155, 13);
        heading.Controls.AddRange([title, _runtimeStatusLabel]);
        root.Controls.Add(heading, 0, 0);

        var profileNav = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.Transparent, Padding = new Padding(0, 4, 0, 0) };
        var groupLabel = new Label { Text = "设置组", AutoSize = true, ForeColor = Color.FromArgb(86, 99, 119), Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 11, 12, 0) };
        profileNav.Controls.Add(groupLabel);
        for (var i = 0; i < 5; i++)
        {
            var index = i;
            var button = new Button { Text = $"{(char)('A' + i)}  设置组", Width = 118, Height = 36, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 8, 0), Tag = i };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => SwitchEditingProfile(index);
            _profileButtons[i] = button;
            profileNav.Controls.Add(button);
        }
        root.Controls.Add(profileNav, 0, 1);

        var pageNav = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, BackColor = Color.White, Padding = new Padding(6, 5, 0, 0), Margin = Padding.Empty };
        var parameterPage = new Button { Text = "参数与预览", Width = 120, Height = 34, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 6, 0) };
        var imagePage = new Button { Text = "图片组管理", Width = 120, Height = 34, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 6, 0) };
        parameterPage.Click += (_, _) => { _imagePageMode = false; RenderPage(); };
        imagePage.Click += (_, _) => { _imagePageMode = true; RenderPage(); };
        pageNav.Controls.AddRange([parameterPage, imagePage]);
        root.Controls.Add(pageNav, 0, 2);

        _pageBody = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(16), Margin = Padding.Empty };
        root.Controls.Add(_pageBody, 0, 3);
        _motionStyle.Items.Clear(); _motionStyle.Items.AddRange(["跳动式", "形变式"]);
        _motionStyle.SelectedIndex = _globalShapeMode ? 1 : 0;
        _stateMode.Items.AddRange(["简易", "高级"]); _stateMode.SelectedIndex = _advancedMode.Checked ? 1 : 0;
        _stateMode.SelectedIndexChanged += (_, _) => { _advancedMode.Checked = _stateMode.SelectedIndex == 1; MarkSettingsPending(); SaveSettings(); };
        _previewImage.Paint += PreviewPlaceholderPaint;
        _idleDelay.Minimum = 100; _idleDelay.Maximum = 5000; _idleDelay.Width = 86;
        _holdDelay.Minimum = 100; _holdDelay.Maximum = 5000; _holdDelay.Value = Math.Clamp(_loadedHoldDelay, 100, 5000); _holdDelay.Width = 86;
        _pinPet.Checked = _loadedPetTopmost; _clickThrough.Checked = _loadedClickThrough;
        _counterFactor.ValueChanged += (_, _) => { if (!_switchingProfile) { _petWindow?.SetCounter(RuntimeShownCount); MarkSettingsPending(); SaveSettings(); } };
        _motionStyle.SelectedIndexChanged += (_, _) => ConfigurePetMotion();
        _motionAmplitude.ValueChanged += (_, _) => ConfigurePetMotion();
        _profileThreshold.ValueChanged += (_, _) => { if (!_switchingProfile) { _profiles[_editingProfile].Threshold = (long)_profileThreshold.Value; UpdateProfileButtons(); SelectProfileForCount(); MarkSettingsPending(); SaveSettings(); } };
        _profileImageGroup.SelectedIndexChanged += (_, _) =>
        {
            if (_switchingProfile || _profileImageGroup.SelectedIndex < 0) return;
            _profiles[_editingProfile].ImageGroupId = _profileImageGroup.SelectedIndex;
            LoadImageGroup(_profileImageGroup.SelectedIndex); UpdateProfileButtons(); RenderPage(); UpdatePetWindow(); MarkSettingsPending(); SaveSettings();
        };
        _thresholdSwitch.CheckedChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _advancedMode.CheckedChanged += (_, _) => { if (!_switchingProfile) { SaveGlobalSettings(); MarkSettingsPending(); SaveSettings(); } };
        _stageCarousel.CheckedChanged += (_, _) =>
        {
            if (_switchingStage) return;
            _carouselModes[_previewStage] = _stageCarousel.Checked;
            if (!_stageCarousel.Checked) _imageIndices[_previewStage] = 0;
            if (ImageStateForRuntimeStage(_currentStage) == _previewStage) UpdatePetWindow();
            RenderSelectedPreview(); MarkSettingsPending(); SaveSettings();
        };
        _idleDelay.ValueChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _inputHold.ValueChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _holdDelay.ValueChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _pauseDuration.ValueChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _backspaceHold.ValueChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _enterHold.ValueChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _pinPet.CheckedChanged += (_, _) => { MarkSettingsPending(); SaveSettings(); };
        _clickThrough.CheckedChanged += (_, _) => { MarkSettingsPending(); SaveSettings(); };
        _togglePet.Click += (_, _) => TogglePetVisibility();
        _imageItems.DragEnter += ImageListDragEnter;
        _imageItems.DragDrop += ImageListDragDrop;
        _switchingProfile = true;
        _profileImageGroup.Items.Clear(); foreach (var group in _imageGroups) _profileImageGroup.Items.Add(group.Name);
        _profileImageGroup.SelectedIndex = _profiles[_editingProfile].ImageGroupId;
        _switchingProfile = false;
        _hintLabel.Text = "设置组按显示数字达到阈值时切换；图片组可分别关联到设置组。数字统计不会读取键入内容。";
        _hintLabel.Dock = DockStyle.Bottom;
        _hintLabel.Height = 25;
        _hintLabel.ForeColor = Color.FromArgb(103, 116, 137);
        Controls.Add(_hintLabel);
        _hintLabel.BringToFront();
        RenderPage();
        UpdateProfileButtons();
        Shown += (_, _) => { StartKeyboardInput(); EnsurePetVisible(); };
        InitializeTrayIcon();
        FormClosing += HandleFormClosing;
    }

    private void RenderPage()
    {
        if (_pageBody is null) return;
        _pageBody.SuspendLayout(); _pageBody.Controls.Clear();
        if (_imagePageMode) BuildImageManagementPage(); else BuildSettingsPreviewPage();
        _pageBody.ResumeLayout(true);
    }

    private void BuildSettingsPreviewPage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Color.White };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var states = BuildStateButtons(); layout.Controls.Add(states, 0, 0); layout.SetColumnSpan(states, 2);
        var preview = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(246, 248, 252), Padding = new Padding(12), Margin = new Padding(0, 0, 12, 0) };
        _selectedLabel.Dock = DockStyle.Top; _selectedLabel.Height = 32; _selectedLabel.TextAlign = ContentAlignment.MiddleLeft; _selectedLabel.Font = new Font(Font, FontStyle.Bold); _selectedLabel.ForeColor = Color.FromArgb(46, 61, 86);
        _previewImage.Dock = DockStyle.Fill; _previewImage.SizeMode = PictureBoxSizeMode.Zoom; _previewImage.BackColor = Color.Transparent;
        preview.Controls.Add(_previewImage); preview.Controls.Add(_selectedLabel);
        var previewColumn = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White };
        previewColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 78)); previewColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 22));
        previewColumn.Controls.Add(preview, 0, 0);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(8, 10, 4, 2), BackColor = Color.White };
        footer.Controls.Add(new Label { Text = "状态模式", AutoSize = true, Margin = new Padding(0, 8, 6, 0), ForeColor = Color.FromArgb(81, 94, 116) });
        footer.Controls.Add(_stateMode);
        _applySettingsButton = ActionButton("应用全部设置", ApplySettings);
        _applySettingsButton.Height = 38; _applySettingsButton.BackColor = Color.FromArgb(48, 93, 157); _applySettingsButton.ForeColor = Color.White;
        footer.Controls.Add(_applySettingsButton);
        previewColumn.Controls.Add(footer, 0, 1);
        layout.Controls.Add(previewColumn, 0, 1);

        var settings = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true, Padding = new Padding(4), BackColor = Color.White };
        AddSettingSection(settings, "设置组", new Control[] { Field("显示计数阈值", _profileThreshold), Field("使用图片组", _profileImageGroup), _thresholdSwitch });
        AddSettingSection(settings, "全局计数", new Control[] { Field("每键显示系数", _counterFactor), _resetCounterButton ??= ActionButton("重置计数", ResetCounter) });
        AddSettingSection(settings, "全局动效", new Control[] { Field("类型", _motionStyle), Field("幅度（像素）", _motionAmplitude) });
        AddSettingSection(settings, "全局状态停留", new Control[] { Field("输入最短显示（毫秒）", _inputHold), Field("闲置判定（毫秒）", _idleDelay), Field("回退保持（毫秒）", _backspaceHold), Field("回车保持（毫秒）", _enterHold), Field("暂停（毫秒）", _pauseDuration), Field("大小写保持（毫秒）", _holdDelay) });
        var presets = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        presets.Controls.AddRange([ActionButton("慢", () => ApplyTimingPreset("slow")), ActionButton("中等", () => ApplyTimingPreset("medium")), ActionButton("快", () => ApplyTimingPreset("fast")), ActionButton("重置", () => ApplyTimingPreset("medium"))]);
        AddSettingSection(settings, "状态停留预设", new Control[] { presets });
        AddSettingSection(settings, "图片播放", new Control[] { _stageCarousel });
        var display = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(4, 12, 4, 4) };
        display.Controls.AddRange([_pinPet, _clickThrough, _togglePet]);
        AddSettingSection(settings, "桌宠窗口", new Control[] { display });
        layout.Controls.Add(settings, 1, 1);
        _pageBody!.Controls.Add(layout);
        RenderSelectedPreview();
    }

    private void BuildImageManagementPage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = Color.White };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        var groupNav = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 3, 0, 0) };
        _imageGroupButtons.Clear();
        for (var i = 0; i < _imageGroups.Count; i++) { var index = i; var button = ActionButton(_imageGroups[i].Name, () => SelectImageGroup(index)); button.Width = 100; button.BackColor = i == _editingImageGroup ? Color.FromArgb(222, 233, 250) : Color.FromArgb(242, 246, 252); _imageGroupButtons.Add(button); groupNav.Controls.Add(button); }
        groupNav.Controls.Add(ActionButton("＋图片组", AddImageGroup));
        groupNav.Controls.Add(ActionButton("删除图片组", RemoveImageGroup));
        groupNav.Controls.Add(ActionButton($"应用到设置组 {(char)('A' + _editingProfile)}", ApplyImageGroupToProfile));
        layout.Controls.Add(groupNav, 0, 0);
        var stateNav = BuildStateButtons(); layout.Controls.Add(stateNav, 0, 1);
        var description = new Label { Text = $"{ImageGroupNames[_previewStage]} · 顺序轮播 · 拖放图片添加 · 下方显示缩略图", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(96, 108, 128), TextAlign = ContentAlignment.MiddleLeft };
        layout.Controls.Add(description, 0, 2);
        BuildImageTiles();
        layout.Controls.Add(_imageItems, 0, 3);
        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        var clipboardActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        clipboardActions.Controls.AddRange([ActionButton("剪切 Ctrl+X", () => CopySelectedImage(true)), ActionButton("复制 Ctrl+C", () => CopySelectedImage(false)), ActionButton("粘贴 Ctrl+V", PasteImages)]);
        var editActions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        editActions.Controls.AddRange([ActionButton("添加图片", () => PickImages(_previewStage)), ActionButton("裁切 3:4", CropSelectedImage), ActionButton("上移", () => MoveImage(-1)), ActionButton("下移", () => MoveImage(1)), ActionButton("删除选中", () => RemoveSelectedImage(_previewStage)), ActionButton("清空此组", () => ClearImages(_previewStage))]);
        actions.Controls.Add(clipboardActions, 0, 0); actions.Controls.Add(editActions, 0, 1);
        layout.Controls.Add(actions, 0, 4);
        _pageBody!.Controls.Add(layout);
    }

    private FlowLayoutPanel BuildStateButtons()
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(0, 3, 0, 0) };
        var count = ImageGroupNames.Length;
        for (var i = 0; i < count; i++)
        {
            var stage = i;
            var button = new Button { Text = ImageGroupNames[i], AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 6, 0), BackColor = i == _previewStage ? Color.FromArgb(222, 233, 250) : Color.FromArgb(246, 248, 252) };
            button.FlatAppearance.BorderSize = 0;
            button.Click += (_, _) => SelectPreviewStage(stage);
            row.Controls.Add(button);
        }
        return row;
    }

    private void AddSettingSection(FlowLayoutPanel parent, string title, IEnumerable<Control> controls)
    {
        var section = new FlowLayoutPanel { Width = 340, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.FromArgb(247, 249, 252), Padding = new Padding(12), Margin = new Padding(4, 4, 8, 8) };
        section.Controls.Add(new Label { Text = title, AutoSize = true, Font = new Font(Font, FontStyle.Bold), ForeColor = Color.FromArgb(49, 65, 91), Margin = new Padding(0, 0, 0, 8) });
        foreach (var control in controls) { control.Margin = new Padding(0, 3, 0, 3); section.Controls.Add(control); }
        parent.Controls.Add(section);
    }

    private Control Field(string label, Control control)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, Margin = Padding.Empty };
        row.Controls.Add(new Label { Text = label, AutoSize = true, Width = 185, Height = 28, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(81, 94, 116) });
        row.Controls.Add(control);
        return row;
    }

    private Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 34, FlatStyle = FlatStyle.Flat, Margin = new Padding(0, 0, 8, 0), BackColor = Color.FromArgb(242, 246, 252) };
        button.FlatAppearance.BorderColor = Color.FromArgb(222, 229, 239); button.Click += (_, _) => action(); return button;
    }

    private void PreviewPlaceholderPaint(object? sender, PaintEventArgs e)
    {
        if (_previewImage.Image is not null) return;
        TextRenderer.DrawText(e.Graphics, _previewText.Text, new Font(Font.FontFamily, 25, FontStyle.Bold), _previewImage.ClientRectangle, Color.FromArgb(92, 111, 145), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void ImageListDragEnter(object? sender, DragEventArgs e) { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; }
    private void ImageListDragDrop(object? sender, DragEventArgs e) { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files) AddImages(_previewStage, files); }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_imagePageMode)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.X: CopySelectedImage(true); return true;
                case Keys.Control | Keys.C: CopySelectedImage(false); return true;
                case Keys.Control | Keys.V: PasteImages(); return true;
            }
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void CopySelectedImage(bool cut)
    {
        var paths = _images[_previewStage];
        if (_selectedImageIndex < 0 || _selectedImageIndex >= paths.Count)
        {
            _hintLabel.Text = "请先选择一张图片。";
            return;
        }
        var path = paths[_selectedImageIndex];
        var entry = new ImageClipboardEntry(Guid.NewGuid().ToString("N"), _imageGroups[_editingImageGroup].Id, _previewStage, _selectedImageIndex, path, cut);
        try
        {
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { path });
            data.SetData(ImageClipboardFormat, entry.Token);
            Clipboard.SetDataObject(data, true);
            _imageClipboard = entry;
            _hintLabel.Text = cut ? "已剪切选中图片；切换图片组或状态后粘贴即可移动。" : "已复制选中图片；切换图片组或状态后可粘贴。";
            BuildImageTiles();
        }
        catch (Exception ex)
        {
            _hintLabel.Text = "剪贴板暂时不可用：" + ex.Message;
        }
    }

    private void PasteImages()
    {
        try
        {
            var data = Clipboard.GetDataObject();
            if (data is null) { _hintLabel.Text = "剪贴板中没有图片。"; return; }
            var token = data.GetDataPresent(ImageClipboardFormat) ? data.GetData(ImageClipboardFormat) as string : null;
            if (_imageClipboard is not null && token == _imageClipboard.Token)
            {
                PasteInternalImage(_imageClipboard);
                return;
            }
            if (data.GetData(DataFormats.FileDrop) is string[] files)
            {
                AddImages(_previewStage, files);
                return;
            }
            if (data.GetDataPresent(DataFormats.Bitmap))
            {
                PasteBitmap(data);
                return;
            }
            _hintLabel.Text = "剪贴板中没有可导入的图片文件或位图。";
        }
        catch (Exception ex)
        {
            _hintLabel.Text = "粘贴失败：" + ex.Message;
        }
    }

    private void PasteInternalImage(ImageClipboardEntry entry)
    {
        if (!File.Exists(entry.Path)) { _hintLabel.Text = "原图片文件已不存在，无法粘贴。"; return; }
        var target = _images[_previewStage];
        var sourceGroupIndex = _imageGroups.FindIndex(group => group.Id == entry.GroupId);
        List<string>? source = null;
        var sourceIndex = -1;
        if (sourceGroupIndex >= 0)
        {
            source = sourceGroupIndex == _editingImageGroup
                ? _images[entry.Stage]
                : _imageGroups[sourceGroupIndex].Images.GetValueOrDefault(entry.Stage);
            if (source is not null)
                sourceIndex = entry.Index < source.Count && source[entry.Index] == entry.Path ? entry.Index : source.IndexOf(entry.Path);
        }
        if (entry.Cut && sourceIndex < 0) { _hintLabel.Text = "剪切的原图片已被移除；请重新选择并剪切。"; return; }
        var sameList = ReferenceEquals(source, target);
        var maximum = _advancedMode.Checked ? 20 : 3;
        if (target.Count >= maximum && !(entry.Cut && sameList))
        {
            _hintLabel.Text = $"该状态最多保存 {maximum} 张图片；请先移除或清空图片。";
            return;
        }
        if (entry.Cut && source is not null)
        {
            source.RemoveAt(sourceIndex);
            if (sourceGroupIndex == _editingImageGroup)
            {
                _imageIndices[entry.Stage] = 0; _hasTriggered[entry.Stage] = false;
            }
            else
            {
                var group = _imageGroups[sourceGroupIndex];
                group.ImageIndices[entry.Stage] = 0; group.HasTriggered[entry.Stage] = false;
            }
        }
        target.Add(entry.Path);
        _selectedImageIndex = target.Count - 1;
        _imageIndices[_previewStage] = 0; _hasTriggered[_previewStage] = false;
        _imageClipboard = entry with { Cut = false, GroupId = _imageGroups[_editingImageGroup].Id, Stage = _previewStage, Index = _selectedImageIndex };
        MarkSettingsPending(); SaveSettings(); RenderPage();
        _hintLabel.Text = entry.Cut ? "图片已移动到当前状态末尾；点击应用设置后生效。" : "图片已复制到当前状态末尾；点击应用设置后生效。";
    }

    private void PasteBitmap(IDataObject data)
    {
        var maximum = _advancedMode.Checked ? 20 : 3;
        if (_images[_previewStage].Count >= maximum)
        {
            _hintLabel.Text = $"该状态最多保存 {maximum} 张图片；请先移除或清空图片。";
            return;
        }
        if (data.GetData(DataFormats.Bitmap) is not Image image)
        {
            _hintLabel.Text = "无法读取剪贴板中的图片。";
            return;
        }
        var destination = Path.Combine(_assetsDir, Guid.NewGuid().ToString("N") + ".png");
        using (image) image.Save(destination, System.Drawing.Imaging.ImageFormat.Png);
        _images[_previewStage].Add(destination);
        _selectedImageIndex = _images[_previewStage].Count - 1;
        _imageIndices[_previewStage] = 0; _hasTriggered[_previewStage] = false;
        MarkSettingsPending(); SaveSettings(); RenderPage();
        _hintLabel.Text = "已粘贴剪贴板图片；点击应用设置后生效。";
    }

    private void MoveImage(int offset)
    {
        var list = _images[_previewStage]; var index = _selectedImageIndex; var target = index + offset;
        if (index < 0 || target < 0 || target >= list.Count) return;
        (list[index], list[target]) = (list[target], list[index]); _selectedImageIndex = target;
        MarkSettingsPending(); SaveSettings(); RenderPage();
    }

    private void RemoveSelectedImage(int stage)
    {
        var index = _selectedImageIndex;
        if (index < 0 || index >= _images[stage].Count) return;
        _images[stage].RemoveAt(index); _selectedImageIndex = _images[stage].Count == 0 ? -1 : Math.Min(index, _images[stage].Count - 1); _imageIndices[stage] = 0; _hasTriggered[stage] = false;
        MarkSettingsPending(); SaveSettings(); RenderPage(); if (ImageStateForRuntimeStage(_currentStage) == stage) UpdatePetWindow();
    }

    private void CropSelectedImage()
    {
        var stage = _previewStage;
        var index = _selectedImageIndex;
        if (index < 0 || index >= _images[stage].Count)
        {
            _hintLabel.Text = "请先在图片组中选择要裁切的图片。";
            return;
        }
        try
        {
            var path = _images[stage][index];
            using var dialog = new CropImageForm(path);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            var crop = dialog.SelectedCrop;
            using var source = Image.FromFile(path);
            using var result = new Bitmap(crop.Width, crop.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(result))
            {
                graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                graphics.DrawImage(source, new Rectangle(0, 0, crop.Width, crop.Height), crop, GraphicsUnit.Pixel);
            }
            var destination = Path.Combine(_assetsDir, Guid.NewGuid().ToString("N") + ".png");
            result.Save(destination, System.Drawing.Imaging.ImageFormat.Png);
            _images[stage][index] = destination;
            _imageIndices[stage] = 0;
            _hasTriggered[stage] = false;
            MarkSettingsPending(); SaveSettings(); RenderPage();
            _hintLabel.Text = $"已裁切为竖版 3:4（{crop.Width}×{crop.Height}），原图保留；点击应用全部设置后桌宠使用新图。";
        }
        catch (Exception ex)
        {
            _hintLabel.Text = "裁切失败：" + ex.Message;
        }
    }

    private void SelectPreviewStage(int stage)
    {
        if (stage < 0 || stage >= ImageGroupNames.Length) return;
        _switchingStage = true;
        _previewStage = stage;
        _selectedImageIndex = _images[stage].Count == 0 ? -1 : Math.Clamp(_selectedImageIndex, 0, _images[stage].Count - 1);
        _stageCarousel.Checked = _carouselModes[stage];
        _switchingStage = false;
        RenderPage();
    }

    private void PickImages(int stage)
    {
        using var dialog = new OpenFileDialog
        {
            Title = $"选择{ImageGroupNames[stage]}图片（可多选）",
            Filter = "支持的图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico|所有文件|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) AddImages(stage, dialog.FileNames);
    }

    private void AddImages(int stage, IEnumerable<string> files)
    {
        var candidates = files.ToList();
        var unsupported = candidates.Where(f => !AllowedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
        var valid = new List<string>();
        foreach (var file in candidates.Except(unsupported))
        {
            try
            {
                using var decoded = Image.FromFile(file);
                valid.Add(file);
            }
            catch { unsupported.Add(file); }
        }
        if (valid.Count == 0)
        {
            _hintLabel.Text = "没有导入图片。支持 PNG、JPG/JPEG、BMP、GIF、TIF/TIFF、ICO；WEBP、SVG、APNG 不支持。";
            return;
        }

        var maximum = _advancedMode.Checked ? 20 : 3;
        var room = Math.Max(0, maximum - _images[stage].Count);
        valid = valid.Take(room).ToList();
        if (valid.Count == 0)
        {
            _hintLabel.Text = $"该状态最多保存 {maximum} 张图片；请先移除或清空图片。";
            return;
        }
        foreach (var file in valid)
        {
            try
            {
                var destination = Path.Combine(_assetsDir, Guid.NewGuid().ToString("N") + Path.GetExtension(file).ToLowerInvariant());
                File.Copy(file, destination, false);
                _images[stage].Add(destination);
            }
            catch (Exception ex) { _hintLabel.Text = "图片复制失败：" + ex.Message; }
        }
        _imageIndices[stage] = 0;
        _hasTriggered[stage] = false;
        _selectedImageIndex = _images[stage].Count - 1;
        _previewStage = stage;
        RenderPage();
        MarkSettingsPending(); SaveSettings();
        if (ImageStateForRuntimeStage(_currentStage) == stage) UpdatePetWindow();
        _hintLabel.Text = unsupported.Count > 0
            ? $"已导入图片并暂存；跳过 {unsupported.Count} 个不支持或损坏的文件。应用设置后桌宠使用新图片。"
            : $"已导入 {_images[stage].Count} 张到“{ImageGroupNames[stage]}”；点击应用设置后桌宠使用新图片。";
    }

    private void ClearImages(int stage)
    {
        _images[stage].Clear(); _imageIndices[stage] = 0; _hasTriggered[stage] = false; _selectedImageIndex = -1;
        RenderPage(); MarkSettingsPending(); SaveSettings();
        if (ImageStateForRuntimeStage(_currentStage) == stage) UpdatePetWindow();
    }

    private static void SetPicture(PictureBox box, string? path)
    {
        var old = box.Image; box.Image = null; old?.Dispose();
        if (path is not null && File.Exists(path))
        {
            try { using var source = Image.FromFile(path); box.Image = new Bitmap(source); }
            catch { }
        }
        box.Invalidate();
    }

    private void RenderSelectedPreview()
    {
        _selectedLabel.Text = $"图片组：{_imageGroups.ElementAtOrDefault(_editingImageGroup)?.Name ?? "图片组"} · {ImageGroupNames[_previewStage]} · {_images[_previewStage].Count} 张";
        _previewText.Text = ImageGroupNames[_previewStage];
        var list = _images[_previewStage];
        SetPicture(_previewImage, list.Count == 0 ? null : list[_carouselModes[_previewStage] ? _imageIndices[_previewStage] % list.Count : 0]);
    }

    private void StartKeyboardInput()
    {
        try
        {
            _keyboard = new RawKeyboardInput();
            _keyboard.ActionReceived += HandleKeyboardAction;
            _hintLabel.Text = "监听可打印键与回退键；只累计按键次数，不读取键入内容。";
        }
        catch (Exception ex) { _hintLabel.Text = ex.Message; }
    }

    private void EnsurePetVisible()
    {
        if (_petWindow is null || _petWindow.IsDisposed)
        {
            _petWindow = new PetWindow(_loadedPetZoom, _loadedPetLocation, _loadedCounterEnabled);
            _petWindow.AppearanceChanged += () =>
            {
                _loadedPetZoom = _petWindow.ZoomPercent;
                _loadedPetLocation = _petWindow.RestingLocation;
                _loadedCounterEnabled = _petWindow.CounterEnabled;
                SaveSettings();
            };
            _petWindow.SetPinned(_pinPet.Checked);
            _petWindow.SetClickThrough(_clickThrough.Checked);
            _petWindow.ConfigureMotion(_appliedRuntime.ShapeMode, _appliedRuntime.Amplitude);
            _petWindow.ExitRequested += ExitApplication;
            _petWindow.FormClosed += (_, _) => _togglePet.Text = "显示小伴侣";
            _petWindow.CompanionHidden += () => _togglePet.Text = "显示小伴侣";
            _petWindow.OpenSettingsRequested += () => { Show(); WindowState = FormWindowState.Normal; Activate(); };
        }
        _petWindow.ShowCompanion();
        _togglePet.Text = "隐藏小伴侣";
        UpdatePetWindow();
    }

    private void TogglePetVisibility()
    {
        if (_petWindow?.Visible == true)
        {
            _petWindow.HideCompanion();
            _togglePet.Text = "显示小伴侣";
        }
        else EnsurePetVisible();
    }

    private void InitializeTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开设置", null, (_, _) => ShowSettings());
        menu.Items.Add("完全退出", null, (_, _) => ExitApplication());
        _trayIcon = new NotifyIcon { Text = "打字小伴侣", Icon = SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();
    }

    private void ShowSettings()
    {
        Show(); WindowState = FormWindowState.Normal; Activate();
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_allowExit && e.CloseReason != CloseReason.WindowsShutDown)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        _allowExit = true;
        _timer.Stop(); _keyboard?.Dispose(); _keyboard = null;
        SaveSettings();
        _petWindow?.Close(); _petWindow = null;
        if (_trayIcon is not null) { _trayIcon.Visible = false; _trayIcon.Dispose(); _trayIcon = null; }
    }

    private void ExitApplication()
    {
        if (_allowExit) return;
        _allowExit = true;
        Close();
    }

    private void HandleKeyboardAction(KeyboardAction action, bool capsOn)
    {
        if (InvokeRequired) { BeginInvoke(() => HandleKeyboardAction(action, capsOn)); return; }
        _capsOn = capsOn;
        switch (action)
        {
            case KeyboardAction.ShiftDown: _shiftDown = true; UpdatePetWindow(); _petWindow?.Bump(); return;
            case KeyboardAction.ShiftUp: _shiftDown = false; UpdatePetWindow(); return;
            case KeyboardAction.EnterDown:
                _lastKeyAt = DateTime.UtcNow; _transientUntil = _lastKeyAt.AddMilliseconds(_appliedRuntime.EnterHold); ActivateStage(3); _petWindow?.Bump(); return;
            case KeyboardAction.EnterUp: _transientUntil = DateTime.UtcNow.AddMilliseconds(_appliedRuntime.EnterHold); ActivateStage(5); return;
            case KeyboardAction.BackspaceDown:
                _inputCount++; _countDirty = true; SelectProfileForCount(); _petWindow?.SetCounter(RuntimeShownCount);
                _lastKeyAt = DateTime.UtcNow; _transientUntil = _lastKeyAt.AddMilliseconds(_appliedRuntime.BackspaceHold); ActivateStage(2); _petWindow?.Bump(); return;
            case KeyboardAction.BackspaceUp: _transientUntil = DateTime.UtcNow.AddMilliseconds(_appliedRuntime.BackspaceHold); ActivateStage(4); return;
            case KeyboardAction.CapsChanged:
                _transientUntil = DateTime.UtcNow.AddMilliseconds(_appliedRuntime.HoldDelay);
                _lastKeyAt = DateTime.UtcNow;
                if (_appliedRuntime.Advanced) ActivateStage(7); else UpdatePetWindow();
                _petWindow?.Bump(); return;
            case KeyboardAction.Typing:
                _inputCount++; _countDirty = true;
                SelectProfileForCount();
                _petWindow?.SetCounter(RuntimeShownCount);
                _lastKeyAt = DateTime.UtcNow; _inputPulseUntil = _lastKeyAt.AddMilliseconds(_appliedRuntime.InputHold); _transientUntil = DateTime.MinValue; ActivateStage(0); _petWindow?.Bump(); return;
        }
    }

    private void ActivateStage(int stage)
    {
        var imageState = ImageStateForRuntimeStage(stage);
        var group = _appliedImageGroups[Math.Clamp(_appliedProfiles[_runtimeProfile].ImageGroupId, 0, _appliedImageGroups.Count - 1)];
        var images = group.Images.TryGetValue(imageState, out var list) ? list : [];
        if (!group.ImageIndices.ContainsKey(imageState)) group.ImageIndices[imageState] = 0;
        if (!group.HasTriggered.ContainsKey(imageState)) group.HasTriggered[imageState] = false;
        if (group.HasTriggered[imageState] && group.CarouselModes.TryGetValue(imageState, out var carousel) && carousel && images.Count > 1)
            group.ImageIndices[imageState] = (group.ImageIndices[imageState] + 1) % images.Count;
        else if (!group.HasTriggered[imageState]) group.ImageIndices[imageState] = 0;
        group.HasTriggered[imageState] = true;
        _currentStage = stage;
        UpdatePetWindow();
    }

    private void UpdateState()
    {
        var now = DateTime.UtcNow;
        if (_countDirty) { _countDirty = false; SaveSettings(); }
        if (_currentStage == 0 && now < _inputPulseUntil) return;
        if (_currentStage != 0 && _transientUntil > now) return;
        if (_lastKeyAt != DateTime.MinValue && (now - _lastKeyAt).TotalMilliseconds < _appliedRuntime.IdleDelay)
        {
            if (_currentStage != 0) ActivateStageWithoutCycling(0);
            return;
        }
        if (_appliedRuntime.Advanced && _currentStage == 0 && _lastKeyAt != DateTime.MinValue)
        {
            _transientUntil = now.AddMilliseconds(_appliedRuntime.PauseDelay);
            ActivateStageWithoutCycling(6);
            return;
        }
        if (_appliedRuntime.Advanced && _currentStage == 6)
        {
            if (_transientUntil > now) return;
            ActivateStage(1);
            return;
        }
        if (_transientUntil > now) return;
        if (_currentStage != 1) ActivateStage(1);
    }

    private void ActivateStageWithoutCycling(int stage)
    {
        _currentStage = stage;
        UpdatePetWindow();
    }

    private void UpdatePetWindow()
    {
        UpdateRuntimeStatus();
        if (_petWindow is null || _petWindow.IsDisposed) return;
        var imageState = ImageStateForRuntimeStage(_currentStage);
        if (_appliedImageGroups.Count == 0 || _appliedProfiles.Count == 0) return;
        var groupId = Math.Clamp(_appliedProfiles[_runtimeProfile].ImageGroupId, 0, _appliedImageGroups.Count - 1);
        var group = _appliedImageGroups[groupId];
        var list = group.Images.TryGetValue(imageState, out var images) ? images : [];
        var carousel = group.CarouselModes.TryGetValue(imageState, out var enabled) && enabled;
        var index = group.ImageIndices.TryGetValue(imageState, out var selected) ? selected : 0;
        string? path = list.Count == 0 ? null : list[carousel ? index % list.Count : 0];
        _petWindow.UpdateState(_currentStage, RuntimeStageNames[_currentStage], path, _capsOn, _shiftDown);
        _petWindow.SetCounter(RuntimeShownCount);
    }

    private void ConfigurePetMotion()
    {
        if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); }
    }

    private void MarkSettingsPending() => _hintLabel.Text = "设置已修改，点击“应用全部设置”后生效。";

    private long RuntimeShownCount => (long)Math.Round(_inputCount * (double)_appliedRuntime.CounterFactor);

    private void UpdateRuntimeStatus()
    {
        if (_appliedProfiles.Count == 0 || _appliedImageGroups.Count == 0) return;
        var profile = Math.Clamp(_runtimeProfile, 0, _appliedProfiles.Count - 1);
        var imageGroup = Math.Clamp(_appliedProfiles[profile].ImageGroupId, 0, _appliedImageGroups.Count - 1);
        _runtimeStatusLabel.Text = $"桌宠当前：设置组 {(char)('A' + profile)} · {_appliedImageGroups[imageGroup].Name} · 显示计数 {RuntimeShownCount}";
    }

    private void SelectProfileForCount()
    {
        if (!_appliedThresholdSwitch || _appliedProfiles.Count == 0) return;
        var shownCount = RuntimeShownCount;
        var target = Enumerable.Range(0, _appliedProfiles.Count).Where(i => _appliedProfiles[i].Threshold <= shownCount).OrderByDescending(i => _appliedProfiles[i].Threshold).FirstOrDefault();
        if (target == _runtimeProfile) return;
        _runtimeProfile = target;
        ApplyRuntimeMotion();
        UpdatePetWindow();
    }

    private void ApplyRuntimeMotion()
    {
        _petWindow?.ConfigureMotion(_appliedRuntime.ShapeMode, _appliedRuntime.Amplitude);
    }

    private void CopyDraftToRuntime()
    {
        StoreEditingProfile(); SaveEditingImageGroup(); SaveGlobalSettings();
        _appliedProfiles.Clear();
        _appliedProfiles.AddRange(_profiles.Select(p => new PetProfile { Threshold = p.Threshold, ImageGroupId = p.ImageGroupId }));
        _appliedImageGroups.Clear();
        _appliedImageGroups.AddRange(_imageGroups.Select(CloneImageGroup));
        _appliedRuntime.CounterFactor = _counterFactor.Value;
        _appliedRuntime.ShapeMode = _motionStyle.SelectedIndex == 1;
        _appliedRuntime.Amplitude = (int)_motionAmplitude.Value;
        _appliedRuntime.Advanced = _advancedMode.Checked;
        _appliedRuntime.IdleDelay = (int)_idleDelay.Value;
        _appliedRuntime.InputHold = (int)_inputHold.Value;
        _appliedRuntime.PauseDelay = (int)_pauseDuration.Value;
        _appliedRuntime.BackspaceHold = (int)_backspaceHold.Value;
        _appliedRuntime.EnterHold = (int)_enterHold.Value;
        _appliedRuntime.HoldDelay = (int)_holdDelay.Value;
        _appliedThresholdSwitch = _thresholdSwitch.Checked;
    }

    private static ImageGroup CloneImageGroup(ImageGroup source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Images = source.Images.ToDictionary(pair => pair.Key, pair => pair.Value.ToList()),
        CarouselModes = source.CarouselModes.ToDictionary(pair => pair.Key, pair => pair.Value),
        ImageIndices = source.ImageIndices.ToDictionary(pair => pair.Key, pair => pair.Value),
        HasTriggered = source.HasTriggered.ToDictionary(pair => pair.Key, pair => pair.Value)
    };

    private void ApplySettings()
    {
        CopyDraftToRuntime();
        if (_appliedThresholdSwitch) SelectProfileForCount();
        else _runtimeProfile = Math.Clamp(_editingProfile, 0, _appliedProfiles.Count - 1);
        ApplyRuntimeMotion(); _petWindow?.SetPinned(_pinPet.Checked); _petWindow?.SetClickThrough(_clickThrough.Checked); UpdatePetWindow(); SaveSettings();
        _hintLabel.Text = "全部设置已应用到桌宠。";
    }

    private void ApplyTimingPreset(string preset)
    {
        var values = preset switch
        {
            "slow" => (Input: 500, Idle: 1500, Pause: 1600, Back: 900, Enter: 900, Other: 750),
            "fast" => (Input: 120, Idle: 350, Pause: 450, Back: 240, Enter: 240, Other: 220),
            _ => (Input: 260, Idle: 800, Pause: 1000, Back: 550, Enter: 550, Other: 450)
        };
        _inputHold.Value = values.Input; _idleDelay.Value = values.Idle; _pauseDuration.Value = values.Pause;
        _backspaceHold.Value = values.Back; _enterHold.Value = values.Enter; _holdDelay.Value = values.Other;
        _hintLabel.Text = $"已载入{(preset == "slow" ? "慢" : preset == "fast" ? "快" : "中等")}速预设，点击“应用全部设置”后生效。";
    }

    private static int ImageStateForRuntimeStage(int stage) => stage switch
    {
        1 => 0, 6 => 1, 0 => 2, 3 or 5 => 3, 2 or 4 => 4, 7 => 5, _ => 0
    };

    private void SaveEditingImageGroup()
    {
        if (_imageGroups.Count == 0 || _editingImageGroup < 0 || _editingImageGroup >= _imageGroups.Count) return;
        var group = _imageGroups[_editingImageGroup];
        group.Images = _images.ToDictionary(pair => pair.Key, pair => pair.Value.ToList());
        group.CarouselModes = _carouselModes.ToDictionary(pair => pair.Key, pair => pair.Value);
        group.ImageIndices = _imageIndices.ToDictionary(pair => pair.Key, pair => pair.Value);
        group.HasTriggered = _hasTriggered.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private void LoadImageGroup(int index, bool saveCurrent = true)
    {
        if (_imageGroups.Count == 0) return;
        index = Math.Clamp(index, 0, _imageGroups.Count - 1);
        if (saveCurrent) SaveEditingImageGroup();
        _editingImageGroup = index;
        var group = _imageGroups[index];
        for (var i = 0; i < ImageGroupNames.Length; i++)
        {
            _images[i] = group.Images.TryGetValue(i, out var paths) ? paths.Where(File.Exists).ToList() : [];
            _carouselModes[i] = group.CarouselModes.TryGetValue(i, out var carousel) && carousel;
            _imageIndices[i] = group.ImageIndices.TryGetValue(i, out var indexValue) ? indexValue : 0;
            _hasTriggered[i] = group.HasTriggered.TryGetValue(i, out var triggered) && triggered;
        }
        _switchingStage = true; _stageCarousel.Checked = _carouselModes[_previewStage]; _switchingStage = false;
    }

    private void SelectImageGroup(int index, bool save = true)
    {
        LoadImageGroup(index, save);
        RenderPage(); UpdatePetWindow();
        if (save) SaveSettings();
    }

    private void ApplyImageGroupToProfile()
    {
        _profiles[_editingProfile].ImageGroupId = _editingImageGroup;
        _switchingProfile = true;
        _profileImageGroup.SelectedIndex = _editingImageGroup;
        _switchingProfile = false;
        UpdateProfileButtons(); UpdatePetWindow(); MarkSettingsPending(); SaveSettings();
    }

    private void AddImageGroup()
    {
        if (_imageGroups.Count >= 10) { _hintLabel.Text = "图片组最多可创建 10 组。"; return; }
        SaveEditingImageGroup();
        _imageGroups.Add(new ImageGroup { Name = $"图片组 {(char)('A' + _imageGroups.Count)}" });
        _profileImageGroup.Items.Add(_imageGroups[^1].Name);
        SelectImageGroup(_imageGroups.Count - 1);
        MarkSettingsPending();
    }

    private void RemoveImageGroup()
    {
        if (_imageGroups.Count <= 1) { _hintLabel.Text = "至少保留一个图片组。"; return; }
        var removed = _editingImageGroup;
        _imageGroups.RemoveAt(removed);
        foreach (var profile in _profiles)
        {
            if (profile.ImageGroupId == removed) profile.ImageGroupId = Math.Max(0, removed - 1);
            else if (profile.ImageGroupId > removed) profile.ImageGroupId--;
        }
        _switchingProfile = true;
        _profileImageGroup.Items.Clear(); foreach (var group in _imageGroups) _profileImageGroup.Items.Add(group.Name);
        _profileImageGroup.SelectedIndex = _profiles[_editingProfile].ImageGroupId;
        _switchingProfile = false;
        SelectImageGroup(Math.Min(removed, _imageGroups.Count - 1), false);
        MarkSettingsPending();
        SaveSettings();
    }

    private void ResetCounter()
    {
        _inputCount = 0; _countDirty = false; _lastKeyAt = DateTime.MinValue; _transientUntil = DateTime.MinValue;
        _inputPulseUntil = DateTime.MinValue;
        _currentStage = 1;
        foreach (var group in _imageGroups)
            for (var key = 0; key < ImageGroupNames.Length; key++) { group.HasTriggered[key] = false; group.ImageIndices[key] = 0; }
        foreach (var group in _appliedImageGroups)
            for (var key = 0; key < ImageGroupNames.Length; key++) { group.HasTriggered[key] = false; group.ImageIndices[key] = 0; }
        foreach (var key in _hasTriggered.Keys.ToList()) { _hasTriggered[key] = false; _imageIndices[key] = 0; }
        SelectProfileForCount(); UpdatePetWindow(); SaveSettings();
        _hintLabel.Text = "累计输入计数已重置。";
    }

    private void SaveGlobalSettings() => _globalShapeMode = _motionStyle.SelectedIndex == 1;

    private void BuildImageTiles()
    {
        _imageItems.SuspendLayout();
        foreach (Control control in _imageItems.Controls.Cast<Control>().ToArray()) { _imageItems.Controls.Remove(control); control.Dispose(); }
        var paths = _images[_previewStage];
        if (paths.Count == 0) _selectedImageIndex = -1;
        else if (_selectedImageIndex < 0 || _selectedImageIndex >= paths.Count) _selectedImageIndex = 0;
        for (var index = 0; index < paths.Count; index++)
        {
            var itemIndex = index;
            var cutPending = _imageClipboard is { Cut: true } entry && entry.GroupId == _imageGroups[_editingImageGroup].Id && entry.Stage == _previewStage && entry.Index == index && entry.Path == paths[index];
            var tile = new Panel { Width = 158, Height = 146, Margin = new Padding(6), Padding = new Padding(5), BackColor = cutPending ? Color.FromArgb(233, 233, 233) : index == _selectedImageIndex ? Color.FromArgb(222, 233, 250) : Color.FromArgb(248, 249, 252), BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand };
            var picture = new PictureBox { Left = 8, Top = 7, Width = 138, Height = 96, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White, Cursor = Cursors.Hand };
            LoadThumbnail(picture, paths[index]);
            var name = new Label { Left = 5, Top = 107, Width = 144, Height = 32, Text = cutPending ? "剪切 · " + Path.GetFileName(paths[index]) : Path.GetFileName(paths[index]), TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true, Cursor = Cursors.Hand, ForeColor = Color.FromArgb(52, 64, 82) };
            tile.Controls.Add(picture); tile.Controls.Add(name);
            tile.Click += (_, _) => SelectImageTile(itemIndex); picture.Click += (_, _) => SelectImageTile(itemIndex); name.Click += (_, _) => SelectImageTile(itemIndex);
            _imageItems.Controls.Add(tile);
        }
        _imageItems.ResumeLayout(true);
        _selectedLabel.Text = _selectedImageIndex >= 0 ? $"选中：{Path.GetFileName(paths[_selectedImageIndex])}" : "尚未选择图片";
    }

    private static void LoadThumbnail(PictureBox picture, string path)
    {
        try { using var source = Image.FromFile(path); picture.Image = new Bitmap(source); }
        catch { picture.Image = null; }
    }

    private void SelectImageTile(int index)
    {
        _selectedImageIndex = index;
        BuildImageTiles();
    }

    private sealed record ImageClipboardEntry(string Token, string GroupId, int Stage, int Index, string Path, bool Cut);

    private static ImageGroup MigrateImageGroup(int id, Dictionary<int, List<string>> oldImages, Dictionary<int, bool> oldModes)
    {
        List<string> Get(int key) => oldImages.TryGetValue(key, out var list) ? list.Where(File.Exists).ToList() : [];
        bool Carousel(params int[] keys) => keys.Any(key => oldModes.TryGetValue(key, out var value) && value);
        return new ImageGroup
        {
            Name = $"图片组 {(char)('A' + id)}",
            Images = new Dictionary<int, List<string>> { [0] = Get(1), [1] = Get(6), [2] = Get(0), [3] = Get(3).Concat(Get(5)).ToList(), [4] = Get(2).Concat(Get(4)).ToList(), [5] = Get(7) },
            CarouselModes = new Dictionary<int, bool> { [0] = Carousel(1), [1] = Carousel(6), [2] = Carousel(0), [3] = Carousel(3, 5), [4] = Carousel(2, 4), [5] = Carousel(7) }
        };
    }

    private void SwitchEditingProfile(int index)
    {
        if (_switchingProfile || index < 0 || index == _editingProfile) return;
        StoreEditingProfile();
        _editingProfile = index;
        LoadEditingProfile(index);
        UpdateProfileButtons();
        RenderPage(); UpdatePetWindow();
    }

    private void UpdateProfileButtons()
    {
        for (var i = 0; i < _profileButtons.Length; i++)
        {
            if (_profileButtons[i] is null) continue;
            var active = i == _editingProfile;
            _profileButtons[i].BackColor = active ? Color.FromArgb(48, 93, 157) : Color.FromArgb(235, 240, 248);
            _profileButtons[i].ForeColor = active ? Color.White : Color.FromArgb(65, 80, 103);
            _profileButtons[i].Text = $"{(char)('A' + i)}  ·  {_profiles[i].Threshold}";
        }
    }

    private void StoreEditingProfile()
    {
        var p = _profiles[_editingProfile];
        p.Threshold = (long)_profileThreshold.Value;
        if (_profileImageGroup.SelectedIndex >= 0) p.ImageGroupId = _profileImageGroup.SelectedIndex;
        SaveEditingImageGroup();
    }

    private void LoadEditingProfile(int index)
    {
        _switchingProfile = true;
        var p = _profiles[index];
        _profileThreshold.Value = Math.Clamp(p.Threshold, (long)_profileThreshold.Minimum, (long)_profileThreshold.Maximum);
        if (_profileImageGroup.Items.Count != _imageGroups.Count) { _profileImageGroup.Items.Clear(); foreach (var g in _imageGroups) _profileImageGroup.Items.Add(g.Name); }
        _profileImageGroup.SelectedIndex = Math.Clamp(p.ImageGroupId, 0, Math.Max(0, _imageGroups.Count - 1));
        _switchingProfile = false;
        SelectImageGroup(_profiles[_editingProfile].ImageGroupId, false);
    }

    private void LoadSettings()
    {
        try
        {
            var path = Path.Combine(_dataDir, "settings.json");
            if (!File.Exists(path)) return;
            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(path));
            if (data is null) return;
            if (data.Profiles.Count == 5)
            {
                _profiles.Clear(); _profiles.AddRange(data.Profiles);
            }
            if (data.ImageGroups.Count > 0)
            {
                _imageGroups.AddRange(data.ImageGroups);
            }
            else
            {
                for (var i = 0; i < 5; i++)
                {
                    var oldImages = data.Profiles.Count == 5 ? data.Profiles[i].Images : data.Images;
                    var oldCarousel = data.Profiles.Count == 5 ? data.Profiles[i].CarouselModes : data.CarouselModes;
                    var group = MigrateImageGroup(i, oldImages, oldCarousel);
                    _imageGroups.Add(group);
                    _profiles[i].ImageGroupId = i;
                }
            }
            while (_imageGroups.Count < 5) _imageGroups.Add(new ImageGroup { Name = $"图片组 {(char)('A' + _imageGroups.Count)}" });
            if (_imageGroups.Count > 10) _imageGroups.RemoveRange(10, _imageGroups.Count - 10);
            _profiles.ForEach(p => p.ImageGroupId = Math.Clamp(p.ImageGroupId, 0, _imageGroups.Count - 1));
            _counterFactor.Value = Math.Clamp(data.CounterFactor ?? (_profiles.Count > 0 ? _profiles[0].Factor : 1M), _counterFactor.Minimum, _counterFactor.Maximum);
            _loadedIdleDelay = Math.Clamp(data.GlobalIdleDelay ?? (_profiles.Count > 0 ? _profiles[0].IdleDelay : InitialIdleMs), 100, 5000);
            _loadedHoldDelay = Math.Clamp(data.HoldDelay, 100, 5000);
            _idleDelay.Value = _loadedIdleDelay; _holdDelay.Value = Math.Clamp(data.GlobalHoldDelay ?? _loadedHoldDelay, 100, 5000);
            _inputHold.Value = Math.Clamp(data.GlobalInputHold ?? 260, 80, 3000);
            _backspaceHold.Value = Math.Clamp(data.GlobalBackspaceHold ?? (_profiles.Count > 0 ? _profiles[0].BackspaceHold : 650), 100, 5000);
            _enterHold.Value = Math.Clamp(data.GlobalEnterHold ?? (_profiles.Count > 0 ? _profiles[0].EnterHold : 650), 100, 5000);
            _pauseDuration.Value = Math.Clamp(data.GlobalPauseDelay ?? (_profiles.Count > 0 ? _profiles[0].PauseDelay : 1000), 100, 30000);
            _motionAmplitude.Value = Math.Clamp(data.GlobalAmplitude ?? (_profiles.Count > 0 ? _profiles[0].Amplitude : 8), 1, 30);
            _globalShapeMode = data.GlobalShapeMode ?? (_profiles.Count > 0 && _profiles[0].ShapeMode);
            _advancedMode.Checked = data.GlobalAdvanced ?? (_profiles.Count > 0 && _profiles[0].Advanced);
            _thresholdSwitch.Checked = data.AutoThresholdSwitch ?? true;
            _persistedEditingProfile = Math.Clamp(data.EditingProfile, 0, _profiles.Count - 1);
            _loadedPetTopmost = data.PetTopmost;
            _loadedClickThrough = data.ClickThrough;
            _loadedPetZoom = Math.Clamp(data.PetZoomPercent ?? 200, 40, 600);
            if (data.PetX.HasValue && data.PetY.HasValue) _loadedPetLocation = new Point(data.PetX.Value, data.PetY.Value);
            _loadedCounterEnabled = data.CounterEnabled ?? true;
            _inputCount = Math.Max(0, data.InputCount);
        }
        catch (Exception ex) { _settingsLoadError = ex.Message; }
    }

    private void SaveSettings()
    {
        if (_settingsLoadError is not null) return;
        try
        {
            Directory.CreateDirectory(_dataDir);
            StoreEditingProfile();
            SaveEditingImageGroup(); SaveGlobalSettings();
            SyncRuntimePlaybackToDraft();
            var data = new SettingsData
            {
                IdleDelay = (int)_idleDelay.Value,
                HoldDelay = (int)_holdDelay.Value,
                Images = new Dictionary<int, List<string>>(),
                CarouselModes = new Dictionary<int, bool>(),
                ImageGroups = _imageGroups,
                Profiles = _profiles,
                InputCount = _inputCount,
                CounterFactor = _counterFactor.Value,
                GlobalIdleDelay = (int)_idleDelay.Value,
                GlobalInputHold = (int)_inputHold.Value,
                GlobalHoldDelay = (int)_holdDelay.Value,
                GlobalBackspaceHold = (int)_backspaceHold.Value,
                GlobalEnterHold = (int)_enterHold.Value,
                GlobalPauseDelay = (int)_pauseDuration.Value,
                GlobalAmplitude = (int)_motionAmplitude.Value,
                GlobalShapeMode = _motionStyle.SelectedIndex == 1,
                GlobalAdvanced = _advancedMode.Checked,
                AutoThresholdSwitch = _thresholdSwitch.Checked,
                EditingProfile = _editingProfile,
                PetTopmost = _pinPet.Checked,
                ClickThrough = _clickThrough.Checked,
                PetZoomPercent = _petWindow?.ZoomPercent ?? _loadedPetZoom,
                PetX = _petWindow?.RestingLocation.X ?? _loadedPetLocation?.X,
                PetY = _petWindow?.RestingLocation.Y ?? _loadedPetLocation?.Y,
                CounterEnabled = _petWindow?.CounterEnabled ?? _loadedCounterEnabled
            };
            var settingsPath = Path.Combine(_dataDir, "settings.json");
            var pendingPath = settingsPath + ".tmp";
            File.WriteAllText(pendingPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(pendingPath, settingsPath, true);
        }
        catch (Exception ex) { _hintLabel.Text = "设置保存失败：" + ex.Message; }
    }

    private void SyncRuntimePlaybackToDraft()
    {
        foreach (var applied in _appliedImageGroups)
        {
            var draft = _imageGroups.FirstOrDefault(group => group.Id == applied.Id);
            if (draft is null) continue;
            draft.ImageIndices = applied.ImageIndices.ToDictionary(pair => pair.Key, pair => pair.Value);
            draft.HasTriggered = applied.HasTriggered.ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }

    private sealed class SettingsData
    {
        public int IdleDelay { get; set; } = 800;
        public int HoldDelay { get; set; } = InitialHoldMs;
        public Dictionary<int, List<string>> Images { get; set; } = new();
        public Dictionary<int, bool> CarouselModes { get; set; } = new();
        public List<ImageGroup> ImageGroups { get; set; } = new();
        public List<PetProfile> Profiles { get; set; } = new();
        public long InputCount { get; set; }
        public decimal? CounterFactor { get; set; }
        public int? GlobalIdleDelay { get; set; }
        public int? GlobalInputHold { get; set; }
        public int? GlobalHoldDelay { get; set; }
        public int? GlobalBackspaceHold { get; set; }
        public int? GlobalEnterHold { get; set; }
        public int? GlobalPauseDelay { get; set; }
        public int? GlobalAmplitude { get; set; }
        public bool? GlobalShapeMode { get; set; }
        public bool? GlobalAdvanced { get; set; }
        public bool? AutoThresholdSwitch { get; set; }
        public int EditingProfile { get; set; }
        public bool PetTopmost { get; set; } = true;
        public bool ClickThrough { get; set; }
        public int? PetZoomPercent { get; set; }
        public int? PetX { get; set; }
        public int? PetY { get; set; }
        public bool? CounterEnabled { get; set; }
    }
}

internal sealed class RuntimeSettings
{
    public decimal CounterFactor { get; set; } = 1M;
    public bool ShapeMode { get; set; }
    public int Amplitude { get; set; } = 8;
    public bool Advanced { get; set; }
    public int IdleDelay { get; set; } = 800;
    public int InputHold { get; set; } = 260;
    public int PauseDelay { get; set; } = 1000;
    public int BackspaceHold { get; set; } = 650;
    public int EnterHold { get; set; } = 650;
    public int HoldDelay { get; set; } = 650;
}

internal sealed class PetProfile
{
    public string Name { get; set; } = "系列";
    public long Threshold { get; set; }
    public decimal Factor { get; set; } = 1M;
    public bool ShapeMode { get; set; }
        public int Amplitude { get; set; } = 8;
    public int IdleDelay { get; set; } = 800;
    public int PauseDelay { get; set; } = 1000;
    public int BackspaceHold { get; set; } = 650;
    public int EnterHold { get; set; } = 650;
    public bool Advanced { get; set; }
    public int ImageGroupId { get; set; }
    public Dictionary<int, List<string>> Images { get; set; } = new();
    public Dictionary<int, bool> CarouselModes { get; set; } = new();
    public Dictionary<int, int> ImageIndices { get; set; } = new();
    public Dictionary<int, bool> HasTriggered { get; set; } = new();
}

internal sealed class ImageGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "图片组";
    public Dictionary<int, List<string>> Images { get; set; } = new();
    public Dictionary<int, bool> CarouselModes { get; set; } = new();
    public Dictionary<int, int> ImageIndices { get; set; } = new();
    public Dictionary<int, bool> HasTriggered { get; set; } = new();
}
