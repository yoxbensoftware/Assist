namespace Assist.Forms.Online;

using System.Text;
using System.Text.Json;
using Assist.Services;

/// <summary>
/// English ↔ Turkish translation dictionary using the free MyMemory API.
/// Supports EN→TR and TR→EN directions with alternative matches.
/// </summary>
internal sealed class TranslationDictionaryForm : Form
{
    private static readonly HttpClient Http = AppConstants.SharedHttpClient;

    private readonly TextBox _txtWord;
    private readonly Button _btnSearch;
    private readonly Button _btnSwap;
    private readonly Button _btnDirEnTr;
    private readonly Button _btnDirTrEn;
    private readonly RichTextBox _rtbResult;
    private readonly ListBox _lstHistory;
    private readonly Panel _historyPanel;

    private bool _isEnToTr = true; // true = EN→TR, false = TR→EN
    private readonly List<string> _history = [];

    private static readonly Color CBackground = Color.FromArgb(18, 18, 22);
    private static readonly Color CPanel = Color.FromArgb(28, 28, 34);
    private static readonly Color CInput = Color.FromArgb(36, 36, 42);
    private static readonly Color CAccent = Color.FromArgb(0, 140, 255);
    private static readonly Color CInactive = Color.FromArgb(55, 55, 62);
    private static readonly Color CText = Color.FromArgb(230, 230, 240);
    private static readonly Color CMuted = Color.FromArgb(140, 140, 155);
    private static readonly Color CHighlight = Color.FromArgb(0, 200, 120);
    private static readonly Color CAlternative = Color.FromArgb(200, 180, 80);

    public TranslationDictionaryForm()
    {
        Text = "\uD83D\uDD04 S\u00F6zl\u00FCk (EN \u2194 TR)";
        ClientSize = new Size(920, 600);
        BackColor = CBackground;
        ForeColor = CText;
        Font = new Font("Segoe UI", 10);

        // ── Top input panel ──
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 64,
            BackColor = CPanel,
            Padding = new Padding(10, 8, 10, 8)
        };

        // Direction toggle buttons
        _btnDirEnTr = MakeDirButton("EN \u2192 TR", 8, true);
        _btnDirTrEn = MakeDirButton("TR \u2192 EN", 96, false);
        _btnDirEnTr.Click += (_, _) => SetDirection(true);
        _btnDirTrEn.Click += (_, _) => SetDirection(false);

