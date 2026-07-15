using System.ComponentModel;
using CC2CheatGUI.Core;

namespace CC2CheatGUI.UI;

public sealed partial class MainForm
{
    // Applies an in-memory cheat/edit, marks dirty, refreshes, and reports.
    private void ApplyEdit(string status, Action edit)
    {
        if (_save == null) return;
        edit();
        MarkDirty();
        RefreshAllSections();
        SetStatus(status);
    }

    private void RefreshAllSections()
    {
        _refreshOverview?.Invoke();
        _refreshCurrency?.Invoke();
        _refreshInventory?.Invoke();
        _refreshBlueprints?.Invoke();
        _refreshIslands?.Invoke();
        _refreshFleet?.Invoke();
        _refreshTrainer?.Invoke();   // so RELOAD re-fingerprints the live targets from the fresh save
    }

    private void RefreshCurrentSection()
    {
        switch (_currentSection)
        {
            case "overview": _refreshOverview?.Invoke(); break;
            case "currency": _refreshCurrency?.Invoke(); break;
            case "inventory": _refreshInventory?.Invoke(); break;
            case "blueprints": _refreshBlueprints?.Invoke(); break;
            case "islands": _refreshIslands?.Invoke(); break;
            case "fleet": _refreshFleet?.Invoke(); break;
            case "live": _refreshTrainer?.Invoke(); break;
        }
    }

    private Action? _refreshOverview, _refreshCurrency, _refreshInventory,
                    _refreshBlueprints, _refreshIslands, _refreshFleet, _refreshTrainer;

    private static Panel Host() => new() { Dock = DockStyle.Fill, BackColor = Cc2Theme.Screen };

    // =================================================================
    // OVERVIEW
    // =================================================================
    private Panel BuildOverviewSection()
    {
        var host = Host();

        var tiles = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 78, BackColor = Cc2Theme.Screen, Padding = new Padding(0, 0, 0, 10) };
        var tCurrency = NewTile("CREDITS", Cc2Theme.Green);
        var tBlueprints = NewTile("BLUEPRINTS", Cc2Theme.Cyan);
        var tIslands = NewTile("ISLANDS OWNED", Cc2Theme.Orange);
        var tFleet = NewTile("YOUR UNITS", Cc2Theme.Cyan);
        var tTeam = NewTile("PLAYER TEAM", Cc2Theme.White);
        foreach (var t in new[] { tCurrency, tBlueprints, tIslands, tFleet, tTeam }) tiles.Controls.Add(t);

        var panel = new ConsolePanel { Title = "QUICK CHEATS", Dock = DockStyle.Fill, TitleFill = Cc2Theme.Cyan };
        var inner = new FlowLayoutPanel { Dock = DockStyle.Fill, BackColor = Cc2Theme.Black, Padding = new Padding(14), AutoScroll = true };

        FlatButton Quick(string text, Color accent, Action<SaveFile> act, string done)
        {
            var b = new FlatButton(text) { Accent = accent, Width = 250, Height = 46, Margin = new Padding(6) };
            b.Click += (_, _) => { if (_save != null) ApplyEdit(done, () => act(_save)); };
            return b;
        }

        inner.Controls.Add(Quick("◆  MAX CREDITS (999,999,999)", Cc2Theme.Green,
            s => { if (s.PlayerTeam != null) s.PlayerTeam.Currency = 999_999_999; }, "Set player credits to 999,999,999."));
        inner.Controls.Add(Quick("❒  UNLOCK ALL BLUEPRINTS", Cc2Theme.Cyan,
            s => s.PlayerTeam?.UnlockAllBlueprints(), "Unlocked all blueprints."));
        inner.Controls.Add(Quick("◈  OWN ALL ISLANDS", Cc2Theme.Orange,
            s => s.OwnAllIslands(), "Captured every island for the player."));
        inner.Controls.Add(Quick("▤  FILL CARRIER HOLD (999 each)", Cc2Theme.Cyan,
            s => s.FillPlayerHolds(), "Filled every player hold slot to 999."));
        inner.Controls.Add(Quick("▟  REPAIR + REFUEL + REARM FLEET", Cc2Theme.Green,
            s => s.BuffPlayerFleet(), "Repaired, refuelled and rearmed all your units."));

