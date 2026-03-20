using System.Net.Http.Json;
using System.Text.Json;
using Assist.Services;

namespace Assist;

public sealed class EarthquakeForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly HttpClient Http = new();

    private readonly ComboBox _cmbRegion;
    private readonly ComboBox _cmbMag;
    private readonly Button _btnFetch;
    private readonly FlowLayoutPanel _resultsPanel;
    private readonly Label _lblCount;

    public EarthquakeForm()
    {
        Text = "Deprem Takibi";
        ClientSize = new Size(920, 640);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Black };

        var lblRegion = new Label
        {
            Text = "Bölge:",
            Location = new Point(10, 12),
            AutoSize = true,
            ForeColor = GreenText
        };

        _cmbRegion = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 110,
            Location = new Point(90, 8),
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat
        };
        _cmbRegion.Items.AddRange(["Türkiye", "Dünya"]);
        _cmbRegion.SelectedIndex = 0;

        var lblMag = new Label
        {
            Text = "Min Büyüklük:",
            Location = new Point(220, 12),
            AutoSize = true,
            ForeColor = GreenText
        };

        _cmbMag = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 70,
            Location = new Point(368, 8),
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat
        };
        _cmbMag.Items.AddRange(["1.0", "2.0", "2.5", "3.0", "4.0", "5.0"]);
        _cmbMag.SelectedIndex = 2;

        _btnFetch = new Button
        {
            Text = "Getir",
            Location = new Point(456, 7),
            Width = 120,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };
        _btnFetch.FlatAppearance.BorderColor = GreenText;

        _lblCount = new Label
        {
            Text = "",
            Location = new Point(594, 12),
            AutoSize = true,
            ForeColor = Color.Yellow
        };

        topPanel.Controls.AddRange([lblRegion, _cmbRegion, lblMag, _cmbMag, _btnFetch, _lblCount]);

        _resultsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.Black,
            WrapContents = false,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(6)
        };

        Controls.Add(_resultsPanel);
        Controls.Add(topPanel);

        _btnFetch.Click += async (_, _) => await FetchAsync();
        Load += async (_, _) => await FetchAsync();
    }

    private async Task FetchAsync()
    {
        await Loading.RunAsync(this, async () =>
        {
            var mag = _cmbMag.SelectedItem?.ToString() ?? "2.5";
            var isTurkey = _cmbRegion.SelectedIndex == 0;
            var start = DateTime.UtcNow.AddDays(-7).ToString("yyyy-MM-dd");

            var url = $"https://earthquake.usgs.gov/fdsnws/event/1/query?format=geojson&limit=50&orderby=time&minmagnitude={mag}&starttime={start}";
            if (isTurkey)
                url += "&minlatitude=35&maxlatitude=43&minlongitude=25&maxlongitude=45";

            var json = await Http.GetFromJsonAsync<JsonElement>(url);
            var features = json.GetProperty("features");

            _resultsPanel.Controls.Clear();
            _lblCount.Text = $"Toplam: {features.GetArrayLength()} deprem";

            if (features.GetArrayLength() == 0)
            {
                _resultsPanel.Controls.Add(new Label
                {
                    Text = "Son 7 günde deprem bulunamadı.",
                    ForeColor = Color.Yellow,
                    AutoSize = true,
                    Padding = new Padding(10)
                });
                return;
            }

            var cardWidth = _resultsPanel.ClientSize.Width - 30;
            foreach (var feature in features.EnumerateArray())
            {
                var props = feature.GetProperty("properties");
                var magnitude = props.TryGetProperty("mag", out var magVal) && magVal.ValueKind == JsonValueKind.Number
                    ? magVal.GetDouble() : 0.0;
                var place = props.TryGetProperty("place", out var placeVal) ? placeVal.GetString() ?? "-" : "-";
                var timeMs = props.TryGetProperty("time", out var timeVal) ? timeVal.GetInt64() : 0;
                var depth = 0.0;
                if (feature.TryGetProperty("geometry", out var geom) &&
                    geom.TryGetProperty("coordinates", out var coords) &&
                    coords.GetArrayLength() >= 3)
                {
                    depth = coords[2].GetDouble();
                }

                var quakeTime = DateTimeOffset.FromUnixTimeMilliseconds(timeMs).LocalDateTime;
                _resultsPanel.Controls.Add(CreateCard(cardWidth, magnitude, place, quakeTime, depth));
            }
        }, "Depremler yükleniyor...");
    }

    private static Panel CreateCard(int width, double magnitude, string place, DateTime quakeTime, double depth)
    {
        var magColor = GetMagnitudeColor(magnitude);
        var elapsed = DateTime.Now - quakeTime;
        var agoText = elapsed.TotalMinutes < 60
            ? $"{elapsed.TotalMinutes:F0} dk önce"
            : elapsed.TotalHours < 24
                ? $"{elapsed.TotalHours:F0} saat önce"
                : $"{elapsed.TotalDays:F0} gün önce";

        var card = new Panel
        {
            Width = width,
            Height = 78,
            BackColor = Color.FromArgb(20, 20, 20),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(3)
        };

        var lblMag = new Label
        {
            Text = $" {magnitude:F1} ",
            Font = new Font("Consolas", 16, FontStyle.Bold),
            ForeColor = Color.Black,
            BackColor = magColor,
            AutoSize = false,
            Width = 64,
            Height = 40,
            Location = new Point(8, 8),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lblPlace = new Label
        {
            Text = place,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold),
            AutoSize = false,
            Width = width - 100,
            Height = 20,
            Location = new Point(82, 8)
        };

        var lblDetails = new Label
        {
            Text = $"📅 {quakeTime:dd.MM.yyyy HH:mm}   |   📏 Derinlik: {depth:F1} km",
            ForeColor = Color.LightGray,
            AutoSize = false,
            Width = width - 100,
            Height = 20,
            Location = new Point(82, 32)
        };

        var lblAgo = new Label
        {
            Text = agoText,
            ForeColor = Color.DarkGray,
            AutoSize = false,
            Width = width - 100,
            Height = 18,
            Location = new Point(82, 54),
            Font = new Font("Consolas", 8)
        };

        card.Controls.AddRange([lblMag, lblPlace, lblDetails, lblAgo]);
        return card;
    }

    private static Color GetMagnitudeColor(double magnitude) => magnitude switch
    {
        < 3.0 => Color.FromArgb(0, 200, 0),
        < 4.0 => Color.FromArgb(180, 200, 0),
        < 5.0 => Color.Yellow,
        < 6.0 => Color.Orange,
        _ => Color.Red
    };
}
