using CC2CheatGUI.Core.Ram;

namespace CC2CheatGUI.UI;

public sealed partial class MainForm
{
    private Label _trainerStatus = null!;
    private FlatButton _attachBtn = null!, _detachBtn = null!;
    private FlowLayoutPanel _cheatList = null!;
    private NumericUpDown _curCredit = null!, _newCredit = null!;
    private Label _creditFindStatus = null!;
    private FlatButton _findCreditBtn = null!, _setCreditBtn = null!, _freezeBtn = null!;

    private Panel BuildTrainerSection()
    {
        var host = Host();

        // Warning banner
        var banner = new Label
        {
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = Cc2Theme.Black,
            ForeColor = Cc2Theme.Yellow,
            Font = Cc2Theme.PixelSmall,
            Padding = new Padding(12, 8, 12, 8),
            Text = "⚠  LIVE MEMORY EDITING — SINGLE-PLAYER ONLY. Edits apply instantly to the running game (no reload).\n" +
                   "Ammo/health cheats sit on code shared with the AI, so they also affect enemy units. Values are found by " +
                   "signature scan each attach; a game patch may require re-finding them.",
        };

        // Attach bar
        var attachBar = new ConsolePanel { Title = "PROCESS", Dock = DockStyle.Top, Height = 96, TitleFill = Cc2Theme.Cyan };
        _attachBtn = new FlatButton("◉  ATTACH TO GAME") { Accent = Cc2Theme.Green, Width = 200, Height = 34, Location = new Point(14, 32) };
        _attachBtn.Click += (_, _) => TrainerAttach();
        _detachBtn = new FlatButton("DETACH") { Accent = Cc2Theme.Red, Width = 110, Height = 34, Location = new Point(224, 32), Enabled = false };
        _detachBtn.Click += (_, _) => TrainerDetach();
        _trainerStatus = Style.Label("Not attached. Start Carrier Command 2, then click ATTACH.", Cc2Theme.MidGrey, Cc2Theme.PixelSmall);
        _trainerStatus.Location = new Point(346, 42);
        _trainerStatus.MaximumSize = new Size(700, 0);
        attachBar.Controls.Add(_attachBtn);
        attachBar.Controls.Add(_detachBtn);
        attachBar.Controls.Add(_trainerStatus);

        // Body: cheats (left) + credit (right)
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Cc2Theme.Screen };

