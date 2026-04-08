namespace Assist.Forms.Online;

using System.Net.Http.Json;
using System.Text.Json;
using Assist.Services;

internal sealed class DecisionOptimizerForm : Form
{
    private static readonly HttpClient Http = AppConstants.SharedHttpClient;

    private readonly ComboBox _cmbUniverse;
    private readonly Button _btnRefresh;
    private readonly Button _btnDecide;
    private readonly DataGridView _grid;
    private readonly RichTextBox _rtb;

    private readonly TrackBar _trkAccuracy;
    private readonly TrackBar _trkCost;
    private readonly TrackBar _trkImpact;
    private readonly TrackBar _trkUncertainty;
    private readonly TrackBar _trkRisk;

    private readonly Label _lblAccuracy;
    private readonly Label _lblCost;
    private readonly Label _lblImpact;
    private readonly Label _lblUncertainty;
    private readonly Label _lblRisk;

    private List<AssetRow> _rows = [];

    public DecisionOptimizerForm()
    {
        Text = "Karar Optimizasyonu (Belirsizlik)";
        ClientSize = new Size(1100, 700);

        var p = UITheme.Palette;
        BackColor = p.Back;
        ForeColor = p.Text;
        Font = new Font("Consolas", 10);

        var top = new Panel { Dock = DockStyle.Top, Height = 66, Padding = new Padding(12), BackColor = p.Surface };

        _cmbUniverse = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 420
        };
        _cmbUniverse.Items.AddRange([
            "FX: USD/TRY",
            "Crypto: BTC/TRY",
            "Crypto: ETH/TRY",
        ]);
        _cmbUniverse.SelectedIndex = 0;

        _btnRefresh = new Button { Text = "↻ Veriyi Çek", Width = 140, Height = 32 };
        _btnRefresh.Click += async (_, _) => await RefreshAsync();

        _btnDecide = new Button { Text = "✓ Karar Ver", Width = 140, Height = 32 };
        _btnDecide.Click += (_, _) => Decide();

        var left = new FlowLayoutPanel { Dock = DockStyle.Left, Width = 420, Padding = new Padding(12), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = p.Back };

        _lblAccuracy = new Label { AutoSize = true };
        _trkAccuracy = NewTrack(50);
        _trkAccuracy.Scroll += (_, _) => SyncWeightLabels();

        _lblCost = new Label { AutoSize = true };
        _trkCost = NewTrack(25);
        _trkCost.Scroll += (_, _) => SyncWeightLabels();

        _lblImpact = new Label { AutoSize = true };
        _trkImpact = NewTrack(25);
        _trkImpact.Scroll += (_, _) => SyncWeightLabels();

        _lblUncertainty = new Label { AutoSize = true };
        _trkUncertainty = NewTrack(15);

        _lblRisk = new Label { AutoSize = true };
        _trkRisk = NewTrack(50);

        left.Controls.Add(MakeSectionTitle("Ağırlıklar (Normalize edilir)"));
        left.Controls.Add(_lblAccuracy);
        left.Controls.Add(_trkAccuracy);
        left.Controls.Add(_lblCost);
        left.Controls.Add(_trkCost);
        left.Controls.Add(_lblImpact);
        left.Controls.Add(_trkImpact);

        left.Controls.Add(MakeSectionTitle("Belirsizlik / Risk"));
        left.Controls.Add(_lblUncertainty);
        left.Controls.Add(_trkUncertainty);
        left.Controls.Add(_lblRisk);
        left.Controls.Add(_trkRisk);

        var center = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _grid.Columns.Add("asset", "Varlık");
        _grid.Columns.Add("price", "Fiyat");
        _grid.Columns.Add("ret", "24s Getiri %");
        _grid.Columns.Add("vol", "Volatilite (proxy) %");
        _grid.Columns.Add("spread", "Maliyet (spread proxy) %");
        _grid.Columns.Add("impact", "Etki (likidite proxy)" );
        _grid.Columns.Add("score", "Skor" );

