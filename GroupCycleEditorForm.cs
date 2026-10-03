using System.Windows.Forms;
using UiButton = AntdUI.Button;
using UiSelect = AntdUI.Select;

namespace TypingPet;

internal sealed class GroupCycleEditorForm : Form
{
    private readonly List<int> _steps;
    private readonly FlowLayoutPanel _stepRow = new() { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, BackColor = Color.White, Padding = new Padding(4, 7, 4, 2) };
    private readonly UiSelect _groupSelect = new() { Width = 115, Height = 34, WheelModifyEnabled = false };
    private readonly Label _hint = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(75, 93, 120) };
    private readonly int _groupCount;
    private int _selectedStep;

    public List<int> Sequence { get; private set; }

    public GroupCycleEditorForm(IEnumerable<int> sequence, int groupCount)
    {
        _groupCount = Math.Clamp(groupCount, 1, 10);
        _steps = sequence.Where(index => index >= 0 && index < _groupCount).ToList();
        if (_steps.Count == 0) _steps.Add(0);
        Sequence = _steps.ToList();
        Text = "编辑回车循环顺序";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(660, 276);
        MinimumSize = new Size(660, 276);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(244, 247, 251);
        Font = new Font("Segoe UI", 9F);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(16, 12, 16, 12) };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);
        root.Controls.Add(new Label { Text = "从第 1 步开始；每按一次 Enter 前进一格，末尾再回到第 1 步。重复组也会作为独立步骤。", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(49, 65, 91) }, 0, 0);
        root.Controls.Add(_stepRow, 0, 1);

        var editRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 5, 0, 0) };
        _groupSelect.Items.AddRange(Enumerable.Range(0, _groupCount).Select(index => $"设置组 {(char)('A' + index)}").ToArray());
        editRow.Controls.Add(_groupSelect);
        editRow.Controls.Add(Button("替换选中", () => { _steps[_selectedStep] = SelectedGroup; RefreshSteps(); }, 88));
        editRow.Controls.Add(Button("添加到末尾", () =>
        {
            if (_steps.Count >= 20) { _hint.Text = "最多设置 20 步。"; return; }
            _steps.Add(SelectedGroup); _selectedStep = _steps.Count - 1; RefreshSteps();
        }, 98));
        editRow.Controls.Add(Button("前移", () => MoveStep(-1), 65));
        editRow.Controls.Add(Button("后移", () => MoveStep(1), 65));
        editRow.Controls.Add(Button("删除", DeleteStep, 65));
        root.Controls.Add(editRow, 0, 2);

        var examples = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 5, 0, 0) };
        examples.Controls.Add(new Label { Text = "快捷示例", Width = 70, Height = 34, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(75, 93, 120) });
        if (_groupCount >= 2) examples.Controls.Add(Button("A ⇄ B", () => SetSteps([0, 1]), 88));
        if (_groupCount >= 3)
        {
            examples.Controls.Add(Button("A → B → C", () => SetSteps([0, 1, 2]), 116));
            examples.Controls.Add(Button("A → B → C → B → A", () => SetSteps([0, 1, 2, 1, 0]), 176));
        }
        root.Controls.Add(examples, 0, 3);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        footer.Controls.Add(_hint, 0, 0);
        var cancel = Button("取消", () => { DialogResult = DialogResult.Cancel; Close(); }, 76);
        var save = Button("保存顺序", () => { Sequence = _steps.ToList(); DialogResult = DialogResult.OK; Close(); }, 92);
        cancel.Dock = DockStyle.Fill; save.Dock = DockStyle.Fill;
        cancel.Margin = new Padding(2, 8, 4, 8); save.Margin = new Padding(2, 8, 0, 8);
        footer.Controls.Add(cancel, 1, 0);
        footer.Controls.Add(save, 2, 0);
        root.Controls.Add(footer, 0, 4);
        RefreshSteps();
    }

    private int SelectedGroup => Math.Clamp(_groupSelect.SelectedIndex, 0, _groupCount - 1);

    private void SetSteps(IEnumerable<int> steps)
    {
        _steps.Clear(); _steps.AddRange(steps); _selectedStep = 0; RefreshSteps();
    }

    private void MoveStep(int offset)
    {
        var target = _selectedStep + offset;
        if (target < 0 || target >= _steps.Count) return;
        (_steps[_selectedStep], _steps[target]) = (_steps[target], _steps[_selectedStep]);
        _selectedStep = target; RefreshSteps();
    }

    private void DeleteStep()
    {
        if (_steps.Count == 1) { _hint.Text = "循环顺序至少保留一步。"; return; }
        _steps.RemoveAt(_selectedStep);
        _selectedStep = Math.Min(_selectedStep, _steps.Count - 1);
        RefreshSteps();
    }

    private void RefreshSteps()
    {
        _stepRow.SuspendLayout();
        foreach (Control control in _stepRow.Controls.Cast<Control>().ToArray()) { _stepRow.Controls.Remove(control); control.Dispose(); }
        for (var i = 0; i < _steps.Count; i++)
        {
            var index = i;
            var button = Button($"{i + 1}  {(char)('A' + _steps[i])}", () => { _selectedStep = index; RefreshSteps(); }, 62);
            var active = i == _selectedStep;
            button.Type = active ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
            button.DefaultBack = active ? Color.FromArgb(48, 93, 157) : Color.FromArgb(233, 240, 249);
            button.ForeColor = active ? Color.White : Color.FromArgb(30, 45, 68);
            button.ForeHover = active ? Color.White : Color.FromArgb(30, 45, 68);
            _stepRow.Controls.Add(button);
        }
        _stepRow.ResumeLayout(true);
        _groupSelect.SelectedIndex = _steps[_selectedStep];
        _hint.Text = $"第 {_selectedStep + 1} 步 / 共 {_steps.Count} 步";
    }

    private static UiButton Button(string text, Action action, int width)
    {
        var button = new UiButton { Text = text, Width = width, Height = 34, Radius = 8, Margin = new Padding(0, 0, 6, 0), DefaultBack = Color.FromArgb(233, 240, 249), ForeColor = Color.FromArgb(30, 45, 68), ForeHover = Color.FromArgb(30, 45, 68), ForeActive = Color.FromArgb(30, 45, 68), BackHover = Color.FromArgb(215, 229, 247), BackActive = Color.FromArgb(202, 219, 242) };
        button.Click += (_, _) => action();
        return button;
    }
}
