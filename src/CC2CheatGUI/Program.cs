using CC2CheatGUI.UI;

namespace CC2CheatGUI;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        int shotsIdx = Array.IndexOf(args, "--shots");
        if (shotsIdx >= 0 && args.Length > shotsIdx + 2)
        {
            RenderShots(args[shotsIdx + 1], args[shotsIdx + 2]);
            return;
        }

        Application.Run(new MainForm());
    }

    /// <summary>Off-screen render of every section to PNG for visual verification.</summary>
    private static void RenderShots(string savePath, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var form = new MainForm
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-6000, -6000),
        };
        form.Show();
        Application.DoEvents();
        form.ExternalLoad(savePath);
        Application.DoEvents();

        foreach (var key in form.SectionKeys)
        {
            form.ExternalShow(key);
            for (int i = 0; i < 4; i++) Application.DoEvents();
            System.Threading.Thread.Sleep(120);
            Application.DoEvents();
            using var bmp = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
            bmp.Save(Path.Combine(outDir, $"section-{key}.png"), System.Drawing.Imaging.ImageFormat.Png);
        }
        form.Close();
    }
}
