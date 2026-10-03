using System.Windows.Forms;

namespace TypingPet;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        AntdUI.Config.IsLight = true;
        using var singleInstance = new Mutex(true, @"Local\TypingPet.SingleInstance", out var firstInstance);
        if (!firstInstance)
        {
            if (!args.Contains("--background", StringComparer.OrdinalIgnoreCase))
                MessageBox.Show("打字小伴侣已经在运行。请从系统托盘打开现有设置窗口。", "打字小伴侣", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        try { UserDataPaths.Initialize(); }
        catch (Exception ex)
        {
            MessageBox.Show($"无法创建或迁移 data 文件夹“{UserDataPaths.Root}”：{ex.Message}\n请将程序解压到可写入的文件夹后重试。", "打字小伴侣", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        using var context = new ApplicationContext();
        using var settings = new MainForm();
        settings.FormClosed += (_, _) => context.ExitThread();
        settings.StartCompanion();
        settings.Hide();
        Application.Run(context);
    }
}
