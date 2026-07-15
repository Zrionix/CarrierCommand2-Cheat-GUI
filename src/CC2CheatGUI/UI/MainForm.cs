using System.Drawing.Text;
using CC2CheatGUI.Core;
using CC2CheatGUI.Core.Ram;

namespace CC2CheatGUI.UI;

public sealed partial class MainForm : Form
{
    private SaveFile? _save;
    private bool _dirty;
    private readonly Cc2Trainer _trainer = new();

    // shell
    private readonly ComboBox _slotCombo = new();
    private readonly Panel _content = new();
    private readonly Panel _nav = new();
    private readonly List<(NavButton Button, string Key)> _navItems = new();
    private readonly Dictionary<string, Panel> _sections = new(StringComparer.Ordinal);
    private FlatButton _saveButton = null!;
    private string _statusText = "No save loaded.";
    private string _currentSection = "overview";

    public MainForm()
    {
        Text = "Carrier Command 2 — Save Editor";
        BackColor = Cc2Theme.Screen;
        ForeColor = Cc2Theme.White;
        Font = Cc2Theme.Data;
        Width = 1200;
        Height = 780;
        MinimumSize = new Size(1000, 660);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;

        BuildContentHost();
        BuildNav();
        BuildTopBar();
        BuildStatusBar();

        Load += (_, _) => { RefreshSlots(); ShowSection("overview"); };
        FormClosing += (_, e) =>
        {
            if (!ConfirmDiscardIfDirty()) { e.Cancel = true; return; }
            try { _trainer.Dispose(); } catch { }
        };
    }

