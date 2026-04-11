namespace Assist.SDLC.Forms;

using Assist.Services;
using Assist.SDLC.Domain;
using Assist.SDLC.Services;

/// <summary>Reports &amp; Outputs — generate and export orchestration reports.</summary>
internal sealed class ReportsForm : SdlcBaseForm
{
    private readonly TextBox _txtTaskId;
    private readonly Button _btnGenerate;
    private readonly Button _btnExportMd;
    private readonly Button _btnExportJson;
    private readonly RichTextBox _output;

    public ReportsForm()
    {
        Text = "📋 Reports & Outputs";
        Size = new Size(840, 520);

        int y = 12;
        Controls.Add(CreateLabel("Task ID:", 12, y));
        _txtTaskId = CreateTextBox(110, y, 250);
        Controls.Add(_txtTaskId);

        _btnGenerate = CreateButton("Rapor Üret", 380, y, 120, 28);
        _btnExportMd = CreateButton("MD Export", 510, y, 110, 28);
        _btnExportJson = CreateButton("JSON Export", 628, y, 110, 28);

        _btnGenerate.Click += (_, _) => Generate();
        _btnExportMd.Click += (_, _) => Export("md");
        _btnExportJson.Click += (_, _) => Export("json");

        Controls.AddRange([_btnGenerate, _btnExportMd, _btnExportJson]);
        y += 42;

        _output = CreateOutputBox(DockStyle.None);
        _output.Location = new Point(12, y);
        _output.Size = new Size(800, 440);
        _output.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_output);
    }

    private void Generate()
    {
        var taskId = _txtTaskId.Text.Trim();
        if (string.IsNullOrEmpty(taskId))
        {
            AppendOutput(_output, "⚠ Task ID gerekli.");
            return;
        }

        var md = SdlcRuntime.Documentation.GenerateConsolidatedMarkdown(taskId);
        _output.Clear();
        _output.Text = md;
    }

    private void Export(string format)
    {
        var taskId = _txtTaskId.Text.Trim();
        if (string.IsNullOrEmpty(taskId)) return;

        var report = SdlcRuntime.Documentation.GenerateReport(taskId);
        var content = format switch
        {
            "md" => SdlcRuntime.Reports.ExportAsMarkdown(report),
            "json" => SdlcRuntime.Reports.ExportAsJson(report),
            _ => SdlcRuntime.Reports.ExportAsText(report)
        };

        using var dlg = new SaveFileDialog
        {
            Filter = format switch
            {
                "md" => "Markdown|*.md",
                "json" => "JSON|*.json",
                _ => "Text|*.txt"
            },
            FileName = $"report_{taskId}.{format}"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            File.WriteAllText(dlg.FileName, content);
            AppendOutput(_output, $"✅ Dışa aktarıldı: {dlg.FileName}");
        }
    }
}
