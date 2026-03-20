namespace Assist;

public sealed class UnitConverterForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);

    private static readonly Dictionary<string, (string[] Units, double[] Factors)> Categories = new()
    {
        ["Uzunluk"] = (["mm", "cm", "m", "km", "inç", "fit", "yard", "mil"],
                       [0.001, 0.01, 1, 1000, 0.0254, 0.3048, 0.9144, 1609.344]),
        ["Ağırlık"] = (["mg", "g", "kg", "ton", "ons (oz)", "libre (lb)"],
                       [0.000001, 0.001, 1, 1000, 0.0283495, 0.453592]),
        ["Veri Boyutu"] = (["bit", "byte", "KB", "MB", "GB", "TB"],
                           [1, 8, 8192, 8_388_608, 8_589_934_592, 8_796_093_022_208]),
        ["Hız"] = (["m/s", "km/h", "mph", "knot"],
                   [1, 0.277778, 0.44704, 0.514444]),
        ["Zaman"] = (["ms", "sn", "dk", "saat", "gün"],
                     [0.001, 1, 60, 3600, 86400]),
        ["Alan"] = (["mm²", "cm²", "m²", "km²", "hektar", "acre"],
                    [0.000001, 0.0001, 1, 1_000_000, 10_000, 4046.86]),
        ["Hacim"] = (["mL", "L", "m³", "galon (US)", "fl oz"],
                     [0.000001, 0.001, 1, 0.00378541, 0.0000295735])
    };

    private readonly ComboBox _cmbCategory;
    private readonly ComboBox _cmbFrom;
    private readonly ComboBox _cmbTo;
    private readonly TextBox _txtInput;
    private readonly TextBox _txtResult;
    private readonly CheckBox _chkTemp;

    public UnitConverterForm()
    {
        Text = "Birim Çevirici";
        ClientSize = new Size(700, 400);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        int y = 16;
        Controls.Add(MakeLabel("Kategori:", 20, y));

        _cmbCategory = MakeCombo(160, y, 200);
        _cmbCategory.Items.AddRange([.. Categories.Keys, "Sıcaklık"]);
        _cmbCategory.SelectedIndex = 0;
        Controls.Add(_cmbCategory);

        y += 44;
        Controls.Add(MakeLabel("Girdi:", 20, y + 3));

        _txtInput = new TextBox
        {
            Location = new Point(160, y),
            Width = 300,
            Height = 28,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 14),
            Text = "1"
        };
        Controls.Add(_txtInput);

        y += 44;
        Controls.Add(MakeLabel("Kaynak:", 20, y));
        _cmbFrom = MakeCombo(160, y, 200);
        Controls.Add(_cmbFrom);

        var btnSwap = new Button
        {
            Text = "⇅",
            Location = new Point(374, y - 2),
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

        y += 44;
        Controls.Add(MakeLabel("Hedef:", 20, y));
        _cmbTo = MakeCombo(160, y, 200);
        Controls.Add(_cmbTo);

        y += 54;
        Controls.Add(MakeLabel("Sonuç:", 20, y + 3));

        _txtResult = new TextBox
        {
            Location = new Point(160, y),
            Width = 400,
            Height = 28,
            BackColor = Color.FromArgb(15, 15, 15),
            ForeColor = Color.Cyan,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 14),
            ReadOnly = true
        };
        Controls.Add(_txtResult);

        y += 50;
        _chkTemp = new CheckBox { Visible = false };

        var lblFormula = new Label
        {
            Text = "",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = Color.DarkGray,
            Font = new Font("Consolas", 9),
            Tag = "formula"
        };
        Controls.Add(lblFormula);

        _cmbCategory.SelectedIndexChanged += (_, _) => PopulateUnits();
        _cmbFrom.SelectedIndexChanged += (_, _) => Convert();
        _cmbTo.SelectedIndexChanged += (_, _) => Convert();
        _txtInput.TextChanged += (_, _) => Convert();

        PopulateUnits();
    }

    private void PopulateUnits()
    {
        var cat = _cmbCategory.SelectedItem?.ToString() ?? "";
        _cmbFrom.Items.Clear();
        _cmbTo.Items.Clear();

        if (cat == "Sıcaklık")
        {
            string[] tempUnits = ["°C", "°F", "K"];
            _cmbFrom.Items.AddRange(tempUnits);
            _cmbTo.Items.AddRange(tempUnits);
        }
        else if (Categories.TryGetValue(cat, out var data))
        {
            _cmbFrom.Items.AddRange(data.Units);
            _cmbTo.Items.AddRange(data.Units);
        }

        if (_cmbFrom.Items.Count > 0) _cmbFrom.SelectedIndex = 0;
        if (_cmbTo.Items.Count > 1) _cmbTo.SelectedIndex = 1;
    }

    private void Convert()
    {
        if (!double.TryParse(_txtInput.Text, out var input) ||
            _cmbFrom.SelectedIndex < 0 || _cmbTo.SelectedIndex < 0)
        {
            _txtResult.Text = "";
            return;
        }

        var cat = _cmbCategory.SelectedItem?.ToString() ?? "";
        var from = _cmbFrom.SelectedItem?.ToString() ?? "";
        var to = _cmbTo.SelectedItem?.ToString() ?? "";

        double result;
        if (cat == "Sıcaklık")
        {
            result = ConvertTemperature(input, from, to);
        }
        else if (Categories.TryGetValue(cat, out var data))
        {
            var fromFactor = data.Factors[_cmbFrom.SelectedIndex];
            var toFactor = data.Factors[_cmbTo.SelectedIndex];
            result = input * fromFactor / toFactor;
        }
        else
        {
            return;
        }

        _txtResult.Text = result.ToString("G10");

        var formula = Controls.OfType<Label>().FirstOrDefault(l => l.Tag is "formula");
        if (formula is not null)
            formula.Text = $"{input:G6} {from} = {result:G10} {to}";
    }

    private static double ConvertTemperature(double value, string from, string to)
    {
        var celsius = from switch
        {
            "°F" => (value - 32) * 5.0 / 9.0,
            "K" => value - 273.15,
            _ => value
        };

        return to switch
        {
            "°F" => celsius * 9.0 / 5.0 + 32,
            "K" => celsius + 273.15,
            _ => celsius
        };
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
