namespace Assist;

using Assist.Forms.Core;
using Assist.Services;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // Handle uninstall request from Add/Remove Programs
        if (args.Contains("--uninstall"))
        {
            var result = MessageBox.Show(
                "Assist uygulamasını kaldırmak istediğinize emin misiniz?",
                "Assist — Kaldır",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                AppInstallerService.Uninstall();
                MessageBox.Show(
                    "Assist başarıyla kaldırıldı.\nUygulama dosyalarını manuel olarak silebilirsiniz.",
                    "Kaldırma Tamamlandı",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            return;
        }

        // First run: show setup wizard if no credentials exist
        if (PasswordStore.LoadLogin() is null)
        {
            using var wizard = new SetupWizardForm();
            if (wizard.ShowDialog() != DialogResult.OK || !wizard.SetupCompleted)
                return;
        }

        // Register in Windows (Add/Remove Programs, shortcuts) if not already done
        if (!AppInstallerService.IsInstalled)
            AppInstallerService.Install();

        using var loginForm = new LoginForm();
        if (loginForm.ShowDialog() == DialogResult.OK && loginForm.IsAuthenticated)
        {
            Application.Run(new MainMDIForm());
        }
    }
}
