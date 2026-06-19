namespace Assist.Forms.SystemTools.Monitoring;

internal sealed class ScreenDimOverlayForm : Form
{
    private const int MouseMoveThresholdPixels = 3;
    private readonly Action _closeAll;
    private readonly Point _initialCursorPosition;
    private readonly DateTime _armedAfterUtc;

    private ScreenDimOverlayForm(Screen screen, Action closeAll)
    {
        _closeAll = closeAll;
        _initialCursorPosition = Cursor.Position;
        _armedAfterUtc = DateTime.UtcNow.AddMilliseconds(250);

        Text = "NoSleep Guardian Screen Dim";
        StartPosition = FormStartPosition.Manual;
        Bounds = screen.Bounds;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        KeyPreview = true;
        BackColor = Color.Black;
        Opacity = 0.98;
        Cursor = Cursors.Default;

        MouseMove += (_, _) => CloseOnRealMouseMove();
        MouseDown += (_, _) => _closeAll();
        KeyDown += (_, _) => _closeAll();
        Deactivate += (_, _) => TopMost = true;
    }

    public static void ShowUntilUserInput()
    {
        var overlays = new List<ScreenDimOverlayForm>();

        void CloseAll()
        {
            foreach (var overlay in overlays.ToArray())
            {
                if (!overlay.IsDisposed)
                    overlay.Close();
            }
        }

        foreach (var screen in Screen.AllScreens)
            overlays.Add(new ScreenDimOverlayForm(screen, CloseAll));

        foreach (var overlay in overlays)
            overlay.Show();

        overlays.FirstOrDefault(ScreenContainsCursor)?.Activate();
    }

    private static bool ScreenContainsCursor(ScreenDimOverlayForm overlay) =>
        overlay.Bounds.Contains(Cursor.Position);

    private void CloseOnRealMouseMove()
    {
        if (DateTime.UtcNow < _armedAfterUtc)
            return;

        var current = Cursor.Position;
        var dx = Math.Abs(current.X - _initialCursorPosition.X);
        var dy = Math.Abs(current.Y - _initialCursorPosition.Y);
        if (dx > MouseMoveThresholdPixels || dy > MouseMoveThresholdPixels)
            _closeAll();
    }
}
