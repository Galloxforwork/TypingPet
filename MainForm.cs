using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows.Forms;
using UiButton = AntdUI.Button;
using UiNumber = AntdUI.InputNumber;
using UiSelect = AntdUI.Select;

namespace TypingPet;

internal sealed class MainForm : Form
{
    private const int InitialIdleMs = 800;
    private const int InitialHoldMs = 650;
    private static readonly string[] RuntimeStageNames = ["输入中", "待机中", "回退按下", "回车按下", "回退抬起", "回车抬起", "暂停", "大小写切换"];
    private static readonly string[] ImageGroupNames = ["待机", "暂停", "输入中", "回车系", "回退系", "大小写切换"];
    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico"];
    private const string ImageClipboardFormat = "TypingPet.ImageSelection";
    private readonly string _dataDir = UserDataPaths.Root;
    private readonly string _assetsDir;
    private readonly Dictionary<int, List<string>> _images = Enumerable.Range(0, 6).ToDictionary(i => i, _ => new List<string>());
    private readonly Dictionary<int, List<string>> _backgrounds = Enumerable.Range(0, 6).ToDictionary(i => i, _ => new List<string>());
    private readonly Dictionary<int, bool> _backgroundCarouselModes = Enumerable.Range(0, 6).ToDictionary(i => i, _ => false);
    private readonly Dictionary<int, int> _backgroundIndices = Enumerable.Range(0, 6).ToDictionary(i => i, _ => 0);
    private readonly Dictionary<int, bool> _backgroundHasTriggered = Enumerable.Range(0, 6).ToDictionary(i => i, _ => false);
    private readonly Dictionary<int, bool> _carouselModes = Enumerable.Range(0, 6).ToDictionary(i => i, _ => false);
    private readonly Dictionary<int, int> _imageIndices = Enumerable.Range(0, 6).ToDictionary(i => i, _ => 0);
    private readonly Dictionary<int, bool> _hasTriggered = Enumerable.Range(0, 6).ToDictionary(i => i, _ => false);
    private readonly Label _previewText = new();
    private readonly PictureBox _previewImage = new();
    private readonly Label _selectedLabel = new();
    private readonly Label _hintLabel = new();
    private readonly Label _runtimeStatusLabel = new();
    private readonly UiNumber _idleDelay = new() { Minimum = 100, Maximum = 2000, Width = 90, WheelModifyEnabled = false };
    private readonly UiNumber _holdDelay = new() { Minimum = 100, Maximum = 2000, Width = 90, WheelModifyEnabled = false };
    private readonly UiNumber _inputHold = new() { Minimum = 80, Maximum = 2000, Increment = 20, Value = 240, Width = 90, WheelModifyEnabled = false };
    private readonly UiNumber _pauseDuration = new() { Minimum = 100, Maximum = 2000, Increment = 100, Value = 1000, Width = 90, WheelModifyEnabled = false };
    private readonly UiNumber _backspaceHold = new() { Minimum = 100, Maximum = 2000, Increment = 100, Value = 650, Width = 90, WheelModifyEnabled = false };
    private readonly UiNumber _enterHold = new() { Minimum = 100, Maximum = 2000, Increment = 100, Value = 650, Width = 90, WheelModifyEnabled = false };
    private readonly UiNumber _counterFactor = new() { Minimum = 0.2M, Maximum = 10M, DecimalPlaces = 1, Increment = 0.1M, Value = 1M, Width = 90, WheelModifyEnabled = false };
    private readonly UiNumber _counterSizePercent = new() { Minimum = 30, Maximum = 300, Value = 100, Width = 86, WheelModifyEnabled = false };
    private readonly UiNumber _counterDistance = new() { Minimum = -200, Maximum = 400, Value = 2, Width = 86, WheelModifyEnabled = false };
    private readonly UiNumber _counterTextOutlineWidth = new() { Minimum = 0, Maximum = 12, Value = 0, Width = 74, WheelModifyEnabled = false };
    private readonly UiNumber _counterBorderWidth = new() { Minimum = 0, Maximum = 12, Value = 1, Width = 74, WheelModifyEnabled = false };
    private readonly UiNumber _counterOpacity = new() { Minimum = 0, Maximum = 100, Value = 100, Width = 74, WheelModifyEnabled = false };
    private readonly UiNumber _motionAmplitude = new() { Minimum = 0, Maximum = 200, Value = 10, Width = 86, WheelModifyEnabled = false };
    private readonly UiNumber _animationSpeed = new() { Minimum = 0, Maximum = 3, DecimalPlaces = 1, Increment = 0.1M, Value = 1M, Width = 86, WheelModifyEnabled = false };
    private readonly UiNumber _scaleAmplitude = new() { Minimum = 0, Maximum = 200, Value = 0, Width = 86, WheelModifyEnabled = false };
    private readonly UiNumber _swayAmplitude = new() { Minimum = 0, Maximum = 200, Value = 0, Width = 86, WheelModifyEnabled = false };
    private readonly TrackBar _thresholdSlider = new() { Minimum = 0, Maximum = 10000, TickStyle = TickStyle.None };
    private readonly TrackBar _amplitudeSlider = new() { Minimum = 0, Maximum = 200, TickStyle = TickStyle.None };
    private readonly TrackBar _speedSlider = new() { Minimum = 0, Maximum = 30, TickStyle = TickStyle.None };
    private readonly TrackBar _scaleSlider = new() { Minimum = 0, Maximum = 200, TickStyle = TickStyle.None };
    private readonly TrackBar _swaySlider = new() { Minimum = 0, Maximum = 200, TickStyle = TickStyle.None };
    private readonly TrackBar _counterFactorSlider = new() { Minimum = 2, Maximum = 100, TickStyle = TickStyle.None };
    private readonly TrackBar _counterSizeSlider = new() { Minimum = 30, Maximum = 300, TickStyle = TickStyle.None };
    private readonly TrackBar _counterDistanceSlider = new() { Minimum = -200, Maximum = 400, TickStyle = TickStyle.None };
    private readonly TrackBar _counterTextOutlineSlider = new() { Minimum = 0, Maximum = 12, TickStyle = TickStyle.None };
    private readonly TrackBar _counterBorderWidthSlider = new() { Minimum = 0, Maximum = 12, TickStyle = TickStyle.None };
    private readonly TrackBar _counterOpacitySlider = new() { Minimum = 0, Maximum = 100, TickStyle = TickStyle.None };
    private readonly TrackBar _idleSlider = new() { Minimum = 100, Maximum = 2000, TickStyle = TickStyle.None };
    private readonly TrackBar _pauseSlider = new() { Minimum = 100, Maximum = 2000, TickStyle = TickStyle.None };
    private readonly TrackBar _inputHoldSlider = new() { Minimum = 80, Maximum = 2000, TickStyle = TickStyle.None };
    private readonly TrackBar _enterHoldSlider = new() { Minimum = 100, Maximum = 2000, TickStyle = TickStyle.None };
    private readonly TrackBar _backspaceHoldSlider = new() { Minimum = 100, Maximum = 2000, TickStyle = TickStyle.None };
    private readonly TrackBar _holdSlider = new() { Minimum = 100, Maximum = 2000, TickStyle = TickStyle.None };
    private readonly UiSelect _motionStyle = new() { Width = 120, WheelModifyEnabled = false };
    private readonly ToggleActionButton _stageCarousel = new() { Text = "输入时轮播状态组图片" };
    private readonly ToggleActionButton _restartInputCarousel = new() { Text = "从头轮播" };
    private readonly ToggleActionButton _startWithWindows = new() { Text = "开机自启", Checked = true };
    private readonly ToggleActionButton _counterOutline = new() { Text = "数字显示边框" };
    private readonly ToggleActionButton _counterBorderFillEnabled = new() { Text = "底色开启" };
    private readonly ToggleActionButton _counterInMainLayer = new() { Text = "并入桌宠主体图层", Enabled = false };
    private readonly ToggleActionButton _counterVisibility = new() { Text = "显示计数" };
    private readonly UiNumber _profileThreshold = new() { Minimum = 0, Maximum = 10000, Increment = 10, Width = 86, WheelModifyEnabled = false };
    private readonly CheckBox _advancedMode = new() { Text = "高级", AutoSize = true };
    private readonly UiButton _simpleModeButton = new() { Text = "简易" };
    private readonly UiButton _advancedModeButton = new() { Text = "高级" };
    private readonly UiButton[] _switchModeButtons = new UiButton[3];
    private readonly Label _cycleSummary = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, ForeColor = Color.FromArgb(75, 93, 120) };
    private readonly List<UiButton> _profileButtons = new();
    private readonly List<UiButton> _imageGroupButtons = new();
    private readonly UiSelect _profileImageGroup = new() { Width = 145, WheelModifyEnabled = false };
    private readonly FlowLayoutPanel _imageItems = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AllowDrop = true, Padding = new Padding(6) };
    private readonly FlowLayoutPanel _backgroundItems = new() { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AllowDrop = true, Padding = new Padding(6) };
    private readonly ToggleActionButton _backgroundCarousel = new() { Text = "输入时轮换背景" };
    private int _selectedBackgroundIndex = -1;
    private readonly List<ImageGroup> _imageGroups = new();
    private int _editingImageGroup;
    private UiButton? _resetCounterButton;
    private UiButton? _resetTodayInputButton;
    private UiButton? _resetTodayWordsButton;
    private readonly Label _todayInputValue = new() { Text = "0", TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(36, 52, 76), BackColor = Color.FromArgb(232, 239, 248), Padding = new Padding(7, 0, 0, 0) };
    private readonly Label _todayWordsValue = new() { Text = "0", TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(36, 52, 76), BackColor = Color.FromArgb(232, 239, 248), Padding = new Padding(7, 0, 0, 0) };
    private readonly UiButton _showCountButton = new() { Text = "显示次数" };
    private readonly UiButton _showWordsButton = new() { Text = "显示单词" };
    private readonly ToolTip _toolTips = new();
    private readonly Label _runtimeCounterValue = new() { Text = "0", TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(36, 52, 76), BackColor = Color.FromArgb(232, 239, 248), Padding = new Padding(7, 0, 0, 0), AutoEllipsis = true };
    private UiButton? _applySettingsButton;
    private readonly Label _saveStatusLabel = new() { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
    private UiButton? _parameterPageButton;
    private UiButton? _imagePageButton;
    private UiButton? _counterPageButton;
    private int _selectedImageIndex = -1;
    private ImageClipboardEntry? _imageClipboard;
    private ImageLayer _activeImageLayer = ImageLayer.Foreground;
    private readonly Label _imageEditHint = new() { AutoSize = false, Width = 320, Height = 34, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(103, 116, 137), AutoEllipsis = true };
    private readonly List<PetProfile> _appliedProfiles = new();
    private readonly List<ImageGroup> _appliedImageGroups = new();
    private readonly RuntimeSettings _appliedRuntime = new();
    private GroupSwitchMode _draftSwitchMode = GroupSwitchMode.Threshold;
    private GroupSwitchMode _appliedSwitchMode = GroupSwitchMode.Threshold;
    private List<int> _cycleSequence = [0, 1];
    private List<int> _appliedCycleSequence = [0, 1];
    private int _appliedCycleIndex;
    private bool _appliedCarouselEnabled;
    private bool _appliedRestartInputCarouselFromFirst;
    private int _persistedEditingProfile;
    private bool _allowExit;
    private bool _cycleEditorOpen;
    private NotifyIcon? _trayIcon;
    private DateTime _inputPulseUntil = DateTime.MinValue;
    private Panel? _pageBody;
    private TableLayoutPanel? _settingsLayout;
    private TableLayoutPanel? _imageLayout;
    private TableLayoutPanel? _counterLayout;
    private FlowLayoutPanel? _imageGroupNav;
    private FlowLayoutPanel? _profileNav;
    private Label? _imageDescription;
    private UiButton? _applyImageGroupButton;
    private readonly List<UiButton> _stateButtons = new();
    private bool _imagePageMode;
    private bool _counterPageMode;
    private readonly ToggleActionButton _pinPet = new() { Text = "置顶", Checked = true };
    private readonly ToggleActionButton _clickThrough = new() { Text = "穿透" };
    private readonly UiButton _togglePet = new() { Text = "隐藏小伴侣", Width = 112, Height = 34, Radius = 8 };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 80 };
    private RawKeyboardInput? _keyboard;
    private PetWindow? _petWindow;
    private DateTime _lastKeyAt = DateTime.MinValue;
    private bool _lastActivityWasTyping;
    private bool _backspaceHeld;
    private bool _enterHeld;
    private int? _enterArtworkProfileOverride;
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
    private bool _counterBorderEnabled;
    private bool _loadedCounterBorderFillEnabled;
    private string _counterFontFamily = "Segoe UI";
    private FontStyle _counterFontStyle = FontStyle.Bold;
    private Color _counterTextFill = Color.FromArgb(35, 43, 56);
    private Color _counterTextOutline = Color.Empty;
    private Color _counterBorderColor = Color.FromArgb(85, 100, 122);
    private Color _counterBorderFill = Color.Empty;
    private bool _loadedStartWithWindows = true;
    private bool _migratedImagePaths;
    private int _missingImagePaths;
    private string? _settingsLoadError;
    private long _inputCount;
    private long _todayInputCount;
    private long _todayWordCount;
    private bool _wordHasInput;
    private bool _dailyStatsDirty;
    private DateTime _dailyStatsDate = DateTime.Today;
    private DateTime _lastDailyStatsSaveAt = DateTime.MinValue;
    private bool _showWordsOnPet;
    private bool _countDirty;
    private DateTime _lastCountSaveAt = DateTime.MinValue;
    private readonly Dictionary<string, string> _imageDisplayNames = new(StringComparer.OrdinalIgnoreCase);
    private AppliedSettingsData? _loadedAppliedSettings;
    private bool _appliedPinned = true;
    private bool _appliedClickThrough;
    private bool _hasPendingDraft;
    private readonly List<PetProfile> _profiles = Enumerable.Range(0, 5).Select(i => new PetProfile { Name = $"设置组 {(char)('A' + i)}", Threshold = new long[] { 0, 100, 300, 500, 1000 }[i], ImageGroupId = i }).ToList();
    private int _editingProfile;
    private int _runtimeProfile;
    private bool _globalShapeMode;
    private bool _switchingProfile;
    private bool _initializing = true;

    public MainForm()
    {
        _assetsDir = UserDataPaths.Assets;
        Directory.CreateDirectory(_assetsDir);
        var starterImportError = StarterProfileImporter.TryInstall(AppContext.BaseDirectory, _dataDir);
        LoadSettings();
        LoadDailyStatistics();
        if (_imageGroups.Count == 0)
            while (_imageGroups.Count < 5) _imageGroups.Add(new ImageGroup { Name = $"图片组 {(char)('A' + _imageGroups.Count)}" });
        BuildWindow();
        if (_settingsLoadError is not null)
            _hintLabel.Text = "读取已有设置失败，已保留原设置文件且暂停写入：" + _settingsLoadError;
        else if (starterImportError is not null)
            _hintLabel.Text = "随附设置导入失败，未修改已有设置：" + starterImportError;
        _editingProfile = _persistedEditingProfile;
        LoadEditingProfile(_editingProfile);
        UpdateProfileButtons();
        CopyDraftToRuntime();
        RestoreAppliedSettings();
        RenderPage();
        RenderSelectedPreview();
        UpdateDailyCounterValues();
        if (_appliedSwitchMode == GroupSwitchMode.Threshold) SelectProfileForCount();
        UpdateRuntimeStatus();
        _initializing = false;
        if (_settingsLoadError is null && _hasPendingDraft) _hintLabel.Text = "上次有未应用的修改；当前桌宠仍使用最后已应用的设置。";
        if (_settingsLoadError is null && (_migratedImagePaths || !File.Exists(UserDataPaths.Settings))) SaveSettings();
        if (_missingImagePaths > 0) _hintLabel.Text = $"有 {_missingImagePaths} 张旧图片暂时找不到，记录已保留；请在图片组中重新添加。";
        if (_settingsLoadError is null)
        {
            var dataReport = DataFolderIntegrityService.CheckAndClean(_dataDir);
            if (dataReport.Issues.Count > 0)
                _hintLabel.Text = $"data 校验发现 {dataReport.Issues.Count} 项问题，已暂停可能影响数据的清理；点击“校验并清理”查看详情。";
            else if (dataReport.RemovedImageCount > 0)
                _hintLabel.Text = $"data 校验完成，已自动清理 {dataReport.RemovedImageCount} 张未引用图片。";
        }
        _timer.Tick += (_, _) => UpdateState();
        _timer.Start();
    }

    private void BuildWindow()
    {
        Text = "Typing Pad · 设置";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1300, 950);
        MinimumSize = new Size(1050, 680);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        TopMost = false;
        BackColor = Color.FromArgb(244, 247, 251);
        Font = new Font("Segoe UI", 9F);
        // Keep the editing page tall enough for the compact settings grid at the default size.
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 12, RowCount = 50, BackColor = BackColor, Padding = new Padding(18, 4, 18, 4), Margin = Padding.Empty };
        for (var column = 0; column < 12; column++) root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 12));
        for (var row = 0; row < 50; row++) root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / 50));
        Controls.Add(root);

        var title = new Label { Text = "Typing Pad", Font = new Font(Font.FontFamily, 18, FontStyle.Bold), ForeColor = Color.FromArgb(31, 41, 55), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        _runtimeStatusLabel.AutoSize = false;
        _runtimeStatusLabel.Dock = DockStyle.Fill;
        _runtimeStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _runtimeStatusLabel.AutoEllipsis = true;
        _runtimeStatusLabel.ForeColor = Color.FromArgb(81, 94, 116);
        _saveStatusLabel.ForeColor = Color.FromArgb(58, 75, 98);
        _applySettingsButton = ActionButton("保存并应用", ApplySettings);
        _applySettingsButton.Width = 136;
        _applySettingsButton.Height = 38;
        _applySettingsButton.Type = AntdUI.TTypeMini.Primary;
        _applySettingsButton.BackColor = Color.FromArgb(48, 93, 157);
        _applySettingsButton.ForeColor = Color.White;
        _applySettingsButton.ForeHover = Color.White;
        _applySettingsButton.ForeActive = Color.White;
        _applySettingsButton.BackHover = Color.FromArgb(39, 80, 143);
        _applySettingsButton.BackActive = Color.FromArgb(31, 67, 120);
        title.Margin = Padding.Empty;
        _runtimeStatusLabel.Margin = Padding.Empty;
        root.Controls.Add(title, 0, 0); root.SetColumnSpan(title, 2); root.SetRowSpan(title, 2);
        root.Controls.Add(_runtimeStatusLabel, 2, 0); root.SetColumnSpan(_runtimeStatusLabel, 6); root.SetRowSpan(_runtimeStatusLabel, 2);

        var globalSettings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 12, RowCount = 3, BackColor = Color.FromArgb(226, 235, 247), BorderStyle = BorderStyle.None, Margin = new Padding(0, 0, 0, 2), Padding = new Padding(6, 2, 6, 2) };
        for (var column = 0; column < 12; column++) globalSettings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 12));
        for (var row = 0; row < 3; row++) globalSettings.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));
        var globalLabel = new Label { Text = "全局设置", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(31, 50, 76), Font = new Font(Font, FontStyle.Bold) };
        globalSettings.Controls.Add(globalLabel, 0, 0);
        globalSettings.SetColumnSpan(globalLabel, 2); globalSettings.SetRowSpan(globalLabel, 3);
        var switchRow = CreateGridRow(12);
        switchRow.BackColor = Color.FromArgb(250, 252, 255);
        var switchLabel = new Label { Text = "组切换", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(36, 54, 80), Font = new Font(Font, FontStyle.Bold), Margin = Padding.Empty };
        switchRow.Controls.Add(switchLabel, 0, 0); switchRow.SetColumnSpan(switchLabel, 2);
        var modeNames = new[] { "固定组", "计数阈值", "回车循环" };
        for (var i = 0; i < modeNames.Length; i++)
        {
            var mode = (GroupSwitchMode)i;
            var button = ActionButton(modeNames[i], () => SetDraftSwitchMode(mode));
            button.Dock = DockStyle.Fill; button.Margin = new Padding(2, 1, 2, 1); button.Height = 26;
            _switchModeButtons[i] = button;
            switchRow.Controls.Add(button, 2 + i * 2, 0); switchRow.SetColumnSpan(button, 2);
        }
        var editSequence = ActionButton("编辑顺序", EditCycleSequence);
        editSequence.Dock = DockStyle.Fill; editSequence.Margin = new Padding(2, 1, 2, 1); editSequence.Height = 26;
        switchRow.Controls.Add(editSequence, 8, 0); switchRow.SetColumnSpan(editSequence, 2);
        _cycleSummary.Dock = DockStyle.Fill; switchRow.Controls.Add(_cycleSummary, 10, 0); switchRow.SetColumnSpan(_cycleSummary, 2);
        globalSettings.Controls.Add(switchRow, 2, 0); globalSettings.SetColumnSpan(switchRow, 10);
        var optionsRow = CreateGridRow(12); optionsRow.BackColor = Color.FromArgb(250, 252, 255);
        var inputLabel = new Label { Text = "输入", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(36, 54, 80), Font = new Font(Font, FontStyle.Bold) };
        optionsRow.Controls.Add(inputLabel, 0, 0); optionsRow.SetColumnSpan(inputLabel, 2);
        _stageCarousel.Dock = DockStyle.Fill; _stageCarousel.Margin = new Padding(2, 1, 2, 1); _stageCarousel.Height = 26;
        optionsRow.Controls.Add(_stageCarousel, 2, 0); optionsRow.SetColumnSpan(_stageCarousel, 2);
        _restartInputCarousel.Dock = DockStyle.Fill; _restartInputCarousel.Margin = new Padding(2, 1, 2, 1); _restartInputCarousel.Height = 26;
        optionsRow.Controls.Add(_restartInputCarousel, 4, 0); optionsRow.SetColumnSpan(_restartInputCarousel, 2);
        var petLabel = new Label { Text = "桌宠窗口", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(36, 54, 80), Font = new Font(Font, FontStyle.Bold) };
        optionsRow.Controls.Add(petLabel, 6, 0); optionsRow.SetColumnSpan(petLabel, 2);
        _pinPet.Dock = DockStyle.Fill; _clickThrough.Dock = DockStyle.Fill; _togglePet.Dock = DockStyle.Fill;
        _pinPet.Margin = new Padding(0); _clickThrough.Margin = new Padding(0); _togglePet.Margin = new Padding(2, 1, 2, 1); _togglePet.Height = 26;
        optionsRow.Controls.Add(_pinPet, 8, 0); optionsRow.Controls.Add(_clickThrough, 9, 0);
        optionsRow.Controls.Add(_togglePet, 10, 0); optionsRow.SetColumnSpan(_togglePet, 2);
        globalSettings.Controls.Add(optionsRow, 2, 1); globalSettings.SetColumnSpan(optionsRow, 10);
        var launchRow = CreateGridRow(12); launchRow.BackColor = Color.FromArgb(250, 252, 255);
        var launchLabel = new Label { Text = "启动与数据", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(36, 54, 80), Font = new Font(Font, FontStyle.Bold) };
        launchRow.Controls.Add(launchLabel, 0, 0); launchRow.SetColumnSpan(launchLabel, 2);
        _startWithWindows.Dock = DockStyle.Fill; _startWithWindows.Margin = new Padding(2, 1, 2, 1); _startWithWindows.Height = 26;
        launchRow.Controls.Add(_startWithWindows, 2, 0); launchRow.SetColumnSpan(_startWithWindows, 2);
        var openData = ActionButton("打开数据文件夹", () =>
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_dataDir) { UseShellExecute = true }); }
            catch (Exception ex) { _hintLabel.Text = "打开数据文件夹失败：" + ex.Message; }
        });
        openData.Dock = DockStyle.Fill; openData.Height = 26; openData.Margin = new Padding(2, 1, 2, 1);
        launchRow.Controls.Add(openData, 4, 0); launchRow.SetColumnSpan(openData, 2);
        var checkData = ActionButton("校验并清理", ShowDataFolderIntegrity);
        checkData.Dock = DockStyle.Fill; checkData.Height = 26; checkData.Margin = new Padding(2, 1, 2, 1);
        launchRow.Controls.Add(checkData, 6, 0); launchRow.SetColumnSpan(checkData, 2);
        _saveStatusLabel.Dock = DockStyle.Fill; _saveStatusLabel.TextAlign = ContentAlignment.MiddleCenter;
        launchRow.Controls.Add(_saveStatusLabel, 8, 0); launchRow.SetColumnSpan(_saveStatusLabel, 2);
        _applySettingsButton.Dock = DockStyle.Fill; _applySettingsButton.Height = 26; _applySettingsButton.Margin = new Padding(2, 1, 2, 1);
        launchRow.Controls.Add(_applySettingsButton, 10, 0); launchRow.SetColumnSpan(_applySettingsButton, 2);
        globalSettings.Controls.Add(launchRow, 2, 2); globalSettings.SetColumnSpan(launchRow, 10);
        root.Controls.Add(globalSettings, 0, 2); root.SetColumnSpan(globalSettings, 12); root.SetRowSpan(globalSettings, 6);
        UpdateSwitchModeButtons();

        _profileNav = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 40, WrapContents = false, AutoScroll = true, Padding = Padding.Empty, Margin = Padding.Empty };
        var profileActions = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 40, WrapContents = false, Padding = Padding.Empty, Margin = Padding.Empty };
        var addProfile = ActionButton("＋新建", AddProfile); addProfile.Width = 84;
        var removeProfile = ActionButton("删除", RemoveProfile); removeProfile.Width = 80;
        profileActions.FlowDirection = FlowDirection.RightToLeft;
        profileActions.Controls.AddRange([removeProfile, addProfile]);
        var profileGrid = CreateGridRow(12);
        var profileLabel = new Label { Text = "设置组", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(86, 99, 119), Font = new Font(Font, FontStyle.Bold), Margin = Padding.Empty };
        profileGrid.Controls.Add(profileLabel, 0, 0); profileGrid.SetColumnSpan(profileLabel, 2);
        profileGrid.Controls.Add(_profileNav, 2, 0); profileGrid.SetColumnSpan(_profileNav, 6);
        profileGrid.Controls.Add(profileActions, 8, 0); profileGrid.SetColumnSpan(profileActions, 4);
        root.Controls.Add(profileGrid, 0, 8); root.SetColumnSpan(profileGrid, 12); root.SetRowSpan(profileGrid, 2);
        RefreshProfileNav();

        _imageGroupNav = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 40, WrapContents = false, AutoScroll = true, Padding = Padding.Empty, Margin = Padding.Empty };
        var imageActions = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 40, WrapContents = false, Padding = Padding.Empty, Margin = Padding.Empty };
        var addGroupButton = ActionButton("＋新建", AddImageGroup); addGroupButton.Width = 84;
        var removeGroupButton = ActionButton("删除", RemoveImageGroup); removeGroupButton.Width = 80;
        _applyImageGroupButton = ActionButton("应用到当前设置组", ApplyImageGroupToProfile); _applyImageGroupButton.Width = 120; _applyImageGroupButton.Height = 30;
        imageActions.FlowDirection = FlowDirection.RightToLeft;
        imageActions.Controls.AddRange([removeGroupButton, addGroupButton]);
        var imageGrid = CreateGridRow(12);
        var imageLabel = new Label { Text = "图片组", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(86, 99, 119), Font = new Font(Font, FontStyle.Bold), Margin = Padding.Empty };
        imageGrid.Controls.Add(imageLabel, 0, 0); imageGrid.SetColumnSpan(imageLabel, 2);
        imageGrid.Controls.Add(_imageGroupNav, 2, 0); imageGrid.SetColumnSpan(_imageGroupNav, 6);
        imageGrid.Controls.Add(imageActions, 8, 0); imageGrid.SetColumnSpan(imageActions, 4);
        root.Controls.Add(imageGrid, 0, 10); root.SetColumnSpan(imageGrid, 12); root.SetRowSpan(imageGrid, 2);
        RefreshImageGroupNav();

        var pageNav = CreateGridRow(12);
        pageNav.BackColor = Color.White;
        var parameterPage = new UiButton { Text = "参数与预览", Width = 120, Height = 34, Radius = 8, Margin = new Padding(0, 0, 6, 0), ForeColor = Color.FromArgb(36, 52, 76), ForeHover = Color.FromArgb(36, 52, 76), ForeActive = Color.FromArgb(36, 52, 76), BackHover = Color.FromArgb(210, 226, 248), BackActive = Color.FromArgb(198, 217, 244) };
        var imagePage = new UiButton { Text = "图片组管理", Width = 120, Height = 34, Radius = 8, Margin = new Padding(0, 0, 6, 0), ForeColor = Color.FromArgb(36, 52, 76), ForeHover = Color.FromArgb(36, 52, 76), ForeActive = Color.FromArgb(36, 52, 76), BackHover = Color.FromArgb(210, 226, 248), BackActive = Color.FromArgb(198, 217, 244) };
        var counterPage = new UiButton { Text = "计数器外观", Width = 120, Height = 34, Radius = 8, Margin = new Padding(0, 0, 6, 0), ForeColor = Color.FromArgb(36, 52, 76), ForeHover = Color.FromArgb(36, 52, 76), ForeActive = Color.FromArgb(36, 52, 76), BackHover = Color.FromArgb(210, 226, 248), BackActive = Color.FromArgb(198, 217, 244) };
        _parameterPageButton = parameterPage; _imagePageButton = imagePage; _counterPageButton = counterPage;
        parameterPage.Click += (_, _) => { CommitFocusedNumberInput(); _imagePageMode = false; _counterPageMode = false; RenderPage(); };
        imagePage.Click += (_, _) => { CommitFocusedNumberInput(); _imagePageMode = true; _counterPageMode = false; RenderPage(); };
        counterPage.Click += (_, _) => { CommitFocusedNumberInput(); _imagePageMode = false; _counterPageMode = true; RenderPage(); };
        parameterPage.Dock = DockStyle.Fill; parameterPage.Margin = new Padding(2, 2, 2, 2);
        imagePage.Dock = DockStyle.Fill; imagePage.Margin = new Padding(2, 2, 2, 2);
        counterPage.Dock = DockStyle.Fill; counterPage.Margin = new Padding(2, 2, 2, 2);
        pageNav.Controls.Add(parameterPage, 0, 0); pageNav.SetColumnSpan(parameterPage, 2);
        pageNav.Controls.Add(imagePage, 2, 0); pageNav.SetColumnSpan(imagePage, 2);
        pageNav.Controls.Add(counterPage, 4, 0); pageNav.SetColumnSpan(counterPage, 2);
        var applyImageGroupActions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Padding = Padding.Empty, Margin = Padding.Empty };
        _applyImageGroupButton.Width = 120; _applyImageGroupButton.Height = 30;
        _applyImageGroupButton.Dock = DockStyle.None; _applyImageGroupButton.Margin = new Padding(2, 2, 2, 2);
        applyImageGroupActions.Controls.Add(_applyImageGroupButton);
        pageNav.Controls.Add(applyImageGroupActions, 8, 0); pageNav.SetColumnSpan(applyImageGroupActions, 4);
        root.Controls.Add(pageNav, 0, 12); root.SetColumnSpan(pageNav, 12); root.SetRowSpan(pageNav, 2);

        var stateNav = BuildStateButtons();
        root.Controls.Add(stateNav, 0, 14); root.SetColumnSpan(stateNav, 12); root.SetRowSpan(stateNav, 2);

        _pageBody = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = Padding.Empty, Margin = Padding.Empty };
        root.Controls.Add(_pageBody, 0, 16); root.SetColumnSpan(_pageBody, 12); root.SetRowSpan(_pageBody, 33);
        _motionStyle.Items.Clear(); _motionStyle.Items.AddRange(["跳动式", "形变式"]);
        ConfigureEditorControls();
        _motionStyle.SelectedIndex = _globalShapeMode ? 1 : 0;
        _simpleModeButton.Click += (_, _) => SetStateMode(false);
        _advancedModeButton.Click += (_, _) => SetStateMode(true);
        _previewImage.Paint += PreviewPlaceholderPaint;
        _idleDelay.Minimum = 100; _idleDelay.Maximum = 2000; _idleDelay.Width = 86;
        _holdDelay.Minimum = 100; _holdDelay.Maximum = 2000; _holdDelay.Value = Math.Clamp(_loadedHoldDelay, 100, 2000); _holdDelay.Width = 86;
        _pinPet.Checked = _loadedPetTopmost; _clickThrough.Checked = _loadedClickThrough;
        _startWithWindows.Checked = _loadedStartWithWindows;
        _counterOutline.Checked = _counterBorderEnabled;
        _counterBorderFillEnabled.Checked = _loadedCounterBorderFillEnabled;
        _counterVisibility.Checked = _loadedCounterEnabled;
        _counterInMainLayer.Checked = false;
        _counterFactor.ValueChanged += (_, _) => { if (!_switchingProfile) { _petWindow?.SetCounter(DisplayedCounterValue); MarkSettingsPending(); SaveSettings(); } };
        _counterOutline.CheckedChanged += (_, _) => { if (!_switchingProfile) { PreviewCounterAppearance(); SaveSettings(); } };
        _counterInMainLayer.CheckedChanged += (_, _) => { _counterInMainLayer.Checked = false; };
        _counterVisibility.CheckedChanged += (_, _) =>
        {
            _loadedCounterEnabled = _counterVisibility.Checked;
            _petWindow?.SetCounterEnabled(_loadedCounterEnabled);
            SaveSettings();
        };
        foreach (var number in new[] { _counterSizePercent, _counterDistance, _counterTextOutlineWidth, _counterBorderWidth, _counterOpacity })
            number.ValueChanged += (_, _) => { if (!_switchingProfile) { PreviewCounterAppearance(); SaveSettings(); } };
        _motionStyle.SelectedIndexChanged += (_, _) => ConfigurePetMotion();
        _motionAmplitude.ValueChanged += (_, _) => ConfigurePetMotion();
        _profileThreshold.ValueChanged += (_, _) => { if (!_switchingProfile) { _profiles[_editingProfile].Threshold = (long)_profileThreshold.Value; UpdateProfileButtons(); SelectProfileForCount(); MarkSettingsPending(); SaveSettings(); } };
        _profileImageGroup.SelectedIndexChanged += (_, _) =>
        {
            if (_switchingProfile || _profileImageGroup.SelectedIndex < 0) return;
            _profiles[_editingProfile].ImageGroupId = _profileImageGroup.SelectedIndex;
            LoadImageGroup(_profileImageGroup.SelectedIndex); UpdateProfileButtons(); RenderPage(); UpdatePetWindow(); MarkSettingsPending(); SaveSettings();
        };
        _advancedMode.CheckedChanged += (_, _) => { if (!_switchingProfile) { UpdateStateModeButtons(); SaveGlobalSettings(); MarkSettingsPending(); SaveSettings(); } };
        _stageCarousel.CheckedChanged += (_, _) => { MarkSettingsPending(); SaveSettings(); };
        _restartInputCarousel.CheckedChanged += (_, _) => { MarkSettingsPending(); SaveSettings(); };
        _backgroundCarousel.CheckedChanged += (_, _) =>
        {
            if (_switchingProfile) return;
            _backgroundCarouselModes[_previewStage] = _backgroundCarousel.Checked;
            MarkSettingsPending(); SaveSettings();
        };
        _idleDelay.ValueChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _inputHold.ValueChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _holdDelay.ValueChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _pauseDuration.ValueChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _backspaceHold.ValueChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _enterHold.ValueChanged += (_, _) => { if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); } };
        _pinPet.CheckedChanged += (_, _) => { MarkSettingsPending(); SaveSettings(); };
        _clickThrough.CheckedChanged += (_, _) => { MarkSettingsPending(); SaveSettings(); };
        _startWithWindows.CheckedChanged += (_, _) =>
        {
            var error = StartupRegistration.Synchronize(_startWithWindows.Checked);
            if (error is not null) _hintLabel.Text = "设置开机自启失败：" + error;
            SaveSettings();
        };
        _togglePet.Click += (_, _) => TogglePetVisibility();
        _imageItems.DragEnter += ImageListDragEnter;
        _imageItems.DragDrop += ImageListDragDrop;
        _imageItems.Click += (_, _) => { SetActiveImageLayer(ImageLayer.Foreground); BuildImageTiles(); BuildBackgroundTiles(); };
        _backgroundItems.DragEnter += ImageListDragEnter;
        _backgroundItems.DragDrop += BackgroundListDragDrop;
        _backgroundItems.Click += (_, _) => { SetActiveImageLayer(ImageLayer.Background); BuildImageTiles(); BuildBackgroundTiles(); };
        _switchingProfile = true;
        _profileImageGroup.Items.Clear(); foreach (var group in _imageGroups) _profileImageGroup.Items.Add(group.Name);
        _profileImageGroup.SelectedIndex = _profiles[_editingProfile].ImageGroupId;
        _switchingProfile = false;
        _hintLabel.Text = "可选择固定组、计数阈值或回车循环；图片组可分别关联到设置组。数字统计不会读取键入内容。";
        _hintLabel.Dock = DockStyle.Fill;
        _hintLabel.Height = 25;
        _hintLabel.ForeColor = Color.FromArgb(103, 116, 137);
        _hintLabel.Margin = Padding.Empty;
        root.Controls.Add(_hintLabel, 0, 49); root.SetColumnSpan(_hintLabel, 12); root.SetRowSpan(_hintLabel, 1);
        RenderPage();
        UpdateProfileButtons();
        UpdateSaveStatus();
        InitializeTrayIcon();
        FormClosing += HandleFormClosing;
    }

    internal void StartCompanion()
    {
        _ = Handle; // Keep the hidden settings form alive for its tray menu and message loop.
        StartKeyboardInput();
        EnsurePetVisible();
        var startupError = StartupRegistration.Synchronize(_startWithWindows.Checked);
        if (startupError is not null) _hintLabel.Text = "设置开机自启失败：" + startupError;
    }

    private void RenderPage()
    {
        if (_pageBody is null) return;
        if (_settingsLayout is null) BuildSettingsPreviewPage();
        if (_imageLayout is null) BuildImageManagementPage();
        if (_counterLayout is null) BuildCounterAppearancePage();
        RefreshImageGroupNav();
        _pageBody.SuspendLayout();
        var selectedPage = _counterPageMode ? _counterLayout! : _imagePageMode ? _imageLayout! : _settingsLayout!;
        foreach (var page in new Control[] { _settingsLayout!, _imageLayout!, _counterLayout! })
        {
            if (!ReferenceEquals(page, selectedPage)) _pageBody.Controls.Remove(page);
        }
        if (!_pageBody.Controls.Contains(selectedPage)) _pageBody.Controls.Add(selectedPage);
        if (_imagePageMode)
        {
            BuildImageTiles();
            BuildBackgroundTiles();
            SetActiveImageLayer(_activeImageLayer);
        }
        else if (!_counterPageMode)
        {
            RenderSelectedPreview();
        }
        _pageBody.ResumeLayout(true);
        foreach (var button in _stateButtons)
        {
            var active = (int)button.Tag! == _previewStage;
            button.Type = active ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            button.DefaultBack = active ? Color.FromArgb(36, 117, 232) : Color.FromArgb(246, 248, 252);
            button.ForeColor = active ? Color.White : Color.FromArgb(36, 52, 76);
            button.ForeHover = active ? Color.White : Color.FromArgb(36, 52, 76);
            button.ForeActive = active ? Color.White : Color.FromArgb(36, 52, 76);
            button.BackHover = active ? Color.FromArgb(30, 104, 210) : Color.FromArgb(225, 235, 248);
            button.BackActive = active ? Color.FromArgb(21, 87, 176) : Color.FromArgb(214, 228, 245);
        }
        SetPageButton(_parameterPageButton, !_imagePageMode && !_counterPageMode);
        SetPageButton(_imagePageButton, _imagePageMode);
        SetPageButton(_counterPageButton, _counterPageMode);
        if (_applyImageGroupButton is not null) _applyImageGroupButton.Visible = _imagePageMode;
        if (_imageDescription is not null) _imageDescription.Text = $"前景 · {_imageGroups.ElementAtOrDefault(_editingImageGroup)?.Name ?? "图片组"} · {ImageGroupNames[_previewStage]} · {_images[_previewStage].Count} 张";
        _switchingProfile = true;
        _backgroundCarousel.Checked = _backgroundCarouselModes[_previewStage];
        _switchingProfile = false;

        static void SetPageButton(UiButton? button, bool active)
        {
            if (button is null) return;
            button.Type = active ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            button.DefaultBack = active ? Color.FromArgb(36, 117, 232) : Color.FromArgb(237, 242, 248);
            button.ForeColor = active ? Color.White : Color.FromArgb(36, 52, 76);
        }
    }

    private void BuildSettingsPreviewPage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 12, RowCount = 1, BackColor = Color.White, Margin = Padding.Empty, Padding = new Padding(4) };
        for (var column = 0; column < 12; column++) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 12));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var preview = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.White, Margin = new Padding(2), Padding = new Padding(8), BorderStyle = BorderStyle.FixedSingle };
        preview.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        preview.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        preview.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        var previewHeader = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        previewHeader.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); previewHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previewHeader.Controls.Add(new Label { Text = "预览", Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold), ForeColor = Color.FromArgb(24, 43, 76), TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);
        _selectedLabel.Dock = DockStyle.Fill; _selectedLabel.TextAlign = ContentAlignment.MiddleLeft; _selectedLabel.Font = new Font(Font.FontFamily, 8.5F, FontStyle.Regular); _selectedLabel.ForeColor = Color.FromArgb(103, 122, 149); _selectedLabel.AutoEllipsis = true; _selectedLabel.Margin = Padding.Empty;
        previewHeader.Controls.Add(_selectedLabel, 0, 1);
        preview.Controls.Add(previewHeader, 0, 0);
        _previewImage.Dock = DockStyle.Fill; _previewImage.SizeMode = PictureBoxSizeMode.Zoom; _previewImage.BackColor = Color.FromArgb(239, 245, 252); _previewImage.Margin = new Padding(0, 2, 0, 4);
        preview.Controls.Add(_previewImage, 0, 1);
        var stateMode = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        stateMode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34)); stateMode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33)); stateMode.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        stateMode.Controls.Add(new Label { Text = "状态模式", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(81, 94, 116), TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty }, 0, 0);
        foreach (var button in new[] { _simpleModeButton, _advancedModeButton }) { button.Dock = DockStyle.Fill; button.Height = 32; button.Margin = new Padding(2, 4, 2, 4); }
        stateMode.Controls.Add(_simpleModeButton, 1, 0); stateMode.Controls.Add(_advancedModeButton, 2, 0); preview.Controls.Add(stateMode, 0, 2);
        _toolTips.SetToolTip(_todayInputValue, "可打印键和退格键各计 1 次；不读取键入内容。");
        _toolTips.SetToolTip(_todayWordsValue, "不读取键入内容；每次输入段在空格或回车时计为一个。中文输入法按拼音输入段计算，不做语义分词。");
        layout.Controls.Add(preview, 0, 0); layout.SetColumnSpan(preview, 4);

        FlowLayoutPanel SettingsColumn(bool autoScroll = false)
            => new() { Dock = DockStyle.Fill, AutoScroll = autoScroll, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.White, Padding = new Padding(2, 1, 2, 1), Margin = Padding.Empty };
        var center = SettingsColumn(); var right = SettingsColumn();
        layout.Controls.Add(center, 4, 0); layout.SetColumnSpan(center, 4);
        layout.Controls.Add(right, 8, 0); layout.SetColumnSpan(right, 4);

        AddSettingSection(center, "设置组", new Control[] { SliderNumberRow("显示计数阈值", _profileThreshold, _thresholdSlider), Field("使用图片组", _profileImageGroup) });
        AddSettingSection(center, "全局动效", new Control[]
        {
            Field("类型", _motionStyle), SliderNumberRow("幅度（像素）", _motionAmplitude, _amplitudeSlider),
            SliderNumberRow("动画速度（0关闭）", _animationSpeed, _speedSlider, value => value / 10M, value => (int)Math.Round(value * 10M)),
            SliderNumberRow("缩放幅度", _scaleAmplitude, _scaleSlider), SliderNumberRow("摇摆幅度", _swayAmplitude, _swaySlider)
        });
        var presets = new FlowLayoutPanel { Height = 34, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = Padding.Empty, Tag = "fit" };
        presets.Controls.AddRange([ActionButton("慢", () => ApplyTimingPreset("slow")), ActionButton("中等", () => ApplyTimingPreset("medium")), ActionButton("快", () => ApplyTimingPreset("fast")), ActionButton("重置", () => ApplyTimingPreset("medium"))]);
        foreach (var button in presets.Controls.OfType<UiButton>()) { button.Width = 48; button.Height = 30; button.Margin = new Padding(0, 0, 4, 0); }
        AddSettingSection(center, "响应时间", new Control[] { presets, Field("状态切换延迟", PlaceholderValue()), Field("输入动画延迟", PlaceholderValue()), Field("回车动画延迟", PlaceholderValue()) });

        _runtimeCounterValue.Text = DisplayedCounterValue.ToString("N0");
        AddSettingSection(right, "全局计数", new Control[]
        {
            CounterValueActionRow("当前显示计数", _runtimeCounterValue, _resetCounterButton ??= ActionButton("重置", ResetCounter)),
            SliderNumberRow("每键显示系数", _counterFactor, _counterFactorSlider, value => value / 10M, value => (int)Math.Round(value * 10M)),
            CounterDisplayModeRow(),
            CounterValueActionRow("今日输入次数", _todayInputValue, _resetTodayInputButton ??= ActionButton("重置", ResetTodayInputCount)),
            CounterValueActionRow("今日输入单词", _todayWordsValue, _resetTodayWordsButton ??= ActionButton("重置", ResetTodayWordCount))
        });
        var timingGuide = new Label
        {
            Text = "顺序：回退按下（随按键保持）→ 松开后保持回退时长；新文字/空格输入立即进入输入中 → 静默达到待机判定后进入待机。高级模式会先显示暂停。输入保持与待机判定并行，按较长时间结束输入阶段；暂停从此后开始，不与前段重叠。",
            Height = 76, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(103, 116, 137),
            AutoEllipsis = true, Tag = "fit", Margin = Padding.Empty
        };
        AddSettingSection(right, "扩展时间（高级模式生效）", new Control[]
        {
            SliderNumberRow("待机判定 ms", _idleDelay, _idleSlider), SliderNumberRow("暂停时长 ms", _pauseDuration, _pauseSlider),
            SliderNumberRow("输入中 ms", _inputHold, _inputHoldSlider), SliderNumberRow("回车系 ms", _enterHold, _enterHoldSlider),
            SliderNumberRow("回退系 ms", _backspaceHold, _backspaceHoldSlider), SliderNumberRow("大小写 ms", _holdDelay, _holdSlider), timingGuide
        });

        void FitColumn(FlowLayoutPanel column)
        {
            var sectionWidth = Math.Max(220, column.ClientSize.Width - 12);
            foreach (var section in column.Controls.OfType<FlowLayoutPanel>())
            {
                section.Width = sectionWidth;
                if (section.Controls.Count > 0 && section.Controls[0] is Label heading) heading.Width = Math.Max(190, sectionWidth - 18);
                foreach (Control child in section.Controls)
                {
                    if (child.Tag?.ToString() == "field" || child.Tag?.ToString() == "fit") child.Width = Math.Max(190, sectionWidth - 18);
                }
            }
        }
        void FitColumns() { FitColumn(center); FitColumn(right); }
        center.SizeChanged += (_, _) => FitColumn(center); right.SizeChanged += (_, _) => FitColumn(right); layout.SizeChanged += (_, _) => FitColumns();
        FitColumns();
        _settingsLayout = layout;
        _pageBody!.Controls.Add(layout);
        RenderSelectedPreview();
    }
    private void BuildImageManagementPage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = Color.White, Margin = Padding.Empty, Padding = new Padding(4) };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 80));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

        var panels = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Color.White, Margin = Padding.Empty, Padding = Padding.Empty };
        panels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); panels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        TableLayoutPanel ImagePane()
        {
            var pane = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.White, Margin = new Padding(3), Padding = new Padding(8), BorderStyle = BorderStyle.None };
            pane.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); pane.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); pane.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            return pane;
        }
        var foregroundPane = ImagePane(); var backgroundPane = ImagePane();
        _imageDescription = new Label { Text = "前景", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(36, 52, 76), Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty };
        foregroundPane.Controls.Add(_imageDescription, 0, 0);        var backgroundHeader = new Label { Text = "背景 · 与当前状态对应，画面不形变", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(36, 52, 76), Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty };
        backgroundPane.Controls.Add(backgroundHeader, 0, 0);

        foreach (var list in new[] { _imageItems, _backgroundItems })
        {
            list.Dock = DockStyle.Fill; list.BackColor = Color.FromArgb(239, 245, 252); list.BorderStyle = BorderStyle.None; list.Padding = new Padding(7); list.Margin = new Padding(0, 2, 0, 2);
        }
        BuildImageTiles(); BuildBackgroundTiles();
        foregroundPane.Controls.Add(_imageItems, 0, 1); backgroundPane.Controls.Add(_backgroundItems, 0, 1);
        var foregroundFooter = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1, Margin = Padding.Empty };
        foregroundFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var backgroundFooter = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        backgroundFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 174)); backgroundFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _backgroundCarousel.Text = "输入时轮换背景"; _backgroundCarousel.ForeColor = Color.FromArgb(39, 57, 82); _backgroundCarousel.Dock = DockStyle.Fill; _backgroundCarousel.Margin = Padding.Empty;
        backgroundFooter.Controls.Add(_backgroundCarousel, 0, 0);
        var frontHint = new Label { Text = "当前状态单独设置 · 前景参与动效", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(105, 122, 146), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = Padding.Empty };
        foregroundFooter.Controls.Add(frontHint, 0, 0);
        var backHint = new Label { Text = "拖放图片导入 · 背景不参与形变", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(105, 122, 146), TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty };
        backgroundFooter.Controls.Add(backHint, 1, 0);
        foregroundPane.Controls.Add(foregroundFooter, 0, 2); backgroundPane.Controls.Add(backgroundFooter, 0, 2);
        panels.Controls.Add(foregroundPane, 0, 0); panels.Controls.Add(backgroundPane, 1, 0); layout.Controls.Add(panels, 0, 0);

        var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = new Padding(2, 4, 2, 0), Padding = Padding.Empty };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        FlowLayoutPanel ActionRow(Padding padding = default) => new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Padding = padding, Margin = Padding.Empty };
        var foregroundActions = ActionRow();
        var backgroundActions = ActionRow();
        var commonActions = ActionRow();
        var editButtons = new[]
        {
            ActionButton("添加前景", () => PickImages(_previewStage)),
            ActionButton("清空前景", () => ClearImages(_previewStage)),
            ActionButton("添加背景", PickBackgrounds),
            ActionButton("清空背景", ClearBackgrounds),
            ActionButton("前置", () => MoveSelectedImageToEdge(true)),
            ActionButton("后置", () => MoveSelectedImageToEdge(false)),
            ActionButton("3:4 裁切", CropSelectedImage),
            ActionButton("删除选中", RemoveSelectedLayerImage)
        };
        foreach (var button in editButtons) { button.Width = 110; button.Height = 30; button.Margin = new Padding(3, 2, 3, 2); }
        foregroundActions.Controls.AddRange(editButtons.Take(2).Cast<Control>().ToArray());
        backgroundActions.Controls.AddRange(editButtons.Skip(2).Take(2).Cast<Control>().ToArray());
        commonActions.Controls.AddRange(editButtons.Skip(4).Take(4).Cast<Control>().ToArray());
        _imageEditHint.Text = "选中图片后可用 Ctrl+X / Ctrl+C / Ctrl+V"; _imageEditHint.Margin = new Padding(8, 0, 0, 0); _imageEditHint.Width = 260;
        commonActions.Controls.Add(_imageEditHint);
        actions.Controls.Add(foregroundActions, 0, 0); actions.Controls.Add(backgroundActions, 1, 0);
        actions.Controls.Add(commonActions, 0, 1); actions.SetColumnSpan(commonActions, 2);
        layout.Controls.Add(actions, 0, 1);
        _imageLayout = layout;
        _pageBody!.Controls.Add(layout);
    }
    private void RefreshImageGroupNav()
    {
        if (_imageGroupNav is null) return;
        if (_imageGroupButtons.Count != _imageGroups.Count)
        {
            foreach (Control control in _imageGroupNav.Controls.Cast<Control>().ToArray()) { _imageGroupNav.Controls.Remove(control); control.Dispose(); }
            _imageGroupButtons.Clear();
            for (var i = 0; i < _imageGroups.Count; i++)
            {
                var index = i;
                var button = ActionButton(_imageGroups[i].Name, () => SelectImageGroup(index));
                button.Width = 108; button.Height = 36;
                button.Margin = new Padding(0, 0, 6, 0);
                _imageGroupButtons.Add(button);
                _imageGroupNav.Controls.Add(button);
            }
        }
        for (var i = 0; i < _imageGroupButtons.Count; i++)
        {
            _imageGroupButtons[i].Text = _imageGroups[i].Name;
            _imageGroupButtons[i].Type = i == _editingImageGroup ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            _imageGroupButtons[i].DefaultBack = i == _editingImageGroup ? Color.FromArgb(36, 117, 232) : Color.FromArgb(235, 240, 248);
            _imageGroupButtons[i].ForeColor = i == _editingImageGroup ? Color.White : Color.FromArgb(65, 80, 103);
            _imageGroupButtons[i].ForeHover = i == _editingImageGroup ? Color.White : Color.FromArgb(32, 52, 78);
            _imageGroupButtons[i].ForeActive = i == _editingImageGroup ? Color.White : Color.FromArgb(32, 52, 78);
            _imageGroupButtons[i].BackHover = i == _editingImageGroup ? Color.FromArgb(30, 104, 210) : Color.FromArgb(215, 227, 244);
            _imageGroupButtons[i].BackActive = i == _editingImageGroup ? Color.FromArgb(21, 87, 176) : Color.FromArgb(202, 219, 242);
        }
        if (_applyImageGroupButton is not null) _applyImageGroupButton.Text = "应用到当前设置组";
    }

    private void RefreshProfileNav()
    {
        if (_profileNav is null) return;
        if (_profileButtons.Count != _profiles.Count)
        {
            foreach (Control control in _profileNav.Controls.Cast<Control>().ToArray()) { _profileNav.Controls.Remove(control); control.Dispose(); }
            _profileButtons.Clear();
            for (var i = 0; i < _profiles.Count; i++)
            {
                var index = i;
                var button = ActionButton("", () => SwitchEditingProfile(index));
                button.Width = 108;
                button.Height = 36;
                button.Margin = new Padding(0, 0, 5, 0);
                _profileButtons.Add(button);
                _profileNav.Controls.Add(button);
            }
        }
        UpdateProfileButtons();
    }

    private void AddProfile()
    {
        if (_profiles.Count >= 10) { _hintLabel.Text = "设置组最多可创建 10 组。"; return; }
        CommitFocusedNumberInput();
        StoreEditingProfile();
        var nextThreshold = Math.Min(10_000_000, _profiles.Max(profile => profile.Threshold) + 500);
        _profiles.Add(new PetProfile { Name = $"设置组 {(char)('A' + _profiles.Count)}", Threshold = nextThreshold, ImageGroupId = _editingImageGroup });
        _editingProfile = _profiles.Count - 1;
        LoadEditingProfile(_editingProfile);
        RefreshProfileNav();
        RenderPage();
        MarkSettingsPending(); SaveSettings();
    }

    private void RemoveProfile()
    {
        if (_profiles.Count <= 1) { _hintLabel.Text = "至少保留一个设置组。"; return; }
        CommitFocusedNumberInput();
        StoreEditingProfile();
        var removed = _editingProfile;
        _profiles.RemoveAt(removed);
        for (var i = 0; i < _profiles.Count; i++) _profiles[i].Name = $"设置组 {(char)('A' + i)}";
        _editingProfile = Math.Min(removed, _profiles.Count - 1);
        _cycleSequence = NormalizeCycleSequence(_cycleSequence.Where(index => index != removed).Select(index => index > removed ? index - 1 : index), _profiles.Count);
        LoadEditingProfile(_editingProfile);
        RefreshProfileNav();
        UpdateSwitchModeButtons();
        RenderPage();
        MarkSettingsPending(); SaveSettings();
    }

    private TableLayoutPanel BuildStateButtons()
    {
        var row = CreateGridRow(12);
        row.Padding = new Padding(0, 2, 0, 2);
        var count = ImageGroupNames.Length;
        for (var i = 0; i < count; i++)
        {
            var stage = i;
            var button = new UiButton { Text = ImageGroupNames[i], Dock = DockStyle.Fill, Radius = 8, Margin = new Padding(2, 1, 2, 1), DefaultBack = i == _previewStage ? Color.FromArgb(222, 233, 250) : Color.FromArgb(246, 248, 252), ForeColor = Color.FromArgb(36, 52, 76), ForeHover = Color.FromArgb(36, 52, 76), ForeActive = Color.FromArgb(36, 52, 76), BackHover = Color.FromArgb(210, 226, 248), BackActive = Color.FromArgb(198, 217, 244), Tag = i };
            button.Click += (_, _) => SelectPreviewStage(stage);
            _stateButtons.Add(button);
            row.Controls.Add(button, i * 2, 0);
            row.SetColumnSpan(button, 2);
        }
        return row;
    }

    private static TableLayoutPanel CreateGridRow(int columnCount)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = columnCount,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        for (var column = 0; column < columnCount; column++)
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columnCount));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return panel;
    }

    private FlowLayoutPanel AddSettingSection(FlowLayoutPanel parent, string title, IEnumerable<Control> controls)
    {
        var content = controls.ToList();
        var section = new FlowLayoutPanel { Width = 330, Height = 30, AutoSize = false, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.FromArgb(241, 245, 250), Padding = new Padding(8, 6, 8, 6), Margin = new Padding(2, 2, 2, 4) };
        section.Controls.Add(new Label { Text = title, Width = 300, Height = 22, Font = new Font(Font, FontStyle.Bold), ForeColor = Color.FromArgb(24, 43, 76), Margin = Padding.Empty, AutoEllipsis = true });
        var height = 34;
        foreach (var control in content)
        {
            control.Margin = new Padding(0, 1, 0, 1);
            section.Controls.Add(control);
            height += control.Height + 2;
        }
        section.Height = height + section.Padding.Vertical;
        parent.Controls.Add(section);
        return section;
    }

    private Control Field(string label, Control control, int width = 300, int labelWidth = 112)
    {
        var row = new TableLayoutPanel { Width = width, Height = 34, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Tag = "field" };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(52, 67, 89), Margin = Padding.Empty, AutoEllipsis = true }, 0, 0);
        control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 2, 2, 2);
        row.Controls.Add(control, 1, 0);
        return row;
    }

    private static Control PlaceholderValue() => new Label
    {
        Text = "【占位符】", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = Color.FromArgb(119, 132, 151), BackColor = Color.FromArgb(232, 237, 244),
        BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(6, 0, 0, 0), Margin = Padding.Empty
    };
    private Control CompactField(string label, Control control)
    {
        var cell = new TableLayoutPanel { Width = 94, Height = 49, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        cell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cell.RowStyles.Add(new RowStyle(SizeType.Absolute, 18));
        cell.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
        cell.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(52, 67, 89), Margin = Padding.Empty }, 0, 0);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 1, 3, 2);
        cell.Controls.Add(control, 0, 1);
        return cell;
    }

    private void ConfigureEditorControls()
    {
        var ink = Color.FromArgb(30, 45, 68);
        var border = Color.FromArgb(175, 190, 211);
        var focus = Color.FromArgb(69, 115, 184);
        foreach (var number in new[] { _idleDelay, _holdDelay, _inputHold, _pauseDuration, _backspaceHold, _enterHold, _counterFactor, _counterSizePercent, _counterDistance, _counterTextOutlineWidth, _counterBorderWidth, _counterOpacity, _motionAmplitude, _animationSpeed, _scaleAmplitude, _swayAmplitude, _profileThreshold })
        {
            number.ForeColor = ink;
            number.BackColor = Color.White;
            number.BorderColor = border;
            number.BorderActive = focus;
            number.SelectionColor = Color.FromArgb(105, 147, 190, 245);
            number.EnabledValueTextChange = false;
            number.ShowControl = false;
            number.Height = 31;
            number.Leave += (_, _) => CommitNumberInput(number);
            number.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;
                CommitNumberInput(number);
                e.Handled = true;
                e.SuppressKeyPress = true;
            };
        }
        foreach (var select in new[] { _motionStyle, _profileImageGroup })
        {
            select.ForeColor = ink;
            select.BackColor = Color.White;
            select.BorderColor = border;
            select.BorderActive = focus;
            select.SelectionColor = Color.FromArgb(105, 147, 190, 245);
            select.Height = 34;
        }
        foreach (var toggle in new[] { _stageCarousel, _restartInputCarousel, _pinPet, _clickThrough, _backgroundCarousel, _startWithWindows, _counterVisibility })
        { toggle.Radius = 8; toggle.Height = 34; }
        foreach (var toggle in new[] { _counterOutline, _counterInMainLayer, _counterBorderFillEnabled }) { toggle.Radius = 8; toggle.Height = 30; }
        _stageCarousel.Width = 240;
        _stageCarousel.Margin = Padding.Empty;
        _restartInputCarousel.Width = 94;
        _pinPet.Width = 64; _pinPet.Margin = new Padding(0, 0, 4, 0);
        _clickThrough.Width = 64; _clickThrough.Margin = new Padding(0, 0, 4, 0);
        _backgroundCarousel.Width = 164;
        _startWithWindows.Width = 118;
        _togglePet.Width = 70;
        _togglePet.Height = 34;
        _togglePet.Margin = new Padding(0, 0, 0, 0);
        _togglePet.Text = "隐藏";
        _togglePet.ForeColor = ink;
        _togglePet.ForeHover = ink;
        _togglePet.ForeActive = ink;
        _togglePet.DefaultBack = Color.FromArgb(233, 240, 249);
        _togglePet.BackHover = Color.FromArgb(215, 229, 247);
        _togglePet.BackActive = Color.FromArgb(202, 219, 242);
        foreach (var button in new[] { _simpleModeButton, _advancedModeButton, _showCountButton, _showWordsButton })
        {
            button.Radius = 8; button.Height = 32; button.ForeColor = ink; button.ForeHover = ink; button.ForeActive = ink;
            button.DefaultBack = Color.FromArgb(233, 240, 249); button.BackHover = Color.FromArgb(215, 229, 247); button.BackActive = Color.FromArgb(202, 219, 242);
        }
        _showCountButton.Click += (_, _) => SetCounterDisplayMode(false);
        _showWordsButton.Click += (_, _) => SetCounterDisplayMode(true);
        _animationSpeed.ValueChanged += (_, _) => ConfigurePetMotion();
        _scaleAmplitude.ValueChanged += (_, _) => ConfigurePetMotion();
        _swayAmplitude.ValueChanged += (_, _) => ConfigurePetMotion();
        UpdateStateModeButtons();
        UpdateCounterDisplayButtons();
    }

    private void CommitFocusedNumberInput()
    {
        foreach (var number in new[] { _idleDelay, _holdDelay, _inputHold, _pauseDuration, _backspaceHold, _enterHold, _counterFactor, _counterSizePercent, _counterDistance, _counterTextOutlineWidth, _counterBorderWidth, _counterOpacity, _motionAmplitude, _animationSpeed, _scaleAmplitude, _swayAmplitude, _profileThreshold })
            if (number.ContainsFocus) CommitNumberInput(number);
    }

    private static void CommitNumberInput(UiNumber number)
    {
        var raw = number.Text.Trim();
        var parsed = decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var value)
            || decimal.TryParse(raw, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value);
        if (!parsed) value = number.Minimum ?? 0;
        value = Math.Clamp(value, number.Minimum ?? decimal.MinValue, number.Maximum ?? decimal.MaxValue);
        value = decimal.Round(value, number.DecimalPlaces, MidpointRounding.AwayFromZero);
        number.Value = value;
        number.Text = value.ToString(number.DecimalPlaces > 0 ? $"F{number.DecimalPlaces}" : "0", System.Globalization.CultureInfo.CurrentCulture);
    }

    private Control SliderNumberRow(string label, UiNumber number, TrackBar slider, Func<int, decimal>? fromSlider = null, Func<decimal, int>? toSlider = null)
    {
        fromSlider ??= value => value;
        toSlider ??= value => (int)Math.Round(value);
        var row = new TableLayoutPanel { Height = 30, MinimumSize = new Size(200, 30), ColumnCount = 3, RowCount = 1, Margin = new Padding(0, 1, 0, 1), Padding = Padding.Empty, BackColor = Color.Transparent, Tag = "fit" };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        row.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(42, 61, 89), AutoEllipsis = true, Margin = Padding.Empty }, 0, 0);
        slider.Dock = DockStyle.Fill; slider.AutoSize = false; slider.Height = 26; slider.Margin = new Padding(2, 0, 3, 0);
        number.Dock = DockStyle.Fill; number.Width = 86; number.Height = 28; number.Margin = Padding.Empty;
        void SyncSlider() { var next = Math.Clamp(toSlider(number.Value), slider.Minimum, slider.Maximum); if (slider.Value != next) slider.Value = next; }
        number.ValueChanged += (_, _) => SyncSlider();
        slider.ValueChanged += (_, _) => { var next = Math.Clamp(fromSlider(slider.Value), number.Minimum ?? decimal.MinValue, number.Maximum ?? decimal.MaxValue); if (number.Value != next) number.Value = next; };
        row.Controls.Add(slider, 1, 0); row.Controls.Add(number, 2, 0);
        SyncSlider();
        return row;
    }

    private Control CounterValueActionRow(string label, Control value, UiButton button)
    {
        var row = new TableLayoutPanel { Width = 300, Height = 34, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty, Tag = "fit" };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        row.Controls.Add(new Label { Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(52, 67, 89), AutoEllipsis = true, Margin = Padding.Empty }, 0, 0);
        value.Dock = DockStyle.Fill; value.Margin = new Padding(0, 2, 4, 2);
        button.Dock = DockStyle.Fill; button.Height = 30; button.Margin = new Padding(2, 2, 0, 2);
        row.Controls.Add(value, 1, 0); row.Controls.Add(button, 2, 0);
        return row;
    }

    private Control CounterDisplayModeRow()
    {
        var row = new TableLayoutPanel { Width = 300, Height = 34, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty, Tag = "fit" };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        row.Controls.Add(new Label { Text = "计数操作", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(52, 67, 89), Margin = Padding.Empty }, 0, 0);
        _showCountButton.Dock = DockStyle.Fill; _showCountButton.Margin = new Padding(0, 1, 3, 1);
        _showWordsButton.Dock = DockStyle.Fill; _showWordsButton.Margin = new Padding(3, 1, 0, 1);
        row.Controls.Add(_showCountButton, 1, 0); row.Controls.Add(_showWordsButton, 2, 0);
        return row;
    }

    private void BuildCounterAppearancePage()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Top, Height = 280, ColumnCount = 12, RowCount = 1, BackColor = Color.White, Padding = new Padding(10), Margin = Padding.Empty };
        for (var column = 0; column < 12; column++) layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 12));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 254));

        var windowCard = CounterCard("窗口", "显示、字体、尺寸与透明度", 5, out var windowFields);
        windowFields.Controls.Add(_counterVisibility, 0, 0);
        windowFields.Controls.Add(CounterFontButton(), 0, 1);
        windowFields.Controls.Add(FillSlider("大小 %", _counterSizePercent, _counterSizeSlider), 0, 2);
        windowFields.Controls.Add(FillSlider("距离 px", _counterDistance, _counterDistanceSlider), 0, 3);
        windowFields.Controls.Add(FillSlider("整体透明度", _counterOpacity, _counterOpacitySlider), 0, 4);
        _counterVisibility.Text = "显示计数";
        _counterVisibility.Dock = DockStyle.Fill;
        _counterVisibility.Margin = new Padding(1, 2, 1, 2);
        _toolTips.SetToolTip(_counterVisibility, "立即显示或隐藏桌宠数字，并保存此开关状态。");

        var textCard = CounterCard("数字", "字体填充与外线", 3, out var textFields);
        textFields.Controls.Add(ColorPickerRow("填充色", () => _counterTextFill, color => { _counterTextFill = color; PreviewCounterAppearance(); }), 0, 0);
        textFields.Controls.Add(ColorPickerRow("外线色", () => _counterTextOutline, color => { _counterTextOutline = color; PreviewCounterAppearance(); }), 0, 1);
        textFields.Controls.Add(FillSlider("外线粗细", _counterTextOutlineWidth, _counterTextOutlineSlider), 0, 2);

        var borderCard = CounterCard("边框", "边框显示、颜色与底色", 5, out var borderFields);
        _counterOutline.Text = "显示边框";
        _counterOutline.Dock = DockStyle.Fill;
        _counterBorderFillEnabled.Dock = DockStyle.Fill;
        borderFields.Controls.Add(_counterOutline, 0, 0);
        borderFields.Controls.Add(ColorPickerRow("边框色", () => _counterBorderColor, color => { _counterBorderColor = color; PreviewCounterAppearance(); }), 0, 1);
        borderFields.Controls.Add(_counterBorderFillEnabled, 0, 2);
        borderFields.Controls.Add(ColorPickerRow("底色", () => _counterBorderFill, color => { _counterBorderFill = color; PreviewCounterAppearance(); }), 0, 3);
        borderFields.Controls.Add(FillSlider("边框粗细", _counterBorderWidth, _counterBorderWidthSlider), 0, 4);
        _counterBorderFillEnabled.CheckedChanged += (_, _) => { if (!_switchingProfile) { PreviewCounterAppearance(); SaveSettings(); } };

        layout.Controls.Add(windowCard, 0, 0); layout.SetColumnSpan(windowCard, 4);
        layout.Controls.Add(textCard, 4, 0); layout.SetColumnSpan(textCard, 4);
        layout.Controls.Add(borderCard, 8, 0); layout.SetColumnSpan(borderCard, 4);
        _counterLayout = layout;
        _pageBody!.Controls.Add(layout);

        Control FillSlider(string label, UiNumber number, TrackBar slider)
        {
            var control = SliderNumberRow(label, number, slider);
            control.Dock = DockStyle.Fill;
            return control;
        }

        Control CounterFontButton()
        {
            var button = ActionButton($"字体：{_counterFontFamily}", () => { });
            button.Click += (_, _) =>
            {
                using var currentFont = new Font(_counterFontFamily, 14, _counterFontStyle);
                using var dialog = new FontDialog
                {
                    Font = currentFont,
                    ShowEffects = false,
                    ShowColor = false,
                    FontMustExist = true
                };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                _counterFontFamily = dialog.Font.FontFamily.Name;
                _counterFontStyle = dialog.Font.Style;
                button.Text = $"字体：{_counterFontFamily}";
                PreviewCounterAppearance();
                SaveSettings();
            };
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(1, 2, 1, 2);
            return button;
        }

        Control ColorPickerRow(string labelText, Func<Color> getColor, Action<Color> setColor)
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 1, 0, 1), Padding = Padding.Empty };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            row.Controls.Add(new Label { Text = labelText, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(42, 61, 89), Margin = Padding.Empty }, 0, 0);
            var colorButton = ActionButton("", () => { });
            colorButton.Dock = DockStyle.Fill;
            colorButton.Margin = new Padding(2, 2, 1, 2);
            void UpdateColorButton()
            {
                var color = getColor();
                colorButton.Text = color.IsEmpty ? "透明" : $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                colorButton.DefaultBack = color.IsEmpty ? Color.FromArgb(233, 240, 249) : color;
                var contrast = color.IsEmpty || color.GetBrightness() > 0.58F ? Color.FromArgb(30, 45, 68) : Color.White;
                colorButton.ForeColor = contrast; colorButton.ForeHover = contrast; colorButton.ForeActive = contrast;
            }
            UpdateColorButton();
            colorButton.Click += (_, _) =>
            {
                using var dialog = new ColorDialog { Color = getColor().IsEmpty ? Color.White : getColor(), FullOpen = true, AnyColor = true };
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                setColor(dialog.Color);
                UpdateColorButton();
                SaveSettings();
            };
            row.Controls.Add(colorButton, 1, 0);
            return row;
        }

        Panel CounterCard(string title, string description, int rowCount, out TableLayoutPanel fields)
        {
            var card = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(239, 244, 251), Margin = new Padding(4), Padding = new Padding(10) };
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            content.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font(Font, FontStyle.Bold), ForeColor = Color.FromArgb(31, 50, 76), Margin = Padding.Empty }, 0, 0);
            content.Controls.Add(new Label { Text = description, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, ForeColor = Color.FromArgb(103, 116, 137), Margin = Padding.Empty }, 0, 1);
            fields = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = rowCount, GrowStyle = TableLayoutPanelGrowStyle.FixedSize, BackColor = Color.Transparent, Margin = Padding.Empty, Padding = Padding.Empty };
            fields.RowStyles.Clear();
            for (var i = 0; i < rowCount; i++) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.Controls.Add(fields, 0, 2);
            card.Controls.Add(content);
            return card;
        }
    }

    private void SetStateMode(bool advanced)
    {
        if (_advancedMode.Checked == advanced) return;
        _advancedMode.Checked = advanced;
    }

    private void UpdateStateModeButtons()
    {
        SetChoiceButton(_simpleModeButton, !_advancedMode.Checked);
        SetChoiceButton(_advancedModeButton, _advancedMode.Checked);
    }

    private void SetCounterDisplayMode(bool showWords)
    {
        if (_showWordsOnPet == showWords) return;
        _showWordsOnPet = showWords;
        UpdateCounterDisplayButtons();
        _petWindow?.SetCounter(DisplayedCounterValue);
        UpdateRuntimeStatus();
        MarkSettingsPending(); SaveSettings();
    }

    private void UpdateCounterDisplayButtons()
    {
        SetChoiceButton(_showCountButton, !_showWordsOnPet);
        SetChoiceButton(_showWordsButton, _showWordsOnPet);
    }

    private static void SetChoiceButton(UiButton button, bool selected)
    {
        button.Type = selected ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
        button.DefaultBack = selected ? Color.FromArgb(31, 112, 232) : Color.FromArgb(233, 240, 249);
        button.BackHover = selected ? Color.FromArgb(26, 99, 211) : Color.FromArgb(215, 229, 247);
        button.BackActive = selected ? Color.FromArgb(21, 87, 188) : Color.FromArgb(202, 219, 242);
        button.ForeColor = selected ? Color.White : Color.FromArgb(30, 45, 68);
        button.ForeHover = button.ForeColor; button.ForeActive = button.ForeColor;
    }

    private UiButton ActionButton(string text, Action action)
    {
        var width = Math.Max(76, TextRenderer.MeasureText(text, Font).Width + 26);
        var button = new UiButton { Text = text, Width = width, Height = 34, Radius = 8, Margin = new Padding(0, 0, 8, 0), DefaultBack = Color.FromArgb(233, 240, 249), ForeColor = Color.FromArgb(30, 45, 68), ForeHover = Color.FromArgb(30, 45, 68), ForeActive = Color.FromArgb(30, 45, 68), BackHover = Color.FromArgb(215, 229, 247), BackActive = Color.FromArgb(202, 219, 242) };
        button.Click += (_, _) => action(); return button;
    }

    private void PreviewPlaceholderPaint(object? sender, PaintEventArgs e)
    {
        if (_previewImage.Image is not null) return;
        TextRenderer.DrawText(e.Graphics, _previewText.Text, new Font(Font.FontFamily, 25, FontStyle.Bold), _previewImage.ClientRectangle, Color.FromArgb(92, 111, 145), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }

    private void ImageListDragEnter(object? sender, DragEventArgs e) { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; }
    private void ImageListDragDrop(object? sender, DragEventArgs e) { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files) { SetActiveImageLayer(ImageLayer.Foreground); AddImages(_previewStage, files); } }
    private void BackgroundListDragDrop(object? sender, DragEventArgs e) { if (e.Data?.GetData(DataFormats.FileDrop) is string[] files) { SetActiveImageLayer(ImageLayer.Background); AddBackgrounds(_previewStage, files); } }

    private List<string> ActiveImages(int stage) => _activeImageLayer == ImageLayer.Foreground ? _images[stage] : _backgrounds[stage];
    private int ActiveImageIndex
    {
        get => _activeImageLayer == ImageLayer.Foreground ? _selectedImageIndex : _selectedBackgroundIndex;
        set { if (_activeImageLayer == ImageLayer.Foreground) _selectedImageIndex = value; else _selectedBackgroundIndex = value; }
    }

    private void SetActiveImageLayer(ImageLayer layer)
    {
        _activeImageLayer = layer;
        _imageEditHint.Text = $"当前：{(layer == ImageLayer.Foreground ? "前景" : "背景")} · 剪切 Ctrl+X / 复制 Ctrl+C / 粘贴 Ctrl+V";
    }

    private void ResetLayerPlayback(int stage, ImageLayer layer)
    {
        if (layer == ImageLayer.Foreground) { _imageIndices[stage] = 0; _hasTriggered[stage] = false; }
        else { _backgroundIndices[stage] = 0; _backgroundHasTriggered[stage] = false; }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.S))
        {
            CommitFocusedNumberInput();
            _applySettingsButton?.Focus();
            ApplySettings();
            return true;
        }
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
        var paths = ActiveImages(_previewStage);
        var index = ActiveImageIndex;
        if (index < 0 || index >= paths.Count)
        {
            _hintLabel.Text = "请先选择一张图片。";
            return;
        }
        var path = paths[index];
        var entry = new ImageClipboardEntry(Guid.NewGuid().ToString("N"), _imageGroups[_editingImageGroup].Id, _previewStage, _activeImageLayer, index, path, cut);
        try
        {
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { path });
            data.SetData(ImageClipboardFormat, entry.Token);
            Clipboard.SetDataObject(data, true);
            _imageClipboard = entry;
            _hintLabel.Text = cut ? "已剪切选中图片；切换图片组或状态后粘贴即可移动。" : "已复制选中图片；切换图片组或状态后可粘贴。";
            if (_activeImageLayer == ImageLayer.Foreground) BuildImageTiles(); else BuildBackgroundTiles();
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
                if (_activeImageLayer == ImageLayer.Foreground) AddImages(_previewStage, files);
                else AddBackgrounds(_previewStage, files);
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
        var target = ActiveImages(_previewStage);
        var sourceGroupIndex = _imageGroups.FindIndex(group => group.Id == entry.GroupId);
        List<string>? source = null;
        var sourceIndex = -1;
        if (sourceGroupIndex >= 0)
        {
            source = sourceGroupIndex == _editingImageGroup
                ? entry.Layer == ImageLayer.Foreground ? _images[entry.Stage] : _backgrounds[entry.Stage]
                : (entry.Layer == ImageLayer.Foreground ? _imageGroups[sourceGroupIndex].Images : _imageGroups[sourceGroupIndex].Backgrounds).GetValueOrDefault(entry.Stage);
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
                ResetLayerPlayback(entry.Stage, entry.Layer);
            }
            else
            {
                var group = _imageGroups[sourceGroupIndex];
                if (entry.Layer == ImageLayer.Foreground) { group.ImageIndices[entry.Stage] = 0; group.HasTriggered[entry.Stage] = false; }
                else { group.BackgroundIndices[entry.Stage] = 0; group.BackgroundHasTriggered[entry.Stage] = false; }
            }
        }
        target.Add(entry.Path);
        ActiveImageIndex = target.Count - 1;
        ResetLayerPlayback(_previewStage, _activeImageLayer);
        _imageClipboard = entry with { Cut = false, GroupId = _imageGroups[_editingImageGroup].Id, Stage = _previewStage, Layer = _activeImageLayer, Index = ActiveImageIndex };
        MarkSettingsPending(); SaveSettings(); RenderPage();
        _hintLabel.Text = entry.Cut ? "图片已移动到当前状态末尾；点击全局设置中的“保存并应用”后生效。" : "图片已复制到当前状态末尾；点击全局设置中的“保存并应用”后生效。";
    }

    private void PasteBitmap(IDataObject data)
    {
        var maximum = _advancedMode.Checked ? 20 : 3;
        var images = ActiveImages(_previewStage);
        if (images.Count >= maximum)
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
        images.Add(destination);
        _imageDisplayNames[destination] = "剪贴板图片.png";
        ActiveImageIndex = images.Count - 1;
        ResetLayerPlayback(_previewStage, _activeImageLayer);
        MarkSettingsPending(); SaveSettings(); RenderPage();
        _hintLabel.Text = "已粘贴剪贴板图片；点击全局设置中的“保存并应用”后生效。";
    }

    private void MoveImageToEdge(bool first)
    {
        var list = _images[_previewStage];
        var index = _selectedImageIndex;
        if (index < 0 || index >= list.Count) return;
        var target = first ? 0 : list.Count - 1;
        if (index == target) return;
        var path = list[index];
        list.RemoveAt(index);
        list.Insert(target, path);
        _selectedImageIndex = target;
        MarkSettingsPending(); SaveSettings(); RenderPage();
        _hintLabel.Text = first ? "选中图片已前置到首位。" : "选中图片已后置到末位。";
    }

    private void MoveSelectedImageToEdge(bool first)
    {
        if (ActiveImageIndex < 0 || ActiveImageIndex >= ActiveImages(_previewStage).Count) { _hintLabel.Text = "请先在前景或背景区选择一张图片。"; return; }
        if (_activeImageLayer == ImageLayer.Foreground) MoveImageToEdge(first);
        else MoveBackgroundToEdge(first);
    }

    private void RemoveSelectedLayerImage()
    {
        if (ActiveImageIndex < 0 || ActiveImageIndex >= ActiveImages(_previewStage).Count) { _hintLabel.Text = "请先在前景或背景区选择一张图片。"; return; }
        if (_activeImageLayer == ImageLayer.Foreground) RemoveSelectedImage(_previewStage);
        else RemoveSelectedBackgroundImage();
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
        var images = ActiveImages(stage);
        var index = ActiveImageIndex;
        if (index < 0 || index >= images.Count)
        {
            _hintLabel.Text = "请先在图片组中选择要裁切的图片。";
            return;
        }
        try
        {
            var path = images[index];
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
            _imageDisplayNames[destination] = Path.GetFileNameWithoutExtension(DisplayName(path)) + "（裁切）.png";
            images[index] = destination;
            ResetLayerPlayback(stage, _activeImageLayer);
            MarkSettingsPending(); SaveSettings(); RenderPage();
            _hintLabel.Text = $"已裁切为竖版 3:4（{crop.Width}×{crop.Height}），原图保留；点击全局设置中的“保存并应用”后桌宠使用新图。";
        }
        catch (Exception ex)
        {
            _hintLabel.Text = "裁切失败：" + ex.Message;
        }
    }

    private void SelectPreviewStage(int stage)
    {
        if (stage < 0 || stage >= ImageGroupNames.Length) return;
        _previewStage = stage;
        _selectedImageIndex = _images[stage].Count == 0 ? -1 : Math.Clamp(_selectedImageIndex, 0, _images[stage].Count - 1);
        _selectedBackgroundIndex = _backgrounds[stage].Count == 0 ? -1 : Math.Clamp(_selectedBackgroundIndex, 0, _backgrounds[stage].Count - 1);
        RenderPage();
    }

    private void PickImages(int stage)
    {
        SetActiveImageLayer(ImageLayer.Foreground);
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
                _imageDisplayNames[destination] = Path.GetFileName(file);
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
            ? $"已导入图片并暂存；跳过 {unsupported.Count} 个不支持或损坏的文件。点击全局设置中的“保存并应用”后桌宠使用新图片。"
            : $"已导入 {_images[stage].Count} 张到“{ImageGroupNames[stage]}”；点击全局设置中的“保存并应用”后桌宠使用新图片。";
    }

    private void ClearImages(int stage)
    {
        SetActiveImageLayer(ImageLayer.Foreground);
        _images[stage].Clear(); _imageIndices[stage] = 0; _hasTriggered[stage] = false; _selectedImageIndex = -1;
        RenderPage(); MarkSettingsPending(); SaveSettings();
        if (ImageStateForRuntimeStage(_currentStage) == stage) UpdatePetWindow();
    }

    private void PickBackgrounds()
    {
        SetActiveImageLayer(ImageLayer.Background);
        using var dialog = new OpenFileDialog
        {
            Title = $"选择{ImageGroupNames[_previewStage]}背景（可多选）",
            Filter = "支持的图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.ico|所有文件|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) AddBackgrounds(_previewStage, dialog.FileNames);
    }

    private void AddBackgrounds(int stage, IEnumerable<string> files)
    {
        var maximum = _advancedMode.Checked ? 20 : 3;
        var room = Math.Max(0, maximum - _backgrounds[stage].Count);
        if (room == 0) { _hintLabel.Text = $"该状态最多保存 {maximum} 张背景。"; return; }
        var added = 0;
        var skipped = 0;
        foreach (var file in files)
        {
            if (added >= room) { skipped++; continue; }
            if (!AllowedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant())) { skipped++; continue; }
            try
            {
                using var decoded = Image.FromFile(file);
                var destination = Path.Combine(_assetsDir, Guid.NewGuid().ToString("N") + Path.GetExtension(file).ToLowerInvariant());
                File.Copy(file, destination, false);
                _backgrounds[stage].Add(destination);
                _imageDisplayNames[destination] = Path.GetFileName(file);
                added++;
            }
            catch { skipped++; }
        }
        if (added == 0) { _hintLabel.Text = "没有导入背景。支持 PNG、JPG/JPEG、BMP、GIF、TIF/TIFF、ICO。"; return; }
        _selectedBackgroundIndex = _backgrounds[stage].Count - 1;
        _backgroundIndices[stage] = 0;
        _backgroundHasTriggered[stage] = false;
        MarkSettingsPending(); SaveSettings(); RenderPage();
        _hintLabel.Text = $"已导入 {added} 张背景" + (skipped > 0 ? $"；跳过 {skipped} 个文件。" : "；点击全局设置中的“保存并应用”后生效。");
    }

    private void MoveBackgroundToEdge(bool first)
    {
        var list = _backgrounds[_previewStage];
        var index = _selectedBackgroundIndex;
        if (index < 0 || index >= list.Count) return;
        var target = first ? 0 : list.Count - 1;
        if (index == target) return;
        var path = list[index]; list.RemoveAt(index); list.Insert(target, path);
        _selectedBackgroundIndex = target;
        _backgroundIndices[_previewStage] = 0;
        _backgroundHasTriggered[_previewStage] = false;
        MarkSettingsPending(); SaveSettings(); RenderPage();
    }

    private void RemoveSelectedBackgroundImage()
    {
        var list = _backgrounds[_previewStage];
        if (_selectedBackgroundIndex < 0 || _selectedBackgroundIndex >= list.Count) return;
        list.RemoveAt(_selectedBackgroundIndex);
        _selectedBackgroundIndex = list.Count == 0 ? -1 : Math.Min(_selectedBackgroundIndex, list.Count - 1);
        _backgroundIndices[_previewStage] = 0;
        _backgroundHasTriggered[_previewStage] = false;
        MarkSettingsPending(); SaveSettings(); RenderPage();
    }

    private void ClearBackgrounds()
    {
        SetActiveImageLayer(ImageLayer.Background);
        _backgrounds[_previewStage].Clear();
        _selectedBackgroundIndex = -1;
        _backgroundIndices[_previewStage] = 0;
        _backgroundHasTriggered[_previewStage] = false;
        MarkSettingsPending(); SaveSettings(); RenderPage();
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
        _selectedLabel.Text = $"{_imageGroups.ElementAtOrDefault(_editingImageGroup)?.Name ?? "图片组"} · {ImageGroupNames[_previewStage]} · 前景 {_images[_previewStage].Count} / 背景 {_backgrounds[_previewStage].Count}";
        _previewText.Text = ImageGroupNames[_previewStage];
        var list = _images[_previewStage];
        var backgrounds = _backgrounds[_previewStage];
        var frontPath = list.Count == 0 ? null : list[_stageCarousel.Checked ? Math.Abs(_imageIndices[_previewStage]) % list.Count : 0];
        var backgroundPath = backgrounds.Count == 0 ? null : backgrounds[Math.Clamp(_backgroundIndices[_previewStage], 0, backgrounds.Count - 1)];
        if (backgroundPath is null) { SetPicture(_previewImage, frontPath); return; }
        var old = _previewImage.Image;
        _previewImage.Image = null;
        old?.Dispose();
        try
        {
            var preview = new Bitmap(320, 320, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(preview))
            {
                graphics.Clear(Color.Transparent);
                DrawPreviewLayer(graphics, backgroundPath, preview.Size);
                if (frontPath is not null) DrawPreviewLayer(graphics, frontPath, preview.Size);
            }
            _previewImage.Image = preview;
        }
        catch { SetPicture(_previewImage, frontPath); }
        _previewImage.Invalidate();
    }

    private static void DrawPreviewLayer(Graphics graphics, string path, Size bounds)
    {
        using var source = Image.FromFile(path);
        var scale = Math.Min((double)bounds.Width / source.Width, (double)bounds.Height / source.Height);
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.DrawImage(source, (bounds.Width - width) / 2, (bounds.Height - height) / 2, width, height);
    }

    private void StartKeyboardInput()
    {
        try
        {
            _keyboard = new RawKeyboardInput();
            _keyboard.ActionReceived += HandleKeyboardAction;
            _hintLabel.Text = "监听可打印键、空格、回车与回退键；计数不读取键入内容。今日词数按空格/回车结算。";
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
                if (_counterVisibility.Checked != _loadedCounterEnabled) _counterVisibility.Checked = _loadedCounterEnabled;
                SaveSettings();
            };
            _petWindow.SetPinned(_appliedPinned);
            _petWindow.SetClickThrough(_appliedClickThrough);
            _petWindow.ConfigureMotion(_appliedRuntime.ShapeMode, _appliedRuntime.Amplitude, _appliedRuntime.AnimationSpeed, _appliedRuntime.ScaleAmplitude, _appliedRuntime.SwayAmplitude);
            ApplyRuntimeCounterAppearance();
            _petWindow.ExitRequested += ExitApplication;
            _petWindow.FormClosed += (_, _) => _togglePet.Text = "显示";
            _petWindow.CompanionHidden += () => _togglePet.Text = "显示";
            _petWindow.OpenSettingsRequested += ShowSettings;
        }
        _petWindow.ShowCompanion();
        _togglePet.Text = "隐藏";
        UpdatePetWindow();
    }

    private void TogglePetVisibility()
    {
        if (_petWindow?.Visible == true)
        {
            _petWindow.HideCompanion();
            _togglePet.Text = "显示";
        }
        else EnsurePetVisible();
    }

    private void InitializeTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("打开设置", null, (_, _) => ShowSettings());
        menu.Items.Add("完全退出", null, (_, _) => ExitApplication());
        _trayIcon = new NotifyIcon { Text = "打字小伴侣", Icon = Icon, ContextMenuStrip = menu, Visible = true };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();
    }

    private void ShowSettings()
    {
        CommitFocusedNumberInput();
        StoreEditingProfile();
        _editingProfile = Math.Clamp(_runtimeProfile, 0, _profiles.Count - 1);
        LoadEditingProfile(_editingProfile);
        var activeGroup = _appliedProfiles.Count == 0
            ? _profiles[_editingProfile].ImageGroupId
            : _appliedProfiles[Math.Clamp(_runtimeProfile, 0, _appliedProfiles.Count - 1)].ImageGroupId;
        LoadImageGroup(Math.Clamp(activeGroup, 0, _imageGroups.Count - 1), saveCurrent: false);
        _previewStage = 2; // Image-group order: idle, pause, typing.
        RenderPage();
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
        SaveCounter();
        SaveDailyStatistics();
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
        if (_cycleEditorOpen) return;
        _capsOn = capsOn;
        switch (action)
        {
            case KeyboardAction.ShiftDown:
                _shiftDown = true;
                _lastActivityWasTyping = true;
                _lastKeyAt = DateTime.UtcNow; _inputPulseUntil = _lastKeyAt.AddMilliseconds(_appliedRuntime.InputHold); _transientUntil = DateTime.MinValue;
                ActivateStageWithoutCycling(0); _petWindow?.Bump(); return;
            case KeyboardAction.ShiftUp:
                _shiftDown = false;
                _lastKeyAt = DateTime.UtcNow;
                _inputPulseUntil = _lastKeyAt.AddMilliseconds(_appliedRuntime.InputHold);
                UpdatePetWindow(); return;
            case KeyboardAction.EnterDown:
                CompleteCurrentWord();
                _enterArtworkProfileOverride = _runtimeProfile;
                AdvanceCycleOnEnter();
                _lastActivityWasTyping = true; _enterHeld = true;
                _lastKeyAt = DateTime.UtcNow; _transientUntil = DateTime.MinValue; ActivateStage(3); _petWindow?.Bump();
                UpdateRuntimeStatus();
                if (_appliedSwitchMode == GroupSwitchMode.EnterCycle) SaveSettings();
                return;
            case KeyboardAction.EnterUp:
                _enterHeld = false; _lastActivityWasTyping = true; _lastKeyAt = DateTime.UtcNow;
                _transientUntil = _lastKeyAt.AddMilliseconds(_appliedRuntime.EnterHold); ActivateStageWithoutCycling(5); return;
            case KeyboardAction.BackspaceDown:
                _inputCount++; _countDirty = true; _todayInputCount++; _dailyStatsDirty = true; UpdateDailyCounterValues(); SelectProfileForCount(); _petWindow?.SetCounter(DisplayedCounterValue);
                _lastActivityWasTyping = true; _backspaceHeld = true;
                _lastKeyAt = DateTime.UtcNow; _transientUntil = DateTime.MinValue; ActivateStage(2); _petWindow?.Bump(); return;
            case KeyboardAction.BackspaceUp:
                _backspaceHeld = false; _lastActivityWasTyping = true; _lastKeyAt = DateTime.UtcNow;
                _transientUntil = _lastKeyAt.AddMilliseconds(_appliedRuntime.BackspaceHold); ActivateStageWithoutCycling(4); return;
            case KeyboardAction.CapsChanged:
                _lastActivityWasTyping = true;
                _lastKeyAt = DateTime.UtcNow;
                _transientUntil = _lastKeyAt.AddMilliseconds(_appliedRuntime.HoldDelay);
                ActivateStage(7);
                _petWindow?.Bump(); return;
            case KeyboardAction.Typing:
            case KeyboardAction.Space:
                var isSpace = action == KeyboardAction.Space;
                _lastActivityWasTyping = true;
                _inputCount++; _countDirty = true; _todayInputCount++; _dailyStatsDirty = true;
                if (isSpace) CompleteCurrentWord(); else _wordHasInput = true;
                UpdateDailyCounterValues();
                SelectProfileForCount();
                _petWindow?.SetCounter(DisplayedCounterValue);
                _lastKeyAt = DateTime.UtcNow; _inputPulseUntil = _lastKeyAt.AddMilliseconds(_appliedRuntime.InputHold); _transientUntil = DateTime.MinValue; ActivateStage(0); _petWindow?.Bump(); return;
        }
    }

    private void ActivateStage(int stage)
    {
        if (stage is not (3 or 5)) _enterArtworkProfileOverride = null;
        var imageState = ImageStateForRuntimeStage(stage);
        var profileIndex = stage is 3 or 5 && _enterArtworkProfileOverride.HasValue
            ? _enterArtworkProfileOverride.Value
            : _runtimeProfile;
        var group = _appliedImageGroups[Math.Clamp(_appliedProfiles[profileIndex].ImageGroupId, 0, _appliedImageGroups.Count - 1)];
        var images = group.Images.TryGetValue(imageState, out var list) ? list : [];
        if (!group.ImageIndices.ContainsKey(imageState)) group.ImageIndices[imageState] = 0;
        if (!group.HasTriggered.ContainsKey(imageState)) group.HasTriggered[imageState] = false;
        var restartingInput = stage == 0 && _appliedCarouselEnabled && _appliedRestartInputCarouselFromFirst && _currentStage != 0;
        if (restartingInput)
            group.ImageIndices[imageState] = 0;
        else if (group.HasTriggered[imageState] && _appliedCarouselEnabled && images.Count > 1)
            group.ImageIndices[imageState] = (group.ImageIndices[imageState] + 1) % images.Count;
        else if (!group.HasTriggered[imageState]) group.ImageIndices[imageState] = 0;
        group.HasTriggered[imageState] = true;
        var backgrounds = group.Backgrounds.TryGetValue(imageState, out var backgroundList) ? backgroundList : [];
        if (!group.BackgroundIndices.ContainsKey(imageState)) group.BackgroundIndices[imageState] = 0;
        if (!group.BackgroundHasTriggered.ContainsKey(imageState)) group.BackgroundHasTriggered[imageState] = false;
        if (stage is not (4 or 5))
        {
            if (group.BackgroundCarouselModes.TryGetValue(imageState, out var rotateBackground) && rotateBackground && backgrounds.Count > 1)
            {
                if (group.BackgroundHasTriggered[imageState]) group.BackgroundIndices[imageState] = (group.BackgroundIndices[imageState] + 1) % backgrounds.Count;
                else group.BackgroundIndices[imageState] = 0;
            }
            else group.BackgroundIndices[imageState] = 0;
            group.BackgroundHasTriggered[imageState] = true;
        }
        _currentStage = stage;
        UpdatePetWindow();
    }

    private void UpdateState()
    {
        var now = DateTime.UtcNow;
        if (_countDirty && now - _lastCountSaveAt >= TimeSpan.FromSeconds(2)) SaveCounter();
        EnsureDailyStatisticsDate();
        if (_dailyStatsDirty && now - _lastDailyStatsSaveAt >= TimeSpan.FromSeconds(2)) SaveDailyStatistics();
        if ((_currentStage == 2 && _backspaceHeld) || (_currentStage == 3 && _enterHeld)) return;
        if (_currentStage != 0 && _transientUntil > now) return;

        if (_currentStage == 6)
        {
            if (_transientUntil > now) return;
            _lastActivityWasTyping = false;
            _inputPulseUntil = DateTime.MinValue;
            _transientUntil = DateTime.MinValue;
            ActivateStage(1);
            return;
        }

        if (_lastActivityWasTyping && _lastKeyAt != DateTime.MinValue)
        {
            var inputCompleteAt = _lastKeyAt.AddMilliseconds(_appliedRuntime.IdleDelay);
            if (_inputPulseUntil > inputCompleteAt) inputCompleteAt = _inputPulseUntil;
            if (_transientUntil > inputCompleteAt) inputCompleteAt = _transientUntil;
            if (now < inputCompleteAt) return;

            // Enter, Backspace and Caps each finish their own input-state artwork;
            // never insert the generic input image between that artwork and Pause/Idle.
            _lastActivityWasTyping = false;
            _inputPulseUntil = DateTime.MinValue;
            _transientUntil = DateTime.MinValue;
            if (_appliedRuntime.Advanced)
            {
                _transientUntil = now.AddMilliseconds(_appliedRuntime.PauseDelay);
                ActivateStageWithoutCycling(6);
                return;
            }
            _backspaceHeld = false;
            _enterHeld = false;
            ActivateStage(1);
            return;
        }

        if (_transientUntil > now) return;
        if (_currentStage != 1) ActivateStage(1);
    }

    private void ActivateStageWithoutCycling(int stage)
    {
        if (stage is not (3 or 5)) _enterArtworkProfileOverride = null;
        _currentStage = stage;
        UpdatePetWindow();
    }

    private void UpdatePetWindow()
    {
        UpdateRuntimeStatus();
        if (_petWindow is null || _petWindow.IsDisposed) return;
        var imageState = ImageStateForRuntimeStage(_currentStage);
        if (_appliedImageGroups.Count == 0 || _appliedProfiles.Count == 0) return;
        var profileIndex = _currentStage is 3 or 5 && _enterArtworkProfileOverride.HasValue
            ? _enterArtworkProfileOverride.Value
            : _runtimeProfile;
        var groupId = Math.Clamp(_appliedProfiles[profileIndex].ImageGroupId, 0, _appliedImageGroups.Count - 1);
        var group = _appliedImageGroups[groupId];
        var list = group.Images.TryGetValue(imageState, out var images) ? images : [];
        var index = group.ImageIndices.TryGetValue(imageState, out var selected) ? selected : 0;
        string? path = list.Count == 0 ? null : list[_appliedCarouselEnabled ? index % list.Count : 0];
        var backgrounds = group.Backgrounds.TryGetValue(imageState, out var backgroundList) ? backgroundList : [];
        var backgroundIndex = group.BackgroundIndices.TryGetValue(imageState, out var selectedBackground) ? selectedBackground : 0;
        string? backgroundPath = backgrounds.Count == 0 ? null : backgrounds[Math.Clamp(backgroundIndex, 0, backgrounds.Count - 1)];
        _petWindow.UpdateState(_currentStage, RuntimeStageNames[_currentStage], path, backgroundPath, _capsOn, _shiftDown);
        _petWindow.SetCounter(DisplayedCounterValue);
    }

    private void ConfigurePetMotion()
    {
        if (!_switchingProfile) { MarkSettingsPending(); SaveSettings(); }
    }

    private void MarkSettingsPending()
    {
        if (_initializing) return;
        _hasPendingDraft = true;
        _hintLabel.Text = "设置已修改，点击全局设置中的“保存并应用”后生效，也可按 Ctrl+S。";
        UpdateSaveStatus();
    }

    private void UpdateSaveStatus()
    {
        _saveStatusLabel.Text = _hasPendingDraft ? "● 待应用" : "✓ 已应用";
        _saveStatusLabel.ForeColor = _hasPendingDraft ? Color.FromArgb(174, 103, 27) : Color.FromArgb(61, 129, 87);
    }

    private long RuntimeShownCount => (long)Math.Round(_inputCount * (double)_appliedRuntime.CounterFactor);
    private long DisplayedCounterValue => _showWordsOnPet ? _todayWordCount : RuntimeShownCount;

    private void UpdateRuntimeStatus()
    {
        if (_appliedProfiles.Count == 0 || _appliedImageGroups.Count == 0) return;
        var profile = Math.Clamp(_runtimeProfile, 0, _appliedProfiles.Count - 1);
        var imageGroup = Math.Clamp(_appliedProfiles[profile].ImageGroupId, 0, _appliedImageGroups.Count - 1);
        _runtimeStatusLabel.Text = $"桌宠当前：设置组 {(char)('A' + profile)} · {_appliedImageGroups[imageGroup].Name} · 显示计数 {DisplayedCounterValue}";
        _runtimeCounterValue.Text = DisplayedCounterValue.ToString("N0");
    }

    private void SelectProfileForCount()
    {
        if (_appliedSwitchMode != GroupSwitchMode.Threshold || _appliedProfiles.Count == 0) return;
        var shownCount = RuntimeShownCount;
        var target = Enumerable.Range(0, _appliedProfiles.Count).Where(i => _appliedProfiles[i].Threshold <= shownCount).OrderByDescending(i => _appliedProfiles[i].Threshold).FirstOrDefault();
        if (target == _runtimeProfile) return;
        _runtimeProfile = target;
        ApplyRuntimeMotion();
        UpdatePetWindow();
    }

    private void AdvanceCycleOnEnter()
    {
        if (_appliedSwitchMode != GroupSwitchMode.EnterCycle || _appliedCycleSequence.Count == 0) return;
        _appliedCycleIndex = (_appliedCycleIndex + 1) % _appliedCycleSequence.Count;
        _runtimeProfile = _appliedCycleSequence[_appliedCycleIndex];
    }

    private void ApplyRuntimeMotion()
    {
        _petWindow?.ConfigureMotion(_appliedRuntime.ShapeMode, _appliedRuntime.Amplitude, _appliedRuntime.AnimationSpeed, _appliedRuntime.ScaleAmplitude, _appliedRuntime.SwayAmplitude);
    }

    private void ApplyRuntimeCounterAppearance()
    {
        _petWindow?.ConfigureCounterAppearance(_appliedRuntime.CounterBorderEnabled, _appliedRuntime.CounterInsideMainLayer,
            _appliedRuntime.CounterSizePercent, _appliedRuntime.CounterDistancePixels, _appliedRuntime.CounterFontFamily,
            _appliedRuntime.CounterFontStyle, ReadColor(_appliedRuntime.CounterTextFillArgb), ReadColor(_appliedRuntime.CounterTextOutlineArgb),
            _appliedRuntime.CounterTextOutlineWidth, ReadColor(_appliedRuntime.CounterBorderColorArgb), ReadColor(_appliedRuntime.CounterBorderFillArgb),
            _appliedRuntime.CounterBorderFillEnabled, _appliedRuntime.CounterBorderWidth, _appliedRuntime.CounterOpacity);
    }

    private void PreviewCounterAppearance()
    {
        _appliedRuntime.CounterBorderEnabled = _counterOutline.Checked;
        _appliedRuntime.CounterSizePercent = (int)_counterSizePercent.Value;
        _appliedRuntime.CounterDistancePixels = (int)_counterDistance.Value;
        _appliedRuntime.CounterInsideMainLayer = false;
        CopyCounterStyleToRuntime();
        ApplyRuntimeCounterAppearance();
    }

    private void CopyCounterStyleToRuntime()
    {
        _appliedRuntime.CounterFontFamily = _counterFontFamily;
        _appliedRuntime.CounterFontStyle = _counterFontStyle;
        _appliedRuntime.CounterTextFillArgb = _counterTextFill.ToArgb();
        _appliedRuntime.CounterTextOutlineArgb = _counterTextOutline.ToArgb();
        _appliedRuntime.CounterTextOutlineWidth = (int)_counterTextOutlineWidth.Value;
        _appliedRuntime.CounterBorderColorArgb = _counterBorderColor.ToArgb();
        _appliedRuntime.CounterBorderFillArgb = _counterBorderFill.ToArgb();
        _appliedRuntime.CounterBorderFillEnabled = _counterBorderFillEnabled.Checked;
        _appliedRuntime.CounterBorderWidth = (int)_counterBorderWidth.Value;
        _appliedRuntime.CounterOpacity = (int)_counterOpacity.Value;
    }

    private static Color ReadColor(int argb) => argb == 0 ? Color.Empty : Color.FromArgb(argb);

    private void CopyDraftToRuntime()
    {
        StoreEditingProfile(); SaveEditingImageGroup(); SaveGlobalSettings();
        _appliedProfiles.Clear();
        _appliedProfiles.AddRange(_profiles.Select(p => new PetProfile { Threshold = p.Threshold, ImageGroupId = p.ImageGroupId }));
        _appliedImageGroups.Clear();
        _appliedImageGroups.AddRange(_imageGroups.Select(CloneImageGroup));
        _appliedRuntime.CounterFactor = _counterFactor.Value;
        _appliedRuntime.CounterBorderEnabled = _counterOutline.Checked;
        _appliedRuntime.CounterInsideMainLayer = false;
        _appliedRuntime.CounterSizePercent = (int)_counterSizePercent.Value;
        _appliedRuntime.CounterDistancePixels = (int)_counterDistance.Value;
        CopyCounterStyleToRuntime();
        _appliedRuntime.ShapeMode = _motionStyle.SelectedIndex == 1;
        _appliedRuntime.Amplitude = (int)_motionAmplitude.Value;
        _appliedRuntime.AnimationSpeed = _animationSpeed.Value;
        _appliedRuntime.ScaleAmplitude = (int)_scaleAmplitude.Value;
        _appliedRuntime.SwayAmplitude = (int)_swayAmplitude.Value;
        _appliedRuntime.Advanced = _advancedMode.Checked;
        _appliedRuntime.IdleDelay = (int)_idleDelay.Value;
        _appliedRuntime.InputHold = (int)_inputHold.Value;
        _appliedRuntime.PauseDelay = (int)_pauseDuration.Value;
        _appliedRuntime.BackspaceHold = (int)_backspaceHold.Value;
        _appliedRuntime.EnterHold = (int)_enterHold.Value;
        _appliedRuntime.HoldDelay = (int)_holdDelay.Value;
        _appliedSwitchMode = _draftSwitchMode;
        _appliedCycleSequence = NormalizeCycleSequence(_cycleSequence, _profiles.Count);
        _appliedCarouselEnabled = _stageCarousel.Checked;
        _appliedRestartInputCarouselFromFirst = _restartInputCarousel.Checked;
        _appliedPinned = _pinPet.Checked;
        _appliedClickThrough = _clickThrough.Checked;
    }

    private void RestoreAppliedSettings()
    {
        var saved = _loadedAppliedSettings;
        if (saved is null || saved.Profiles.Count is < 1 or > 10 || saved.ImageGroups.Count == 0) return;
        _appliedProfiles.Clear();
        _appliedProfiles.AddRange(saved.Profiles);
        _appliedImageGroups.Clear();
        _appliedImageGroups.AddRange(saved.ImageGroups);
        _appliedRuntime.CounterFactor = saved.Runtime.CounterFactor;
        _appliedRuntime.CounterBorderEnabled = saved.Runtime.CounterBorderEnabled;
        _appliedRuntime.CounterInsideMainLayer = false;
        _appliedRuntime.CounterSizePercent = Math.Clamp(saved.Runtime.CounterSizePercent, 30, 300);
        _appliedRuntime.CounterDistancePixels = Math.Clamp(saved.Runtime.CounterDistancePixels, -200, 400);
        _appliedRuntime.CounterFontFamily = saved.Runtime.CounterFontFamily;
        _appliedRuntime.CounterFontStyle = saved.Runtime.CounterFontStyle;
        _appliedRuntime.CounterTextFillArgb = saved.Runtime.CounterTextFillArgb;
        _appliedRuntime.CounterTextOutlineArgb = saved.Runtime.CounterTextOutlineArgb;
        _appliedRuntime.CounterTextOutlineWidth = saved.Runtime.CounterTextOutlineWidth;
        _appliedRuntime.CounterBorderColorArgb = saved.Runtime.CounterBorderColorArgb;
        _appliedRuntime.CounterBorderFillArgb = saved.Runtime.CounterBorderFillArgb;
        _appliedRuntime.CounterBorderFillEnabled = saved.Runtime.CounterBorderFillEnabled;
        _appliedRuntime.CounterBorderWidth = saved.Runtime.CounterBorderWidth;
        _appliedRuntime.CounterOpacity = saved.Runtime.CounterOpacity;
        _appliedRuntime.ShapeMode = saved.Runtime.ShapeMode;
        _appliedRuntime.Amplitude = saved.Runtime.Amplitude;
        _appliedRuntime.AnimationSpeed = saved.Runtime.AnimationSpeed;
        _appliedRuntime.ScaleAmplitude = saved.Runtime.ScaleAmplitude;
        _appliedRuntime.SwayAmplitude = saved.Runtime.SwayAmplitude;
        _appliedRuntime.Advanced = saved.Runtime.Advanced;
        _appliedRuntime.IdleDelay = Math.Clamp(saved.Runtime.IdleDelay, 100, 2000);
        _appliedRuntime.InputHold = Math.Clamp(saved.Runtime.InputHold, 80, 2000);
        _appliedRuntime.PauseDelay = Math.Clamp(saved.Runtime.PauseDelay, 100, 2000);
        _appliedRuntime.BackspaceHold = Math.Clamp(saved.Runtime.BackspaceHold, 100, 2000);
        _appliedRuntime.EnterHold = Math.Clamp(saved.Runtime.EnterHold, 100, 2000);
        _appliedRuntime.HoldDelay = Math.Clamp(saved.Runtime.HoldDelay, 100, 2000);
        _appliedSwitchMode = saved.SwitchMode ?? (saved.ThresholdSwitch ? GroupSwitchMode.Threshold : GroupSwitchMode.Fixed);
        _appliedCycleSequence = NormalizeCycleSequence(saved.CycleSequence, saved.Profiles.Count);
        _appliedCycleIndex = Math.Clamp(saved.CycleIndex, 0, _appliedCycleSequence.Count - 1);
        _appliedCarouselEnabled = saved.GlobalCarouselEnabled ?? saved.ImageGroups.Any(group => group.CarouselModes.Values.Any(enabled => enabled));
        _appliedRestartInputCarouselFromFirst = saved.RestartInputCarouselFromFirst;
        _runtimeProfile = _appliedSwitchMode == GroupSwitchMode.EnterCycle
            ? _appliedCycleSequence[_appliedCycleIndex]
            : Math.Clamp(saved.SelectedProfile, 0, _appliedProfiles.Count - 1);
        _appliedPinned = saved.PetTopmost;
        _appliedClickThrough = saved.ClickThrough;
    }

    private static ImageGroup CloneImageGroup(ImageGroup source) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Images = source.Images.ToDictionary(pair => pair.Key, pair => pair.Value.ToList()),
        Backgrounds = source.Backgrounds.ToDictionary(pair => pair.Key, pair => pair.Value.ToList()),
        BackgroundCarouselModes = source.BackgroundCarouselModes.ToDictionary(pair => pair.Key, pair => pair.Value),
        BackgroundIndices = source.BackgroundIndices.ToDictionary(pair => pair.Key, pair => pair.Value),
        BackgroundHasTriggered = source.BackgroundHasTriggered.ToDictionary(pair => pair.Key, pair => pair.Value),
        CarouselModes = source.CarouselModes.ToDictionary(pair => pair.Key, pair => pair.Value),
        ImageIndices = source.ImageIndices.ToDictionary(pair => pair.Key, pair => pair.Value),
        HasTriggered = source.HasTriggered.ToDictionary(pair => pair.Key, pair => pair.Value)
    };

    private void ApplySettings()
    {
        CommitFocusedNumberInput();
        var previousMode = _appliedSwitchMode;
        var previousSequence = _appliedCycleSequence.ToArray();
        var previousIndex = _appliedCycleIndex;
        CopyDraftToRuntime();
        if (_appliedSwitchMode == GroupSwitchMode.Threshold) SelectProfileForCount();
        else if (_appliedSwitchMode == GroupSwitchMode.Fixed) _runtimeProfile = Math.Clamp(_editingProfile, 0, _appliedProfiles.Count - 1);
        else
        {
            _appliedCycleIndex = previousMode == GroupSwitchMode.EnterCycle && previousSequence.SequenceEqual(_appliedCycleSequence)
                ? Math.Clamp(previousIndex, 0, _appliedCycleSequence.Count - 1) : 0;
            _runtimeProfile = _appliedCycleSequence[_appliedCycleIndex];
        }
        ApplyRuntimeMotion(); ApplyRuntimeCounterAppearance(); _petWindow?.SetPinned(_appliedPinned); _petWindow?.SetClickThrough(_appliedClickThrough); UpdatePetWindow();
        _hasPendingDraft = false;
        SaveSettings();
        UpdateSaveStatus();
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
        _hintLabel.Text = $"已载入{(preset == "slow" ? "慢" : preset == "fast" ? "快" : "中等")}速预设，点击全局设置中的“保存并应用”后生效。";
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
        group.Backgrounds = _backgrounds.ToDictionary(pair => pair.Key, pair => pair.Value.ToList());
        group.BackgroundCarouselModes = _backgroundCarouselModes.ToDictionary(pair => pair.Key, pair => pair.Value);
        group.BackgroundIndices = _backgroundIndices.ToDictionary(pair => pair.Key, pair => pair.Value);
        group.BackgroundHasTriggered = _backgroundHasTriggered.ToDictionary(pair => pair.Key, pair => pair.Value);
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
            _images[i] = group.Images.TryGetValue(i, out var paths) ? paths.ToList() : [];
            _backgrounds[i] = group.Backgrounds.TryGetValue(i, out var backgroundPaths) ? backgroundPaths.ToList() : [];
            _backgroundCarouselModes[i] = group.BackgroundCarouselModes.TryGetValue(i, out var backgroundCarousel) && backgroundCarousel;
            _backgroundIndices[i] = group.BackgroundIndices.TryGetValue(i, out var backgroundIndex) ? backgroundIndex : 0;
            _backgroundHasTriggered[i] = group.BackgroundHasTriggered.TryGetValue(i, out var backgroundTriggered) && backgroundTriggered;
            _carouselModes[i] = group.CarouselModes.TryGetValue(i, out var carousel) && carousel;
            _imageIndices[i] = group.ImageIndices.TryGetValue(i, out var indexValue) ? indexValue : 0;
            _hasTriggered[i] = group.HasTriggered.TryGetValue(i, out var triggered) && triggered;
        }
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
        for (var i = 0; i < _imageGroups.Count; i++) _imageGroups[i].Name = $"图片组 {(char)('A' + i)}";
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
            for (var key = 0; key < ImageGroupNames.Length; key++) { group.HasTriggered[key] = false; group.ImageIndices[key] = 0; group.BackgroundHasTriggered[key] = false; group.BackgroundIndices[key] = 0; }
        foreach (var group in _appliedImageGroups)
            for (var key = 0; key < ImageGroupNames.Length; key++) { group.HasTriggered[key] = false; group.ImageIndices[key] = 0; group.BackgroundHasTriggered[key] = false; group.BackgroundIndices[key] = 0; }
        foreach (var key in _hasTriggered.Keys.ToList()) { _hasTriggered[key] = false; _imageIndices[key] = 0; }
        foreach (var key in _backgroundHasTriggered.Keys.ToList()) { _backgroundHasTriggered[key] = false; _backgroundIndices[key] = 0; }
        SelectProfileForCount(); UpdateRuntimeStatus(); UpdatePetWindow(); SaveCounter(); SaveSettings();
        _hintLabel.Text = "累计输入计数已重置。";
    }

    private void ResetTodayInputCount()
    {
        _todayInputCount = 0; _dailyStatsDirty = true; UpdateDailyCounterValues(); SaveDailyStatistics();
    }

    private void ResetTodayWordCount()
    {
        _todayWordCount = 0; _wordHasInput = false; _dailyStatsDirty = true; UpdateDailyCounterValues();
        _petWindow?.SetCounter(DisplayedCounterValue); SaveDailyStatistics();
    }

    private void CompleteCurrentWord()
    {
        if (!_wordHasInput) return;
        _todayWordCount++;
        _wordHasInput = false;
        _dailyStatsDirty = true;
        UpdateDailyCounterValues();
    }

    private void UpdateDailyCounterValues()
    {
        _todayInputValue.Text = _todayInputCount.ToString("N0");
        _todayWordsValue.Text = _todayWordCount.ToString("N0");
        _runtimeCounterValue.Text = DisplayedCounterValue.ToString("N0");
        UpdateRuntimeStatus();
        _petWindow?.SetCounter(DisplayedCounterValue);
    }

    private void EnsureDailyStatisticsDate()
    {
        if (_dailyStatsDate.Date == DateTime.Today) return;
        _dailyStatsDate = DateTime.Today;
        _todayInputCount = 0; _todayWordCount = 0; _wordHasInput = false; _dailyStatsDirty = true;
        UpdateDailyCounterValues();
    }

    private void LoadDailyStatistics()
    {
        try
        {
            if (File.Exists(UserDataPaths.DailyStatistics))
            {
                var data = JsonSerializer.Deserialize<DailyStatisticsData>(File.ReadAllText(UserDataPaths.DailyStatistics));
                if (data is not null && DateTime.TryParseExact(data.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var savedDate) && savedDate.Date == DateTime.Today)
                {
                    _dailyStatsDate = savedDate.Date;
                    _todayInputCount = Math.Max(0, data.InputCount);
                    _todayWordCount = Math.Max(0, data.WordCount);
                    _wordHasInput = data.WordHasInput;
                }
            }
            UpdateDailyCounterValues();
        }
        catch (Exception ex) { _hintLabel.Text = "读取今日计数失败：" + ex.Message; }
    }

    private void SaveDailyStatistics()
    {
        if (_settingsLoadError is not null) return;
        try
        {
            Directory.CreateDirectory(_dataDir);
            var data = new DailyStatisticsData { Date = _dailyStatsDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), InputCount = _todayInputCount, WordCount = _todayWordCount, WordHasInput = _wordHasInput };
            var pending = UserDataPaths.DailyStatistics + ".tmp";
            File.WriteAllText(pending, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(pending, UserDataPaths.DailyStatistics, true);
            _dailyStatsDirty = false;
            _lastDailyStatsSaveAt = DateTime.UtcNow;
        }
        catch (Exception ex) { _lastDailyStatsSaveAt = DateTime.UtcNow; _hintLabel.Text = "今日计数保存失败：" + ex.Message; }
    }

    private void SaveGlobalSettings() => _globalShapeMode = _motionStyle.SelectedIndex == 1;

    private void BuildImageTiles()
    {
        var scroll = _imageItems.AutoScrollPosition;
        _imageItems.SuspendLayout();
        ClearImageTiles(_imageItems);
        var paths = _images[_previewStage];
        if (paths.Count == 0) _selectedImageIndex = -1;
        else if (_selectedImageIndex < 0 || _selectedImageIndex >= paths.Count) _selectedImageIndex = 0;
        if (paths.Count == 0)
        {
            var empty = new Label
            {
                Width = 300, Height = 150,
                Text = $"{ImageGroupNames[_previewStage]}还没有前景图片\n将图片拖到这里，或点击下方“添加前景”",
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(247, 249, 252),
                ForeColor = Color.FromArgb(78, 96, 123),
                AllowDrop = true,
                Margin = new Padding(6)
            };
            empty.DragEnter += ImageListDragEnter;
            empty.DragDrop += ImageListDragDrop;
            empty.Click += (_, _) => { SetActiveImageLayer(ImageLayer.Foreground); BuildBackgroundTiles(); };
            _imageItems.Controls.Add(empty);
        }
        for (var index = 0; index < paths.Count; index++)
        {
            var itemIndex = index;
            var cutPending = _imageClipboard is { Cut: true } entry && entry.GroupId == _imageGroups[_editingImageGroup].Id && entry.Stage == _previewStage && entry.Layer == ImageLayer.Foreground && entry.Index == index && entry.Path == paths[index];
            var tile = new Panel { Width = 150, Height = 210, Margin = new Padding(4), Padding = new Padding(5), BackColor = cutPending ? Color.FromArgb(233, 233, 233) : _activeImageLayer == ImageLayer.Foreground && index == _selectedImageIndex ? Color.FromArgb(222, 233, 250) : Color.White, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand, AllowDrop = true };
            var picture = new PictureBox { Left = 16, Top = 8, Width = 116, Height = 152, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White, Cursor = Cursors.Hand, AllowDrop = true };
            LoadThumbnail(picture, paths[index]);
            var displayName = DisplayName(paths[index]);
            var name = new Label { Left = 7, Top = 166, Width = 134, Height = 38, Text = $"{index + 1}. " + (cutPending ? "剪切 · " : "") + displayName, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true, Cursor = Cursors.Hand, ForeColor = Color.FromArgb(52, 64, 82), AllowDrop = true };
            tile.Controls.Add(picture); tile.Controls.Add(name);
            tile.Click += (_, _) => SelectImageTile(itemIndex); picture.Click += (_, _) => SelectImageTile(itemIndex); name.Click += (_, _) => SelectImageTile(itemIndex);
            foreach (Control control in new Control[] { tile, picture, name }) { control.DragEnter += ImageListDragEnter; control.DragDrop += ImageListDragDrop; }
            _imageItems.Controls.Add(tile);
        }
        _imageItems.ResumeLayout(true);
        _imageItems.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
        _selectedLabel.Text = _selectedImageIndex >= 0 ? $"选中：{DisplayName(paths[_selectedImageIndex])}" : "尚未选择图片";
    }

    private void BuildBackgroundTiles()
    {
        var scroll = _backgroundItems.AutoScrollPosition;
        _backgroundItems.SuspendLayout();
        ClearImageTiles(_backgroundItems);
        var paths = _backgrounds[_previewStage];
        if (paths.Count == 0) _selectedBackgroundIndex = -1;
        else if (_selectedBackgroundIndex < 0 || _selectedBackgroundIndex >= paths.Count) _selectedBackgroundIndex = 0;
        if (paths.Count == 0)
        {
            var empty = new Label
            {
                Width = 300, Height = 150, Text = "此状态还没有背景\n将图片拖到这里，或点击下方“添加背景”",
                TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.FromArgb(247, 249, 252),
                ForeColor = Color.FromArgb(78, 96, 123), AllowDrop = true, Margin = new Padding(6)
            };
            empty.DragEnter += ImageListDragEnter;
            empty.DragDrop += BackgroundListDragDrop;
            empty.Click += (_, _) => { SetActiveImageLayer(ImageLayer.Background); BuildImageTiles(); };
            _backgroundItems.Controls.Add(empty);
        }
        for (var index = 0; index < paths.Count; index++)
        {
            var itemIndex = index;
            var cutPending = _imageClipboard is { Cut: true } entry && entry.GroupId == _imageGroups[_editingImageGroup].Id && entry.Stage == _previewStage && entry.Layer == ImageLayer.Background && entry.Index == index && entry.Path == paths[index];
            var tile = new Panel { Width = 150, Height = 210, Margin = new Padding(4), Padding = new Padding(5), BackColor = cutPending ? Color.FromArgb(233, 233, 233) : _activeImageLayer == ImageLayer.Background && index == _selectedBackgroundIndex ? Color.FromArgb(222, 233, 250) : Color.White, BorderStyle = BorderStyle.FixedSingle, Cursor = Cursors.Hand, AllowDrop = true };
            var picture = new PictureBox { Left = 16, Top = 8, Width = 116, Height = 152, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White, Cursor = Cursors.Hand, AllowDrop = true };
            LoadThumbnail(picture, paths[index]);
            var name = new Label { Left = 7, Top = 166, Width = 134, Height = 38, Text = $"{index + 1}. " + (cutPending ? "剪切 · " : "") + DisplayName(paths[index]), TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true, Cursor = Cursors.Hand, ForeColor = Color.FromArgb(52, 64, 82), AllowDrop = true };
            tile.Controls.Add(picture); tile.Controls.Add(name);
            void Select() { SetActiveImageLayer(ImageLayer.Background); _selectedBackgroundIndex = itemIndex; BuildImageTiles(); BuildBackgroundTiles(); }
            tile.Click += (_, _) => Select(); picture.Click += (_, _) => Select(); name.Click += (_, _) => Select();
            foreach (Control control in new Control[] { tile, picture, name }) { control.DragEnter += ImageListDragEnter; control.DragDrop += BackgroundListDragDrop; }
            _backgroundItems.Controls.Add(tile);
        }
        _backgroundItems.ResumeLayout(true);
        _backgroundItems.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
    }

    private static void ClearImageTiles(FlowLayoutPanel panel)
    {
        foreach (Control control in panel.Controls.Cast<Control>().ToArray())
        {
            foreach (var picture in control.Controls.OfType<PictureBox>())
            {
                var old = picture.Image;
                picture.Image = null;
                old?.Dispose();
            }
            panel.Controls.Remove(control);
            control.Dispose();
        }
    }

    private string DisplayName(string path) => _imageDisplayNames.TryGetValue(path, out var name) ? name : Path.GetFileName(path);

    private static void LoadThumbnail(PictureBox picture, string path)
    {
        try { using var source = Image.FromFile(path); picture.Image = new Bitmap(source); }
        catch { picture.Image = null; }
    }

    private void SelectImageTile(int index)
    {
        SetActiveImageLayer(ImageLayer.Foreground);
        _selectedImageIndex = index;
        BuildImageTiles();
        BuildBackgroundTiles();
    }

    private enum ImageLayer { Foreground, Background }
    private sealed record ImageClipboardEntry(string Token, string GroupId, int Stage, ImageLayer Layer, int Index, string Path, bool Cut);

    private static ImageGroup MigrateImageGroup(int id, Dictionary<int, List<string>> oldImages, Dictionary<int, bool> oldModes)
    {
        List<string> Get(int key) => oldImages.TryGetValue(key, out var list) ? list.ToList() : [];
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
        CommitFocusedNumberInput();
        StoreEditingProfile();
        _editingProfile = index;
        LoadEditingProfile(index);
        UpdateProfileButtons();
        RenderPage(); UpdatePetWindow();
    }

    private static List<int> NormalizeCycleSequence(IEnumerable<int>? sequence, int profileCount)
    {
        var result = sequence?.Where(index => index >= 0 && index < profileCount).Take(20).ToList() ?? [];
        return result.Count == 0 ? profileCount > 1 ? [0, 1] : [0] : result;
    }

    private void SetDraftSwitchMode(GroupSwitchMode mode)
    {
        if (_draftSwitchMode == mode) return;
        _draftSwitchMode = mode;
        UpdateSwitchModeButtons();
        MarkSettingsPending();
        SaveSettings();
    }

    private void EditCycleSequence()
    {
        using var editor = new GroupCycleEditorForm(_cycleSequence, _profiles.Count);
        DialogResult result;
        _cycleEditorOpen = true;
        try { result = editor.ShowDialog(this); }
        finally { _cycleEditorOpen = false; }
        if (result != DialogResult.OK) return;
        var sequence = NormalizeCycleSequence(editor.Sequence, _profiles.Count);
        if (_cycleSequence.SequenceEqual(sequence)) return;
        _cycleSequence = sequence;
        UpdateSwitchModeButtons();
        MarkSettingsPending();
        SaveSettings();
    }

    private void UpdateSwitchModeButtons()
    {
        for (var i = 0; i < _switchModeButtons.Length; i++)
        {
            var button = _switchModeButtons[i];
            if (button is null) continue;
            var active = i == (int)_draftSwitchMode;
            button.Type = active ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            button.DefaultBack = active ? Color.FromArgb(36, 117, 232) : Color.FromArgb(237, 242, 248);
            button.ForeColor = active ? Color.White : Color.FromArgb(48, 65, 90);
            button.ForeHover = active ? Color.White : Color.FromArgb(24, 43, 68);
            button.ForeActive = active ? Color.White : Color.FromArgb(24, 43, 68);
            button.BackHover = active ? Color.FromArgb(30, 104, 210) : Color.FromArgb(225, 235, 248);
            button.BackActive = active ? Color.FromArgb(21, 87, 176) : Color.FromArgb(214, 228, 245);
        }
        _cycleSummary.Text = "顺序：" + string.Join(" → ", _cycleSequence.Select(index => ((char)('A' + index)).ToString()));
    }

    private void UpdateProfileButtons()
    {
        for (var i = 0; i < _profileButtons.Count; i++)
        {
            if (_profileButtons[i] is null) continue;
            var active = i == _editingProfile;
            _profileButtons[i].Type = active ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            _profileButtons[i].DefaultBack = active ? Color.FromArgb(36, 117, 232) : Color.FromArgb(235, 240, 248);
            _profileButtons[i].ForeColor = active ? Color.White : Color.FromArgb(65, 80, 103);
            _profileButtons[i].ForeHover = active ? Color.White : Color.FromArgb(32, 52, 78);
            _profileButtons[i].ForeActive = active ? Color.White : Color.FromArgb(32, 52, 78);
            _profileButtons[i].BackHover = active ? Color.FromArgb(30, 104, 210) : Color.FromArgb(215, 227, 244);
            _profileButtons[i].BackActive = active ? Color.FromArgb(21, 87, 176) : Color.FromArgb(202, 219, 242);
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
            _profileThreshold.Value = Math.Clamp(p.Threshold, (long)(_profileThreshold.Minimum ?? 0), (long)(_profileThreshold.Maximum ?? 10000));
        if (_profileImageGroup.Items.Count != _imageGroups.Count) { _profileImageGroup.Items.Clear(); foreach (var g in _imageGroups) _profileImageGroup.Items.Add(g.Name); }
        _profileImageGroup.SelectedIndex = Math.Clamp(p.ImageGroupId, 0, Math.Max(0, _imageGroups.Count - 1));
        _switchingProfile = false;
        SelectImageGroup(_profiles[_editingProfile].ImageGroupId, false);
    }

    private void NormalizeLoadedImagePaths(SettingsData data)
    {
        var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var legacyAssets = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TypingPetPrototype", "assets");

        string Resolve(string original)
        {
            if (mapped.TryGetValue(original, out var existing)) return existing;
            var path = original;
            try
            {
                if (!Path.IsPathFullyQualified(path))
                    path = Path.GetFullPath(Path.Combine(_dataDir, path.Replace('/', Path.DirectorySeparatorChar)));
                else if (Path.GetFullPath(path).StartsWith(Path.GetFullPath(legacyAssets) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    var copied = Path.Combine(_assetsDir, Path.GetFileName(path));
                    if (File.Exists(copied)) path = copied;
                }

                if (File.Exists(path) && !Path.GetFullPath(path).StartsWith(Path.GetFullPath(_assetsDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    var destination = Path.Combine(_assetsDir, Guid.NewGuid().ToString("N") + Path.GetExtension(path));
                    File.Copy(path, destination);
                    path = destination;
                }
                if (!File.Exists(path)) missing.Add(original);
                if (Path.IsPathFullyQualified(original) && !string.Equals(original, path, StringComparison.OrdinalIgnoreCase))
                    _migratedImagePaths = true;
                if (Path.IsPathFullyQualified(original) && path.StartsWith(Path.GetFullPath(_assetsDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    _migratedImagePaths = true;
            }
            catch { path = original; missing.Add(original); }
            mapped[original] = path;
            return path;
        }

        void Rewrite(Dictionary<int, List<string>> images)
        {
            foreach (var key in images.Keys.ToList()) images[key] = images[key].Select(Resolve).ToList();
        }

        Rewrite(data.Images);
        foreach (var profile in data.Profiles) Rewrite(profile.Images);
        foreach (var group in data.ImageGroups) { Rewrite(group.Images); Rewrite(group.Backgrounds); }
        if (data.AppliedSettings is not null)
            foreach (var group in data.AppliedSettings.ImageGroups) { Rewrite(group.Images); Rewrite(group.Backgrounds); }
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in data.ImageDisplayNames)
            names[mapped.TryGetValue(pair.Key, out var resolved) ? resolved : pair.Key] = pair.Value;
        data.ImageDisplayNames = names;
        _missingImagePaths = missing.Count;
    }

    private string SerializePortableSettings(SettingsData data)
    {
        var root = JsonSerializer.SerializeToNode(data)!;
        string Portable(string path)
        {
            if (!Path.IsPathFullyQualified(path)) return path;
            var relative = Path.GetRelativePath(_dataDir, path);
            return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                ? relative.Replace(Path.DirectorySeparatorChar, '/') : path;
        }
        void Rewrite(JsonNode? node)
        {
            if (node is not JsonObject obj) return;
            foreach (var pair in obj.ToArray())
            {
                if (pair.Key is "Images" or "Backgrounds" && pair.Value is JsonObject states)
                {
                    foreach (var state in states.ToArray())
                        if (state.Value is JsonArray paths)
                            for (var i = 0; i < paths.Count; i++)
                                if (paths[i] is JsonValue value && value.TryGetValue<string>(out var path)) paths[i] = Portable(path);
                }
                else if (pair.Key == "ImageDisplayNames" && pair.Value is JsonObject names)
                {
                    foreach (var name in names.ToArray())
                    {
                        var portable = Portable(name.Key);
                        if (portable == name.Key) continue;
                        names.Remove(name.Key);
                        names[portable] = name.Value?.DeepClone();
                    }
                }
                else if (pair.Value is JsonObject child) Rewrite(child);
                else if (pair.Value is JsonArray array)
                    foreach (var item in array) Rewrite(item);
            }
        }
        Rewrite(root);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private void LoadSettings()
    {
        try
        {
            var path = UserDataPaths.Settings;
            if (!File.Exists(path)) { LoadCounter(); return; }
            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(path));
            if (data is null) return;
            NormalizeLoadedImagePaths(data);
            if (data.Profiles.Count is >= 1 and <= 10)
            {
                _profiles.Clear(); _profiles.AddRange(data.Profiles);
            }
            foreach (var profile in _profiles) profile.Threshold = Math.Clamp(profile.Threshold, 0, 10000);
            if (data.ImageGroups.Count > 0)
            {
                _imageGroups.AddRange(data.ImageGroups);
            }
            else
            {
                for (var i = 0; i < 5; i++)
                {
                    var oldImages = data.Profiles.Count > i ? data.Profiles[i].Images : data.Images;
                    var oldCarousel = data.Profiles.Count > i ? data.Profiles[i].CarouselModes : data.CarouselModes;
                    var group = MigrateImageGroup(i, oldImages, oldCarousel);
                    _imageGroups.Add(group);
                    if (i < _profiles.Count) _profiles[i].ImageGroupId = i;
                }
            }
            if (_imageGroups.Count == 0) _imageGroups.Add(new ImageGroup { Name = "图片组 A" });
            if (_imageGroups.Count > 10) _imageGroups.RemoveRange(10, _imageGroups.Count - 10);
            _profiles.ForEach(p => p.ImageGroupId = Math.Clamp(p.ImageGroupId, 0, _imageGroups.Count - 1));
            _counterFactor.Value = Math.Clamp(data.CounterFactor ?? (_profiles.Count > 0 ? _profiles[0].Factor : 1M), _counterFactor.Minimum ?? 0.2M, _counterFactor.Maximum ?? 10M);
            _counterBorderEnabled = data.CounterBorderEnabled ?? false;
            _counterSizePercent.Value = Math.Clamp(data.CounterSizePercent ?? 100, 30, 300);
            _counterDistance.Value = Math.Clamp(data.CounterDistancePixels ?? 2, -200, 400);
            _counterFontFamily = string.IsNullOrWhiteSpace(data.CounterFontFamily) ? "Segoe UI" : data.CounterFontFamily;
            _counterFontStyle = (FontStyle)(data.CounterFontStyle ?? (int)FontStyle.Bold);
            _counterTextFill = Color.FromArgb(data.CounterTextFillArgb ?? Color.FromArgb(35, 43, 56).ToArgb());
            _counterTextOutline = data.CounterTextOutlineArgb is null or 0 ? Color.Empty : Color.FromArgb(data.CounterTextOutlineArgb.Value);
            _counterTextOutlineWidth.Value = Math.Clamp(data.CounterTextOutlineWidth ?? 0, 0, 12);
            _counterBorderColor = Color.FromArgb(data.CounterBorderColorArgb ?? Color.FromArgb(85, 100, 122).ToArgb());
            _counterBorderFill = data.CounterBorderFillArgb is null or 0 ? Color.Empty : Color.FromArgb(data.CounterBorderFillArgb.Value);
            _loadedCounterBorderFillEnabled = data.CounterBorderFillEnabled ?? false;
            _counterBorderWidth.Value = Math.Clamp(data.CounterBorderWidth ?? 1, 0, 12);
            _counterOpacity.Value = Math.Clamp(data.CounterOpacity ?? 100, 0, 100);
            _loadedIdleDelay = Math.Clamp(data.GlobalIdleDelay ?? (_profiles.Count > 0 ? _profiles[0].IdleDelay : InitialIdleMs), 100, 2000);
            _loadedHoldDelay = Math.Clamp(data.HoldDelay, 100, 2000);
            _idleDelay.Value = _loadedIdleDelay; _holdDelay.Value = Math.Clamp(data.GlobalHoldDelay ?? _loadedHoldDelay, 100, 2000);
        _inputHold.Value = Math.Clamp(data.GlobalInputHold ?? 260, 80, 2000);
            _backspaceHold.Value = Math.Clamp(data.GlobalBackspaceHold ?? (_profiles.Count > 0 ? _profiles[0].BackspaceHold : 650), 100, 2000);
            _enterHold.Value = Math.Clamp(data.GlobalEnterHold ?? (_profiles.Count > 0 ? _profiles[0].EnterHold : 650), 100, 2000);
        _pauseDuration.Value = Math.Clamp(data.GlobalPauseDelay ?? (_profiles.Count > 0 ? _profiles[0].PauseDelay : 1000), 100, 2000);
            _motionAmplitude.Value = Math.Clamp(data.GlobalAmplitude ?? (_profiles.Count > 0 ? _profiles[0].Amplitude : 10), 0, 200);
            _animationSpeed.Value = Math.Clamp(data.GlobalAnimationSpeed ?? 1M, 0M, 3M);
            _scaleAmplitude.Value = Math.Clamp(data.GlobalScaleAmplitude ?? 0, 0, 200);
            _swayAmplitude.Value = Math.Clamp(data.GlobalSwayAmplitude ?? 0, 0, 200);
            _globalShapeMode = data.GlobalShapeMode ?? (_profiles.Count > 0 && _profiles[0].ShapeMode);
            _advancedMode.Checked = data.GlobalAdvanced ?? (_profiles.Count > 0 && _profiles[0].Advanced);
            _draftSwitchMode = data.SwitchMode ?? ((data.AutoThresholdSwitch ?? true) ? GroupSwitchMode.Threshold : GroupSwitchMode.Fixed);
            _cycleSequence = NormalizeCycleSequence(data.CycleSequence, _profiles.Count);
            _stageCarousel.Checked = data.GlobalCarouselEnabled ?? _imageGroups.Any(group => group.CarouselModes.Values.Any(enabled => enabled));
            _restartInputCarousel.Checked = data.RestartInputCarouselFromFirst ?? false;
            _showWordsOnPet = data.ShowWordsOnPet ?? false;
            _persistedEditingProfile = Math.Clamp(data.EditingProfile, 0, _profiles.Count - 1);
            _loadedPetTopmost = data.PetTopmost;
            _loadedClickThrough = data.ClickThrough;
            _loadedPetZoom = Math.Clamp(data.PetZoomPercent ?? 200, 40, 600);
            if (data.PetX.HasValue && data.PetY.HasValue) _loadedPetLocation = new Point(data.PetX.Value, data.PetY.Value);
            _loadedCounterEnabled = data.CounterEnabled ?? true;
            _loadedStartWithWindows = data.StartWithWindows ?? true;
            _inputCount = Math.Max(0, data.InputCount);
            foreach (var pair in data.ImageDisplayNames) _imageDisplayNames[pair.Key] = pair.Value;
            _loadedAppliedSettings = data.AppliedSettings;
            _hasPendingDraft = data.HasPendingDraft;
            LoadCounter();
        }
        catch (Exception ex) { _settingsLoadError = ex.Message; }
    }

    private void LoadCounter()
    {
        try
        {
            var path = UserDataPaths.Counter;
            if (File.Exists(path) && long.TryParse(File.ReadAllText(path), out var savedCount))
                _inputCount = Math.Max(0, savedCount);
        }
        catch (IOException) { /* Keep the last count from settings.json if the counter file is unavailable. */ }
    }

    private void SaveSettings()
    {
        if (_initializing || _settingsLoadError is not null) return;
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
                ImageDisplayNames = _imageDisplayNames,
                Profiles = _profiles,
                InputCount = _inputCount,
                CounterFactor = _counterFactor.Value,
                CounterBorderEnabled = _counterOutline.Checked,
                CounterInsideMainLayer = false,
                CounterSizePercent = (int)_counterSizePercent.Value,
                CounterDistancePixels = (int)_counterDistance.Value,
                CounterFontFamily = _counterFontFamily,
                CounterFontStyle = (int)_counterFontStyle,
                CounterTextFillArgb = _counterTextFill.ToArgb(),
                CounterTextOutlineArgb = _counterTextOutline.ToArgb(),
                CounterTextOutlineWidth = (int)_counterTextOutlineWidth.Value,
                CounterBorderColorArgb = _counterBorderColor.ToArgb(),
                CounterBorderFillArgb = _counterBorderFill.ToArgb(),
                CounterBorderFillEnabled = _counterBorderFillEnabled.Checked,
                CounterBorderWidth = (int)_counterBorderWidth.Value,
                CounterOpacity = (int)_counterOpacity.Value,
                GlobalIdleDelay = (int)_idleDelay.Value,
                GlobalInputHold = (int)_inputHold.Value,
                GlobalHoldDelay = (int)_holdDelay.Value,
                GlobalBackspaceHold = (int)_backspaceHold.Value,
                GlobalEnterHold = (int)_enterHold.Value,
                GlobalPauseDelay = (int)_pauseDuration.Value,
                GlobalAmplitude = (int)_motionAmplitude.Value,
                GlobalAnimationSpeed = _animationSpeed.Value,
                GlobalScaleAmplitude = (int)_scaleAmplitude.Value,
                GlobalSwayAmplitude = (int)_swayAmplitude.Value,
                GlobalShapeMode = _motionStyle.SelectedIndex == 1,
                GlobalAdvanced = _advancedMode.Checked,
                AutoThresholdSwitch = _draftSwitchMode == GroupSwitchMode.Threshold,
                SwitchMode = _draftSwitchMode,
                CycleSequence = _cycleSequence,
                GlobalCarouselEnabled = _stageCarousel.Checked,
                RestartInputCarouselFromFirst = _restartInputCarousel.Checked,
                ShowWordsOnPet = _showWordsOnPet,
                EditingProfile = _editingProfile,
                PetTopmost = _pinPet.Checked,
                ClickThrough = _clickThrough.Checked,
                PetZoomPercent = _petWindow?.ZoomPercent ?? _loadedPetZoom,
                PetX = _petWindow?.RestingLocation.X ?? _loadedPetLocation?.X,
                PetY = _petWindow?.RestingLocation.Y ?? _loadedPetLocation?.Y,
                CounterEnabled = _petWindow?.CounterEnabled ?? _loadedCounterEnabled,
                StartWithWindows = _startWithWindows.Checked,
                AppliedSettings = new AppliedSettingsData
                {
                    Profiles = _appliedProfiles,
                    ImageGroups = _appliedImageGroups,
                    Runtime = _appliedRuntime,
                    ThresholdSwitch = _appliedSwitchMode == GroupSwitchMode.Threshold,
                    SwitchMode = _appliedSwitchMode,
                    CycleSequence = _appliedCycleSequence,
                    CycleIndex = _appliedCycleIndex,
                    GlobalCarouselEnabled = _appliedCarouselEnabled,
                    RestartInputCarouselFromFirst = _appliedRestartInputCarouselFromFirst,
                    SelectedProfile = _runtimeProfile,
                    PetTopmost = _appliedPinned,
                    ClickThrough = _appliedClickThrough
                },
                HasPendingDraft = _hasPendingDraft
            };
            var settingsPath = UserDataPaths.Settings;
            var pendingPath = settingsPath + ".tmp";
            File.WriteAllText(pendingPath, SerializePortableSettings(data));
            if (File.Exists(settingsPath)) File.Copy(settingsPath, settingsPath + ".bak", overwrite: true);
            File.Move(pendingPath, settingsPath, true);
            var backupPending = settingsPath + ".bak.tmp";
            File.Copy(settingsPath, backupPending, overwrite: true);
            File.Move(backupPending, settingsPath + ".bak", true);
            var integrity = DataFolderIntegrityService.CheckAndClean(_dataDir, inspectImageContent: false);
            if (integrity.Issues.Count > 0)
                _hintLabel.Text = $"设置已保存；data 校验发现 {integrity.Issues.Count} 项问题，已暂停自动清理。点击“校验并清理”查看详情。";
        }
        catch (Exception ex) { _hintLabel.Text = "设置保存失败：" + ex.Message; }
    }

    private void ShowDataFolderIntegrity()
    {
        var report = DataFolderIntegrityService.CheckAndClean(_dataDir);
        var status = report.Issues.Count == 0 ? "设置文件与图片引用校验通过。" : $"发现 {report.Issues.Count} 项需要留意的问题：\n• " + string.Join("\n• ", report.Issues.Take(8));
        var message = $"数据目录：\n{report.DataDirectory}\n\n{status}\n\n" +
            $"图片文件：{report.AssetImageCount}\n设置引用：{report.ReferencedImageCount}\n未引用图片：{report.UnusedImageCount}\n本次自动清理：{report.RemovedImageCount}\n无法读取图片：{report.InvalidImageCount}" +
            (report.Issues.Count > 8 ? $"\n另有 {report.Issues.Count - 8} 项问题。" : string.Empty);
        _hintLabel.Text = report.Issues.Count > 0
            ? $"data 校验发现 {report.Issues.Count} 项问题；自动清理已按保护规则跳过有风险的文件。"
            : report.RemovedImageCount > 0
                ? $"data 校验通过，已清理 {report.RemovedImageCount} 张未引用图片。"
                : "data 校验通过，没有未引用图片。";
        MessageBox.Show(this, message, "Typing Pad · data 校验", MessageBoxButtons.OK,
            report.Issues.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private void SaveCounter()
    {
        if (_settingsLoadError is not null) return;
        try
        {
            Directory.CreateDirectory(_dataDir);
            var path = UserDataPaths.Counter;
            var pending = path + ".tmp";
            File.WriteAllText(pending, _inputCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
            File.Move(pending, path, true);
            _countDirty = false;
            _lastCountSaveAt = DateTime.UtcNow;
        }
        catch (Exception ex) { _lastCountSaveAt = DateTime.UtcNow; _hintLabel.Text = "计数保存失败：" + ex.Message; }
    }

    private void SyncRuntimePlaybackToDraft()
    {
        foreach (var applied in _appliedImageGroups)
        {
            var draft = _imageGroups.FirstOrDefault(group => group.Id == applied.Id);
            if (draft is null) continue;
            draft.ImageIndices = applied.ImageIndices.ToDictionary(pair => pair.Key, pair => pair.Value);
            draft.HasTriggered = applied.HasTriggered.ToDictionary(pair => pair.Key, pair => pair.Value);
            draft.BackgroundIndices = applied.BackgroundIndices.ToDictionary(pair => pair.Key, pair => pair.Value);
            draft.BackgroundHasTriggered = applied.BackgroundHasTriggered.ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }

    private sealed class SettingsData
    {
        public int IdleDelay { get; set; } = 800;
        public int HoldDelay { get; set; } = InitialHoldMs;
        public Dictionary<int, List<string>> Images { get; set; } = new();
        public Dictionary<int, bool> CarouselModes { get; set; } = new();
        public List<ImageGroup> ImageGroups { get; set; } = new();
        public Dictionary<string, string> ImageDisplayNames { get; set; } = new();
        public List<PetProfile> Profiles { get; set; } = new();
        public AppliedSettingsData? AppliedSettings { get; set; }
        public bool HasPendingDraft { get; set; }
        public long InputCount { get; set; }
        public decimal? CounterFactor { get; set; }
        public bool? CounterBorderEnabled { get; set; }
        public bool? CounterInsideMainLayer { get; set; }
        public int? CounterSizePercent { get; set; }
        public int? CounterDistancePixels { get; set; }
        public string? CounterFontFamily { get; set; }
        public int? CounterFontStyle { get; set; }
        public int? CounterTextFillArgb { get; set; }
        public int? CounterTextOutlineArgb { get; set; }
        public int? CounterTextOutlineWidth { get; set; }
        public int? CounterBorderColorArgb { get; set; }
        public int? CounterBorderFillArgb { get; set; }
        public bool? CounterBorderFillEnabled { get; set; }
        public int? CounterBorderWidth { get; set; }
        public int? CounterOpacity { get; set; }
        public int? GlobalIdleDelay { get; set; }
        public int? GlobalInputHold { get; set; }
        public int? GlobalHoldDelay { get; set; }
        public int? GlobalBackspaceHold { get; set; }
        public int? GlobalEnterHold { get; set; }
        public int? GlobalPauseDelay { get; set; }
        public int? GlobalAmplitude { get; set; }
        public decimal? GlobalAnimationSpeed { get; set; }
        public int? GlobalScaleAmplitude { get; set; }
        public int? GlobalSwayAmplitude { get; set; }
        public bool? GlobalShapeMode { get; set; }
        public bool? GlobalAdvanced { get; set; }
        public bool? AutoThresholdSwitch { get; set; }
        public GroupSwitchMode? SwitchMode { get; set; }
        public List<int>? CycleSequence { get; set; }
        public bool? GlobalCarouselEnabled { get; set; }
        public bool? RestartInputCarouselFromFirst { get; set; }
        public bool? ShowWordsOnPet { get; set; }
        public int EditingProfile { get; set; }
        public bool PetTopmost { get; set; } = true;
        public bool ClickThrough { get; set; }
        public int? PetZoomPercent { get; set; }
        public int? PetX { get; set; }
        public int? PetY { get; set; }
        public bool? CounterEnabled { get; set; }
        public bool? StartWithWindows { get; set; }
    }

    private sealed class AppliedSettingsData
    {
        public List<PetProfile> Profiles { get; set; } = new();
        public List<ImageGroup> ImageGroups { get; set; } = new();
        public RuntimeSettings Runtime { get; set; } = new();
        public bool ThresholdSwitch { get; set; } = true;
        public GroupSwitchMode? SwitchMode { get; set; }
        public List<int>? CycleSequence { get; set; }
        public int CycleIndex { get; set; }
        public bool? GlobalCarouselEnabled { get; set; }
        public bool RestartInputCarouselFromFirst { get; set; }
        public int SelectedProfile { get; set; }
        public bool PetTopmost { get; set; } = true;
        public bool ClickThrough { get; set; }
    }
}

internal sealed class DailyStatisticsData
{
    public string Date { get; set; } = "";
    public long InputCount { get; set; }
    public long WordCount { get; set; }
    public bool WordHasInput { get; set; }
}

internal sealed class ToggleActionButton : AntdUI.Button
{
    private bool _checked;
    public event EventHandler? CheckedChanged;
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            ApplyStateStyle();
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public ToggleActionButton()
    {
        Radius = 8;
        Height = 34;
        Click += (_, _) => Checked = !Checked;
        ApplyStateStyle();
    }

    private void ApplyStateStyle()
    {
        Type = _checked ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
        DefaultBack = _checked ? Color.FromArgb(31, 112, 232) : Color.FromArgb(233, 240, 249);
        BackHover = _checked ? Color.FromArgb(26, 99, 211) : Color.FromArgb(215, 229, 247);
        BackActive = _checked ? Color.FromArgb(21, 87, 188) : Color.FromArgb(202, 219, 242);
        ForeColor = _checked ? Color.White : Color.FromArgb(30, 45, 68);
        ForeHover = ForeColor; ForeActive = ForeColor;
    }
}

internal enum GroupSwitchMode { Fixed, Threshold, EnterCycle }

internal sealed class RuntimeSettings
{
    public decimal CounterFactor { get; set; } = 1M;
    public bool CounterBorderEnabled { get; set; }
    public bool CounterInsideMainLayer { get; set; }
    public int CounterSizePercent { get; set; } = 100;
    public int CounterDistancePixels { get; set; } = 2;
    public string CounterFontFamily { get; set; } = "Segoe UI";
    public FontStyle CounterFontStyle { get; set; } = FontStyle.Bold;
    public int CounterTextFillArgb { get; set; } = Color.FromArgb(35, 43, 56).ToArgb();
    public int CounterTextOutlineArgb { get; set; }
    public int CounterTextOutlineWidth { get; set; }
    public int CounterBorderColorArgb { get; set; } = Color.FromArgb(85, 100, 122).ToArgb();
    public int CounterBorderFillArgb { get; set; }
    public bool CounterBorderFillEnabled { get; set; }
    public int CounterBorderWidth { get; set; } = 1;
    public int CounterOpacity { get; set; } = 100;
    public bool ShapeMode { get; set; }
    public int Amplitude { get; set; } = 8;
    public decimal AnimationSpeed { get; set; } = 1M;
    public int ScaleAmplitude { get; set; }
    public int SwayAmplitude { get; set; }
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
    public Dictionary<int, List<string>> Backgrounds { get; set; } = new();
    public Dictionary<int, bool> BackgroundCarouselModes { get; set; } = new();
    public Dictionary<int, int> BackgroundIndices { get; set; } = new();
    public Dictionary<int, bool> BackgroundHasTriggered { get; set; } = new();
    public Dictionary<int, bool> CarouselModes { get; set; } = new();
    public Dictionary<int, int> ImageIndices { get; set; } = new();
    public Dictionary<int, bool> HasTriggered { get; set; } = new();
}