        var cheatsPanel = new ConsolePanel { Title = "TOGGLE CHEATS", Dock = DockStyle.Fill, TitleFill = Cc2Theme.Cyan };
        _cheatList = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Cc2Theme.Black, Padding = new Padding(10), AutoScroll = true };
        cheatsPanel.Controls.Add(_cheatList);

        var creditPanel = new ConsolePanel { Title = "SET / FREEZE CREDIT", Dock = DockStyle.Right, Width = 430, TitleFill = Cc2Theme.Green };
        creditPanel.Controls.Add(BuildCreditControls());

        body.Controls.Add(cheatsPanel);
        body.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 12, BackColor = Cc2Theme.Screen });
        body.Controls.Add(creditPanel);

        host.Controls.Add(body);
        host.Controls.Add(attachBar);
        host.Controls.Add(banner);

        _refreshTrainer = RefreshTrainer;
        return host;
    }

    private Control BuildCreditControls()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Cc2Theme.Black, Padding = new Padding(14) };

        var step1 = new SectionHeader("STEP 1 — FIND YOUR CREDIT") { Accent = Cc2Theme.Green };
        var lbl1 = Style.Label("Read the credit shown on your in-game HUD and enter it:", Cc2Theme.MidGrey, Cc2Theme.PixelSmall);
        lbl1.Location = new Point(4, 30); lbl1.MaximumSize = new Size(390, 0);

        _curCredit = new NumericUpDown { Maximum = 2_000_000_000, Minimum = 0, Width = 160, Location = new Point(4, 56) };
        Style.ApplyDark(_curCredit);
        _findCreditBtn = new FlatButton("FIND") { Accent = Cc2Theme.Cyan, Width = 90, Height = 26, Location = new Point(172, 56), Enabled = false };
        _findCreditBtn.Click += (_, _) => TrainerFindCredit();

        _creditFindStatus = Style.Label("", Cc2Theme.Cyan, Cc2Theme.PixelSmall);
        _creditFindStatus.Location = new Point(4, 92); _creditFindStatus.MaximumSize = new Size(390, 0);

        var hint = Style.Label("If FIND returns many matches, earn or spend a little in-game, update the\nnumber above, and click FIND again to narrow it down.", Cc2Theme.DimGrey, Cc2Theme.PixelSmall);
        hint.Location = new Point(4, 118);

        var step2 = new SectionHeader("STEP 2 — SET A NEW VALUE") { Accent = Cc2Theme.Green };
        step2.Location = new Point(0, 160); step2.Dock = DockStyle.None; step2.Width = 400;
        var lbl2 = Style.Label("New credit amount:", Cc2Theme.MidGrey, Cc2Theme.PixelSmall);
        lbl2.Location = new Point(4, 188);
        _newCredit = new NumericUpDown { Maximum = 2_000_000_000, Minimum = 0, Value = 1_000_000_000, Width = 160, Location = new Point(4, 210) };
        Style.ApplyDark(_newCredit);
        _setCreditBtn = new FlatButton("SET") { Accent = Cc2Theme.Green, Width = 90, Height = 26, Location = new Point(172, 210), Enabled = false };
        _setCreditBtn.Click += (_, _) => TrainerSetCredit();
        _freezeBtn = new FlatButton("FREEZE: OFF") { Accent = Cc2Theme.MidGrey, Width = 130, Height = 26, Location = new Point(268, 210), Enabled = false };
        _freezeBtn.Click += (_, _) => TrainerToggleFreeze();

        p.Controls.Add(step1);
        p.Controls.Add(lbl1);
        p.Controls.Add(_curCredit);
        p.Controls.Add(_findCreditBtn);
        p.Controls.Add(_creditFindStatus);
        p.Controls.Add(hint);
        p.Controls.Add(step2);
        p.Controls.Add(lbl2);
        p.Controls.Add(_newCredit);
        p.Controls.Add(_setCreditBtn);
        p.Controls.Add(_freezeBtn);
        step1.Dock = DockStyle.None; step1.Location = new Point(0, 0); step1.Width = 400;
        return p;
    }

    // ---- actions ----

    private void TrainerAttach()
    {
        try
        {
            _trainer.Attach();
            SetStatus(_trainer.StatusText);
        }
        catch (Exception ex)
        {
            Cc2MessageBox.Show(this, "ATTACH FAILED", ex.Message, Cc2Theme.Red);
        }
        RefreshTrainer();
    }

    private void TrainerDetach()
    {
        _trainer.Detach();
        RefreshTrainer();
        SetStatus("Detached from game. All live cheats restored.");
    }

    private void TrainerFindCredit()
    {
        try
        {
            int n = _trainer.FindCredit((int)_curCredit.Value);
            _creditFindStatus.ForeColor = n == 0 ? Cc2Theme.Red : n <= 8 ? Cc2Theme.Green : Cc2Theme.Yellow;
            _creditFindStatus.Text = n == 0
                ? "No match. Check the exact number, or you may not be in an active game."
                : n <= 8
                    ? $"{n} candidate(s) — ready. Enter a new amount below and click SET."
                    : $"{n} candidates — too many. Change your credit in-game and FIND again to narrow.";
            _setCreditBtn.Enabled = _freezeBtn.Enabled = n > 0;
        }
        catch (Exception ex) { Cc2MessageBox.Show(this, "SCAN FAILED", ex.Message, Cc2Theme.Red); }
    }

    private void TrainerSetCredit()
    {
        try
        {
            int n = _trainer.SetCredit((int)_newCredit.Value);
            SetStatus($"Wrote {(long)_newCredit.Value:N0} to {n} address(es). Check your in-game HUD.");
            _creditFindStatus.ForeColor = Cc2Theme.Green;
            _creditFindStatus.Text = $"Set {n} address(es) to {(long)_newCredit.Value:N0}.";
        }
        catch (Exception ex) { Cc2MessageBox.Show(this, "WRITE FAILED", ex.Message, Cc2Theme.Red); }
    }

    private void TrainerToggleFreeze()
    {
        if (_trainer.FrozenCredit == null)
        {
            _trainer.FreezeCredit((int)_newCredit.Value);
            _freezeBtn.Text = "FREEZE: ON";
            _freezeBtn.Accent = Cc2Theme.Cyan;
        }
        else
        {
            _trainer.UnfreezeCredit();
            _freezeBtn.Text = "FREEZE: OFF";
            _freezeBtn.Accent = Cc2Theme.MidGrey;
        }
        _freezeBtn.Invalidate();
    }

    private void RefreshTrainer()
    {
        bool a = _trainer.Attached;
        _attachBtn.Enabled = !a;
        _detachBtn.Enabled = a;
        _findCreditBtn.Enabled = a;
        if (!a) { _setCreditBtn.Enabled = _freezeBtn.Enabled = false; }
        _trainerStatus.ForeColor = a ? Cc2Theme.Green : Cc2Theme.MidGrey;
        _trainerStatus.Text = a ? _trainer.StatusText + "\n" + _trainer.ModuleInfo : _trainer.StatusText;

        _cheatList.Controls.Clear();
        foreach (var cheat in _trainer.Cheats)
            _cheatList.Controls.Add(BuildCheatRow(cheat, a));
    }

    private Control BuildCheatRow(TrainerCheat cheat, bool attached)
    {
        var row = new Panel { Width = 620, Height = 58, Margin = new Padding(0, 0, 0, 8), BackColor = Cc2Theme.Screen };
        bool ok = attached && cheat.Resolved;

        var toggle = new FlatButton(cheat.Enabled ? "● ON" : "○ OFF")
        {
            Accent = cheat.Enabled ? Cc2Theme.Green : Cc2Theme.MidGrey,
            Width = 84, Height = 40, Location = new Point(6, 8), Enabled = ok,
        };
        toggle.Click += (_, _) =>
        {
            try
            {
                _trainer.ToggleCheat(cheat, !cheat.Enabled);
                SetStatus($"{cheat.Label}: {(cheat.Enabled ? "ON" : "OFF")}.");
            }
            catch (Exception ex) { Cc2MessageBox.Show(this, "TOGGLE FAILED", ex.Message, Cc2Theme.Red); }
            RefreshTrainer();
        };

        var name = Style.Label(cheat.Label, ok ? Cc2Theme.White : Cc2Theme.DimGrey, Cc2Theme.PixelBody);
        name.Location = new Point(100, 8);
        string sub = !attached ? "attach to resolve" : cheat.Resolved ? $"{cheat.FoundCount} site(s) found" : "signature not found on this build";
        if (cheat.AffectsEnemies) sub += "  •  affects enemies too";
        if (cheat.Experimental) sub += "  •  experimental";
        var subLbl = Style.Label(sub, cheat.Resolved || !attached ? Cc2Theme.MidGrey : Cc2Theme.Red, Cc2Theme.PixelSmall);
        subLbl.Location = new Point(100, 30);

        row.Controls.Add(toggle);
        row.Controls.Add(name);
        row.Controls.Add(subLbl);
        return row;
    }
}
