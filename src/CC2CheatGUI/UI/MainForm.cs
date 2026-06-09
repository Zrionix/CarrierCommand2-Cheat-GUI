using System.ComponentModel;
using CC2CheatGUI.Core;

namespace CC2CheatGUI.UI;

public sealed class MainForm : Form
{
    private readonly ComboBox _slotCombo = new();
    private readonly Button _openButton = new();
    private readonly Button _reloadButton = new();
    private readonly Button _saveButton = new();
    private readonly StatusStrip _statusStrip = new();
    private readonly ToolStripStatusLabel _statusLabel = new();

    // Inventory tab
    private readonly ListBox _containerList = new();
    private readonly DataGridView _inventoryGrid = new();
    private readonly NumericUpDown _bulkValue = new();
    private readonly NumericUpDown _addItemId = new();
    private readonly NumericUpDown _addItemQty = new();

    // Currency tab
    private readonly NumericUpDown _currentMoney = new();
    private readonly NumericUpDown _newMoney = new();
    private readonly DataGridView _matchGrid = new();
    private readonly Label _currencyStatus = new();

    private SaveFile? _save;
    private InventoryContainer? _currentContainer;
    private BindingList<InventoryRow> _rows = new();
    private List<CurrencyMatch> _matches = new();
    private BindingList<MatchRow> _matchRows = new();
    private bool _dirty;

    public MainForm()
    {
        Text = "Carrier Command 2 — Cheat GUI";
        Width = 1000;
        Height = 720;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 560);
        Font = new Font("Segoe UI", 9f);

        Controls.Add(BuildTabs());
        Controls.Add(BuildTopBar());
        Controls.Add(BuildStatusBar());

