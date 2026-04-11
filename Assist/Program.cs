namespace Assist;

using Assist.Forms.Core;
using Assist.Services;

/// <summary>
/// Application entry point.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Main entry point — initializes the application, handles uninstall requests,
    /// runs the setup wizard on first launch, and starts the login flow.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        ThemeService.Load();

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
            UITheme.Apply(wizard);
            if (wizard.ShowDialog() != DialogResult.OK || !wizard.SetupCompleted)
                return;
        }

        // Register in Windows (Add/Remove Programs, shortcuts) if not already done
        if (!AppInstallerService.IsInstalled)
            AppInstallerService.Install();

        if (System.Diagnostics.Debugger.IsAttached)
        {
            LaunchMainWindow();
            return;
        }

        using var loginForm = new LoginForm();
        UITheme.Apply(loginForm);
        if (loginForm.ShowDialog() == DialogResult.OK && loginForm.IsAuthenticated)
            LaunchMainWindow();
    }

    /// <summary>
    /// Shows a splash screen and starts the main MDI window.
    /// </summary>
    private static void LaunchMainWindow()
    {
        using var splash = new SplashForm();
        splash.Show();
        splash.Refresh(); // force immediate paint — avoids Application.DoEvents() re-entrancy risk

        var mainForm = new MainMDIForm();
        mainForm.Shown += (_, _) => splash.Close();
        Application.Run(mainForm);
    }
}
