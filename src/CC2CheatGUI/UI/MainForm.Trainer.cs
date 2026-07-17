using CC2CheatGUI.Core;
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

    // live inventory (carrier hold + warehouses)
    private DataGridView _holdGrid = null!;
    private FlatButton _holdLocateBtn = null!, _holdApplyBtn = null!, _holdFillBtn = null!;
    private NumericUpDown _holdFillValue = null!;
    private Label _holdStatus = null!;
    private ComboBox _targetCombo = null!;
    private int[]? _holdLiveValues;
    private bool _holdLocating;
    private SaveFile? _targetsSave;

    private sealed class LiveTarget
    {
        public required string Label { get; init; }
        public required int[] Row { get; init; }
        public required bool ConsumableDrift { get; init; }
        public override string ToString() => Label;
    }

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
                   "The Unlimited Ammo cheat sits on code shared with the AI, so it also grants enemy units unlimited ammo. " +
                   "Credit is found by signature scan each attach; a game patch may require re-finding it.",
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

        // Body: left column (cheats + credit) | live carrier hold (fills the rest).
        var body = new Panel { Dock = DockStyle.Fill, BackColor = Cc2Theme.Screen };

        var leftCol = new Panel { Dock = DockStyle.Left, Width = 430, BackColor = Cc2Theme.Screen };

        var creditPanel = new ConsolePanel { Title = "SET / FREEZE CREDIT", Dock = DockStyle.Fill, TitleFill = Cc2Theme.Green };
        creditPanel.Controls.Add(BuildCreditControls());

        var cheatsPanel = new ConsolePanel { Title = "TOGGLE CHEATS", Dock = DockStyle.Top, Height = 222, TitleFill = Cc2Theme.Cyan };
        _cheatList = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Cc2Theme.Black, Padding = new Padding(10), AutoScroll = true };
        cheatsPanel.Controls.Add(_cheatList);

        leftCol.Controls.Add(creditPanel);
        leftCol.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Cc2Theme.Screen });
        leftCol.Controls.Add(cheatsPanel);

        var holdPanel = new ConsolePanel { Title = "LIVE INVENTORY", Dock = DockStyle.Fill, TitleFill = Cc2Theme.Orange };
        holdPanel.Controls.Add(BuildHoldControls());

        body.Controls.Add(holdPanel);
        body.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Cc2Theme.Screen });
        body.Controls.Add(leftCol);

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
        var lbl1 = Style.Label("Your current credit — auto-filled from your loaded save when you attach.\nEdit it only if you've earned/spent since loading, then click FIND:", Cc2Theme.MidGrey, Cc2Theme.PixelSmall);
        lbl1.Location = new Point(4, 26); lbl1.MaximumSize = new Size(390, 0);

        _curCredit = new NumericUpDown { Maximum = 2_000_000_000, Minimum = 0, Width = 160, Location = new Point(4, 56) };
        Style.ApplyDark(_curCredit);
        _findCreditBtn = new FlatButton("FIND") { Accent = Cc2Theme.Cyan, Width = 90, Height = 26, Location = new Point(172, 56), Enabled = false };
        _findCreditBtn.Click += (_, _) => TrainerFindCredit();

        _creditFindStatus = Style.Label("", Cc2Theme.Cyan, Cc2Theme.PixelSmall);
        _creditFindStatus.Location = new Point(4, 90); _creditFindStatus.MaximumSize = new Size(390, 0);

        var hint = Style.Label("Many matches? Earn/spend a little in-game, update the number, FIND again.", Cc2Theme.DimGrey, Cc2Theme.PixelSmall);
        hint.Location = new Point(4, 120); hint.MaximumSize = new Size(390, 0);

        var step2 = new SectionHeader("STEP 2 — SET A NEW VALUE") { Accent = Cc2Theme.Green };
        step2.Location = new Point(0, 140); step2.Dock = DockStyle.None; step2.Width = 400;
        var lbl2 = Style.Label("New credit amount:", Cc2Theme.MidGrey, Cc2Theme.PixelSmall);
        lbl2.Location = new Point(4, 164);
        _newCredit = new NumericUpDown { Maximum = 2_000_000_000, Minimum = 0, Value = 1_000_000_000, Width = 160, Location = new Point(4, 184) };
        Style.ApplyDark(_newCredit);
        _setCreditBtn = new FlatButton("SET") { Accent = Cc2Theme.Green, Width = 90, Height = 26, Location = new Point(172, 184), Enabled = false };
        _setCreditBtn.Click += (_, _) => TrainerSetCredit();
        _freezeBtn = new FlatButton("FREEZE: OFF") { Accent = Cc2Theme.MidGrey, Width = 130, Height = 26, Location = new Point(268, 184), Enabled = false };
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

    private Control BuildHoldControls()
    {
        var p = new Panel { Dock = DockStyle.Fill, BackColor = Cc2Theme.Black };

        var top = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = Cc2Theme.Black, Padding = new Padding(10, 8, 10, 4) };
        var tgtLbl = new Label { Text = "TARGET", AutoSize = true, ForeColor = Cc2Theme.MidGrey, BackColor = Color.Transparent, Font = Cc2Theme.PixelSmall, Location = new Point(10, 13) };
        _targetCombo = new ComboBox { Width = 430, Location = new Point(78, 9) };
        Style.ApplyDark(_targetCombo);
        _holdLocateBtn = new FlatButton("◎  LOCATE") { Accent = Cc2Theme.Orange, Width = 120, Height = 28, Location = new Point(10, 44), Enabled = false };
        _holdLocateBtn.Click += (_, _) => TrainerLocateHold();
        _holdStatus = Style.Label("Attach, load your in-game save, pick a target, then LOCATE.", Cc2Theme.MidGrey, Cc2Theme.PixelSmall);
        _holdStatus.Location = new Point(142, 50); _holdStatus.MaximumSize = new Size(400, 0);
        top.Controls.Add(tgtLbl);
        top.Controls.Add(_targetCombo);
        top.Controls.Add(_holdLocateBtn);
        top.Controls.Add(_holdStatus);

        _holdGrid = Style.Grid();
        _holdGrid.Dock = DockStyle.Fill;
        _holdGrid.ReadOnly = false;
        _holdGrid.AllowUserToAddRows = false;
        AddHoldCol("ID", 7, true);
        AddHoldCol("ITEM", 34, true);
        AddHoldCol("CATEGORY", 23, true);
        AddHoldCol("LIVE", 16, true);
        AddHoldCol("NEW QTY", 20, false);
        _holdGrid.CellEndEdit += HoldGrid_CellEndEdit;
        _holdGrid.DataError += (_, e) => e.ThrowException = false;

        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = Cc2Theme.Black, Padding = new Padding(10, 8, 10, 8) };
        var fillLbl = new Label { Text = "SET ALL TO", AutoSize = true, ForeColor = Cc2Theme.MidGrey, BackColor = Color.Transparent, Font = Cc2Theme.PixelSmall, Location = new Point(8, 15) };
        _holdFillValue = new NumericUpDown { Maximum = 1_000_000, Minimum = 0, Value = 999, Width = 90, Location = new Point(92, 11) };
        Style.ApplyDark(_holdFillValue);
        _holdFillBtn = new FlatButton("SET ALL NEW") { Accent = Cc2Theme.Cyan, Width = 116, Height = 28, Location = new Point(190, 9), Enabled = false };
        _holdFillBtn.Click += (_, _) => HoldSetAllNew((int)_holdFillValue.Value);
        _holdApplyBtn = new FlatButton("APPLY TO GAME") { Accent = Cc2Theme.Green, Width = 150, Height = 28, Location = new Point(316, 9), Enabled = false };
        _holdApplyBtn.Click += (_, _) => TrainerApplyHold();
        bottom.Controls.Add(fillLbl);
        bottom.Controls.Add(_holdFillValue);
        bottom.Controls.Add(_holdFillBtn);
        bottom.Controls.Add(_holdApplyBtn);

        p.Controls.Add(_holdGrid);
        p.Controls.Add(bottom);
        p.Controls.Add(top);
        return p;
    }

    private void AddHoldCol(string header, int weight, bool readOnly)
    {
        _holdGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            FillWeight = weight,
            ReadOnly = readOnly,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        });
    }

    // ---- actions ----

    private void TrainerAttach()
    {
        try
        {
            _trainer.Attach();
            SetStatus(_trainer.StatusText);
            PrefillCreditFromSave();
        }
        catch (Exception ex)
        {
            Cc2MessageBox.Show(this, "ATTACH FAILED", ex.Message, Cc2Theme.Red);
        }
        RefreshTrainer();
    }

    /// <summary>Auto-fill the "current credit" box from the loaded save so the user rarely has to type it.</summary>
    private void PrefillCreditFromSave()
    {
        if (!_trainer.Attached) return;
        if (_save?.PlayerTeam == null)
        {
            _creditFindStatus.ForeColor = Cc2Theme.MidGrey;
            _creditFindStatus.Text = "Tip: load your save (top-left) so I can auto-fill your credit — then just click FIND.";
            return;
        }
        long c = Math.Clamp(_save.PlayerTeam.Currency, 0, (long)_curCredit.Maximum);
        _curCredit.Value = c;
        _creditFindStatus.ForeColor = Cc2Theme.Cyan;
        _creditFindStatus.Text = $"Auto-filled ₵{c:N0} from your save. Click FIND to lock on (edit the number first if it's changed in-game).";
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
            var live = _trainer.ReadCredit();
            _creditFindStatus.ForeColor = Cc2Theme.Green;
            _creditFindStatus.Text = live.HasValue
                ? $"Set {n} address(es). Live credit now reads ₵{(long)live.Value:N0}."
                : $"Set {n} address(es) to {(long)_newCredit.Value:N0}.";
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

    /// <summary>Rebuild the target dropdown (carrier hold + distinctive warehouses) from the loaded save.</summary>
    private void RebuildLiveTargets()
    {
        var prev = (_targetCombo.SelectedItem as LiveTarget)?.Label;
        _targetCombo.Items.Clear();
        if (_save != null)
        {
            if (_save.PlayerCarrierHold is { } hold)
                _targetCombo.Items.Add(new LiveTarget { Label = "★ Carrier Hold", Row = SaveFile.RowOf(hold), ConsumableDrift = true });

            foreach (var c in _save.Containers)
            {
                if (c.Kind != ContainerKind.IslandStock) continue;
                var row = SaveFile.PositionalRow(c, 61);
                if (!Cc2Trainer.IsRowFindable(row)) continue;   // sparse/generic stock: use the offline tab
                _targetCombo.Items.Add(new LiveTarget { Label = c.Label, Row = row, ConsumableDrift = false });
            }
        }

        int idx = 0;
        if (prev != null)
            for (int i = 0; i < _targetCombo.Items.Count; i++)
                if (((LiveTarget)_targetCombo.Items[i]).Label == prev) { idx = i; break; }
        if (_targetCombo.Items.Count > 0) _targetCombo.SelectedIndex = idx;
    }

    /// <summary>Re-read the loaded save from disk so live locates match the game's current state,
    /// unless there are unsaved tool edits (which we must not discard).</summary>
    private void RefreshSaveFromDisk()
    {
        if (!_dirty && _save != null && System.IO.File.Exists(_save.Path))
        {
            try { _save = SaveFile.Load(_save.Path); RebuildLiveTargets(); _targetsSave = _save; }
            catch { /* keep the existing in-memory save */ }
        }
    }

    private void TrainerLocateHold()
    {
        if (!_trainer.Attached || _holdLocating) return;
        RefreshSaveFromDisk();
        if (_targetCombo.SelectedItem is not LiveTarget target)
        {
            _holdStatus.ForeColor = Cc2Theme.Red;
            _holdStatus.Text = _save == null
                ? "Load the save you're playing (top-left) first — I fingerprint its inventory."
                : "No live-editable inventory in this save. (Warehouses need distinctive stock.)";
            return;
        }
        int[] row = target.Row;
        bool drift = target.ConsumableDrift;
        string what = target.Label.Replace("★ ", "");
        _holdLocating = true;
        _holdLocateBtn.Enabled = false;
        _holdApplyBtn.Enabled = _holdFillBtn.Enabled = false;
        _holdStatus.ForeColor = Cc2Theme.Cyan;
        _holdStatus.Text = $"Locating {what} in the game's memory… (a few seconds)";

        System.Threading.Tasks.Task.Run(() =>
        {
            int copies = 0; int[]? live = null; string err = "";
            try { copies = _trainer.LocateInventory(row, drift); live = _trainer.ReadHold(); }
            catch (Exception ex) { err = ex.Message; }
            BeginInvoke((Action)(() =>
            {
                _holdLocating = false;
                _holdLocateBtn.Enabled = _trainer.Attached;
                if (err.Length > 0)
                {
                    _holdStatus.ForeColor = Cc2Theme.Red;
                    _holdStatus.Text = "Locate failed: " + err;
                    return;
                }
                if (copies == 0 || live == null)
                {
                    _holdApplyBtn.Enabled = _holdFillBtn.Enabled = false;
                    _holdStatus.ForeColor = Cc2Theme.Red;
                    _holdStatus.Text = $"Couldn't find {what}. Save your game in-game (F5), then click LOCATE again — I read your latest save automatically.";
                    return;
                }
                PopulateHoldGrid(live);
                _holdApplyBtn.Enabled = _holdFillBtn.Enabled = true;
                _holdStatus.ForeColor = Cc2Theme.Green;
                _holdStatus.Text = $"Locked on {what} ({copies} cop{(copies == 1 ? "y" : "ies")}). Edit NEW QTY (or Fill + SET ALL), then APPLY.";
            }));
        });
    }

    private void PopulateHoldGrid(int[] live)
    {
        _holdLiveValues = live;
        _holdGrid.Rows.Clear();
        for (int i = 0; i < live.Length; i++)
            _holdGrid.Rows.Add(i, ItemCatalog.NameOf(i), ItemCatalog.CategoryOf(i), live[i], live[i]);
    }

    private void HoldSetAllNew(int value)
    {
        foreach (DataGridViewRow r in _holdGrid.Rows) r.Cells[4].Value = value;
    }

    private void HoldGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.ColumnIndex != 4 || e.RowIndex < 0) return;
        var cell = _holdGrid.Rows[e.RowIndex].Cells[4];
        long fallback = _holdLiveValues != null && e.RowIndex < _holdLiveValues.Length ? _holdLiveValues[e.RowIndex] : 0;
        cell.Value = long.TryParse(cell.Value?.ToString()?.Replace(",", "").Trim(), out var v) && v >= 0
            ? (int)Math.Min(v, int.MaxValue)
            : (int)fallback;
    }

    private void TrainerApplyHold()
    {
        if (_trainer.HoldCopyCount == 0) return;
        try
        {
            int n = _trainer.HoldSlotCount;
            var vals = new int[n];
            for (int i = 0; i < n; i++)
            {
                long fallback = _holdLiveValues != null && i < _holdLiveValues.Length ? _holdLiveValues[i] : 0;
                object? cell = i < _holdGrid.Rows.Count ? _holdGrid.Rows[i].Cells[4].Value : null;
                vals[i] = int.TryParse(cell?.ToString()?.Replace(",", "").Trim(), out var v) ? v : (int)fallback;
            }
            int copies = _trainer.WriteHold(vals);
            var live = _trainer.ReadHold();
            if (live != null) PopulateHoldGrid(live);
            _holdStatus.ForeColor = Cc2Theme.Green;
            _holdStatus.Text = $"Applied to {copies} copy(ies) — no reload needed. Check your STOCK screen.";
            SetStatus($"Live carrier hold updated across {copies} copies.");
        }
        catch (Exception ex) { Cc2MessageBox.Show(this, "APPLY FAILED", ex.Message, Cc2Theme.Red); }
    }

    private void RefreshTrainer()
    {
        if (_targetsSave != _save)
        {
            RebuildLiveTargets();
            _targetsSave = _save;
            if (_trainer.Attached) PrefillCreditFromSave();   // refresh auto-fill after a reload
        }

        bool a = _trainer.Attached;
        _attachBtn.Enabled = !a;
        _detachBtn.Enabled = a;
        _findCreditBtn.Enabled = a;
        _holdLocateBtn.Enabled = a && !_holdLocating && _targetCombo.Items.Count > 0;
        if (!a)
        {
            _setCreditBtn.Enabled = _freezeBtn.Enabled = false;
            _holdApplyBtn.Enabled = _holdFillBtn.Enabled = false;
            _holdGrid.Rows.Clear();
            _holdLiveValues = null;
            _holdStatus.ForeColor = Cc2Theme.MidGrey;
            _holdStatus.Text = "Attach, load your in-game save, pick a target, then LOCATE.";
        }
        _trainerStatus.ForeColor = a ? Cc2Theme.Green : Cc2Theme.MidGrey;
        _trainerStatus.Text = a ? _trainer.StatusText + "\n" + _trainer.ModuleInfo : _trainer.StatusText;

        // If a save was loaded after attaching, auto-fill the (still-untouched) credit box from it.
        if (a && _curCredit.Value == 0 && _save?.PlayerTeam != null) PrefillCreditFromSave();

        _cheatList.Controls.Clear();
        foreach (var cheat in _trainer.Cheats)
            _cheatList.Controls.Add(BuildCheatRow(cheat, a));
        _cheatList.Controls.Add(BuildToggleRow("Protect Carrier (freeze hull)",
            "player-only  •  lock on right after loading/saving",
            a, _trainer.Protecting, TrainerToggleProtect));
        _cheatList.Controls.Add(BuildToggleRow("Unlimited Fuel (carrier)",
            "player-only  •  holds fuel at its current level",
            a, _trainer.FuelFreezing, TrainerToggleFuel));
    }

    private Control BuildToggleRow(string title, string subtitle, bool attached, bool on, Action onClick)
    {
        var row = new Panel { Width = 380, Height = 50, Margin = new Padding(0, 0, 0, 8), BackColor = Cc2Theme.Screen };
        var toggle = new FlatButton(on ? "● ON" : "○ OFF")
        {
            Accent = on ? Cc2Theme.Green : Cc2Theme.MidGrey,
            Width = 84, Height = 36, Location = new Point(6, 6), Enabled = attached,
        };
        toggle.Click += (_, _) => onClick();

        var name = Style.Label(title, attached ? Cc2Theme.White : Cc2Theme.DimGrey, Cc2Theme.PixelBody);
        name.Location = new Point(100, 5);
        var sub = Style.Label(subtitle, Cc2Theme.MidGrey, Cc2Theme.PixelSmall);
        sub.Location = new Point(100, 26);

        row.Controls.Add(toggle);
        row.Controls.Add(name);
        row.Controls.Add(sub);
        return row;
    }

    /// <summary>Locate the player carrier in memory (HP + fuel) once per session; reused by both carrier toggles.</summary>
    private bool EnsureCarrierLocated()
    {
        if (_trainer.CarrierHpCount > 0 || _trainer.CarrierFuelCount > 0) return true;

        RefreshSaveFromDisk();
        var vs = (_save?.PlayerCarrierHold as VehicleHoldContainer)?.State;
        if (vs is not { HasHitpoints: true, HasFuel: true })
        {
            Cc2MessageBox.Show(this, "CARRIER",
                "Load the save you're playing (top-left) first — I locate your carrier by its HP + fuel.",
                Cc2Theme.Yellow);
            return false;
        }

        int hp = (int)(vs.Hitpoints ?? 0);
        int fuelBits = BitConverter.SingleToInt32Bits((float)(vs.Fuel ?? 0));
        int n;
        try { n = _trainer.LocateCarrierProtect(fuelBits, hp); }
        catch (Exception ex) { Cc2MessageBox.Show(this, "CARRIER", ex.Message, Cc2Theme.Red); return false; }

        if (n == 0)
        {
            Cc2MessageBox.Show(this, "CARRIER",
                "Couldn't pin down your carrier in memory.\n\n" +
                "It's found by your carrier's exact fuel level, so do this right after loading or saving " +
                "(before you've burned much fuel). Save in-game (F5) and try again.",
                Cc2Theme.Red);
            return false;
        }
        return true;
    }

    private void TrainerToggleProtect()
    {
        if (_trainer.Protecting) { _trainer.StopProtect(); SetStatus("Carrier protection OFF."); RefreshTrainer(); return; }
        if (!EnsureCarrierLocated()) return;
        _trainer.StartProtect(100_000);
        SetStatus($"Carrier protection ON — hull frozen ({_trainer.CarrierHpCount} field(s)). Player-only.");
        RefreshTrainer();
    }

    private void TrainerToggleFuel()
    {
        if (_trainer.FuelFreezing) { _trainer.StopFuelFreeze(); SetStatus("Unlimited fuel OFF."); RefreshTrainer(); return; }
        if (!EnsureCarrierLocated()) return;
        _trainer.StartFuelFreeze();   // hold at current level
        var f = _trainer.ReadCarrierFuel();
        SetStatus($"Unlimited fuel ON — carrier fuel held{(f is > 0 ? $" at {f:N0}L" : "")}. Player-only.");
        RefreshTrainer();
    }

    private Control BuildCheatRow(TrainerCheat cheat, bool attached)
    {
        var row = new Panel { Width = 620, Height = 50, Margin = new Padding(0, 0, 0, 8), BackColor = Cc2Theme.Screen };
        bool ok = attached && cheat.Resolved;

        var toggle = new FlatButton(cheat.Enabled ? "● ON" : "○ OFF")
        {
            Accent = cheat.Enabled ? Cc2Theme.Green : Cc2Theme.MidGrey,
            Width = 84, Height = 36, Location = new Point(6, 6), Enabled = ok,
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
        name.Location = new Point(100, 5);
        string sub = !attached ? "attach to resolve" : cheat.Resolved ? $"{cheat.FoundCount} site(s) found" : "signature not found on this build";
        if (cheat.AffectsEnemies) sub += "  •  affects enemies too";
        if (cheat.Experimental) sub += "  •  experimental";
        var subLbl = Style.Label(sub, cheat.Resolved || !attached ? Cc2Theme.MidGrey : Cc2Theme.Red, Cc2Theme.PixelSmall);
        subLbl.Location = new Point(100, 26);

        row.Controls.Add(toggle);
        row.Controls.Add(name);
        row.Controls.Add(subLbl);
        return row;
    }
}