        var god = new FlatButton("★  ARMAGEDDON — APPLY EVERYTHING") { Accent = Cc2Theme.Yellow, Width = 512, Height = 46, Margin = new Padding(6) };
        god.Click += (_, _) =>
        {
            if (_save == null) return;
            ApplyEdit("Applied all cheats. Review, then SAVE CHANGES.", () =>
            {
                if (_save.PlayerTeam != null) { _save.PlayerTeam.Currency = 999_999_999; _save.PlayerTeam.UnlockAllBlueprints(); }
                _save.OwnAllIslands();
                _save.FillPlayerHolds();
                _save.BuffPlayerFleet();
            });
        };
        inner.Controls.Add(god);

        var note = Style.Label(
            "Cheats are applied in memory — click SAVE CHANGES (top-right) to write the file.\n" +
            "A timestamped .bak backup is created automatically. Close Carrier Command 2 before saving.",
            Cc2Theme.MidGrey, Cc2Theme.PixelSmall);
        note.Margin = new Padding(8, 14, 0, 0);
        note.MaximumSize = new Size(900, 0);
        inner.Controls.Add(note);

        panel.Controls.Add(inner);
        host.Controls.Add(panel);
        host.Controls.Add(tiles);

        _refreshOverview = () =>
        {
            bool has = _save != null;
            var p = _save?.PlayerTeam;
            tCurrency.Value = has && p != null ? p.Currency.ToString("N0") : "—";
            tBlueprints.Value = has && p != null ? $"{p.UnlockedBlueprintCount}" : "—";
            int owned = 0, total = _save?.Islands.Count ?? 0;
            if (_save != null && int.TryParse(_save.PlayerTeamId, out var pt))
                owned = _save.Islands.Count(i => i.TeamControl == pt);
            tIslands.Value = has ? $"{owned}/{total}" : "—";
            tFleet.Value = has ? _save!.PlayerUnits.Count().ToString() : "—";
            tTeam.Value = has ? (_save!.PlayerTeamId.Length > 0 ? _save.PlayerTeamId : "?") : "—";
            foreach (Control c in inner.Controls) c.Enabled = has;
        };
        return host;
    }

    private static StatTile NewTile(string label, Color accent) => new()
    {
        Label = label, Accent = accent, Width = 200, Height = 64, Margin = new Padding(0, 0, 10, 0),
    };

    // =================================================================
    // CURRENCY
    // =================================================================
    private DataGridView _currencyGrid = null!;
    private Panel BuildCurrencySection()
    {
        var host = Host();
        var panel = new ConsolePanel { Title = "TEAM CREDITS", Dock = DockStyle.Fill, TitleFill = Cc2Theme.Green };

        var grid = Style.Grid();
        grid.Dock = DockStyle.Fill;
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TEAM", ReadOnly = true, FillWeight = 20 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "KIND", ReadOnly = true, FillWeight = 30 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "CREDITS", FillWeight = 50 });
        grid.CellEndEdit += CurrencyCellEndEdit;
        grid.CellFormatting += (_, e) =>
        {
            if (_save == null || e.RowIndex < 0 || e.RowIndex >= _save.Teams.Count) return;
            var team = _save.Teams[e.RowIndex];
            e.CellStyle!.ForeColor = team.IsPlayer ? Cc2Theme.Cyan
                : team.IsAi && !team.IsNeutral ? Cc2Theme.Red
                : Cc2Theme.MidGrey;
        };
        _currencyGrid = grid;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, BackColor = Cc2Theme.Black, Padding = new Padding(8, 8, 0, 0) };
        toolbar.Controls.Add(Style.Label("Set player:", Cc2Theme.MidGrey, Cc2Theme.PixelSmall));
        foreach (var (txt, val) in new (string, long)[] { ("1,000,000", 1_000_000), ("100,000,000", 100_000_000), ("MAX (999,999,999)", 999_999_999) })
        {
            var b = new FlatButton(txt) { Accent = Cc2Theme.Green, Width = txt.Length > 12 ? 160 : 120, Height = 26, Margin = new Padding(4, 2, 4, 2) };
            long v = val;
            b.Click += (_, _) => { if (_save?.PlayerTeam != null) ApplyEdit($"Player credits set to {v:N0}.", () => _save!.PlayerTeam!.Currency = v); };
            toolbar.Controls.Add(b);
        }

        panel.Controls.Add(grid);
        panel.Controls.Add(toolbar);
        host.Controls.Add(panel);

        _refreshCurrency = () =>
        {
            grid.Rows.Clear();
            if (_save == null) return;
            foreach (var t in _save.Teams)
                grid.Rows.Add((t.IsPlayer ? "★ " : "") + t.Id, t.Kind, t.Currency.ToString("N0"));
        };
        return host;
    }

    private void CurrencyCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_save == null || e.ColumnIndex != 2 || e.RowIndex < 0 || e.RowIndex >= _save.Teams.Count) return;
        var raw = _currencyGrid.Rows[e.RowIndex].Cells[2].Value?.ToString()?.Replace(",", "").Trim();
        if (long.TryParse(raw, out var v) && v >= 0)
        {
            if (v > 4_294_967_295) v = 4_294_967_295; // uint32 ceiling
            _save.Teams[e.RowIndex].Currency = v;
            MarkDirty();
            SetStatus($"Team {_save.Teams[e.RowIndex].Id} credits set to {v:N0}.");
        }
        _currencyGrid.Rows[e.RowIndex].Cells[2].Value = _save.Teams[e.RowIndex].Currency.ToString("N0");
    }

    // =================================================================
    // BLUEPRINTS
    // =================================================================
    private Label _bpCount = null!;
    private Panel BuildBlueprintsSection()
    {
        var host = Host();
        var panel = new ConsolePanel { Title = "BLUEPRINTS / TECH", Dock = DockStyle.Fill, TitleFill = Cc2Theme.Cyan };
        var inner = new Panel { Dock = DockStyle.Fill, BackColor = Cc2Theme.Black, Padding = new Padding(20) };

        _bpCount = Style.Label("—", Cc2Theme.Cyan, Cc2Theme.PixelHuge);
        _bpCount.Location = new Point(20, 20);
        var cap = Style.Label("blueprints unlocked for the player team", Cc2Theme.MidGrey, Cc2Theme.PixelBody);
        cap.Location = new Point(24, 84);

        var unlock = new FlatButton("❒  UNLOCK ALL BLUEPRINTS") { Accent = Cc2Theme.Green, Width = 260, Height = 40, Location = new Point(24, 130) };
        unlock.Click += (_, _) => { if (_save?.PlayerTeam != null) ApplyEdit("Unlocked all blueprints (64-bit set).", () => _save!.PlayerTeam!.UnlockAllBlueprints()); };
        var clear = new FlatButton("CLEAR ALL") { Accent = Cc2Theme.Red, Width = 130, Height = 40, Location = new Point(296, 130) };
        clear.Click += (_, _) => { if (_save?.PlayerTeam != null) ApplyEdit("Cleared all blueprints.", () => _save!.PlayerTeam!.ClearBlueprints()); };

        var note = Style.Label(
            "CC2 stores unlocked tech as a bit array on the player team. \"Unlock all\" fills it with\n" +
            "64 unlocked slots (every vehicle & attachment blueprint). This grants the full tech tree\n" +
            "instantly. Extra bits beyond the game's blueprint count are harmless.",
            Cc2Theme.MidGrey, Cc2Theme.PixelSmall);
        note.Location = new Point(24, 190);

        inner.Controls.Add(_bpCount);
        inner.Controls.Add(cap);
        inner.Controls.Add(unlock);
        inner.Controls.Add(clear);
        inner.Controls.Add(note);
        panel.Controls.Add(inner);
        host.Controls.Add(panel);

        _refreshBlueprints = () =>
        {
            _bpCount.Text = _save?.PlayerTeam != null ? _save.PlayerTeam.UnlockedBlueprintCount.ToString() : "—";
            unlock.Enabled = clear.Enabled = _save?.PlayerTeam != null;
        };
        return host;
    }

    // =================================================================
    // ISLANDS
    // =================================================================
    private DataGridView _islandGrid = null!;
    private ComboBox _islandOwnerCombo = null!;
    private Panel BuildIslandsSection()
    {
        var host = Host();
        var panel = new ConsolePanel { Title = "ISLANDS", Dock = DockStyle.Fill, TitleFill = Cc2Theme.Orange };

        var grid = Style.Grid();
        grid.Dock = DockStyle.Fill;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = true;
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ISLAND", ReadOnly = true, FillWeight = 20 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "BIOME", ReadOnly = true, FillWeight = 40 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "OWNER", ReadOnly = true, FillWeight = 40 });
        grid.CellFormatting += (_, e) =>
        {
            if (_save == null || e.RowIndex < 0 || e.RowIndex >= _save.Islands.Count || e.ColumnIndex != 2) return;
            int owner = _save.Islands[e.RowIndex].TeamControl;
            int.TryParse(_save.PlayerTeamId, out var pt);
            e.CellStyle!.ForeColor = owner == pt ? Cc2Theme.Cyan : owner == 0 ? Cc2Theme.MidGrey : Cc2Theme.Red;
        };
        _islandGrid = grid;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, BackColor = Cc2Theme.Black, Padding = new Padding(8, 8, 0, 0) };
        toolbar.Controls.Add(Style.Label("Set selected to:", Cc2Theme.MidGrey, Cc2Theme.PixelSmall));
        _islandOwnerCombo = new ComboBox { Width = 150, Height = 24 };
        Style.ApplyDark(_islandOwnerCombo);
        toolbar.Controls.Add(_islandOwnerCombo);
        var applySel = new FlatButton("APPLY") { Accent = Cc2Theme.Cyan, Width = 90, Height = 26, Margin = new Padding(6, 2, 4, 2) };
        applySel.Click += (_, _) => ApplyIslandOwner();
        toolbar.Controls.Add(applySel);
        var ownAll = new FlatButton("◈  OWN ALL ISLANDS") { Accent = Cc2Theme.Orange, Width = 180, Height = 26, Margin = new Padding(16, 2, 4, 2) };
        ownAll.Click += (_, _) => { if (_save != null) ApplyEdit("All islands captured for the player.", () => _save.OwnAllIslands()); };
        toolbar.Controls.Add(ownAll);

        panel.Controls.Add(grid);
        panel.Controls.Add(toolbar);
        host.Controls.Add(panel);

        _refreshIslands = () =>
        {
            grid.Rows.Clear();
            _islandOwnerCombo.Items.Clear();
            if (_save == null) return;
            foreach (var t in _save.Teams) _islandOwnerCombo.Items.Add(new OwnerOption(int.TryParse(t.Id, out var i) ? i : 0, $"{t.Id} — {t.Kind}"));
            if (!_save.Teams.Any(t => t.Id == "0")) _islandOwnerCombo.Items.Insert(0, new OwnerOption(0, "0 — Neutral"));
            if (_islandOwnerCombo.Items.Count > 0) _islandOwnerCombo.SelectedIndex = Math.Min(1, _islandOwnerCombo.Items.Count - 1);
            foreach (var isl in _save.Islands)
                grid.Rows.Add($"#{isl.Id}", isl.BiomeName, OwnerLabel(isl.TeamControl));
        };
        return host;
    }

    private sealed record OwnerOption(int Team, string Text) { public override string ToString() => Text; }

    private string OwnerLabel(int team)
    {
        if (_save != null && int.TryParse(_save.PlayerTeamId, out var pt) && team == pt) return $"team {team} (YOU)";
        return team == 0 ? "neutral" : $"team {team}";
    }

    private void ApplyIslandOwner()
    {
        if (_save == null || _islandOwnerCombo.SelectedItem is not OwnerOption opt) return;
        int changed = 0;
        foreach (DataGridViewRow row in _islandGrid.SelectedRows)
            if (row.Index >= 0 && row.Index < _save.Islands.Count)
            {
                _save.Islands[row.Index].TeamControl = opt.Team;
                row.Cells[2].Value = OwnerLabel(opt.Team);
                changed++;
            }
        if (changed > 0) { MarkDirty(); SetStatus($"Set {changed} island(s) to owner {opt.Team}."); _refreshOverview?.Invoke(); }
    }

    // =================================================================
    // FLEET
    // =================================================================
    private DataGridView _fleetGrid = null!;
    private List<VehicleState> _fleetUnits = new();
    private Panel BuildFleetSection()
    {
        var host = Host();
        var panel = new ConsolePanel { Title = "YOUR FLEET", Dock = DockStyle.Fill, TitleFill = Cc2Theme.Cyan };

        var grid = Style.Grid();
        grid.Dock = DockStyle.Fill;
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "UNIT", ReadOnly = true, FillWeight = 40 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "HITPOINTS", FillWeight = 22 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "FUEL", ReadOnly = true, FillWeight = 20 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "WEAPONS", ReadOnly = true, FillWeight = 18 });
        grid.CellEndEdit += FleetCellEndEdit;
        _fleetGrid = grid;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, BackColor = Cc2Theme.Black, Padding = new Padding(8, 8, 0, 0) };
        FlatButton Bulk(string txt, Color c, Action act)
        {
            var b = new FlatButton(txt) { Accent = c, Width = 150, Height = 26, Margin = new Padding(4, 2, 4, 2) };
            b.Click += (_, _) => { if (_save != null) act(); };
            return b;
        }
        toolbar.Controls.Add(Bulk("REPAIR ALL", Cc2Theme.Green, () => ApplyEdit("Repaired all units (HP 100,000).", () => _save!.BuffPlayerFleet(repair: true, refuel: false, rearm: false))));
        toolbar.Controls.Add(Bulk("REFUEL ALL", Cc2Theme.Cyan, () => ApplyEdit("Refuelled all units.", () => _save!.BuffPlayerFleet(repair: false, refuel: true, rearm: false))));
        toolbar.Controls.Add(Bulk("REARM ALL", Cc2Theme.Orange, () => ApplyEdit("Rearmed all weapons.", () => _save!.BuffPlayerFleet(repair: false, refuel: false, rearm: true))));
        toolbar.Controls.Add(Bulk("▟  ALL THREE", Cc2Theme.Yellow, () => ApplyEdit("Repaired + refuelled + rearmed the fleet.", () => _save!.BuffPlayerFleet())));

        panel.Controls.Add(grid);
        panel.Controls.Add(toolbar);
        host.Controls.Add(panel);

        _refreshFleet = () =>
        {
            grid.Rows.Clear();
            _fleetUnits = _save?.PlayerUnits.ToList() ?? new List<VehicleState>();
            foreach (var u in _fleetUnits)
            {
                string hp = u.HasHitpoints ? (u.Hitpoints?.ToString("N0") ?? "—") : "—";
                string fuel = u.HasFuel ? (u.Fuel is double f ? f.ToString("N0") : "—") : "—";
                int weapons = u.Attachments.Count(a => a.HasAmmo);
                grid.Rows.Add(_save!.DescribeUnit(u).Replace("★ ", "").Replace("  [YOURS]", ""), hp, fuel, weapons > 0 ? weapons.ToString() : "—");
            }
        };
        return host;
    }

    private void FleetCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_save == null || e.ColumnIndex != 1 || e.RowIndex < 0 || e.RowIndex >= _fleetUnits.Count) return;
        var unit = _fleetUnits[e.RowIndex];
        var raw = _fleetGrid.Rows[e.RowIndex].Cells[1].Value?.ToString()?.Replace(",", "").Trim();
        if (unit.HasHitpoints && long.TryParse(raw, out var v) && v >= 0)
        {
            unit.Hitpoints = v;
            MarkDirty();
            SetStatus($"Unit {unit.Id} hitpoints set to {v:N0}.");
        }
        _fleetGrid.Rows[e.RowIndex].Cells[1].Value = unit.HasHitpoints ? unit.Hitpoints?.ToString("N0") : "—";
    }
}