    // -----------------------------------------------------------------
    // Top bar: wordmark + save picker + actions
    // -----------------------------------------------------------------
    private void BuildTopBar()
    {
        var bar = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Cc2Theme.Black };
        bar.Paint += (_, e) =>
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Cc2Theme.Black)) g.FillRectangle(b, bar.ClientRectangle);
            // cyan accent hairline along the bottom
            using (var b = new SolidBrush(Cc2Theme.CyanDim)) g.FillRectangle(b, 0, bar.Height - 1, bar.Width, 1);
            Cc2Theme.DrawPixelText(g, "CARRIER COMMAND II", Cc2Theme.PixelTitle, Cc2Theme.Cyan, 20, 16, shadow: true);
            Cc2Theme.DrawPixelText(g, "// SAVE EDITOR", Cc2Theme.PixelBody, Cc2Theme.MidGrey, 22, 48);
        };

        var save = new FlatButton("SAVE CHANGES") { Accent = Cc2Theme.Green, Primary = true, Width = 150, Height = 34, Enabled = false };
        save.Click += (_, _) => SaveChanges();
        _saveButton = save;

        var reload = new FlatButton("RELOAD") { Width = 78, Height = 34 };
        // Re-read the currently loaded save from disk (so it picks up an in-game save), rather than
        // resetting the slot picker to the first slot.
        reload.Click += (_, _) =>
        {
            if (_save != null && System.IO.File.Exists(_save.Path)) LoadSave(_save.Path);
            else RefreshSlots();
        };
        var open = new FlatButton("OPEN…") { Width = 78, Height = 34 };
        open.Click += (_, _) => OpenFileManually();

        _slotCombo.Width = 300;
        _slotCombo.Height = 26;
        Style.ApplyDark(_slotCombo);
        _slotCombo.SelectedIndexChanged += (_, _) => LoadSelectedSlot();

        var lbl = Style.Label("SAVE SLOT", Cc2Theme.MidGrey, Cc2Theme.PixelSmall);

        void Reflow()
        {
            int right = bar.Width - 16;
            save.Location = new Point(right - save.Width, 38); right -= save.Width + 10;
            open.Location = new Point(right - open.Width, 38); right -= open.Width + 6;
            reload.Location = new Point(right - reload.Width, 38); right -= reload.Width + 16;
            _slotCombo.Location = new Point(right - _slotCombo.Width, 42);
            lbl.Location = new Point(right - _slotCombo.Width, 24);
        }
        bar.Resize += (_, _) => Reflow();

        bar.Controls.Add(save);
        bar.Controls.Add(open);
        bar.Controls.Add(reload);
        bar.Controls.Add(_slotCombo);
        bar.Controls.Add(lbl);
        Controls.Add(bar);
        Reflow();
    }

    // -----------------------------------------------------------------
    // Left nav rail
    // -----------------------------------------------------------------
    private FlowLayoutPanel _navFlow = null!;
    private void BuildNav()
    {
        _nav.Dock = DockStyle.Left;
        _nav.Width = 190;
        _nav.BackColor = Cc2Theme.Screen;
        _nav.Paint += (_, e) =>
        {
            using var pen = new Pen(Cc2Theme.Grid);
            e.Graphics.DrawLine(pen, _nav.Width - 1, 0, _nav.Width - 1, _nav.Height);
        };

        _navFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Cc2Theme.Screen,
            Padding = new Padding(0, 8, 0, 0),
        };
        _nav.Controls.Add(_navFlow);

        AddNav("overview", "OVERVIEW", "◆");
        AddNav("currency", "CURRENCY", "$");
        AddNav("inventory", "INVENTORY", "▤");
        AddNav("blueprints", "BLUEPRINTS", "❒");
        AddNav("islands", "ISLANDS", "◈");
        AddNav("fleet", "FLEET", "▟");
        AddNav("live", "LIVE ▸ TRAINER", "◉");

        Controls.Add(_nav);
    }

    private void AddNav(string key, string label, string glyph)
    {
        var btn = new NavButton(label, glyph) { Dock = DockStyle.None, Width = 188, Height = 38, Margin = new Padding(0) };
        btn.Click += (_, _) => ShowSection(key);
        _navFlow.Controls.Add(btn);
        _navItems.Add((btn, key));
    }

    private void BuildContentHost()
    {
        _content.Dock = DockStyle.Fill;
        _content.BackColor = Cc2Theme.Screen;
        _content.Padding = new Padding(16);
        Controls.Add(_content);

        _sections["overview"] = BuildOverviewSection();
        _sections["currency"] = BuildCurrencySection();
        _sections["inventory"] = BuildInventorySection();
        _sections["blueprints"] = BuildBlueprintsSection();
        _sections["islands"] = BuildIslandsSection();
        _sections["fleet"] = BuildFleetSection();
        _sections["live"] = BuildTrainerSection();

        foreach (var panel in _sections.Values)
        {
            panel.Dock = DockStyle.Fill;
            panel.Visible = false;
            _content.Controls.Add(panel);
        }
    }

    private void ShowSection(string key)
    {
        _currentSection = key;
        foreach (var (btn, k) in _navItems) { btn.Selected = k == key; btn.Invalidate(); }
        foreach (var kv in _sections) kv.Value.Visible = kv.Key == key;
        if (_sections.TryGetValue(key, out var p)) p.BringToFront();
        RefreshCurrentSection();
    }

    // -----------------------------------------------------------------
    // Status bar
    // -----------------------------------------------------------------
    private Panel _statusBar = null!;
    private void BuildStatusBar()
    {
        var bar = new BufferedPanel { Dock = DockStyle.Bottom, Height = 26, BackColor = Cc2Theme.Black };
        bar.Paint += (_, e) =>
        {
            var g = e.Graphics;
            // Clear the full bar each paint so resized/relaid text never leaves trails.
            using (var bg = new SolidBrush(Cc2Theme.Black)) g.FillRectangle(bg, bar.ClientRectangle);
            using (var b = new SolidBrush(Cc2Theme.Grid)) g.FillRectangle(b, 0, 0, bar.Width, 1);
            // Clip the left status text so it can't run under the right-aligned indicator.
            string right = _dirty ? "● UNSAVED CHANGES" : (_save != null ? "● SAVED" : "");
            var sz = g.MeasureString(right, Cc2Theme.PixelSmall);
            int rightX = bar.Width - (int)sz.Width - 12;
            var leftClip = g.Clip;
            g.SetClip(new Rectangle(10, 0, Math.Max(0, rightX - 18), bar.Height));
            Cc2Theme.DrawPixelText(g, _statusText, Cc2Theme.PixelSmall, Cc2Theme.MidGrey, 10, 6);
            g.Clip = leftClip;
            var col = _dirty ? Cc2Theme.Yellow : Cc2Theme.Green;
            Cc2Theme.DrawPixelText(g, right, Cc2Theme.PixelSmall, col, rightX, 6);
        };
        _statusBar = bar;
        Controls.Add(bar);
    }

    private void SetStatus(string text) { _statusText = text; _statusBar.Invalidate(); }

    // -----------------------------------------------------------------
    // Save selection / loading
    // -----------------------------------------------------------------
    private void RefreshSlots()
    {
        var slots = SaveLocator.FindSlots();
        _slotCombo.Items.Clear();
        foreach (var slot in slots) _slotCombo.Items.Add(slot);

        if (slots.Count == 0)
            SetStatus("No saves auto-detected under %APPDATA%\\Carrier Command 2 — use OPEN… to pick a save.xml.");
        else
        {
            SetStatus($"{slots.Count} save slot(s) detected.");
            _slotCombo.SelectedIndex = 0;
        }
    }

    private void LoadSelectedSlot()
    {
        if (_slotCombo.SelectedItem is SaveSlot slot) LoadSave(slot.SaveXmlPath);
    }

    private void OpenFileManually()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Carrier Command 2 save (*.xml)|*.xml|All files (*.*)|*.*",
            Title = "Open Carrier Command 2 save.xml",
        };
        var root = SaveLocator.FindRoot();
        if (root != null) dialog.InitialDirectory = root;
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadSave(dialog.FileName);
    }

    private void LoadSave(string path)
    {
        if (!ConfirmDiscardIfDirty()) return;
        try
        {
            Cursor = Cursors.WaitCursor;
            _save = SaveFile.Load(path);
            _dirty = false;
            _saveButton.Enabled = true;
            SetStatus($"Loaded: {path}");
            RefreshAllSections();
        }
        catch (Exception ex)
        {
            _save = null;
            _saveButton.Enabled = false;
            Cc2MessageBox.Show(this, "LOAD FAILED", ex.Message, Cc2Theme.Red);
        }
        finally { Cursor = Cursors.Default; UpdateSavedState(); }
    }

    private void SaveChanges()
    {
        if (_save == null) return;
        try
        {
            Cursor = Cursors.WaitCursor;
            string backup = _save.Save();
            _dirty = false;
            UpdateSavedState();
            SetStatus($"Saved. Backup: {Path.GetFileName(backup)}");
            Cc2MessageBox.Show(this, "SAVE WRITTEN",
                $"Changes written successfully.\n\nA backup of the previous file was created:\n{backup}",
                Cc2Theme.Green);
        }
        catch (Exception ex)
        {
            Cc2MessageBox.Show(this, "SAVE FAILED", ex.Message, Cc2Theme.Red);
        }
        finally { Cursor = Cursors.Default; }
    }

    // -----------------------------------------------------------------
    // Dirty tracking
    // -----------------------------------------------------------------
    private void MarkDirty()
    {
        _dirty = true;
        UpdateSavedState();
    }

    private void UpdateSavedState()
    {
        _saveButton.Enabled = _save != null;
        _statusBar.Invalidate();
    }

    private bool ConfirmDiscardIfDirty()
    {
        if (!_dirty) return true;
        return Cc2MessageBox.Confirm(this, "UNSAVED CHANGES",
            "You have unsaved changes. Discard them and continue?");
    }

    // ---- test/render hooks (used by --shots mode) ----
    internal void ExternalLoad(string path) => LoadSave(path);
    internal void ExternalShow(string key) => ShowSection(key);
    internal IReadOnlyList<string> SectionKeys => _navItems.Select(n => n.Key).ToList();
}