        // Swap button
        _btnSwap = new Button
        {
            Text = "\u21C4",
            Location = new Point(182, 12),
            Size = new Size(38, 38),
            BackColor = CInput,
            ForeColor = CText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnSwap.FlatAppearance.BorderColor = CInactive;
        _btnSwap.Click += (_, _) => SetDirection(!_isEnToTr);

        // Word input
        _txtWord = new TextBox
        {
            Location = new Point(228, 10),
            Width = 496,
            Height = 38,
            BackColor = CInput,
            ForeColor = CText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 15),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _txtWord.PlaceholderText = "Kelime veya c\u00FCmle yaz\u0131n...";

        // Search button
        _btnSearch = new Button
        {
            Text = "\uD83D\uDD0D  \u00C7evir",
            Location = new Point(732, 10),
            Size = new Size(170, 38),
            BackColor = CAccent,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnSearch.FlatAppearance.BorderSize = 0;

        topPanel.Controls.AddRange([_btnDirEnTr, _btnDirTrEn, _btnSwap, _txtWord, _btnSearch]);

        // ── History panel (left) ──
        _historyPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 200,
            BackColor = CPanel,
            Padding = new Padding(8, 8, 8, 8)
        };

        var lblHistory = new Label
        {
            Text = "\uD83D\uDCCB  Ge\u00E7mi\u015F",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = CMuted,
            Font = new Font("Segoe UI Semibold", 9),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var btnClearHistory = new Button
        {
            Text = "Temizle",
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = CInput,
            ForeColor = CMuted,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8),
            Cursor = Cursors.Hand
        };
        btnClearHistory.FlatAppearance.BorderSize = 0;
        btnClearHistory.Click += (_, _) =>
        {
            _history.Clear();
            _lstHistory.Items.Clear();
        };

        _lstHistory = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = CPanel,
            ForeColor = CText,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9),
            IntegralHeight = false
        };
        _lstHistory.SelectedIndexChanged += (_, _) =>
        {
            if (_lstHistory.SelectedItem is string item)
            {
                // Parse "EN→TR: hello" or "TR→EN: merhaba"
                var parts = item.Split(": ", 2, StringSplitOptions.None);
                if (parts.Length == 2)
                {
                    _isEnToTr = parts[0].StartsWith("EN");
                    UpdateDirectionButtons();
                    _txtWord.Text = parts[1];
                    _ = SearchAsync();
                }
            }
        };

        _historyPanel.Controls.Add(_lstHistory);
        _historyPanel.Controls.Add(btnClearHistory);
        _historyPanel.Controls.Add(lblHistory);

        // ── Results area ──
        _rtbResult = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = CBackground,
            ForeColor = CText,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10),
            Padding = new Padding(12)
        };

        // ── Layout assembly ──
        Controls.Add(_rtbResult);
        Controls.Add(_historyPanel);
        Controls.Add(topPanel);

        // ── Events ──
        _btnSearch.Click += async (_, _) => await SearchAsync();
        _txtWord.KeyDown += async (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                await SearchAsync();
            }
        };

        ShowWelcome();
    }

    private void SetDirection(bool enToTr)
    {
        _isEnToTr = enToTr;
        UpdateDirectionButtons();
        _txtWord.PlaceholderText = _isEnToTr
            ? "\u0130ngilizce kelime veya c\u00FCmle yaz\u0131n..."
            : "T\u00FCrk\u00E7e kelime veya c\u00FCmle yaz\u0131n...";
    }

    private void UpdateDirectionButtons()
    {
        _btnDirEnTr.BackColor = _isEnToTr ? CAccent : CInactive;
        _btnDirTrEn.BackColor = !_isEnToTr ? CAccent : CInactive;
    }

    private void ShowWelcome()
    {
        _rtbResult.Clear();
        _rtbResult.SelectionFont = new Font("Segoe UI", 14, FontStyle.Bold);
        _rtbResult.SelectionColor = CText;
        _rtbResult.AppendText("\n  \uD83D\uDD04  \u0130ngilizce \u2194 T\u00FCrk\u00E7e S\u00F6zl\u00FCk\n\n");

        _rtbResult.SelectionFont = new Font("Segoe UI", 10);
        _rtbResult.SelectionColor = CMuted;
        _rtbResult.AppendText("  Bir kelime veya c\u00FCmle yaz\u0131p \u00C7evir butonuna bas\u0131n.\n\n");
        _rtbResult.AppendText("  \u2022  Y\u00F6n de\u011Fi\u015Ftirmek i\u00E7in EN\u2192TR / TR\u2192EN butonlar\u0131n\u0131 kullan\u0131n\n");
        _rtbResult.AppendText("  \u2022  \u21C4 butonu ile y\u00F6n\u00FC h\u0131zl\u0131ca ters \u00E7evirin\n");
        _rtbResult.AppendText("  \u2022  Ge\u00E7mi\u015F panelinden \u00F6nceki aramalara t\u0131klay\u0131n\n");
    }

    private async Task SearchAsync()
    {
        var word = _txtWord.Text.Trim();
        if (string.IsNullOrEmpty(word)) return;

        await Loading.RunAsync(this, async () =>
        {
            try
            {
                var langPair = _isEnToTr ? "en|tr" : "tr|en";
                var dirLabel = _isEnToTr ? "EN\u2192TR" : "TR\u2192EN";

                // Add to history
                var historyEntry = $"{dirLabel}: {word}";
                _history.Remove(historyEntry);
                _history.Insert(0, historyEntry);
                if (_history.Count > 30) _history.RemoveAt(_history.Count - 1);
                RefreshHistory();

                var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(word)}&langpair={Uri.EscapeDataString(langPair)}";
                using var httpResp = await Http.GetAsync(url);
                if (!httpResp.IsSuccessStatusCode)
                {
                    ShowError(word, $"API yan\u0131t vermedi (HTTP {(int)httpResp.StatusCode}). L\u00FCtfen tekrar deneyin.");
                    return;
                }
                var jsonStr = await httpResp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(jsonStr);
                var response = doc.RootElement;

                if (response.ValueKind == JsonValueKind.Undefined)
                {
                    ShowError(word, "API yan\u0131t\u0131 al\u0131namad\u0131.");
                    return;
                }

                var mainTranslation = "";
                double mainMatch = 0;

                if (response.TryGetProperty("responseData", out var rd))
                {
                    mainTranslation = rd.TryGetProperty("translatedText", out var tt)
                        ? tt.GetString() ?? ""
                        : "";
                    mainMatch = rd.TryGetProperty("match", out var mm)
                        ? mm.GetDouble()
                        : 0;
                }

                // Gather all translations from matches array first
                var translationIsOriginal = !string.IsNullOrWhiteSpace(mainTranslation) &&
                    mainTranslation.Equals(word, StringComparison.OrdinalIgnoreCase);

                var alternatives = new List<(string Translation, double Quality)>();
                if (response.TryGetProperty("matches", out var matches))
                {
                    foreach (var m in matches.EnumerateArray())
                    {
                        var tr = m.TryGetProperty("translation", out var t)
                            ? t.GetString() ?? "" : "";
                        var qual = m.TryGetProperty("match", out var q)
                            ? q.GetDouble() : 0;

                        if (!string.IsNullOrWhiteSpace(tr) &&
                            !tr.Equals(word, StringComparison.OrdinalIgnoreCase) &&
                            qual > 0.3)
                        {
                            if (alternatives.All(a =>
                                !a.Translation.Equals(tr, StringComparison.OrdinalIgnoreCase)))
                            {
                                alternatives.Add((tr, qual));
                            }
                        }
                    }
                }

                // If the main translation is just the original word, promote the best alternative
                if (translationIsOriginal && alternatives.Count > 0)
                {
                    var best = alternatives.OrderByDescending(a => a.Quality).First();
                    mainTranslation = best.Translation;
                    mainMatch = best.Quality;
                    alternatives.Remove(best);
                }
                else if (string.IsNullOrWhiteSpace(mainTranslation) ||
                         (translationIsOriginal && alternatives.Count == 0))
                {
                    ShowError(word, "\u00C7eviri bulunamad\u0131.");
                    return;
                }

                // Remove duplicates of main translation from alternatives
                alternatives = [.. alternatives
                    .Where(a => !a.Translation.Equals(mainTranslation, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(a => a.Quality)
                    .Take(8)];

                // ── Render results ──
                _rtbResult.Clear();

                // Direction header
                AppendLine($"\n  {dirLabel}", new Font("Segoe UI", 9), CMuted);
                AppendLine("  \u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550\u2550", new Font("Segoe UI", 9), Color.FromArgb(50, 50, 56));

                // Source word
                AppendLine($"\n  \uD83D\uDCD6  {word.ToUpperInvariant()}", new Font("Segoe UI", 14, FontStyle.Bold), CText);

                // Main translation
                AppendLine($"\n  \u27A4  {mainTranslation}", new Font("Segoe UI", 16, FontStyle.Bold), CHighlight);

                // Match quality
                var qualPercent = (int)(mainMatch * 100);
                var qualColor = qualPercent >= 80 ? CHighlight
                    : qualPercent >= 50 ? CAlternative
                    : Color.FromArgb(220, 80, 80);
                AppendLine($"     E\u015Fle\u015Fme: %{qualPercent}", new Font("Segoe UI", 8.5f), qualColor);

                // Alternatives
                if (alternatives.Count > 0)
                {
                    AppendLine("\n  \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500", new Font("Segoe UI", 9), Color.FromArgb(50, 50, 56));
                    AppendLine("\n  \uD83D\uDCA1  Alternatif \u00C7eviriler:", new Font("Segoe UI Semibold", 10), CAlternative);

                    int idx = 1;
                    foreach (var (tr, qual) in alternatives)
                    {
                        var pct = (int)(qual * 100);
                        AppendText($"\n     {idx}. ", new Font("Segoe UI", 9), CMuted);
                        AppendText(tr, new Font("Segoe UI", 10, FontStyle.Bold), CText);
                        AppendText($"  (%{pct})", new Font("Segoe UI", 8), CMuted);
                        idx++;
                    }
                }

                AppendLine("\n", new Font("Segoe UI", 9), CText);
            }
            catch (TaskCanceledException)
            {
                ShowError(word, "\u0130stek zaman a\u015F\u0131m\u0131na u\u011Frad\u0131. L\u00FCtfen tekrar deneyin.");
            }
            catch (HttpRequestException ex)
            {
                var detail = ex.StatusCode.HasValue ? $" (HTTP {(int)ex.StatusCode})" : "";
                ShowError(word, $"API'ye eri\u015Filemedi{detail}. \u0130nternet ba\u011Flant\u0131n\u0131z\u0131 kontrol edin.");
            }
            catch (Exception ex)
            {
                ShowError(word, $"Hata: {ex.Message}");
            }
        }, "\u00C7evriliyor...");
    }

    private void ShowError(string word, string message)
    {
        _rtbResult.Clear();
        AppendLine($"\n  \u274C  '{word}'", new Font("Segoe UI", 12, FontStyle.Bold), Color.FromArgb(220, 80, 80));
        AppendLine($"\n  {message}", new Font("Segoe UI", 10), CMuted);
    }

    private void RefreshHistory()
    {
        _lstHistory.Items.Clear();
        foreach (var h in _history)
            _lstHistory.Items.Add(h);
    }

    // ── RichTextBox helpers ──

    private void AppendText(string text, Font font, Color color)
    {
        _rtbResult.SelectionFont = font;
        _rtbResult.SelectionColor = color;
        _rtbResult.AppendText(text);
    }

    private void AppendLine(string text, Font font, Color color)
    {
        AppendText(text + "\n", font, color);
    }

    // ── UI factory ──

    private static Button MakeDirButton(string text, int x, bool active) => new()
    {
        Text = text,
        Location = new Point(x, 12),
        Size = new Size(82, 38),
        BackColor = active ? CAccent : CInactive,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 9, FontStyle.Bold),
        Cursor = Cursors.Hand
    };
}
