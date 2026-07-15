namespace CC2CheatGUI.UI;

/// <summary>A small modal dialog styled to match the CC2 console theme.</summary>
public static class Cc2MessageBox
{
    public static void Show(IWin32Window owner, string title, string message, Color accent)
        => Run(owner, title, message, accent, confirm: false);

    public static bool Confirm(IWin32Window owner, string title, string message)
        => Run(owner, title, message, Cc2Theme.Yellow, confirm: true);

    private static bool Run(IWin32Window owner, string title, string message, Color accent, bool confirm)
    {
        bool result = false;
        using var dlg = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Cc2Theme.Screen,
            Width = 480,
            Height = 220,
            ShowInTaskbar = false,
        };

        var panel = new ConsolePanel { Title = title, Dock = DockStyle.Fill, TitleFill = accent, BorderColor = accent };
        var body = new Label
        {
            Text = message,
            ForeColor = Cc2Theme.White,
            BackColor = Cc2Theme.Black,
            Font = Cc2Theme.Data,
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 14, 16, 14),
        };

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = Cc2Theme.Black,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 12, 0),
        };

        var ok = new FlatButton(confirm ? "DISCARD" : "OK") { Accent = confirm ? Cc2Theme.Red : accent, Width = 110, Height = 30 };
        ok.Click += (_, _) => { result = true; dlg.Close(); };
        buttons.Controls.Add(ok);
        if (confirm)
        {
            var cancel = new FlatButton("CANCEL") { Accent = Cc2Theme.White, Width = 110, Height = 30 };
            cancel.Click += (_, _) => { result = false; dlg.Close(); };
            buttons.Controls.Add(cancel);
        }

        panel.Controls.Add(body);
        panel.Controls.Add(buttons);
        dlg.Controls.Add(panel);
        dlg.AcceptButton = null;
        dlg.ShowDialog(owner);
        return result;
    }
}
