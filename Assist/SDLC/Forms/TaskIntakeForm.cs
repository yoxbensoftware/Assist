namespace Assist.SDLC.Forms;

using Assist.Services;
using Assist.SDLC.Domain;
using Assist.SDLC.Services;

/// <summary>Task Intake — enter prompt, select project, set priority and risk.</summary>
internal sealed class TaskIntakeForm : SdlcBaseForm
{
    private readonly TextBox _txtTitle;
    private readonly TextBox _txtPrompt;
    private readonly ComboBox _cmbPriority;
    private readonly ComboBox _cmbRisk;
    private readonly TextBox _txtSolution;
    private readonly CheckBox _chkApproval;
    private readonly TextBox _txtNotes;
    private readonly Button _btnSubmit;
    private readonly RichTextBox _output;

    public TaskIntakeForm()
    {
        Text = "📥 Task Intake";
        Size = new Size(780, 620);

        int y = 12;

        Controls.Add(CreateLabel("Başlık:", 12, y));
        _txtTitle = CreateTextBox(130, y, 600);
        Controls.Add(_txtTitle);
        y += 34;

        Controls.Add(CreateLabel("Prompt / Görev:", 12, y));
        _txtPrompt = CreateTextBox(130, y, 600, multiline: true, height: 100);
        Controls.Add(_txtPrompt);
        y += 108;

        Controls.Add(CreateLabel("Solution Yolu:", 12, y));
        _txtSolution = CreateTextBox(130, y, 600);
        Controls.Add(_txtSolution);
        y += 34;

        Controls.Add(CreateLabel("Öncelik:", 12, y));
        _cmbPriority = new ComboBox
        {
            Location = new Point(130, y),
            Width = 160,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = UITheme.Palette.Surface,
            ForeColor = UITheme.Palette.Text,
            Font = new Font("Consolas", 10)
        };
        _cmbPriority.Items.AddRange(Enum.GetNames<SdlcTaskPriority>());
        _cmbPriority.SelectedIndex = 1; // Medium
        Controls.Add(_cmbPriority);

        Controls.Add(CreateLabel("Risk:", 320, y));
        _cmbRisk = new ComboBox
        {
            Location = new Point(380, y),
            Width = 160,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = UITheme.Palette.Surface,
            ForeColor = UITheme.Palette.Text,
            Font = new Font("Consolas", 10)
        };
        _cmbRisk.Items.AddRange(Enum.GetNames<SdlcTaskRiskLevel>());
        _cmbRisk.SelectedIndex = 1;
        Controls.Add(_cmbRisk);
        y += 34;

        _chkApproval = new CheckBox
        {
            Text = "İnsan Onayı Zorunlu",
            Location = new Point(130, y),
            AutoSize = true,
            ForeColor = UITheme.Palette.Text,
            Font = new Font("Consolas", 10)
        };
        Controls.Add(_chkApproval);
        y += 30;

        Controls.Add(CreateLabel("Notlar:", 12, y));
        _txtNotes = CreateTextBox(130, y, 600, multiline: true, height: 50);
        Controls.Add(_txtNotes);
        y += 58;

        _btnSubmit = CreateButton("▶  Gönder", 130, y, 160, 34);
        _btnSubmit.Click += OnSubmit;
        Controls.Add(_btnSubmit);
        y += 44;

        _output = CreateOutputBox();
        _output.Dock = DockStyle.None;
        _output.Location = new Point(12, y);
        _output.Size = new Size(740, 120);
        Controls.Add(_output);
    }

    private async void OnSubmit(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtTitle.Text))
        {
            AppendOutput(_output, "⚠ Başlık boş olamaz.");
            return;
        }

        var task = new SdlcTask
        {
            Title = _txtTitle.Text.Trim(),
            Prompt = _txtPrompt.Text.Trim(),
            SolutionPath = string.IsNullOrWhiteSpace(_txtSolution.Text) ? null : _txtSolution.Text.Trim(),
            Priority = Enum.Parse<SdlcTaskPriority>(_cmbPriority.SelectedItem!.ToString()!),
            RiskLevel = Enum.Parse<SdlcTaskRiskLevel>(_cmbRisk.SelectedItem!.ToString()!),
            RequiresHumanApproval = _chkApproval.Checked,
            Notes = string.IsNullOrWhiteSpace(_txtNotes.Text) ? null : _txtNotes.Text.Trim()
        };

        _btnSubmit.Enabled = false;
        AppendOutput(_output, $"🚀 Task [{task.Id}] gönderildi…");

        try
        {
            await SdlcRuntime.Orchestrator.RunAsync(task);
            AppendOutput(_output, $"✅ Task [{task.Id}] tamamlandı.");
        }
        catch (Exception ex)
        {
            AppendOutput(_output, $"❌ Hata: {ex.Message}");
        }
        finally
        {
            _btnSubmit.Enabled = true;
        }
    }
}
