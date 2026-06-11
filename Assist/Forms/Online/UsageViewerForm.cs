namespace Assist.Forms.Online;

using Assist;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

internal sealed class UsageViewerForm : Form
{
    private sealed record UsageSite(string Name, string Url, string ProfileFolder);

    private readonly TextBox _txtUrl = new();
    private readonly ComboBox _cmbSite = new();
    private readonly Button _btnGo = new();
    private readonly Button _btnBack = new();
    private readonly Button _btnForward = new();
    private readonly Button _btnRefresh = new();
    private readonly Button _btnReloadAll = new();
    private readonly TabControl _tabControl = new();
    private readonly Dictionary<TabPage, WebView2> _views = new();
    private readonly Dictionary<string, TabPage> _tabsByName = new(StringComparer.OrdinalIgnoreCase);

    private static readonly UsageSite[] Sites =
    [
        new("Cursor", "https://cursor.com/dashboard/spending", "cursor"),
        new("GitHub", "https://github.com/settings/billing", "github"),
        new("Claude", "https://platform.claude.com/settings/billing", "claude")
    ];

    public UsageViewerForm()
    {
        Text = "Usage Center";
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        BackColor = Color.Black;
        ForeColor = Color.FromArgb(0, 255, 0);
        Font = new Font("Consolas", 9f);

        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Color.FromArgb(18, 18, 18),
            Padding = new Padding(8, 8, 8, 8)
        };

        _cmbSite = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Left = 8,
            Top = 10
        };
        _cmbSite.Items.AddRange(Sites.Select(x => x.Name).ToArray());
        _cmbSite.SelectedIndex = 0;

        _txtUrl = new TextBox
        {
            Left = 168,
            Top = 10,
            Width = 460,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Text = Sites[0].Url
        };

        _btnGo = CreateButton("Git", 640);
        _btnBack = CreateButton("←", 700);
        _btnForward = CreateButton("→", 750);
        _btnRefresh = CreateButton("↻", 800);
        _btnReloadAll = CreateButton("All", 850);

        topBar.Controls.AddRange([_cmbSite, _txtUrl, _btnGo, _btnBack, _btnForward, _btnRefresh, _btnReloadAll]);

        _tabControl.Dock = DockStyle.Fill;
        _tabControl.Appearance = TabAppearance.FlatButtons;
        _tabControl.ItemSize = new Size(0, 1);
        _tabControl.SizeMode = TabSizeMode.Fixed;
        _tabControl.Multiline = false;
        _tabControl.Padding = new Point(0, 0);
        _tabControl.DrawMode = TabDrawMode.Normal;
        _tabControl.SelectedIndexChanged += (_, _) => SyncSelectionToUi();

        foreach (var site in Sites)
        {
            var tab = new TabPage(site.Name)
            {
                BackColor = Color.Black
            };
            _tabsByName[site.Name] = tab;

            var view = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = Color.Black
            };

            tab.Controls.Add(view);
            _views[tab] = view;
            _tabControl.TabPages.Add(tab);
        }

        Controls.Add(_tabControl);
        Controls.Add(topBar);

        Load += async (_, _) => await InitializeAsync();

        _cmbSite.SelectedIndexChanged += (_, _) =>
        {
            var site = Sites[_cmbSite.SelectedIndex];
            _txtUrl.Text = site.Url;
            ActivateSite(site.Name);
        };
        _btnGo.Click += (_, _) => NavigateCurrent(_txtUrl.Text.Trim());
        _btnBack.Click += (_, _) =>
        {
            var view = ActiveView();
            if (view?.CanGoBack == true)
                view.GoBack();
        };
        _btnForward.Click += (_, _) =>
        {
            var view = ActiveView();
            if (view?.CanGoForward == true)
                view.GoForward();
        };
        _btnRefresh.Click += (_, _) => ActiveView()?.Reload();
        _btnReloadAll.Click += (_, _) => ReloadAll();
        _txtUrl.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                NavigateCurrent(_txtUrl.Text.Trim());
            }
        };
    }

    private static Button CreateButton(string text, int left) => new()
    {
        Text = text,
        Width = 44,
        Height = 28,
        Left = left,
        Top = 9,
        BackColor = Color.FromArgb(35, 35, 35),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat
    };

    private async Task InitializeAsync()
    {
        foreach (var site in Sites)
        {
            var tab = _tabsByName[site.Name];
            var view = _views[tab];

            var userDataFolder = Path.Combine(AppConstants.AppDataPath, "WebView2", site.ProfileFolder);
            Directory.CreateDirectory(userDataFolder);

            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            await view.EnsureCoreWebView2Async(environment);
            view.CoreWebView2.Navigate(site.Url);
        }

        ActivateSite(Sites[0].Name);
    }

    private void ActivateSite(string name)
    {
        if (!_tabsByName.TryGetValue(name, out var tab))
            return;

        _tabControl.SelectedTab = tab;
        if (tab.Controls.Count == 0)
            return;

        _txtUrl.Text = Sites.First(s => s.Name == name).Url;
    }

    private void SyncSelectionToUi()
    {
        var current = _tabControl.SelectedTab;
        if (current is null)
            return;

        var site = Sites.FirstOrDefault(s => string.Equals(s.Name, current.Text, StringComparison.OrdinalIgnoreCase));
        if (site is not null)
        {
            _cmbSite.SelectedItem = site.Name;
            _txtUrl.Text = site.Url;
        }
    }

    private WebView2? ActiveView()
    {
        var tab = _tabControl.SelectedTab;
        return tab is null || !_views.TryGetValue(tab, out var view) ? null : view;
    }

    private void NavigateCurrent(string url)
    {
        var view = ActiveView();
        if (view?.CoreWebView2 is null)
            return;

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        view.CoreWebView2.Navigate(uri.ToString());
    }

    private void ReloadAll()
    {
        foreach (var view in _views.Values)
            view.Reload();
    }
}
