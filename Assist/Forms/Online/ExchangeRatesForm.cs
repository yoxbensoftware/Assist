using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Text.Json;
using Assist.Services;

namespace Assist.Forms.Online;

internal sealed class ExchangeRatesForm : Form
{
    private static readonly Color CBack = Color.FromArgb(8, 8, 8);
    private static readonly Color CPanel = Color.FromArgb(14, 14, 14);
    private static readonly Color CCard = Color.FromArgb(18, 18, 18);
    private static readonly Color CCardSel = Color.FromArgb(0, 28, 10);
    private static readonly Color CBorder = Color.FromArgb(0, 90, 24);
    private static readonly Color CText = Color.FromArgb(222, 222, 222);
    private static readonly Color CMuted = Color.FromArgb(120, 120, 120);
    private static readonly Color CGreen = Color.FromArgb(66, 255, 132);
    private static readonly Color CRed = Color.FromArgb(255, 86, 86);
    private static readonly Color CGrid = Color.FromArgb(28, 28, 28);

    private sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel() => DoubleBuffered = true;
    }

    private sealed record AssetDef(string Ticker, string Name, string PairLabel, string Emoji, string Currency, double Multiplier = 1.0);

    private sealed record PeriodDef(string Label, string Range, string Interval, string TimeFormat);

    private sealed record AssetPrice(double Current, double ChangePct, string Currency, IReadOnlyList<(DateTime Dt, double Price)> History);

    private static readonly AssetDef[] Assets =
    [
        // Real assets / forex / commodities
        new("USDTRY=X", "ABD Doları", "USD / TRY", "🇺🇸", "TRY"),
        new("EURTRY=X", "Euro", "EUR / TRY", "🇪🇺", "TRY"),
        new("GBPTRY=X", "İngiliz Sterlini", "GBP / TRY", "🇬🇧", "TRY"),
        new("JPYTRY=X", "Japon Yeni", "100 JPY / TRY", "🇯🇵", "TRY", 100.0),
        new("CHFTRY=X", "İsviçre Frangı", "CHF / TRY", "🇨🇭", "TRY"),
        new("GC=F", "Altın", "XAU / USD", "🥇", "USD"),
        new("SI=F", "Gümüş", "XAG / USD", "🥈", "USD"),
        new("CL=F", "Ham Petrol", "WTI / USD", "🛢️", "USD"),
        new("PL=F", "Platin", "XPT / USD", "⚪", "USD"),
        new("PA=F", "Paladyum", "XPD / USD", "⚫", "USD"),

        // Coins
        new("BTC-USD", "Bitcoin", "BTC / USD", "₿", "USD"),
        new("ETH-USD", "Ethereum", "ETH / USD", "Ξ", "USD"),
        new("BNB-USD", "BNB", "BNB / USD", "◆", "USD"),
        new("SOL-USD", "Solana", "SOL / USD", "◎", "USD"),
        new("XRP-USD", "XRP", "XRP / USD", "◈", "USD"),
        new("ADA-USD", "Cardano", "ADA / USD", "A", "USD"),
        new("DOGE-USD", "Dogecoin", "DOGE / USD", "Ð", "USD"),
        new("TRX-USD", "Tron", "TRX / USD", "T", "USD"),
        new("AVAX-USD", "Avalanche", "AVAX / USD", "A", "USD"),
        new("LTC-USD", "Litecoin", "LTC / USD", "Ł", "USD"),
    ];

    private static readonly PeriodDef[] Periods =
    [
        new("Günlük", "1d", "5m", "HH:mm"),
        new("Haftalık", "5d", "1h", "dd MMM HH:mm"),
        new("Aylık", "1mo", "1d", "dd MMM"),
        new("Yıllık", "1y", "1d", "dd MMM"),
    ];

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    static ExchangeRatesForm()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Assist/1.0");
    }

    private readonly Dictionary<string, AssetPrice> _prices = [];
    private readonly Dictionary<string, Panel> _cards = [];

    private AssetDef _selectedAsset = Assets[0];
    private PeriodDef _selectedPeriod = Periods[0];
    private Point _hoverPoint = Point.Empty;
    private bool _isLoading;
    private DateTime _lastUpdated;

    private Panel _topBar = null!;
    private Label _titleLabel = null!;
    private Button[] _periodButtons = null!;
    private Button _refreshButton = null!;
    private Label _statusLabel = null!;

    private FlowLayoutPanel _cardList = null!;
    private Label _headerAsset = null!;
    private Label _headerPrice = null!;
    private Label _headerChange = null!;
    private Label _headerPair = null!;
    private DoubleBufferedPanel _chartCanvas = null!;

    public ExchangeRatesForm()
    {
        Text = "📊 Piyasa 20";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 620);
        Size = new Size(1280, 760);
        BackColor = CBack;
        ForeColor = CText;
        DoubleBuffered = true;

        BuildUi();
        Shown += async (_, _) => await RefreshAllAsync();
    }

    private void BuildUi()
    {
        SuspendLayout();
        BuildTopBar();
        BuildLeftPane();
        BuildRightPane();
        ResumeLayout(performLayout: true);
    }

    private void BuildTopBar()
    {
        _topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = Color.FromArgb(11, 11, 11),
        };

        _titleLabel = new Label
        {
            Text = "📊 Piyasa 20 · 10 gerçek varlık + 10 coin",
            AutoSize = true,
            ForeColor = CGreen,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Location = new Point(14, 15),
        };
        _topBar.Controls.Add(_titleLabel);

        _periodButtons = new Button[Periods.Length];
        int startX = 320;
        for (int i = 0; i < Periods.Length; i++)
        {
            var index = i;
            var btn = new Button
            {
                Text = Periods[i].Label,
                Size = new Size(86, 30),
                Location = new Point(startX + i * 92, 12),
                FlatStyle = FlatStyle.Flat,
                BackColor = i == 0 ? CGreen : CCard,
                ForeColor = i == 0 ? Color.Black : CText,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            btn.FlatAppearance.BorderColor = CBorder;
            btn.Click += async (_, _) => await SelectPeriodAsync(index);
            _periodButtons[i] = btn;
            _topBar.Controls.Add(btn);
        }

        _refreshButton = new Button
        {
            Text = "⟳ Yenile",
            Size = new Size(80, 30),
            Location = new Point(startX + Periods.Length * 92 + 10, 12),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 52, 20),
            ForeColor = CGreen,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Cursor = Cursors.Hand,
        };
        _refreshButton.FlatAppearance.BorderColor = CBorder;
        _refreshButton.Click += async (_, _) => await RefreshAllAsync();
        _topBar.Controls.Add(_refreshButton);

        _statusLabel = new Label
        {
            Text = "Hazır",
            AutoSize = true,
            ForeColor = CMuted,
            Font = new Font("Segoe UI", 8f),
            Location = new Point(_refreshButton.Right + 16, 18),
        };
        _topBar.Controls.Add(_statusLabel);

        _topBar.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = CBorder });
        Controls.Add(_topBar);
    }

    private void BuildLeftPane()
    {
        var left = new Panel
        {
            Dock = DockStyle.Left,
            Width = 294,
            BackColor = CPanel,
        };

        left.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = CBorder });

        var title = new Label
        {
            Text = "  VARLIKLAR",
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Segoe UI", 7.75f, FontStyle.Bold),
            ForeColor = CMuted,
            BackColor = Color.FromArgb(11, 11, 11),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        left.Controls.Add(title);

        _cardList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = CPanel,
            Padding = new Padding(6, 4, 6, 6),
        };

        foreach (var asset in Assets)
            _cardList.Controls.Add(CreateCard(asset));

        left.Controls.Add(_cardList);
        left.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 1, BackColor = CBorder });
        Controls.Add(left);
    }

    private Panel CreateCard(AssetDef asset)
    {
        var card = new Panel
        {
            Width = 272,
            Height = 78,
            Margin = new Padding(0, 0, 0, 5),
            BackColor = CCard,
            Cursor = Cursors.Hand,
        };
        card.Paint += (_, e) => PaintCard(e.Graphics, card, asset);
        card.Click += async (_, _) => await SelectAssetAsync(asset);
        _cards[asset.Ticker] = card;
        return card;
    }

    private void BuildRightPane()
    {
        var right = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = CBack,
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 88,
            BackColor = Color.FromArgb(10, 10, 10),
            Padding = new Padding(20, 12, 20, 12),
        };

        _headerAsset = new Label
        {
            AutoSize = true,
            ForeColor = CMuted,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Location = new Point(20, 8),
        };

        _headerPrice = new Label
        {
            AutoSize = true,
            ForeColor = CText,
            Font = new Font("Segoe UI", 22f, FontStyle.Bold),
            Location = new Point(20, 24),
        };

        _headerChange = new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
        };

        _headerPair = new Label
        {
            AutoSize = true,
            ForeColor = CMuted,
            Font = new Font("Segoe UI", 8f),
            Location = new Point(20, 60),
        };

        header.Controls.AddRange([_headerAsset, _headerPrice, _headerChange, _headerPair]);
        header.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = CGrid });
        right.Controls.Add(header);

        _chartCanvas = new DoubleBufferedPanel
        {
            Dock = DockStyle.Fill,
            BackColor = CBack,
        };
        _chartCanvas.Paint += OnChartPaint;
        _chartCanvas.MouseMove += (_, e) => { _hoverPoint = e.Location; _chartCanvas.Invalidate(); };
        _chartCanvas.MouseLeave += (_, _) => { _hoverPoint = Point.Empty; _chartCanvas.Invalidate(); };
        right.Controls.Add(_chartCanvas);

        Controls.Add(right);
        UpdateHeader();
    }

    private void PaintCard(Graphics g, Panel card, AssetDef asset)
    {
        bool selected = _selectedAsset.Ticker == asset.Ticker;
        bool hasPrice = _prices.TryGetValue(asset.Ticker, out var price);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(selected ? CCardSel : CCard);

        if (selected)
        {
            using var pen = new Pen(CBorder, 2);
            g.DrawRectangle(pen, 1, 1, card.Width - 2, card.Height - 2);
        }

        using var sepPen = new Pen(Color.FromArgb(34, 34, 34));
        g.DrawLine(sepPen, 8, card.Height - 1, card.Width - 8, card.Height - 1);

        using var emojiFont = new Font("Segoe UI Emoji", 14f);
        using var nameFont = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var pairFont = new Font("Segoe UI", 7.5f);
        using var textBrush = new SolidBrush(CText);
        using var mutedBrush = new SolidBrush(CMuted);

        g.DrawString(asset.Emoji, emojiFont, textBrush, 8, 8);
        g.DrawString(asset.Name, nameFont, textBrush, 44, 8);
        g.DrawString(asset.PairLabel, pairFont, mutedBrush, 44, 29);

        if (hasPrice)
        {
            var clr = price!.ChangePct >= 0 ? CGreen : CRed;
            using var priceFont = new Font("Segoe UI", 10.25f, FontStyle.Bold);
            using var changeFont = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            using var priceBrush = new SolidBrush(CText);
            using var changeBrush = new SolidBrush(clr);

            string current = FormatPrice(price.Current * asset.Multiplier, price.Currency);
            string change = $"{(price.ChangePct >= 0 ? "▲" : "▼")} {Math.Abs(price.ChangePct):F2}%";
            g.DrawString(current, priceFont, priceBrush, 8, 47);
            var chSize = g.MeasureString(change, changeFont);
            g.DrawString(change, changeFont, changeBrush, card.Width - (int)chSize.Width - 8, 30);
        }
        else if (_isLoading)
        {
            using var waitFont = new Font("Segoe UI", 7.5f);
            using var waitBrush = new SolidBrush(CMuted);
            g.DrawString("Yükleniyor...", waitFont, waitBrush, 8, 48);
        }
    }

    private void UpdateHeader()
    {
        if (!IsHandleCreated)
            return;

        _headerAsset.Text = $"{_selectedAsset.Emoji}  {_selectedAsset.Name}  ·  {_selectedPeriod.Label}";

        if (_prices.TryGetValue(_selectedAsset.Ticker, out var price))
        {
            _headerPrice.Text = FormatPrice(price.Current * _selectedAsset.Multiplier, price.Currency);
            bool up = price.ChangePct >= 0;
            _headerChange.Text = $" {(up ? "▲" : "▼")} {Math.Abs(price.ChangePct):F2}%";
            _headerChange.ForeColor = up ? CGreen : CRed;
            _headerChange.Location = new Point(_headerPrice.Left + TextRenderer.MeasureText(_headerPrice.Text, _headerPrice.Font).Width + 10, _headerPrice.Top + 11);
            _headerPair.Text = $"{_selectedAsset.PairLabel}  ·  Son güncelleme: {_lastUpdated:HH:mm:ss}";
        }
        else
        {
            _headerPrice.Text = "—";
            _headerChange.Text = string.Empty;
            _headerPair.Text = _selectedAsset.PairLabel;
        }
    }

    private void OnChartPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        var rc = _chartCanvas.ClientRectangle;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(CBack);

        if (!_prices.TryGetValue(_selectedAsset.Ticker, out var price) || price is null || price.History.Count < 2)
        {
            using var font = new Font("Segoe UI", 11f);
            using var brush = new SolidBrush(CMuted);
            string msg = _isLoading ? "Veriler yükleniyor..." : "Veri bulunamadı";
            var size = g.MeasureString(msg, font);
            g.DrawString(msg, font, brush, (rc.Width - size.Width) / 2, (rc.Height - size.Height) / 2);
            return;
        }

        const int padL = 86;
        const int padR = 18;
        const int padT = 18;
        const int padB = 44;

        int chartW = rc.Width - padL - padR;
        int chartH = rc.Height - padT - padB;
        if (chartW <= 10 || chartH <= 10)
            return;

        double mult = _selectedAsset.Multiplier;
        var history = price.History;
        double minP = history.Min(x => x.Price) * mult;
        double maxP = history.Max(x => x.Price) * mult;
        double range = Math.Max(maxP - minP, 0.0000001);
        double extra = range * 0.06;
        minP -= extra;
        maxP += extra;
        range = maxP - minP;

        double ToY(double v) => padT + chartH - (v - minP) / range * chartH;
        double ToX(int index) => padL + (double)index / (history.Count - 1) * chartW;

        using var gridPen = new Pen(CGrid, 1) { DashStyle = DashStyle.Dash };
        using var axisFont = new Font("Segoe UI", 7.5f);
        using var axisBrush = new SolidBrush(CMuted);
        using var rightAlign = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
        using var centerAlign = new StringFormat { Alignment = StringAlignment.Center };

        for (int i = 0; i <= 5; i++)
        {
            double frac = i / 5.0;
            float y = (float)(padT + frac * chartH);
            double value = maxP - frac * range;
            string label = FormatPrice(value, price.Currency);
            g.DrawLine(gridPen, padL, y, padL + chartW, y);
            g.DrawString(label, axisFont, axisBrush, new RectangleF(0, y - 10, padL - 6, 20), rightAlign);
        }

        for (int i = 0; i <= 4; i++)
        {
            double frac = i / 4.0;
            float x = (float)(padL + frac * chartW);
            int idx = Math.Clamp((int)Math.Round(frac * (history.Count - 1)), 0, history.Count - 1);
            string label = history[idx].Dt.ToString(_selectedPeriod.TimeFormat);
            var labelWidth = g.MeasureString(label, axisFont).Width;
            g.DrawLine(gridPen, x, padT, x, padT + chartH);
            g.DrawString(label, axisFont, axisBrush, new RectangleF(x - labelWidth / 2, padT + chartH + 6, labelWidth + 4, 18), centerAlign);
        }

        var points = history.Select((h, i) => new PointF((float)ToX(i), (float)ToY(h.Price * mult))).ToArray();
        bool isUp = price.ChangePct >= 0;
        var lineColor = isUp ? CGreen : CRed;

        using (var fill = new LinearGradientBrush(new Point(0, padT), new Point(0, padT + chartH), Color.FromArgb(55, lineColor), Color.FromArgb(4, CBack)))
        {
            var poly = points.ToList();
            poly.Add(new PointF(padL + chartW, padT + chartH));
            poly.Add(new PointF(padL, padT + chartH));
            g.FillPolygon(fill, [.. poly]);
        }

        using (var linePen = new Pen(lineColor, 2.1f) { LineJoin = LineJoin.Round })
            g.DrawLines(linePen, points);

        var last = points[^1];
        using (var dotBrush = new SolidBrush(lineColor))
        using (var dotPen = new Pen(CBack, 2))
        {
            g.FillEllipse(dotBrush, last.X - 5, last.Y - 5, 10, 10);
            g.DrawEllipse(dotPen, last.X - 5, last.Y - 5, 10, 10);
        }

        if (_hoverPoint != Point.Empty && _hoverPoint.X >= padL && _hoverPoint.X <= padL + chartW && _hoverPoint.Y >= padT && _hoverPoint.Y <= padT + chartH)
        {
            using var crossPen = new Pen(Color.FromArgb(110, 220, 220, 220), 1) { DashStyle = DashStyle.Dash };
            g.DrawLine(crossPen, _hoverPoint.X, padT, _hoverPoint.X, padT + chartH);
            g.DrawLine(crossPen, padL, _hoverPoint.Y, padL + chartW, _hoverPoint.Y);

            double normalizedX = (double)(_hoverPoint.X - padL) / chartW;
            int idx = Math.Clamp((int)Math.Round(normalizedX * (history.Count - 1)), 0, history.Count - 1);
            var item = history[idx];
            var pt = points[idx];

            using (var hoverDotBrush = new SolidBrush(lineColor))
            using (var hoverDotPen = new Pen(CBack, 2))
            {
                g.FillEllipse(hoverDotBrush, pt.X - 4.5f, pt.Y - 4.5f, 9, 9);
                g.DrawEllipse(hoverDotPen, pt.X - 4.5f, pt.Y - 4.5f, 9, 9);
            }

            string tip = $"{item.Dt:dd MMM yyyy HH:mm}\n{FormatPrice(item.Price * mult, price.Currency)}";
            using var tipFont = new Font("Segoe UI", 8.5f);
            using var tipBack = new SolidBrush(Color.FromArgb(220, 14, 24, 18));
            using var tipBrush = new SolidBrush(CText);
            using var tipPen = new Pen(CBorder);
            var tipSize = g.MeasureString(tip, tipFont);
            int tipW = (int)tipSize.Width + 16;
            int tipH = (int)tipSize.Height + 10;
            int tipX = _hoverPoint.X + 12;
            int tipY = _hoverPoint.Y - tipH - 10;
            if (tipX + tipW > rc.Width - 6) tipX = _hoverPoint.X - tipW - 12;
            if (tipY < padT) tipY = _hoverPoint.Y + 10;
            var rect = new Rectangle(tipX, tipY, tipW, tipH);
            g.FillRectangle(tipBack, rect);
            g.DrawRectangle(tipPen, rect);
            g.DrawString(tip, tipFont, tipBrush, tipX + 8, tipY + 5);
        }
    }

    private async Task RefreshAllAsync()
    {
        if (_isLoading)
            return;

        _isLoading = true;
        SetUiEnabled(false);
        SetStatus("Veriler alınıyor...");

        await Loading.RunAsync(this, async () =>
        {
            var tasks = Assets.Select(asset => FetchAssetAsync(asset, _selectedPeriod));
            await Task.WhenAll(tasks);
            _lastUpdated = DateTime.Now;
        }, "Piyasa verileri yükleniyor...");

        _isLoading = false;
        SetUiEnabled(true);
        SetStatus($"Son güncelleme: {_lastUpdated:HH:mm:ss}");
        InvalidateCards();
        UpdateHeader();
        _chartCanvas.Invalidate();
    }

    private async Task SelectPeriodAsync(int index)
    {
        if (_selectedPeriod == Periods[index])
            return;

        _selectedPeriod = Periods[index];
        for (int i = 0; i < _periodButtons.Length; i++)
        {
            bool active = i == index;
            _periodButtons[i].BackColor = active ? CGreen : CCard;
            _periodButtons[i].ForeColor = active ? Color.Black : CText;
        }

        await RefreshAllAsync();
    }

    private Task SelectAssetAsync(AssetDef asset)
    {
        if (_selectedAsset.Ticker == asset.Ticker)
            return Task.CompletedTask;

        _selectedAsset = asset;
        InvalidateCards();
        UpdateHeader();
        _chartCanvas.Invalidate();
        return Task.CompletedTask;
    }

    private async Task FetchAssetAsync(AssetDef asset, PeriodDef period)
    {
        try
        {
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(asset.Ticker)}?interval={period.Interval}&range={period.Range}";
            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            var result = doc.RootElement.GetProperty("chart").GetProperty("result")[0];
            var meta = result.GetProperty("meta");
            double current = meta.GetProperty("regularMarketPrice").GetDouble();

            var timestamps = result.GetProperty("timestamp").EnumerateArray()
                .Select(x => DateTimeOffset.FromUnixTimeSeconds(x.GetInt64()).LocalDateTime)
                .ToArray();

            var closes = result.GetProperty("indicators").GetProperty("quote")[0].GetProperty("close").EnumerateArray()
                .Select(v => v.ValueKind == JsonValueKind.Null ? (double?)null : v.GetDouble())
                .ToArray();

            var history = timestamps.Zip(closes)
                .Where(x => x.Second.HasValue)
                .Select(x => (Dt: x.First, Price: x.Second!.Value))
                .ToList();

            double changePct = 0;
            if (history.Count > 0 && history[0].Price > 0.0000001)
                changePct = (current - history[0].Price) / history[0].Price * 100.0;

            lock (_prices)
                _prices[asset.Ticker] = new AssetPrice(current, changePct, asset.Currency, history);
        }
        catch
        {
            // keep last known value if fetch fails
        }
    }

    private void InvalidateCards()
    {
        if (!IsHandleCreated)
            return;

        foreach (var card in _cards.Values)
            card.Invalidate();
    }

    private void SetUiEnabled(bool enabled)
    {
        if (!IsHandleCreated)
            return;

        void Apply()
        {
            _refreshButton.Enabled = enabled;
            foreach (var btn in _periodButtons)
                btn.Enabled = enabled;
        }

        if (InvokeRequired)
            BeginInvoke(Apply);
        else
            Apply();
    }

    private void SetStatus(string text)
    {
        if (!IsHandleCreated)
            return;

        void Apply() => _statusLabel.Text = text;
        if (InvokeRequired)
            BeginInvoke(Apply);
        else
            Apply();
    }

    private static string FormatPrice(double value, string currency)
    {
        string prefix = currency switch
        {
            "TRY" => "₺",
            "USD" => "$",
            _ => $"{currency} ",
        };

        double abs = Math.Abs(value);
        string number = abs switch
        {
            >= 100_000 => abs.ToString("N0"),
            >= 1_000 => abs.ToString("N2"),
            >= 100 => abs.ToString("N2"),
            >= 10 => abs.ToString("N3"),
            >= 1 => abs.ToString("N4"),
            _ => abs.ToString("N6"),
        };

        return $"{prefix}{number}";
    }
}
