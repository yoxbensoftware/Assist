namespace Assist.SDLC.Forms;

using Assist.Services;
using Assist.SDLC.Services;

/// <summary>SDLC Settings — timeouts, retry, notification rules, etc.</summary>
internal sealed class SdlcSettingsForm : SdlcBaseForm
{
    public SdlcSettingsForm()
    {
        Text = "⚙️ SDLC Settings";
        Size = new Size(600, 420);

        int y = 16;

        // ── Placeholder settings controls ─────────────────
        var sections = new[]
        {
            ("Model Ayarları", "LLM endpoint, model adı, API key"),
            ("Timeout / Retry", "Agent timeout, max retry, delay between retries"),
            ("Bekleme Politikaları", "Max bekleme, escalation eşiği, auto-remind"),
            ("Bildirim Kuralları", "Hangi event'ler bildirim üretir, severity mapping"),
            ("Onay Kuralları", "Hangi adımlar human approval gerektirir"),
            ("Build / Test Komutları", "dotnet build, dotnet test, custom script yolları"),
            ("Depolama", "History store yolu, max kayıt sayısı"),
            ("Dokümantasyon", "Verbose / compact mod, delta-based mi, end-of-day özeti"),
            ("Session / Kilit", "IDE session lock timeout, auto-unlock süresi"),
        };

        foreach (var (title, desc) in sections)
        {
            var lbl = CreateLabel($"▸ {title}", 16, y, 540);
            lbl.Font = new Font("Consolas", 10, FontStyle.Bold);
            Controls.Add(lbl);
            y += 22;

            var descLbl = CreateLabel($"   {desc}", 16, y, 540);
            descLbl.ForeColor = UITheme.Palette.Muted;
            Controls.Add(descLbl);
            y += 28;
        }

        // TODO: replace placeholders with actual editable controls
        // that persist to a settings JSON file.
    }
}
