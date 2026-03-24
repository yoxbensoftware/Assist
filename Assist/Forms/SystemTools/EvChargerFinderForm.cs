using System.Diagnostics;
using System.Text.Json;

namespace Assist.Forms.SystemTools;

/// <summary>
/// EV Charging Station Finder - Finds electric vehicle charging stations across Turkey
/// Uses Google Places API to locate EV chargers
/// </summary>
internal sealed class EvChargerFinderForm : Form
{
    #region Constants
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };
    private const string DefaultApiKey = "AIzaSyDolQkxpbCHEuVYmh9jhcTZIeRt77Rx1nY";

    // Türkiye'nin büyük şehirleri
    private static readonly Dictionary<string, (double Lat, double Lng)> TurkishCities = new()
    {
        ["İstanbul (Avrupa)"] = (41.0082, 28.9784),
        ["İstanbul (Anadolu)"] = (40.9828, 29.0876),
        ["Ankara"] = (39.9334, 32.8597),
        ["İzmir"] = (38.4192, 27.1287),
        ["Bursa"] = (40.1885, 29.0610),
        ["Antalya"] = (36.8969, 30.7133),
        ["Adana"] = (37.0000, 35.3213),
        ["Konya"] = (37.8746, 32.4932),
        ["Gaziantep"] = (37.0662, 37.3833),
        ["Mersin"] = (36.8121, 34.6415),
        ["Kayseri"] = (38.7312, 35.4787),
        ["Eskişehir"] = (39.7767, 30.5206),
        ["Trabzon"] = (41.0027, 39.7168),
        ["Samsun"] = (41.2867, 36.3300),
        ["Denizli"] = (37.7765, 29.0864),
        ["Muğla (Bodrum)"] = (37.0344, 27.4305),
        ["Aydın (Kuşadası)"] = (37.8560, 27.2590),
        ["Sakarya"] = (40.7569, 30.3781),
        ["Kocaeli"] = (40.7654, 29.9408),
        ["Tekirdağ"] = (40.9833, 27.5167),
        ["Çanakkale"] = (40.1553, 26.4142),
        ["Balıkesir"] = (39.6484, 27.8826),
        ["Manisa"] = (38.6191, 27.4289),
        ["Hatay"] = (36.2025, 36.1603),
        ["Diyarbakır"] = (37.9144, 40.2306),
        ["Şanlıurfa"] = (37.1591, 38.7969),
        ["Malatya"] = (38.3552, 38.3095),
        ["Van"] = (38.5012, 43.3730),
        ["Erzurum"] = (39.9055, 41.2658),
    };
    #endregion

    #region Fields
    private readonly ComboBox _cmbCity;
    private readonly TextBox _txtLocation;
    private readonly NumericUpDown _nudRadius;
    private readonly TextBox _txtApiKey;
    private readonly ListBox _lstStations;
    private readonly TextBox _txtDetails;
    private readonly Button _btnSearch;
    private readonly Button _btnSearchAll;
    private readonly Button _btnOpenMaps;
    private readonly Button _btnAddManual;
    private readonly Button _btnSave;
    private readonly Button _btnLoad;
    private readonly Label _lblStatus;
    private readonly ProgressBar _progressBar;

    private readonly List<ChargingStation> _foundStations = [];
    private readonly HttpClient _httpClient = new();
    private string? _googleApiKey;
    private const string ApiKeySettingsFile = "google_api_key.txt";
    #endregion

    #region Nested Types
    private class ChargingStation
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool HasEvCharger { get; set; } = true;
        public string StationType { get; set; } = "ev_charging";
        public string PlaceId { get; set; } = string.Empty;
        public DateTime FoundDate { get; set; } = DateTime.Now;
        public string Notes { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }
    #endregion

    #region Constructor
    public EvChargerFinderForm()
    {
        Text = "🔌 Elektrikli Araç Şarj İstasyonu Bulucu - Türkiye";
        ClientSize = new Size(950, 780);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);
        StartPosition = FormStartPosition.CenterScreen;

        // Load API key from file or environment
        LoadApiKey();

        // Title
        var lblTitle = new Label
        {
            Text = "🔌 ELEKTRİKLİ ARAÇ ŞARJ İSTASYONU BULUCU",
            Location = new Point(20, 12),
            AutoSize = true,
            ForeColor = Color.Cyan,
            Font = new Font("Consolas", 13, FontStyle.Bold)
        };

        var lblSubtitle = new Label
        {
            Text = "Türkiye Geneli EV Şarj Noktaları",
            Location = new Point(20, 35),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9)
        };

        // API Key Section
        var lblApiKey = new Label
        {
            Text = "🔑 API Key:",
            Location = new Point(20, 65),
            Width = 80,
            ForeColor = Color.Cyan
        };

        _txtApiKey = new TextBox
        {
            Text = _googleApiKey ?? "",
            Location = new Point(100, 62),
            Width = 300,
            BackColor = Color.Black,
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle,
            PasswordChar = '●'
        };
        _txtApiKey.TextChanged += (_, _) => OnApiKeyChanged();

        var btnShowKey = new Button
        {
            Text = "👁",
            Location = new Point(405, 60),
            Width = 30,
            Height = 25,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnShowKey.FlatAppearance.BorderColor = GreenText;
        btnShowKey.Click += (_, _) => _txtApiKey.PasswordChar = _txtApiKey.PasswordChar == '●' ? '\0' : '●';

        var btnGetApiKey = new LinkLabel
        {
            Text = "🔗 API Key Al",
            Location = new Point(445, 65),
            AutoSize = true,
            LinkColor = Color.Yellow,
            ActiveLinkColor = Color.Orange,
            VisitedLinkColor = Color.Yellow
        };
        btnGetApiKey.LinkClicked += (_, _) => OpenApiKeyPage();

        // City Selection
        var lblCity = new Label
        {
            Text = "🏙️ Şehir:",
            Location = new Point(550, 65),
            Width = 70,
            ForeColor = GreenText
        };

        _cmbCity = new ComboBox
        {
            Location = new Point(620, 62),
            Width = 180,
            BackColor = Color.Black,
            ForeColor = GreenText,
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat
        };
        foreach (var city in TurkishCities.Keys)
            _cmbCity.Items.Add(city);
        _cmbCity.SelectedIndex = 2; // Ankara default
        _cmbCity.SelectedIndexChanged += (_, _) => OnCityChanged();

        // Custom coordinates
        var lblCoords = new Label
        {
            Text = "📍 Koordinat:",
            Location = new Point(20, 100),
            Width = 100,
            ForeColor = GreenText
        };

        _txtLocation = new TextBox
        {
            Text = "39.9334,32.8597",
            Location = new Point(120, 97),
            Width = 180,
            BackColor = Color.Black,
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle
        };

        // Search radius
        var lblRadius = new Label
        {
            Text = "📏 Yarıçap (km):",
            Location = new Point(320, 100),
            Width = 120,
            ForeColor = GreenText
        };

        _nudRadius = new NumericUpDown
        {
            Location = new Point(440, 97),
            Width = 70,
            Minimum = 5,
            Maximum = 50,
            Value = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle
        };

        // Progress bar
        _progressBar = new ProgressBar
        {
            Location = new Point(530, 97),
            Width = 270,
            Height = 23,
            Style = ProgressBarStyle.Continuous,
            Visible = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // Buttons row 1
        _btnSearch = new Button
        {
            Text = "🔍 Seçili Şehirde Ara",
            Location = new Point(20, 135),
            Width = 180,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnSearch.FlatAppearance.BorderColor = GreenText;
        _btnSearch.Click += async (_, _) => await SearchCityAsync();

        _btnSearchAll = new Button
        {
            Text = "🇹🇷 Tüm Türkiye'de Ara",
            Location = new Point(210, 135),
            Width = 180,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = Color.Yellow,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnSearchAll.FlatAppearance.BorderColor = Color.Yellow;
        _btnSearchAll.Click += async (_, _) => await SearchAllTurkeyAsync();

        _btnOpenMaps = new Button
        {
            Text = "🗺️ Chrome'da Aç",
            Location = new Point(400, 135),
            Width = 140,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnOpenMaps.FlatAppearance.BorderColor = GreenText;
        _btnOpenMaps.Click += (_, _) => OpenInChrome();

        _btnAddManual = new Button
        {
            Text = "➕ Manuel Ekle",
            Location = new Point(550, 135),
            Width = 130,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = Color.Cyan,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnAddManual.FlatAppearance.BorderColor = Color.Cyan;
        _btnAddManual.Click += (_, _) => AddManualStation();

        // Buttons row 2
        _btnSave = new Button
        {
            Text = "💾 Kaydet",
            Location = new Point(690, 135),
            Width = 100,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = Color.Magenta,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnSave.FlatAppearance.BorderColor = Color.Magenta;
        _btnSave.Click += (_, _) => SaveStations();

        _btnLoad = new Button
        {
            Text = "📂 Yükle",
            Location = new Point(800, 135),
            Width = 100,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = Color.Magenta,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnLoad.FlatAppearance.BorderColor = Color.Magenta;
        _btnLoad.Click += (_, _) => LoadStations();

        // Status
        _lblStatus = new Label
        {
            Text = !string.IsNullOrEmpty(_googleApiKey) 
                ? "✅ API Key hazır. Bir şehir seçip arama yapın veya Tüm Türkiye'de arayın." 
                : "⚠️ API Key girilmedi. Yukarıdan API Key alıp girin.",
            Location = new Point(20, 180),
            Width = 900,
            ForeColor = !string.IsNullOrEmpty(_googleApiKey) ? GreenText : Color.Yellow,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // Station list
        var lblStations = new Label
        {
            Text = "🔌 Bulunan EV Şarj İstasyonları:",
            Location = new Point(20, 210),
            AutoSize = true,
            ForeColor = Color.Cyan,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _lstStations = new ListBox
        {
            Location = new Point(20, 235),
            Width = 430,
            Height = 380,
            BackColor = Color.Black,
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Bottom
        };
        _lstStations.SelectedIndexChanged += (_, _) => ShowStationDetails();

        // Details
        var lblDetails = new Label
        {
            Text = "📋 İstasyon Detayları:",
            Location = new Point(470, 210),
            AutoSize = true,
            ForeColor = Color.Cyan,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtDetails = new TextBox
        {
            Location = new Point(470, 235),
            Width = 460,
            Height = 380,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.Black,
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        // Bottom buttons
        var btnOpenSelected = new Button
        {
            Text = "🗺️ Haritada Aç",
            Location = new Point(20, 630),
            Width = 150,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };
        btnOpenSelected.FlatAppearance.BorderColor = GreenText;
        btnOpenSelected.Click += (_, _) => OpenSelectedInMaps();

        var btnStreetView = new Button
        {
            Text = "👁️ Street View",
            Location = new Point(180, 630),
            Width = 130,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = Color.Orange,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };
        btnStreetView.FlatAppearance.BorderColor = Color.Orange;
        btnStreetView.Click += (_, _) => OpenStreetView();

        var btnDelete = new Button
        {
            Text = "🗑️ Sil",
            Location = new Point(320, 630),
            Width = 80,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = Color.Red,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };
        btnDelete.FlatAppearance.BorderColor = Color.Red;
        btnDelete.Click += (_, _) => DeleteSelected();

        var btnClear = new Button
        {
            Text = "🧹 Listeyi Temizle",
            Location = new Point(410, 630),
            Width = 140,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = Color.Gray,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };
        btnClear.FlatAppearance.BorderColor = Color.Gray;
        btnClear.Click += (_, _) => ClearList();

        // Summary
        var lblSummary = new Label
        {
            Text = "💡 İpucu: 'Tüm Türkiye'de Ara' tüm büyük şehirlerde otomatik arama yapar.",
            Location = new Point(20, 680),
            Width = 600,
            ForeColor = Color.Gray,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };

        // Stats label
        var lblStats = new Label
        {
            Name = "lblStats",
            Text = "📊 Toplam: 0 EV Şarj İstasyonu",
            Location = new Point(20, 715),
            Width = 500,
            ForeColor = Color.Cyan,
            Font = new Font("Consolas", 10, FontStyle.Bold),
            Anchor = AnchorStyles.Left | AnchorStyles.Bottom
        };

        Controls.AddRange([
            lblTitle, lblSubtitle, lblApiKey, _txtApiKey, btnShowKey, btnGetApiKey,
            lblCity, _cmbCity, lblCoords, _txtLocation, lblRadius, _nudRadius, _progressBar,
            _btnSearch, _btnSearchAll, _btnOpenMaps, _btnAddManual, _btnSave, _btnLoad,
            _lblStatus, lblStations, _lstStations, lblDetails, _txtDetails,
            btnOpenSelected, btnStreetView, btnDelete, btnClear, lblSummary, lblStats
        ]);

        // Try to load existing data
        LoadStationsQuiet();
    }
    #endregion

    #region City Selection
    private void OnCityChanged()
    {
        if (_cmbCity.SelectedItem is string cityName && TurkishCities.TryGetValue(cityName, out var coords))
        {
            _txtLocation.Text = $"{coords.Lat:F4},{coords.Lng:F4}";
        }
    }
    #endregion

    #region API Search

    private void LoadApiKey()
    {
        // First check file
        var keyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ApiKeySettingsFile);
        if (File.Exists(keyFile))
        {
            var key = File.ReadAllText(keyFile).Trim();
            if (!string.IsNullOrEmpty(key))
            {
                _googleApiKey = key;
                return;
            }
        }

        // Then check environment variable
        var envKey = Environment.GetEnvironmentVariable("GOOGLE_PLACES_API_KEY");
        if (!string.IsNullOrEmpty(envKey))
        {
            _googleApiKey = envKey;
            return;
        }

        // Use default embedded API key
        _googleApiKey = DefaultApiKey;
    }

    private void OnApiKeyChanged()
    {
        var key = _txtApiKey.Text.Trim();
        _googleApiKey = string.IsNullOrEmpty(key) ? null : key;

        // Save to file
        if (!string.IsNullOrEmpty(key))
        {
            var keyFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ApiKeySettingsFile);
            File.WriteAllText(keyFile, key);
            _lblStatus.Text = "✅ API Key kaydedildi. Artık API ile arama yapabilirsiniz.";
            _lblStatus.ForeColor = GreenText;
        }
        else
        {
            _lblStatus.Text = "⚠️ API Key girilmedi. Chrome ile manuel arama yapın.";
            _lblStatus.ForeColor = Color.Yellow;
        }
    }

    private void OpenApiKeyPage()
    {
        var url = "https://console.cloud.google.com/apis/library/places-backend.googleapis.com";
        try
        {
            // Try to find Chrome
            var chromePaths = new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Google\Chrome\Application\chrome.exe")
            };

            var chromePath = chromePaths.FirstOrDefault(File.Exists);

            if (!string.IsNullOrEmpty(chromePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = chromePath,
                    Arguments = url,
                    UseShellExecute = false
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }

            MessageBox.Show(
                "Google Cloud Console açıldı!\n\n" +
                "Adımlar:\n" +
                "1. Google hesabınızla giriş yapın\n" +
                "2. Proje oluşturun veya seçin\n" +
                "3. 'Places API' etkinleştirin\n" +
                "4. Credentials > Create Credentials > API Key\n" +
                "5. API Key'i kopyalayıp buraya yapıştırın\n\n" +
                "Not: İlk kullanımda $200 ücretsiz kredi alırsınız!",
                "API Key Nasıl Alınır?", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Link açılamadı: {ex.Message}\n\nManuel olarak gidin:\n{url}",
                "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SearchCityAsync()
    {
        if (string.IsNullOrEmpty(_googleApiKey))
        {
            MessageBox.Show(
                "Google Places API Key gerekli!\n\n" +
                "'API Key Al' linkine tıklayıp API Key alın.",
                "API Key Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var cityName = _cmbCity.SelectedItem?.ToString() ?? "Bilinmeyen";

        _btnSearch.Enabled = false;
        _btnSearchAll.Enabled = false;
        _lblStatus.Text = $"🔍 {cityName} araştırılıyor...";
        _lblStatus.ForeColor = Color.Yellow;

        try
        {
            var coords = _txtLocation.Text.Split(',');
            var lat = coords[0].Trim();
            var lng = coords[1].Trim();
            var radius = (int)_nudRadius.Value * 1000; // km to meters

            // Search ONLY for EV charging stations
            var evStations = await SearchEvChargersAsync(lat, lng, radius, cityName);

            // Add all found EV charging stations
            var newCount = 0;
            foreach (var station in evStations)
            {
                if (!_foundStations.Any(s => s.PlaceId == station.PlaceId))
                {
                    _foundStations.Add(station);
                    newCount++;
                }
            }

            RefreshList();
            _lblStatus.Text = $"✅ {cityName}: {newCount} yeni EV şarj istasyonu bulundu! (Toplam: {_foundStations.Count})";
            _lblStatus.ForeColor = GreenText;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"❌ Hata: {ex.Message}";
            _lblStatus.ForeColor = Color.Red;
        }
        finally
        {
            _btnSearch.Enabled = true;
            _btnSearchAll.Enabled = true;
        }
    }

    private async Task SearchAllTurkeyAsync()
    {
        if (string.IsNullOrEmpty(_googleApiKey))
        {
            MessageBox.Show(
                "Google Places API Key gerekli!\n\n" +
                "'API Key Al' linkine tıklayıp API Key alın.",
                "API Key Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Grid-based full-Turkey scan (no city filters, no km limit UI)
        // WARNING: This performs many API calls and may incur charges / rate limits.
        _btnSearch.Enabled = false;
        _btnSearchAll.Enabled = false;
        _progressBar.Visible = true;

        // Bounding box for mainland Turkey (conservative)
        const double latMin = 35.8;
        const double latMax = 42.2;
        const double lngMin = 25.9;
        const double lngMax = 44.9;

        // Use a large radius and tile the box with overlapping centers.
        // radiusMeters controls coverage per request. Google max radius is typically 50000.
        var radiusMeters = 50000;

        // step degrees roughly chosen so tiles overlap (0.4 deg ~ 44km)
        const double step = 0.4;

        var centers = new List<(double Lat, double Lng)>();
        for (double lat = latMin; lat <= latMax; lat += step)
        {
            for (double lng = lngMin; lng <= lngMax; lng += step)
            {
                centers.Add((Math.Round(lat, 4), Math.Round(lng, 4)));
            }
        }

        _progressBar.Maximum = centers.Count;
        _progressBar.Value = 0;

        var totalNew = 0;

        try
        {
            foreach (var (lat, lng) in centers)
            {
                _lblStatus.Text = $"🔍 Tarama: {lat:F4},{lng:F4} ({_progressBar.Value + 1}/{centers.Count})";
                _lblStatus.ForeColor = Color.Yellow;
                Application.DoEvents();

                try
                {
                    // search by keyword to capture variations ("şarj" and "charging")
                    var found = await SearchNearbyByKeywordAsync(lat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        lng.ToString(System.Globalization.CultureInfo.InvariantCulture), radiusMeters, "şarj");
                    foreach (var s in found)
                    {
                        if (!_foundStations.Any(x => x.PlaceId == s.PlaceId))
                        {
                            _foundStations.Add(s);
                            totalNew++;
                        }
                    }

                    // also try English keyword to catch other listings
                    var foundEng = await SearchNearbyByKeywordAsync(lat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        lng.ToString(System.Globalization.CultureInfo.InvariantCulture), radiusMeters, "charging");
                    foreach (var s in foundEng)
                    {
                        if (!_foundStations.Any(x => x.PlaceId == s.PlaceId))
                        {
                            _foundStations.Add(s);
                            totalNew++;
                        }
                    }
                }
                catch
                {
                    // continue on errors
                }

                _progressBar.Value++;
                await Task.Delay(250); // rate limiting
            }

            RefreshList();

            // Auto-save final dataset for 2026 snapshot
            try
            {
                var outPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ev_stations_turkiye_2026.json");
                var json = JsonSerializer.Serialize(_foundStations, IndentedJsonOptions);
                File.WriteAllText(outPath, json);
                _lblStatus.Text = $"✅ Tarama tamamlandı. {totalNew} yeni, toplam {_foundStations.Count}. Kaydedildi: {outPath}";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = $"✅ Tarama tamamlandı. {totalNew} yeni, toplam {_foundStations.Count}. Kaydetme hatası: {ex.Message}";
            }

            _lblStatus.ForeColor = GreenText;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"❌ Hata: {ex.Message}";
            _lblStatus.ForeColor = Color.Red;
        }
        finally
        {
            _btnSearch.Enabled = true;
            _btnSearchAll.Enabled = true;
            _progressBar.Visible = false;
        }
    }

    private async Task<List<ChargingStation>> SearchEvChargersAsync(string lat, string lng, int radius, string cityName)
    {
        var stations = new List<ChargingStation>();

        var url = $"https://maps.googleapis.com/maps/api/place/nearbysearch/json" +
                  $"?location={lat},{lng}" +
                  $"&radius={radius}" +
                  $"&type=electric_vehicle_charging_station" +
                  $"&key={_googleApiKey}";

        var response = await _httpClient.GetStringAsync(url);
        using var doc = JsonDocument.Parse(response);

        if (doc.RootElement.TryGetProperty("results", out var results))
        {
            foreach (var place in results.EnumerateArray())
            {
                var station = new ChargingStation
                {
                    Name = place.GetProperty("name").GetString() ?? "Bilinmeyen",
                    PlaceId = place.GetProperty("place_id").GetString() ?? "",
                    StationType = "ev_charging",
                    HasEvCharger = true,
                    City = cityName,
                    FoundDate = DateTime.Now
                };

                if (place.TryGetProperty("geometry", out var geometry) &&
                    geometry.TryGetProperty("location", out var location))
                {
                    station.Latitude = location.GetProperty("lat").GetDouble();
                    station.Longitude = location.GetProperty("lng").GetDouble();
                }

                if (place.TryGetProperty("vicinity", out var vicinity))
                {
                    station.Address = vicinity.GetString() ?? "";
                }

                stations.Add(station);
            }
        }

        return stations;
    }

    // New: search by keyword and follow next_page_token pages
    private async Task<List<ChargingStation>> SearchNearbyByKeywordAsync(string lat, string lng, int radius, string keyword)
    {
        var stations = new List<ChargingStation>();
        var pageToken = string.Empty;
        var attempts = 0;

        do
        {
            var url = $"https://maps.googleapis.com/maps/api/place/nearbysearch/json" +
                      $"?location={lat},{lng}" +
                      $"&radius={radius}" +
                      $"&keyword={Uri.EscapeDataString(keyword)}" +
                      $"&key={_googleApiKey}";

            if (!string.IsNullOrEmpty(pageToken))
                url += $"&pagetoken={pageToken}";

            // Google requires a short delay before using next_page_token
            if (!string.IsNullOrEmpty(pageToken))
                await Task.Delay(2000);

            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);

            if (doc.RootElement.TryGetProperty("results", out var results))
            {
                foreach (var place in results.EnumerateArray())
                {
                    var station = new ChargingStation
                    {
                        Name = place.GetProperty("name").GetString() ?? "Bilinmeyen",
                        PlaceId = place.GetProperty("place_id").GetString() ?? "",
                        StationType = place.GetProperty("types").EnumerateArray().Select(t => t.GetString()).FirstOrDefault() ?? "ev_charging",
                        HasEvCharger = true,
                        FoundDate = DateTime.Now
                    };

                    if (place.TryGetProperty("geometry", out var geometry) &&
                        geometry.TryGetProperty("location", out var location))
                    {
                        station.Latitude = location.GetProperty("lat").GetDouble();
                        station.Longitude = location.GetProperty("lng").GetDouble();
                    }

                    if (place.TryGetProperty("vicinity", out var vicinity))
                        station.Address = vicinity.GetString() ?? "";

                    stations.Add(station);
                }
            }

            pageToken = string.Empty;
            if (doc.RootElement.TryGetProperty("next_page_token", out var tokenElem))
            {
                pageToken = tokenElem.GetString() ?? string.Empty;
            }

            attempts++;
        } while (!string.IsNullOrEmpty(pageToken) && attempts < 5);

        return stations;
    }
    #endregion

    #region Chrome Integration
    private void OpenInChrome()
    {
        var coords = _txtLocation.Text;
        var cityName = _cmbCity.SelectedItem?.ToString() ?? "Türkiye";

        // Open Google Maps with EV charging search
        var url = $"https://www.google.com/maps/search/elektrikli+araç+şarj+istasyonu/@{coords},12z";

        try
        {
            // Try to find Chrome
            var chromePaths = new[]
            {
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
                Environment.ExpandEnvironmentVariables(@"%LocalAppData%\Google\Chrome\Application\chrome.exe")
            };

            var chromePath = chromePaths.FirstOrDefault(File.Exists);

            if (chromePath != null)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = chromePath,
                    Arguments = url,
                    UseShellExecute = false
                });
            }
            else
            {
                // Fallback to default browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }

            _lblStatus.Text = "🌐 Chrome açıldı. EV şarj istasyonu bulduğunuzda 'Manuel Ekle' ile kaydedin.";
            _lblStatus.ForeColor = Color.Cyan;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Chrome açılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OpenSelectedInMaps()
    {
        if (_lstStations.SelectedIndex < 0 || _lstStations.SelectedIndex >= _displayedStations.Count)
        {
            MessageBox.Show("Lütfen bir istasyon seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var station = _displayedStations[_lstStations.SelectedIndex];
        var url = $"https://www.google.com/maps/search/?api=1&query={station.Latitude},{station.Longitude}";

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void OpenStreetView()
    {
        if (_lstStations.SelectedIndex < 0 || _lstStations.SelectedIndex >= _displayedStations.Count)
        {
            MessageBox.Show("Lütfen bir istasyon seçin.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var station = _displayedStations[_lstStations.SelectedIndex];
        var url = $"https://www.google.com/maps/@{station.Latitude},{station.Longitude},3a,75y,90t/data=!3m6!1e1!3m4!1s!2e0!7i16384!8i8192";

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });

        _lblStatus.Text = "👁️ Street View açıldı. EV şarj aleti görürseniz not ekleyin!";
        _lblStatus.ForeColor = Color.Orange;
    }
    #endregion

    #region Manual Entry
    private void AddManualStation()
    {
        using var dialog = new Form
        {
            Text = "🔌 Manuel EV Şarj İstasyonu Ekle",
            ClientSize = new Size(450, 360),
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var lblName = new Label { Text = "İstasyon Adı:", Location = new Point(20, 20), Width = 120, ForeColor = GreenText };
        var txtName = new TextBox { Location = new Point(150, 17), Width = 270, BackColor = Color.Black, ForeColor = GreenText, BorderStyle = BorderStyle.FixedSingle };

        var lblCity = new Label { Text = "Şehir:", Location = new Point(20, 60), Width = 120, ForeColor = GreenText };
        var cmbCity = new ComboBox 
        { 
            Location = new Point(150, 57), 
            Width = 200, 
            BackColor = Color.Black, 
            ForeColor = GreenText,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (var city in TurkishCities.Keys)
            cmbCity.Items.Add(city);
        cmbCity.SelectedIndex = 2; // Ankara default

        var lblAddress = new Label { Text = "Adres:", Location = new Point(20, 100), Width = 120, ForeColor = GreenText };
        var txtAddress = new TextBox { Location = new Point(150, 97), Width = 270, BackColor = Color.Black, ForeColor = GreenText, BorderStyle = BorderStyle.FixedSingle };

        var lblLat = new Label { Text = "Enlem (Lat):", Location = new Point(20, 140), Width = 120, ForeColor = GreenText };
        var txtLat = new TextBox { Location = new Point(150, 137), Width = 150, BackColor = Color.Black, ForeColor = GreenText, BorderStyle = BorderStyle.FixedSingle };

        var lblLng = new Label { Text = "Boylam (Lng):", Location = new Point(20, 180), Width = 120, ForeColor = GreenText };
        var txtLng = new TextBox { Location = new Point(150, 177), Width = 150, BackColor = Color.Black, ForeColor = GreenText, BorderStyle = BorderStyle.FixedSingle };

        var lblInfo = new Label 
        { 
            Text = "💡 İpucu: Google Maps'te sağ tık > 'Koordinatlar'", 
            Location = new Point(20, 215), 
            Width = 400, 
            ForeColor = Color.Gray 
        };

        var lblNotes = new Label { Text = "Notlar:", Location = new Point(20, 250), Width = 120, ForeColor = GreenText };
        var txtNotes = new TextBox 
        { 
            Location = new Point(150, 247), 
            Width = 270, 
            Height = 50,
            Multiline = true,
            BackColor = Color.Black, 
            ForeColor = GreenText, 
            BorderStyle = BorderStyle.FixedSingle 
        };

        var btnOk = new Button
        {
            Text = "✅ Ekle",
            Location = new Point(150, 310),
            Width = 100,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.OK
        };
        btnOk.FlatAppearance.BorderColor = GreenText;

        var btnCancel = new Button
        {
            Text = "❌ İptal",
            Location = new Point(260, 310),
            Width = 100,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = Color.Red,
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel
        };
        btnCancel.FlatAppearance.BorderColor = Color.Red;

        dialog.Controls.AddRange([lblName, txtName, lblCity, cmbCity, lblAddress, txtAddress, 
                                  lblLat, txtLat, lblLng, txtLng, lblInfo, lblNotes, txtNotes, btnOk, btnCancel]);

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var station = new ChargingStation
                {
                    Name = txtName.Text,
                    City = cmbCity.SelectedItem?.ToString() ?? "",
                    Address = txtAddress.Text,
                    Latitude = double.Parse(txtLat.Text.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture),
                    Longitude = double.Parse(txtLng.Text.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture),
                    HasEvCharger = true,
                    StationType = "ev_charging",
                    Notes = txtNotes.Text,
                    FoundDate = DateTime.Now
                };

                _foundStations.Add(station);
                RefreshList();
                _lblStatus.Text = $"✅ '{station.Name}' eklendi!";
                _lblStatus.ForeColor = GreenText;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void DeleteSelected()
    {
        if (_lstStations.SelectedIndex < 0 || _lstStations.SelectedIndex >= _displayedStations.Count) return;

        var result = MessageBox.Show("Seçili istasyonu silmek istediğinize emin misiniz?", 
            "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            var stationToRemove = _displayedStations[_lstStations.SelectedIndex];
            _foundStations.Remove(stationToRemove);
            RefreshList();
        }
    }
    #endregion

    #region Data Management
    private void SaveStations()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ev_stations.json");
        
        try
        {
            var json = JsonSerializer.Serialize(_foundStations, IndentedJsonOptions);
            
            File.WriteAllText(path, json);
            
            MessageBox.Show($"✅ {_foundStations.Count} istasyon kaydedildi!\n\n{path}", 
                "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadStations()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ev_stations.json");
        
        if (!File.Exists(path))
        {
            MessageBox.Show("Kayıtlı istasyon dosyası bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var stations = JsonSerializer.Deserialize<List<ChargingStation>>(json);
            
            if (stations != null)
            {
                _foundStations.Clear();
                _foundStations.AddRange(stations);
                RefreshList();
                
                MessageBox.Show($"✅ {_foundStations.Count} istasyon yüklendi!", 
                    "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Yükleme hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void LoadStationsQuiet()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ev_stations.json");
        
        if (!File.Exists(path)) return;

        try
        {
            var json = File.ReadAllText(path);
            var stations = JsonSerializer.Deserialize<List<ChargingStation>>(json);
            
            if (stations != null)
            {
                _foundStations.AddRange(stations);
                RefreshList();
            }
        }
        catch
        {
            // Quiet fail
        }
    }

    private List<ChargingStation> _displayedStations = [];

    private void ClearList()
    {
        var result = MessageBox.Show("Tüm bulunan istasyonları silmek istediğinize emin misiniz?", 
            "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _foundStations.Clear();
            RefreshList();
            _lblStatus.Text = "🧹 Liste temizlendi.";
            _lblStatus.ForeColor = Color.Gray;
        }
    }

    private void RefreshList()
    {
        _lstStations.Items.Clear();

        // Show all EV charger stations (already filtered at search time)
        _displayedStations = [.. _foundStations];

        foreach (var station in _displayedStations)
        {
            var cityInfo = !string.IsNullOrEmpty(station.City) ? $" [{station.City}]" : "";
            _lstStations.Items.Add($"🔌 {station.Name}{cityInfo}");
        }

        // Update stats - group by city
        var statsLabel = Controls.Find("lblStats", true).FirstOrDefault() as Label;
        if (statsLabel != null)
        {
            var cityCount = _displayedStations.Select(s => s.City).Where(c => !string.IsNullOrEmpty(c)).Distinct().Count();
            statsLabel.Text = cityCount > 0 
                ? $"📊 Toplam: {_displayedStations.Count} EV Şarj İstasyonu | {cityCount} Şehir"
                : $"📊 Toplam: {_displayedStations.Count} EV Şarj İstasyonu";
        }
    }

    private void ShowStationDetails()
    {
        if (_lstStations.SelectedIndex < 0 || _lstStations.SelectedIndex >= _displayedStations.Count)
        {
            _txtDetails.Text = "";
            return;
        }

        var station = _displayedStations[_lstStations.SelectedIndex];
        var cityInfo = !string.IsNullOrEmpty(station.City) ? station.City : "Belirtilmemiş";

        _txtDetails.Text = $"""
            ╔════════════════════════════════════════╗
            ║    🔌 ELEKTRİKLİ ARAÇ ŞARJ İSTASYONU    ║
            ╚════════════════════════════════════════╝

            📍 İsim:        {station.Name}

            🏙️ Şehir:       {cityInfo}

            📮 Adres:       {station.Address}

            🌐 Koordinat:   {station.Latitude:F6}, {station.Longitude:F6}

            🔌 Tip:         Elektrikli Araç Şarj İstasyonu

            📅 Bulunma:     {station.FoundDate:dd.MM.yyyy HH:mm}

            📝 Notlar:      {(string.IsNullOrEmpty(station.Notes) ? "-" : station.Notes)}

            ─────────────────────────────────────────

            🔗 Google Maps:
            https://maps.google.com/?q={station.Latitude},{station.Longitude}

            🔗 Navigasyon:
            https://www.google.com/maps/dir/?api=1&destination={station.Latitude},{station.Longitude}
            """;
    }

    private static string GetStationTypeName(string type) => type switch
    {
        "ev_charging" => "EV Şarj İstasyonu",
        "electric_vehicle_charging_station" => "EV Şarj İstasyonu",
        _ => "EV Şarj İstasyonu"
    };
    #endregion
}
