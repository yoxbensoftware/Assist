namespace Assist.Forms.SystemTools;

internal partial class SystemInfoForm : Form
{
        public SystemInfoForm()
        {
            InitializeComponent();
            LoadSystemInfo();
        }

        private void LoadSystemInfo()
        {
            var sysInfo = SystemInfoHelper.GetSystemInfo();

            var info = $@"╔════════════════════════════════════════════════════════════════════════════════╗
║                            SİSTEM BİLGİLERİ                                   ║
╚════════════════════════════════════════════════════════════════════════════════╝

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🖥️  BİLGİSAYAR BİLGİLERİ
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Bilgisayar Adı:          {sysInfo.ComputerName}
Kullanıcı Adı:           {sysInfo.UserName}
Üretici:                 {sysInfo.ComputerManufacturer}
Model:                   {sysInfo.ComputerModel}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📊 İŞLETİM SİSTEMİ
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
İşletim Sistemi:         {sysInfo.OperatingSystem}
Sürüm:                   {sysInfo.OSVersion}
Mimari:                  {sysInfo.Architecture}
Kurulum Tarihi:          {sysInfo.SystemInstallDate}
Çalışma Süresi:          {sysInfo.SystemUptime}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🔧 YAZILIM
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
.NET Sürümü:             {sysInfo.DotNetVersion}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
💻 BIOS / FIRMWARE
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
BIOS:                    {sysInfo.BIOS}
BIOS Bilgisi:            {sysInfo.BIOSVersion}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⚙️  İŞLEMCİ
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Model:                   {sysInfo.ProcessorInfo}
Maksimum Hız:            {sysInfo.ProcessorSpeed}
Çekirdek Sayısı:         {Environment.ProcessorCount}

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
💾 BELLEK (RAM)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Toplam RAM:              {sysInfo.RAM}
";

            if (sysInfo.RAMModules.Count > 0)
            {
                info += "\nBellek Modülleri:\n";
                foreach (var module in sysInfo.RAMModules)
                {
                    info += $"  • {module}\n";
                }
            }

            info += $@"
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
💿 DEPOLAMA CİHAZLARI
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
";

            if (sysInfo.StorageDevices.Count > 0)
            {
                foreach (var device in sysInfo.StorageDevices)
                {
                    info += $"  • {device}\n";
                }
            }
            else
            {
                info += "  Bilgi alınamadı\n";
            }

            info += $@"
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🎮 EKRAN KARTI (GPU)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
";

            if (sysInfo.GraphicsCards.Count > 0)
            {
                foreach (var gpu in sysInfo.GraphicsCards)
                {
                    info += $"  • {gpu}\n";
                }
            }
            else
            {
                info += "  Bilgi alınamadı\n";
            }

            info += $@"
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
🌐 AĞ ADAPTÖRLERI
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
";

            if (sysInfo.NetworkAdapters.Count > 0)
            {
                foreach (var adapter in sysInfo.NetworkAdapters)
                {
                    info += $"  • {adapter}\n";
                }
            }
            else
            {
                info += "  Bilgi alınamadı\n";
            }

            info += $@"
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📺 MONITOR
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
";

            if (sysInfo.Monitors.Count > 0)
            {
                foreach (var monitor in sysInfo.Monitors)
                {
                    info += $"  • {monitor}\n";
                }
            }
            else
            {
                info += "  Bilgi alınamadı\n";
            }
            // Sensors (temperatures, fans, loads)
            info += "\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
            info += "🔎 SENSÖRLER (Sıcaklık, Fan, Yük, Saat)\n";
            info += "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n";
            if (sysInfo.Sensors != null && sysInfo.Sensors.Count > 0)
            {
                foreach (var s in sysInfo.Sensors)
                {
                    info += $"  • {s}\n";
                }
            }
            else
            {
                info += "  Sensör bilgisi alınamadı veya yönetici yetkisi gerekli.\n";
            }


            info += $@"
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📅 TARİH VE SAATİ
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Sistem Tarihi:           {DateTime.Now:dd.MM.yyyy}
Sistem Saati:            {DateTime.Now:HH:mm:ss}
";

            txtInfo.Text = info;
        }
}
