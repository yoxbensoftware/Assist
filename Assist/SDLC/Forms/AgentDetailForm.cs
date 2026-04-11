namespace Assist.SDLC.Forms;

using Assist.Services;
using Assist.SDLC.Abstractions;
using Assist.SDLC.Domain;
using Assist.SDLC.Messaging;
using Assist.SDLC.Services;

/// <summary>
/// Single form parameterised by <see cref="AgentRole"/>.
/// Displays agent-specific state, history, and event stream.
/// MDI deduplication is handled externally via a role-keyed dictionary.
/// </summary>
internal sealed class AgentDetailForm : SdlcBaseForm
{
    private readonly AgentRole _role;
    private readonly IAgent _agent;
    private readonly Label _lblState;
    private readonly RichTextBox _output;

    public AgentDetailForm(AgentRole role)
    {
        _role = role;
        _agent = SdlcRuntime.AgentCoordinator.GetAgent(role);

        Text = $"🧠 {role} Agent";
        Size = new Size(740, 500);

        // ── Header ────────────────────────────────────────
        var header = new Panel { Dock = DockStyle.Top, Height = 50 };
        header.Controls.Add(CreateLabel($"Agent: {role}", 12, 6, 200));
        _lblState = CreateLabel($"Durum: {_agent.CurrentState}", 220, 6, 200);
        _lblState.ForeColor = UITheme.Palette.Accent;
        header.Controls.Add(_lblState);

        var btnPause = CreateButton("Pause", 460, 6, 80, 28);
        btnPause.Click += (_, _) => _agent.Pause();
        header.Controls.Add(btnPause);

        var btnResume = CreateButton("Resume", 548, 6, 80, 28);
        btnResume.Click += (_, _) => _agent.Resume();
        header.Controls.Add(btnResume);

        var btnCancel = CreateButton("Cancel", 636, 6, 80, 28);
        btnCancel.Click += (_, _) => _agent.Cancel();
        header.Controls.Add(btnCancel);

        Controls.Add(header);

        // ── Event log ─────────────────────────────────────
        _output = CreateOutputBox();
        Controls.Add(_output);

        // Subscribe to events from this agent
        SdlcRuntime.EventBus.SubscribeAll(OnEvent);
        _agent.StateChanged += (_, state) =>
        {
            if (IsDisposed) return;
            BeginInvoke(() => _lblState.Text = $"Durum: {state}");
        };
    }

    /// <summary>The agent role this form is showing.</summary>
    public AgentRole AgentRole => _role;

    private void OnEvent(SdlcEvent evt)
    {
        if (evt.SourceAgent != _role) return;
        if (IsDisposed) return;
        BeginInvoke(() => AppendOutput(_output,
            $"[{evt.TimestampUtc:HH:mm:ss}] {evt.Type} — {evt.Summary}"));
    }
}
