using System.ComponentModel;
using System.Drawing.Text;
using CC2CheatGUI.Core;

namespace CC2CheatGUI.UI;

public sealed partial class MainForm
{
    private ListBox _containerList = null!;
    private DataGridView _inventoryGrid = null!;
    private NumericUpDown _bulkValue = null!;
    private NumericUpDown _addItemId = null!;
    private NumericUpDown _addItemQty = null!;
    private InventoryContainer? _currentContainer;

    private sealed class InvRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public long Quantity { get; set; }
    }
    private BindingList<InvRow> _rows = new();

    private Panel BuildInventorySection()
    {
        var host = Host();

        // Left: container list
        var listPanel = new ConsolePanel { Title = "CONTAINERS", Dock = DockStyle.Left, Width = 330, TitleFill = Cc2Theme.Cyan };
        _containerList = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Cc2Theme.Black,
            ForeColor = Cc2Theme.White,
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 26,
            IntegralHeight = false,
            Font = Cc2Theme.Data,
        };
        _containerList.DrawItem += ContainerListDrawItem;
        _containerList.SelectedIndexChanged += (_, _) => ShowSelectedContainer();
        listPanel.Controls.Add(_containerList);

        // Right: grid + toolbar
        var rightPanel = new ConsolePanel { Title = "ITEMS", Dock = DockStyle.Fill, TitleFill = Cc2Theme.Cyan };

        var grid = Style.Grid();
        grid.Dock = DockStyle.Fill;
        grid.AutoGenerateColumns = false;
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvRow.Id), HeaderText = "ID", ReadOnly = true, FillWeight = 10 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvRow.Name), HeaderText = "ITEM", ReadOnly = true, FillWeight = 38 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvRow.Category), HeaderText = "CATEGORY", ReadOnly = true, FillWeight = 30 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(InvRow.Quantity), HeaderText = "QTY", FillWeight = 22 });
        grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        grid.DataError += (_, e) => e.ThrowException = false;
        grid.CellEndEdit += InventoryCellEndEdit;
        grid.CellFormatting += (_, e) =>
        {
            if (grid.Columns[e.ColumnIndex].DataPropertyName == nameof(InvRow.Quantity))
                e.CellStyle!.ForeColor = Cc2Theme.Green;
        };
        _inventoryGrid = grid;

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 44, BackColor = Cc2Theme.Black, Padding = new Padding(8, 8, 0, 0), WrapContents = false };
        toolbar.Controls.Add(Style.Label("VALUE", Cc2Theme.MidGrey, Cc2Theme.PixelSmall));
        _bulkValue = new NumericUpDown { Maximum = 1_000_000_000, Minimum = 0, Value = 999, Width = 100 };
        Style.ApplyDark(_bulkValue);
        toolbar.Controls.Add(_bulkValue);
        var setSel = new FlatButton("SET SELECTED") { Accent = Cc2Theme.Cyan, Width = 120, Height = 26, Margin = new Padding(4, 1, 4, 1) };
        setSel.Click += (_, _) => BulkSet(true);
        toolbar.Controls.Add(setSel);
        var setAll = new FlatButton("SET ALL") { Accent = Cc2Theme.Green, Width = 90, Height = 26, Margin = new Padding(4, 1, 4, 1) };
        setAll.Click += (_, _) => BulkSet(false);
        toolbar.Controls.Add(setAll);

        toolbar.Controls.Add(Style.Label("   ADD BY ID", Cc2Theme.MidGrey, Cc2Theme.PixelSmall));
        _addItemId = new NumericUpDown { Maximum = 200, Minimum = 0, Width = 56 };
        Style.ApplyDark(_addItemId);
        toolbar.Controls.Add(_addItemId);
        _addItemQty = new NumericUpDown { Maximum = 1_000_000_000, Minimum = 0, Value = 100, Width = 100 };
        Style.ApplyDark(_addItemQty);
        toolbar.Controls.Add(_addItemQty);
        var addBtn = new FlatButton("APPLY") { Accent = Cc2Theme.Cyan, Width = 80, Height = 26, Margin = new Padding(4, 1, 4, 1) };
        addBtn.Click += (_, _) => AddOrSetById();
        toolbar.Controls.Add(addBtn);

        rightPanel.Controls.Add(grid);
        rightPanel.Controls.Add(toolbar);

        host.Controls.Add(rightPanel);
        host.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 12, BackColor = Cc2Theme.Screen });
        host.Controls.Add(listPanel);

        _inventoryGrid.DataSource = _rows;

        _refreshInventory = PopulateContainers;
        return host;
    }

    private void ContainerListDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        bool sel = (e.State & DrawItemState.Selected) != 0;
        var g = e.Graphics;
        using (var bg = new SolidBrush(sel ? Cc2Theme.ButtonHover : Cc2Theme.Black)) g.FillRectangle(bg, e.Bounds);
        if (sel) using (var bar = new SolidBrush(Cc2Theme.Cyan)) g.FillRectangle(bar, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);

        var item = _containerList.Items[e.Index] as InventoryContainer;
        string text = item?.Label ?? _containerList.Items[e.Index].ToString() ?? "";
        bool yours = text.StartsWith("★");
        Color col = sel ? Cc2Theme.Cyan : yours ? Cc2Theme.Cyan : Cc2Theme.White;
        string kind = item?.Kind == ContainerKind.IslandStock ? "WAREHOUSE" : "HOLD";
        Color kindCol = item?.Kind == ContainerKind.IslandStock ? Cc2Theme.Orange : Cc2Theme.CyanDim;

        var prev = g.TextRenderingHint;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        using (var b = new SolidBrush(kindCol)) g.DrawString(kind, Cc2Theme.PixelSmall, b, e.Bounds.X + 8, e.Bounds.Y + 5);
        string label = text.Replace("★ ", "");
        using (var b = new SolidBrush(col)) g.DrawString(Truncate(label, 40), Cc2Theme.Data, b, e.Bounds.X + 78, e.Bounds.Y + 5);
        g.TextRenderingHint = prev;
    }

    private static string Truncate(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 1) + "…";

    private void PopulateContainers()
    {
        _containerList.Items.Clear();
        _currentContainer = null;
        _rows = new BindingList<InvRow>();
        _inventoryGrid.DataSource = _rows;
        if (_save == null) return;
        foreach (var c in _save.Containers) _containerList.Items.Add(c);
        if (_containerList.Items.Count > 0) _containerList.SelectedIndex = 0;
    }

    private void ShowSelectedContainer()
    {
        _currentContainer = _containerList.SelectedItem as InventoryContainer;
        _rows = new BindingList<InvRow>();
        if (_currentContainer != null)
            foreach (var (itemId, qty) in _currentContainer.Entries)
                _rows.Add(new InvRow
                {
                    Id = itemId,
                    Name = ItemCatalog.NameOf(itemId),
                    Category = ItemCatalog.CategoryOf(itemId),
                    Quantity = qty,
                });
        _inventoryGrid.DataSource = _rows;
    }

    private void InventoryCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_currentContainer == null || e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
        if (_inventoryGrid.Columns[e.ColumnIndex].DataPropertyName != nameof(InvRow.Quantity)) return;
        var row = _rows[e.RowIndex];
        var raw = _inventoryGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString()?.Replace(",", "").Trim();
        if (!long.TryParse(raw, out var qty) || qty < 0)
        {
            _inventoryGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = row.Quantity;
            return;
        }
        row.Quantity = qty;
        if (_currentContainer.SetQuantity(row.Id, qty)) MarkDirty();
    }

    private void BulkSet(bool selectedOnly)
    {
        if (_currentContainer == null) return;
        long qty = (long)_bulkValue.Value;
        IEnumerable<InvRow> targets = selectedOnly
            ? _inventoryGrid.SelectedRows.Cast<DataGridViewRow>().Where(r => r.Index >= 0 && r.Index < _rows.Count).Select(r => _rows[r.Index])
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
        if (!_currentContainer.SetQuantity(id, qty))
        {
            Cc2MessageBox.Show(this, "COULD NOT SET ITEM",
                $"Item ID {id} could not be set on this container.\n\n" +
                "Vehicle holds use fixed positional slots (only existing IDs can be edited there). " +
                "Island/warehouse stock can add new IDs.", Cc2Theme.Yellow);
            return;
        }
        MarkDirty();
        ShowSelectedContainer();
        SetStatus($"Set item {id} ({ItemCatalog.NameOf(id)}) to {qty:N0}.");
    }
}
