using System.Net.Http.Json;
using System.Text.Json;
using Assist.Services;

namespace Assist;

public sealed class CurrencyConverterForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly HttpClient Http = new();

    // Comprehensive fallback list – the API may return even more
    private static readonly string[] DefaultCurrencies =
    [
        // Major
        "USD", "EUR", "GBP", "JPY", "CHF", "CAD", "AUD", "NZD", "CNY",
        // Turkey & Middle East
        "TRY", "AED", "SAR", "QAR", "BHD", "KWD", "OMR", "ILS", "JOD", "EGP", "IQD", "IRR", "LBP",
        // Asia-Pacific
        "INR", "KRW", "SGD", "HKD", "TWD", "THB", "MYR", "IDR", "PHP", "VND", "PKR", "BDT", "LKR",
        // Europe
        "SEK", "NOK", "DKK", "PLN", "CZK", "HUF", "RON", "BGN", "HRK", "ISK", "RSD", "UAH", "GEL", "MDL",
        // Americas
        "MXN", "BRL", "ARS", "CLP", "COP", "PEN", "UYU", "DOP", "CRC",
        // Africa
        "ZAR", "NGN", "KES", "MAD", "TND", "GHS", "XOF", "XAF",
        // Other
        "RUB", "KZT", "UZS", "AZN",
    ];

    private readonly ComboBox _cmbFrom;
    private readonly ComboBox _cmbTo;
    private readonly TextBox _txtAmount;
    private readonly TextBox _txtResult;
    private readonly Label _lblRate;
    private readonly Label _lblUpdate;
    private Dictionary<string, double>? _rates;
    private string _rateBase = "";

    public CurrencyConverterForm()
    {
        Text = "Döviz Çevirici";
        ClientSize = new Size(700, 340);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        int y = 16;
        Controls.Add(MakeLabel("Miktar:", 20, y + 3));

        _txtAmount = new TextBox
        {
            Location = new Point(140, y),
            Width = 250,
            Height = 28,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 14),
            Text = "1"
        };
        Controls.Add(_txtAmount);

        y += 48;
        Controls.Add(MakeLabel("Kaynak:", 20, y));
        _cmbFrom = MakeCombo(140, y, 130);
        Controls.Add(_cmbFrom);

        var btnSwap = new Button
        {
            Text = "⇅",
            Location = new Point(284, y - 2),
            Width = 40,
            Height = 28,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };
        btnSwap.FlatAppearance.BorderColor = GreenText;
        btnSwap.Click += (_, _) =>
        {
            (_cmbFrom.SelectedIndex, _cmbTo.SelectedIndex) = (_cmbTo.SelectedIndex, _cmbFrom.SelectedIndex);
        };
        Controls.Add(btnSwap);

        Controls.Add(MakeLabel("Hedef:", 340, y));
        _cmbTo = MakeCombo(440, y, 130);
        Controls.Add(_cmbTo);

        y += 48;
        var btnConvert = new Button
        {
            Text = "Çevir",
            Location = new Point(140, y),
            Width = 180,
            Height = 34,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 11, FontStyle.Bold)
        };
        btnConvert.FlatAppearance.BorderColor = GreenText;
        btnConvert.Click += async (_, _) => await ConvertAsync();
        Controls.Add(btnConvert);

        y += 52;
        Controls.Add(MakeLabel("Sonuç:", 20, y + 3));

        _txtResult = new TextBox
        {
            Location = new Point(140, y),
            Width = 400,
            Height = 28,
            BackColor = Color.FromArgb(15, 15, 15),
            ForeColor = Color.Cyan,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 14),
            ReadOnly = true
        };
        Controls.Add(_txtResult);

        y += 42;
        _lblRate = new Label
        {
            Text = "",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = Color.DarkGray,
            Font = new Font("Consolas", 9)
        };
        Controls.Add(_lblRate);

        y += 22;
        _lblUpdate = new Label
        {
            Text = "",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = Color.DarkGray,
            Font = new Font("Consolas", 8)
        };
        Controls.Add(_lblUpdate);

        _txtAmount.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await ConvertAsync(); }
        };

        Load += async (_, _) => await LoadCurrenciesAndConvertAsync();
    }

    /// <summary>
    /// Fetches all available currency codes from the API and populates combo boxes.
    /// Falls back to the hardcoded list if the API call fails.
    /// </summary>
    private async Task LoadCurrenciesAndConvertAsync()
    {
        await Loading.RunAsync(this, async () =>
        {
            try
            {
                var url = "https://open.er-api.com/v6/latest/USD";
                var json = await Http.GetFromJsonAsync<JsonElement>(url);
                var rates = json.GetProperty("rates");

                _rates = [];
                foreach (var prop in rates.EnumerateObject())
                    _rates[prop.Name] = prop.Value.GetDouble();
                _rateBase = "USD";

                // Populate combos with ALL currencies from the API (sorted)
                var allCodes = _rates.Keys.Order().ToArray();
                PopulateCombos(allCodes);

                if (json.TryGetProperty("time_last_update_utc", out var timeVal))
                    _lblUpdate.Text = $"Son güncelleme: {timeVal.GetString()}";
            }
            catch
            {
                // API failed – use fallback list
                PopulateCombos(DefaultCurrencies);
            }

            // Trigger initial conversion
            if (_cmbFrom.SelectedItem is not null && _cmbTo.SelectedItem is not null)
                await ConvertCoreAsync();
        }, "Para birimleri yükleniyor...");
    }

    private void PopulateCombos(string[] codes)
    {
        _cmbFrom.SelectedIndexChanged -= OnComboChanged;
        _cmbTo.SelectedIndexChanged -= OnComboChanged;

        _cmbFrom.Items.Clear();
        _cmbTo.Items.Clear();
        _cmbFrom.Items.AddRange(codes);
        _cmbTo.Items.AddRange(codes);

        // Default selections: USD → TRY
        _cmbFrom.SelectedIndex = Array.IndexOf(codes, "USD") is var ui and >= 0 ? ui : 0;
        _cmbTo.SelectedIndex = Array.IndexOf(codes, "TRY") is var ti and >= 0 ? ti : Math.Min(1, codes.Length - 1);

        _cmbFrom.SelectedIndexChanged += OnComboChanged;
        _cmbTo.SelectedIndexChanged += OnComboChanged;
    }

    private async void OnComboChanged(object? sender, EventArgs e) => await ConvertAsync();

    private async Task ConvertAsync()
    {
        if (!double.TryParse(_txtAmount.Text, out _)) return;

        await Loading.RunAsync(this, async () =>
        {
            await ConvertCoreAsync();
        }, "Kur bilgisi alınıyor...");
    }

    private async Task ConvertCoreAsync()
    {
        if (!double.TryParse(_txtAmount.Text, out var amount)) return;

        var from = _cmbFrom.SelectedItem?.ToString() ?? "USD";
        var to = _cmbTo.SelectedItem?.ToString() ?? "TRY";

        if (_rates is null || _rateBase != from)
        {
            try
            {
                var url = $"https://open.er-api.com/v6/latest/{from}";
                var json = await Http.GetFromJsonAsync<JsonElement>(url);
                var rates = json.GetProperty("rates");

                _rates = [];
                foreach (var prop in rates.EnumerateObject())
                    _rates[prop.Name] = prop.Value.GetDouble();

                _rateBase = from;

                if (json.TryGetProperty("time_last_update_utc", out var timeVal))
                    _lblUpdate.Text = $"Son güncelleme: {timeVal.GetString()}";
            }
            catch (HttpRequestException ex)
            {
                _txtResult.Text = $"Hata: {ex.Message}";
                return;
            }
        }

        if (_rates.TryGetValue(to, out var rate))
        {
            var result = amount * rate;
            _txtResult.Text = $"{result:N4} {to}";
            _lblRate.Text = $"1 {from} = {rate:N4} {to}";
        }
        else
        {
            _txtResult.Text = $"{to} kuru bulunamadı.";
        }
    }

    private static Label MakeLabel(string text, int x, int y) => new()
    {
        Text = text,
        Location = new Point(x, y),
        AutoSize = true,
        ForeColor = GreenText
    };

    private static ComboBox MakeCombo(int x, int y, int w) => new()
    {
        Location = new Point(x, y),
        Width = w,
        DropDownStyle = ComboBoxStyle.DropDownList,
        BackColor = Color.Black,
        ForeColor = GreenText,
        FlatStyle = FlatStyle.Flat
    };
}
