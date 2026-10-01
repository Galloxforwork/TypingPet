using System.Windows.Forms;

namespace TypingPet;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var singleInstance = new Mutex(true, @"Local\TypingPet.SingleInstance", out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("打字小伴侣已经在运行。请从系统托盘打开现有设置窗口。", "打字小伴侣", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Application.Run(new MainForm());
    }
}