        Load += (_, _) => RefreshSlots();
        FormClosing += MainForm_FormClosing;
    }

    // ---------------------------------------------------------------------
    // Top bar
    // ---------------------------------------------------------------------

    private Control BuildTopBar()
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 88, Padding = new Padding(10, 8, 10, 6) };

        var line1 = new Label
        {
            Text = "Save file:",
            AutoSize = true,
            Location = new Point(12, 14),
        };

        _slotCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _slotCombo.Location = new Point(80, 10);
        _slotCombo.Width = 540;
        _slotCombo.SelectedIndexChanged += (_, _) => LoadSelectedSlot();

        _openButton.Text = "Open…";
        _openButton.Location = new Point(630, 9);
        _openButton.Width = 80;
        _openButton.Click += (_, _) => OpenFileManually();

        _reloadButton.Text = "Reload";
        _reloadButton.Location = new Point(716, 9);
        _reloadButton.Width = 80;
        _reloadButton.Click += (_, _) => RefreshSlots();

        _saveButton.Text = "💾  Save changes (creates .bak)";
        _saveButton.Location = new Point(12, 46);
        _saveButton.Width = 240;
        _saveButton.Height = 30;
        _saveButton.Enabled = false;
        _saveButton.Click += (_, _) => SaveChanges();

        var warn = new Label
        {
            Text = "Close Carrier Command 2 before saving, or it may overwrite your edits on autosave.",
            AutoSize = true,
            ForeColor = Color.FromArgb(176, 96, 0),
            Location = new Point(266, 53),
        };

        panel.Controls.Add(line1);
        panel.Controls.Add(_slotCombo);
        panel.Controls.Add(_openButton);
        panel.Controls.Add(_reloadButton);
        panel.Controls.Add(_saveButton);
        panel.Controls.Add(warn);
        return panel;
    }

    // ---------------------------------------------------------------------
    // Tabs
    // ---------------------------------------------------------------------

    private Control BuildTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };

        var inventoryTab = new TabPage("Inventory");
        inventoryTab.Controls.Add(BuildInventoryTab());

        var currencyTab = new TabPage("Currency");
        currencyTab.Controls.Add(BuildCurrencyTab());

        tabs.TabPages.Add(inventoryTab);
        tabs.TabPages.Add(currencyTab);
        return tabs;
    }

    private Control BuildInventoryTab()
    {
        var root = new Panel { Dock = DockStyle.Fill };

        _containerList.Dock = DockStyle.Left;
        _containerList.Width = 300;
        _containerList.IntegralHeight = false;
        _containerList.SelectedIndexChanged += (_, _) => ShowSelectedContainer();

        var splitter = new Splitter { Dock = DockStyle.Left, Width = 4 };

        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

        ConfigureInventoryGrid();
        _inventoryGrid.Dock = DockStyle.Fill;

        var bottom = BuildInventoryToolbar();

        right.Controls.Add(_inventoryGrid);
        right.Controls.Add(bottom);

        root.Controls.Add(right);
        root.Controls.Add(splitter);
        root.Controls.Add(_containerList);
        return root;
    }

    private void ConfigureInventoryGrid()
    {
        _inventoryGrid.AutoGenerateColumns = false;
        _inventoryGrid.AllowUserToAddRows = false;
        _inventoryGrid.AllowUserToDeleteRows = false;
        _inventoryGrid.RowHeadersVisible = false;
        _inventoryGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _inventoryGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _inventoryGrid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;

        _inventoryGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(InventoryRow.Id),
            HeaderText = "ID",
            ReadOnly = true,
            FillWeight = 12,
        });
        _inventoryGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(InventoryRow.Name),
            HeaderText = "Item",
            ReadOnly = true,
            FillWeight = 38,
        });
        _inventoryGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(InventoryRow.Category),
            HeaderText = "Category",
            ReadOnly = true,
            FillWeight = 28,
        });
        _inventoryGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(InventoryRow.Quantity),
            HeaderText = "Quantity",
            FillWeight = 22,
        });

        _inventoryGrid.CellEndEdit += InventoryGrid_CellEndEdit;
        // Swallow binding conversion errors (e.g. non-numeric typed into Quantity); CellEndEdit reverts.
        _inventoryGrid.DataError += (_, e) => e.ThrowException = false;
    }

    private Control BuildInventoryToolbar()
    {
        var bar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };

        _bulkValue.Maximum = 1_000_000_000;
        _bulkValue.Minimum = 0;
        _bulkValue.Width = 110;
        _bulkValue.Value = 1000;

        var applySel = new Button { Text = "Set selected rows", Width = 130, Height = 26 };
        applySel.Click += (_, _) => BulkSet(selectedOnly: true);

        var fillAll = new Button { Text = "Set ALL rows", Width = 110, Height = 26 };
        fillAll.Click += (_, _) => BulkSet(selectedOnly: false);

        var sep = new Label { Text = "   |   Add/Set by ID:", AutoSize = true, Padding = new Padding(8, 6, 0, 0) };

        _addItemId.Maximum = 1000;
        _addItemId.Minimum = 0;
        _addItemId.Width = 60;

        _addItemQty.Maximum = 1_000_000_000;
        _addItemQty.Minimum = 0;
        _addItemQty.Width = 110;
        _addItemQty.Value = 100;

        var addBtn = new Button { Text = "Apply", Width = 70, Height = 26 };
        addBtn.Click += (_, _) => AddOrSetById();

        bar.Controls.Add(new Label { Text = "Value:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        bar.Controls.Add(_bulkValue);
        bar.Controls.Add(applySel);
        bar.Controls.Add(fillAll);
        bar.Controls.Add(sep);
        bar.Controls.Add(_addItemId);
        bar.Controls.Add(_addItemQty);
        bar.Controls.Add(addBtn);
        return bar;
    }

    private Control BuildCurrencyTab()
    {
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

        var info = new Label
        {
            Dock = DockStyle.Top,
            Height = 76,
            Text =
                "Carrier Command 2 has no fixed XML tag for money, so locate it by value:\r\n" +
                "1. Read your CURRENT money from the in-game HUD and type it below, then click Find.\r\n" +
                "2. Tick the match(es) flagged as the likely budget (near your team), set the new amount, and Apply.\r\n" +
                "3. Click \"Save changes\" in the top bar to write the file.",
        };

        var findRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40, WrapContents = false };
        _currentMoney.Maximum = 1_000_000_000;
        _currentMoney.Minimum = 0;
        _currentMoney.Width = 140;
        var findBtn = new Button { Text = "Find", Width = 90, Height = 26 };
        findBtn.Click += (_, _) => FindMoney();
        findRow.Controls.Add(new Label { Text = "Current in-game money:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
        findRow.Controls.Add(_currentMoney);
        findRow.Controls.Add(findBtn);

        var applyRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        _newMoney.Maximum = 1_000_000_000;
        _newMoney.Minimum = 0;
        _newMoney.Width = 140;
        _newMoney.Value = 1_000_000;
        var applyBtn = new Button { Text = "Apply to checked", Width = 140, Height = 28 };
        applyBtn.Click += (_, _) => ApplyMoney();
        applyRow.Controls.Add(new Label { Text = "New amount:", AutoSize = true, Padding = new Padding(0, 8, 6, 0) });
        applyRow.Controls.Add(_newMoney);
        applyRow.Controls.Add(applyBtn);
        applyRow.Controls.Add(_currencyStatus);
        _currencyStatus.AutoSize = true;
        _currencyStatus.Padding = new Padding(12, 8, 0, 0);

        ConfigureMatchGrid();
        _matchGrid.Dock = DockStyle.Fill;

        root.Controls.Add(_matchGrid);
        root.Controls.Add(applyRow);
        root.Controls.Add(findRow);
        root.Controls.Add(info);
        return root;
    }

    private void ConfigureMatchGrid()
    {
        _matchGrid.AutoGenerateColumns = false;
        _matchGrid.AllowUserToAddRows = false;
        _matchGrid.RowHeadersVisible = false;
        _matchGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        _matchGrid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = nameof(MatchRow.Apply),
            HeaderText = "Set",
            FillWeight = 8,
        });
        _matchGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MatchRow.Likely),
            HeaderText = "Likely budget?",
            ReadOnly = true,
            FillWeight = 18,
        });
        _matchGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MatchRow.Location),
            HeaderText = "Location in save",
            ReadOnly = true,
            FillWeight = 56,
        });
        _matchGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(MatchRow.Value),
            HeaderText = "Value",
            ReadOnly = true,
            FillWeight = 18,
        });

        // Commit checkbox toggles immediately.
        _matchGrid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_matchGrid.IsCurrentCellDirty)
                _matchGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
    }

    // ---------------------------------------------------------------------
    // Save selection / loading
    // ---------------------------------------------------------------------

    private void RefreshSlots()
    {
        var slots = SaveLocator.FindSlots();
        _slotCombo.Items.Clear();
        foreach (var slot in slots) _slotCombo.Items.Add(slot);

        if (slots.Count == 0)
        {
            SetStatus("No saves auto-detected under %APPDATA%\\Carrier Command 2 — use Open… to pick a save.xml.");
        }
        else
        {
            SetStatus($"{slots.Count} save slot(s) detected.");
            _slotCombo.SelectedIndex = 0; // triggers LoadSelectedSlot
        }
    }

    private void LoadSelectedSlot()
    {
        if (_slotCombo.SelectedItem is SaveSlot slot)
            LoadSave(slot.SaveXmlPath);
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
        if (dialog.ShowDialog(this) == DialogResult.OK)
            LoadSave(dialog.FileName);
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

            PopulateContainers();
            ClearMoneyResults();
            SetStatus($"Loaded: {path}");
            Text = $"Carrier Command 2 — Cheat GUI   [{System.IO.Path.GetFileName(path)}]";
        }
        catch (Exception ex)
        {
            _save = null;
            _saveButton.Enabled = false;
            MessageBox.Show(this,
                $"Could not load the save file:\n\n{ex.Message}",
                "Load failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void PopulateContainers()
    {
        _containerList.Items.Clear();
        _currentContainer = null;
        _rows = new BindingList<InventoryRow>();
        _inventoryGrid.DataSource = _rows;

        if (_save == null) return;
        foreach (var c in _save.Containers) _containerList.Items.Add(c);
        if (_containerList.Items.Count > 0) _containerList.SelectedIndex = 0;
    }

    private void ShowSelectedContainer()
    {
        _currentContainer = _containerList.SelectedItem as InventoryContainer;
        _rows = new BindingList<InventoryRow>();
        if (_currentContainer != null)
        {
            foreach (var (itemId, qty) in _currentContainer.Entries)
            {
                _rows.Add(new InventoryRow
                {
                    Id = itemId,
                    Name = ItemCatalog.NameOf(itemId),
                    Category = ItemCatalog.CategoryOf(itemId),
                    Quantity = qty,
                });
            }
        }
        _inventoryGrid.DataSource = _rows;
    }

    // ---------------------------------------------------------------------
    // Inventory editing
    // ---------------------------------------------------------------------

    private void InventoryGrid_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_currentContainer == null) return;
        if (_inventoryGrid.Columns[e.ColumnIndex].DataPropertyName != nameof(InventoryRow.Quantity)) return;
        if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;

        var row = _rows[e.RowIndex];
        var raw = _inventoryGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
        if (!TryParseQuantity(raw, out long qty))
        {
            // Revert the displayed value.
            _inventoryGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = row.Quantity;
            return;
        }

        if (!WarnIfHuge(qty)) { _inventoryGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = row.Quantity; return; }

        row.Quantity = qty;
        if (_currentContainer.SetQuantity(row.Id, qty)) MarkDirty();
    }

    private void BulkSet(bool selectedOnly)
    {
        if (_currentContainer == null) return;
        long qty = (long)_bulkValue.Value;
        if (!WarnIfHuge(qty)) return;

        IEnumerable<InventoryRow> targets = selectedOnly
            ? _inventoryGrid.SelectedRows.Cast<DataGridViewRow>()
                .Where(r => r.Index >= 0 && r.Index < _rows.Count).Select(r => _rows[r.Index])
            : _rows;

        int changed = 0;
        foreach (var row in targets.ToList())
        {
            row.Quantity = qty;
            if (_currentContainer.SetQuantity(row.Id, qty)) changed++;
        }
        _rows.ResetBindings();
        if (changed > 0) MarkDirty();
        SetStatus($"Set {changed} item(s) to {qty:N0}.");
    }

    private void AddOrSetById()
    {
        if (_currentContainer == null) return;
        int id = (int)_addItemId.Value;
        long qty = (long)_addItemQty.Value;
        if (!WarnIfHuge(qty)) return;

        if (!_currentContainer.SetQuantity(id, qty))
        {
            MessageBox.Show(this,
                $"Item ID {id} could not be set on this container.\n\n" +
                "Vehicle holds use fixed positional slots, so only existing item IDs can be edited there. " +
                "Island/warehouse stock can add new IDs.",
                "Could not set item", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        MarkDirty();
        ShowSelectedContainer(); // refresh to show newly added/updated row
        SetStatus($"Set item {id} ({ItemCatalog.NameOf(id)}) to {qty:N0}.");
    }

    // ---------------------------------------------------------------------
    // Currency
    // ---------------------------------------------------------------------

    private void FindMoney()
    {
        if (_save == null) return;
        long value = (long)_currentMoney.Value;
        _matches = _save.FindValue(value);

        _matchRows = new BindingList<MatchRow>();
        bool autoTickedSingleBudget = false;
        var budgets = _matches.Where(m => m.LikelyBudget).ToList();

        for (int i = 0; i < _matches.Count; i++)
        {
            var m = _matches[i];
            bool tick = false;
            if (budgets.Count == 1 && m.LikelyBudget) { tick = true; autoTickedSingleBudget = true; }
            _matchRows.Add(new MatchRow
            {
                Apply = tick,
                Likely = m.LikelyBudget ? "★ likely" : "",
                Location = m.Description,
                Value = m.Value,
            });
        }

        _matchGrid.DataSource = _matchRows;

        if (_matches.Count == 0)
            _currencyStatus.Text = $"No field equals {value:N0}. Double-check the exact amount on the HUD.";
        else if (autoTickedSingleBudget)
            _currencyStatus.Text = $"{_matches.Count} match(es). Auto-ticked the single likely budget field.";
        else
            _currencyStatus.Text = $"{_matches.Count} match(es). Tick the budget field(s) to change.";
    }

    private void ApplyMoney()
    {
        if (_save == null || _matchRows.Count == 0) return;
        long newValue = (long)_newMoney.Value;

        int applied = 0;
        for (int i = 0; i < _matchRows.Count && i < _matches.Count; i++)
        {
            if (!_matchRows[i].Apply) continue;
            _matches[i].Apply(newValue);
            _matchRows[i].Value = newValue;
            applied++;
        }
        _matchRows.ResetBindings();

        if (applied == 0)
        {
            MessageBox.Show(this, "Tick at least one match first.", "Nothing selected",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        MarkDirty();
        _currencyStatus.Text = $"Set {applied} field(s) to {newValue:N0}. Click \"Save changes\" to write.";
    }

    private void ClearMoneyResults()
    {
        _matches = new List<CurrencyMatch>();
        _matchRows = new BindingList<MatchRow>();
        _matchGrid.DataSource = _matchRows;
        _currencyStatus.Text = "";
    }

    // ---------------------------------------------------------------------
    // Save
    // ---------------------------------------------------------------------

    private void SaveChanges()
    {
        if (_save == null) return;
        try
        {
            Cursor = Cursors.WaitCursor;
            string backup = _save.Save();
            _dirty = false;
            if (Text.StartsWith("*")) Text = Text[1..];
            SetStatus($"Saved. Backup written: {System.IO.Path.GetFileName(backup)}");
            MessageBox.Show(this,
                $"Save written successfully.\n\nA backup of the previous file was created:\n{backup}",
                "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not save:\n\n{ex.Message}", "Save failed",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private void MarkDirty()
    {
        _dirty = true;
        if (!Text.StartsWith("*")) Text = "*" + Text;
    }

    private Control BuildStatusBar()
    {
        _statusLabel.Text = "No save loaded.";
        _statusLabel.Spring = true;
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        _statusStrip.Items.Add(_statusLabel);
        _statusStrip.SizingGrip = false;
        return _statusStrip;
    }

    private void SetStatus(string text)
    {
        _statusLabel.Text = text;
    }

    private static bool TryParseQuantity(object? raw, out long value)
    {
        value = 0;
        if (raw == null) return false;
        var s = raw.ToString();
        if (string.IsNullOrWhiteSpace(s)) return false;
        return long.TryParse(s.Replace(",", "").Trim(), out value);
    }

    private bool WarnIfHuge(long qty)
    {
        const long Threshold = 1_000_000;
        if (qty <= Threshold) return true;
        var result = MessageBox.Show(this,
            $"{qty:N0} is very large. The community reports that extreme quantities can cause in-game lag.\n\nApply anyway?",
            "Large quantity", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        return result == DialogResult.Yes;
    }

    private bool ConfirmDiscardIfDirty()
    {
        if (!_dirty) return true;
        var result = MessageBox.Show(this,
            "You have unsaved changes. Discard them and continue?",
            "Unsaved changes", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        return result == DialogResult.Yes;
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!ConfirmDiscardIfDirty()) e.Cancel = true;
    }

    private sealed class InventoryRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public long Quantity { get; set; }
    }

    private sealed class MatchRow
    {
        public bool Apply { get; set; }
        public string Likely { get; set; } = "";
        public string Location { get; set; } = "";
        public long Value { get; set; }
    }
}