        center.Controls.Add(_grid);

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 200, Padding = new Padding(12), BackColor = p.Surface };
        _rtb = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = p.Back, ForeColor = p.Text, BorderStyle = BorderStyle.FixedSingle };
        bottom.Controls.Add(_rtb);

        top.Controls.Add(_cmbUniverse);
        top.Controls.Add(Spacer(12));
        top.Controls.Add(_btnRefresh);
        top.Controls.Add(Spacer(8));
        top.Controls.Add(_btnDecide);

        Controls.Add(center);
        Controls.Add(left);
        Controls.Add(bottom);
        Controls.Add(top);

        UITheme.Apply(this);
        UITheme.Apply(_grid);

        SyncWeightLabels();
        UpdateRiskLabels();

        _trkUncertainty.Scroll += (_, _) => UpdateRiskLabels();
        _trkRisk.Scroll += (_, _) => UpdateRiskLabels();

        Shown += async (_, _) => await RefreshAsync();
    }

    private static TrackBar NewTrack(int value)
    {
        return new TrackBar
        {
            Minimum = 0,
            Maximum = 100,
            TickFrequency = 10,
            Value = Math.Clamp(value, 0, 100),
            Width = 360
        };
    }

    private static Control Spacer(int w) => new Panel { Width = w, Height = 1 };

    private static Label MakeSectionTitle(string text)
    {
        return new Label { Text = text, AutoSize = true, Font = new Font("Consolas", 11, FontStyle.Bold) };
    }

    private void SyncWeightLabels()
    {
        var a = _trkAccuracy.Value;
        var c = _trkCost.Value;
        var i = _trkImpact.Value;
        var sum = Math.Max(1, a + c + i);

        _lblAccuracy.Text = $"Doğruluk (signal) ağırlığı: {a * 100 / sum}%";
        _lblCost.Text = $"Maliyet (fee/spread) ağırlığı: {c * 100 / sum}%";
        _lblImpact.Text = $"Etki (likidite) ağırlığı: {i * 100 / sum}%";
    }

    private void UpdateRiskLabels()
    {
        _lblUncertainty.Text = $"Belirsizlik seviyesi: %{_trkUncertainty.Value}";
        _lblRisk.Text = $"Risk toleransı: %{_trkRisk.Value} (yüksek = daha riskli seçime izin)";
    }

    private async Task RefreshAsync()
    {
        try
        {
            _btnRefresh.Enabled = false;
            _rtb.Clear();
            _rtb.AppendText("Veriler çekiliyor...\n");

            _rows = await FetchUniverseAsync();
            BindGrid();

            _rtb.AppendText("\nVeri hazır. İstersen \"Karar Ver\" ile skorla.\n");
        }
        catch (Exception ex)
        {
            _rtb.AppendText($"\nHata: {ex.Message}\n");
        }
        finally
        {
            _btnRefresh.Enabled = true;
        }
    }

    private void BindGrid()
    {
        _grid.Rows.Clear();
        foreach (var r in _rows)
        {
            _grid.Rows.Add(
                r.Asset,
                r.PriceText,
                r.Return24hPct.ToString("F2"),
                r.VolatilityProxyPct.ToString("F2"),
                r.SpreadProxyPct.ToString("F3"),
                r.ImpactProxy.ToString("F2"),
                "--");
        }
    }

    private void Decide()
    {
        if (_rows.Count == 0)
        {
            _rtb.AppendText("Önce veri çekmelisiniz.\n");
            return;
        }

        var a = _trkAccuracy.Value;
        var c = _trkCost.Value;
        var i = _trkImpact.Value;
        var sum = Math.Max(1, a + c + i);
        var wA = a / (double)sum;
        var wC = c / (double)sum;
        var wI = i / (double)sum;

        var uncertainty = _trkUncertainty.Value / 100.0;
        var riskTol = _trkRisk.Value / 100.0;

        // Normalize metrics across candidates
        double maxAbsRet = Math.Max(0.0001, _rows.Max(r => Math.Abs(r.Return24hPct)));
        double maxVol = Math.Max(0.0001, _rows.Max(r => r.VolatilityProxyPct));
        double maxSpread = Math.Max(0.0001, _rows.Max(r => r.SpreadProxyPct));
        double maxImpact = Math.Max(0.0001, _rows.Max(r => r.ImpactProxy));

        foreach (var r in _rows)
        {
            // "Accuracy/signal" proxy: positive return is treated as signal, with uncertainty penalty
            var signal = (r.Return24hPct / maxAbsRet);
            var signalAfterUncertainty = signal * (1.0 - uncertainty);

            // Cost proxy: higher spread worse
            var cost = r.SpreadProxyPct / maxSpread;

            // Impact proxy: higher liquidity better
            var impact = r.ImpactProxy / maxImpact;

            // Risk proxy: volatility hurts, but risk tolerance reduces penalty
            var riskPenalty = (r.VolatilityProxyPct / maxVol) * (1.0 - riskTol);

            r.Score = (wA * signalAfterUncertainty) - (wC * cost) + (wI * impact) - riskPenalty;
        }

        var best = _rows.OrderByDescending(r => r.Score).First();

        // Update grid
        for (var idx = 0; idx < _rows.Count; idx++)
        {
            _grid.Rows[idx].Cells[6].Value = _rows[idx].Score.ToString("F4");
        }

        _rtb.AppendText("\n--- Karar ---\n");
        _rtb.AppendText($"Seçim: {best.Asset}\n");
        _rtb.AppendText($"Fiyat: {best.PriceText}\n");
        _rtb.AppendText($"24s getiri: %{best.Return24hPct:F2}, Vol proxy: %{best.VolatilityProxyPct:F2}, Spread proxy: %{best.SpreadProxyPct:F3}\n");
        _rtb.AppendText($"Skor: {best.Score:F4}\n\n");
        _rtb.AppendText("Not: Bu ekran otomatik yatırım tavsiyesi değildir; yalnızca ağırlıklandırılmış çok-kriterli bir karar destek göstergesidir.\n");
    }

    private async Task<List<AssetRow>> FetchUniverseAsync()
    {
        // Pull real-time-ish data:
        // - FX USD/TRY from exchangerate.host
        // - Crypto from Binance ticker (TRY pairs)

        var selected = _cmbUniverse.SelectedItem?.ToString() ?? "";

        if (selected.StartsWith("FX"))
        {
            var fx = await FetchUsdTryAsync();
            return [fx];
        }

        if (selected.Contains("BTC"))
        {
            var btc = await FetchBinanceTryAsync("BTCTRY");
            return [btc];
        }

        if (selected.Contains("ETH"))
        {
            var eth = await FetchBinanceTryAsync("ETHTRY");
            return [eth];
        }

        return [];
    }

    private static async Task<AssetRow> FetchUsdTryAsync()
    {
        // exchangerate.host free endpoint
        var url = "https://api.exchangerate.host/latest?base=USD&symbols=TRY";
        using var resp = await Http.GetAsync(url);
        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        var rate = doc.RootElement.GetProperty("rates").GetProperty("TRY").GetDouble();

        return new AssetRow
        {
            Asset = "USD/TRY",
            PriceText = rate.ToString("F4"),
            Return24hPct = 0, // FX endpoint doesn't provide 24h change without more calls
            VolatilityProxyPct = 0.6,
            SpreadProxyPct = 0.05,
            ImpactProxy = 0.9
        };
    }

    private static async Task<AssetRow> FetchBinanceTryAsync(string symbol)
    {
        var url = $"https://api.binance.com/api/v3/ticker/24hr?symbol={symbol}";
        var data = await Http.GetFromJsonAsync<Binance24Hr>(url);
        if (data is null) throw new InvalidOperationException("Binance verisi alınamadı.");

        var last = double.TryParse(data.lastPrice, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lp)
            ? lp : 0;
        var changePct = double.TryParse(data.priceChangePercent, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cp)
            ? cp : 0;

        // Vol proxy: use high/low range percent
        var high = double.TryParse(data.highPrice, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var hp)
            ? hp : 0;
        var low = double.TryParse(data.lowPrice, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lw)
            ? lw : 0;

        var volProxy = last > 0 && high > 0 && low > 0 ? (high - low) / last * 100.0 : 0;

        // Spread proxy: not available in 24hr endpoint; approximate from volatility
        var spreadProxy = Math.Clamp(volProxy / 200.0, 0.02, 0.50);

        // Impact proxy: use quote volume (TRY) scaled
        var qv = double.TryParse(data.quoteVolume, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var q)
            ? q : 0;
        var impact = qv <= 0 ? 0.1 : Math.Clamp(Math.Log10(qv) / 10.0, 0.1, 1.0);

        return new AssetRow
        {
            Asset = symbol,
            PriceText = last.ToString("N2"),
            Return24hPct = changePct,
            VolatilityProxyPct = volProxy,
            SpreadProxyPct = spreadProxy,
            ImpactProxy = impact
        };
    }

    private sealed class AssetRow
    {
        public required string Asset { get; init; }
        public string PriceText { get; init; } = "--";
        public double Return24hPct { get; init; }
        public double VolatilityProxyPct { get; init; }
        public double SpreadProxyPct { get; init; }
        public double ImpactProxy { get; init; }
        public double Score { get; set; }
    }

    private sealed class Binance24Hr
    {
        public string symbol { get; set; } = "";
        public string lastPrice { get; set; } = "0";
        public string priceChangePercent { get; set; } = "0";
        public string highPrice { get; set; } = "0";
        public string lowPrice { get; set; } = "0";
        public string quoteVolume { get; set; } = "0";
    }
}
