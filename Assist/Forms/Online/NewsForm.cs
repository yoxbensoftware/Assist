using System.Diagnostics;
using Assist.Models;
using Assist.Services;

namespace Assist.Forms.Online;

/// <summary>
/// Form to display and translate news articles.
/// </summary>
internal sealed class NewsForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);

    private readonly DataGridView _dgv = null!;
    private readonly Button _btnTranslateAll = null!;
    private readonly Button _btnTranslateSelected = null!;

    public NewsForm(string title)
    {
        Text = title;
        ClientSize = new Size(800, 600);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        _dgv = CreateDataGridView();
        _btnTranslateAll = new Button
        {
            Text = "Tümünü Türkçeye Çevir",
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat
        };
        _btnTranslateAll.FlatAppearance.BorderColor = GreenText;

        _btnTranslateSelected = new Button
        {
            Text = "Seçiliyi Türkçeye Çevir",
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat
        };
        _btnTranslateSelected.FlatAppearance.BorderColor = GreenText;

        var lblTitle = new Label
        {
            Text = "Haberler",
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = GreenText
        };

        _btnTranslateAll.Click += async (_, _) => await TranslateAllAsync();
        _btnTranslateSelected.Click += async (_, _) => await TranslateSelectedAsync();
        _dgv.CellDoubleClick += OnCellDoubleClick;

        Controls.Add(_dgv);
        Controls.Add(_btnTranslateAll);
        Controls.Add(_btnTranslateSelected);
        Controls.Add(lblTitle);
    }

    private static DataGridView CreateDataGridView()
    {
        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        dgv.RowTemplate.Height = 28;

        UITheme.ApplyDark(dgv);

        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Title", HeaderText = "Başlık" });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source", HeaderText = "Kaynak", Width = 150 });
        dgv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Tarih", Width = 120 });

        return dgv;
    }

    public void SetNews(List<NewsItem> items)
    {
        _dgv.Rows.Clear();
        foreach (var item in items)
        {
            var rowIndex = _dgv.Rows.Add(
                item.Title,
                item.Source ?? string.Empty,
                item.PublishDate?.ToString("g") ?? string.Empty);
            _dgv.Rows[rowIndex].Tag = item;
        }
    }

    private async Task TranslateAllAsync()
    {
        var result = MessageBox.Show(
            "Haber başlıkları internet üzerinden çevirilecektir. Devam etmek istiyor musunuz?",
            "Çeviri onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        _btnTranslateAll.Enabled = false;

        foreach (DataGridViewRow row in _dgv.Rows)
        {
            if (row.Tag is NewsItem newsItem)
            {
                try
                {
                    var translated = await TranslationService.TranslateAsync(newsItem.Title, "tr");
                    row.Cells[0].Value = translated;
                    newsItem.Title = translated;

                    if (!string.IsNullOrEmpty(newsItem.Summary))
                    {
                        newsItem.Summary = await TranslationService.TranslateAsync(newsItem.Summary, "tr");
                    }
                }
                catch
                {
                    // Skip failed translations
                }
            }
        }

        _btnTranslateAll.Enabled = true;
        MessageBox.Show(
            "Tüm haber başlıkları Türkçeye çevrildi.",
            "Çeviri tamam",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async Task TranslateSelectedAsync()
    {
        if (_dgv.SelectedRows.Count == 0)
        {
            MessageBox.Show(
                "Lütfen çevirmek için bir haber seçin.",
                "Seçim yok",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            "Seçili haber internet üzerinden çevirilecektir. Devam etmek istiyor musunuz?",
            "Çeviri onayı",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        var row = _dgv.SelectedRows[0];
        if (row.Tag is NewsItem newsItem)
        {
            try
            {
                var translated = await TranslationService.TranslateAsync(newsItem.Title, "tr");
                row.Cells[0].Value = translated;
                newsItem.Title = translated;

                if (!string.IsNullOrEmpty(newsItem.Summary))
                {
                    newsItem.Summary = await TranslationService.TranslateAsync(newsItem.Summary, "tr");
                }

                MessageBox.Show(
                    "Seçili haber Türkçeye çevrildi.",
                    "Çeviri tamam",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch
            {
                MessageBox.Show(
                    "Çeviri sırasında hata oluştu.",
                    "Hata",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }

    private void OnCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var row = _dgv.Rows[e.RowIndex];
        if (row.Tag is NewsItem newsItem && !string.IsNullOrEmpty(newsItem.Link))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = newsItem.Link,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Failed to open browser
            }
        }
    }
}
