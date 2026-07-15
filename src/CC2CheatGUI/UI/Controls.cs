using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace CC2CheatGUI.UI;

// ---------------------------------------------------------------------------
// ConsolePanel — flat black panel with a 1px hard border and an optional
// inverted title bar. The panel IS the core CC2 motif.
// ---------------------------------------------------------------------------
public sealed class ConsolePanel : Panel
{
    private string _title = "";
    public Color BorderColor { get; set; } = Cc2Theme.PanelBorder;
    public Color TitleFill { get; set; } = Cc2Theme.PureWhite;
    public int TitleHeight { get; set; } = 22;

    public string Title
    {
        get => _title;
        set { _title = value; UpdatePadding(); Invalidate(); }
    }

    public ConsolePanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Cc2Theme.Black;
        UpdatePadding();
    }

    private void UpdatePadding()
    {
        int top = string.IsNullOrEmpty(_title) ? 1 : TitleHeight + 1;
        Padding = new Padding(1, top, 1, 1);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var r = ClientRectangle;
        using (var b = new SolidBrush(Cc2Theme.Black)) g.FillRectangle(b, r);

        if (!string.IsNullOrEmpty(_title))
        {
            var bar = new Rectangle(0, 0, r.Width, TitleHeight);
            Cc2Theme.DrawInvertedHeader(g, bar, _title, Cc2Theme.PixelBody, TitleFill);
        }

        using var pen = new Pen(BorderColor);
        g.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
    }
}

// ---------------------------------------------------------------------------
// SectionHeader — a full-width dark bar with a label (for grouping inside a panel).
// ---------------------------------------------------------------------------
public sealed class SectionHeader : Control
{
    public Color Accent { get; set; } = Cc2Theme.Cyan;

    public SectionHeader(string text)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Text = text;
        Height = 20;
        Dock = DockStyle.Top;
        BackColor = Cc2Theme.Black;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using (var b = new SolidBrush(Cc2Theme.PanelDark))
            g.FillRectangle(b, 0, 0, Width, Height);
        using (var b = new SolidBrush(Accent))
            g.FillRectangle(b, 0, 0, 3, Height);
        Cc2Theme.DrawPixelText(g, Text, Cc2Theme.PixelSmall, Accent, 10, (Height - 15) / 2);
    }
}

// ---------------------------------------------------------------------------
// FlatButton — teal-tinted flat button, hover→highlight, semantic accent text.
// ---------------------------------------------------------------------------
public sealed class FlatButton : Control
{
    public Color Accent { get; set; } = Cc2Theme.White;
    public bool Primary { get; set; }
    private bool _hover, _down;

    public FlatButton(string text)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Text = text;
        Height = 30;
        Cursor = Cursors.Hand;
        BackColor = Cc2Theme.Black;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);

        Color fill = !Enabled ? Cc2Theme.Screen
                   : _down ? Cc2Theme.ButtonDown
                   : _hover ? Cc2Theme.ButtonHover
                   : Primary ? Cc2Theme.ButtonBg : Cc2Theme.ButtonBg;
        Color border = !Enabled ? Cc2Theme.Grid
                     : (_hover || _down) ? Accent
                     : Primary ? Cc2Theme.CyanDim : Cc2Theme.PanelBorder;
        Color text = !Enabled ? Cc2Theme.DimGrey : Accent;

        using (var b = new SolidBrush(fill)) g.FillRectangle(b, 0, 0, Width, Height);
        using (var pen = new Pen(border)) g.DrawRectangle(pen, r);

        var prev = g.TextRenderingHint;
        g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        var sz = g.MeasureString(Text, Cc2Theme.PixelBody);
        int tx = (Width - (int)sz.Width) / 2;
        int ty = (Height - (int)sz.Height) / 2;
        if (_down) { tx += 1; ty += 1; }
        using (var tb = new SolidBrush(text)) g.DrawString(Text, Cc2Theme.PixelBody, tb, tx, ty);
        g.TextRenderingHint = prev;
    }
}

// ---------------------------------------------------------------------------
// NavButton — left-rail navigation item with a selected state.
// ---------------------------------------------------------------------------
public sealed class NavButton : Control
{
    private bool _hover;
    public bool Selected { get; set; }
    public string Glyph { get; set; } = "";

    public NavButton(string text, string glyph)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Text = text;
        Glyph = glyph;
        Height = 38;
        Cursor = Cursors.Hand;
        BackColor = Cc2Theme.Screen;
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Color bg = Selected ? Cc2Theme.ButtonHover : _hover ? Cc2Theme.Grid : Cc2Theme.Screen;
        using (var b = new SolidBrush(bg)) g.FillRectangle(b, 0, 0, Width, Height);
        if (Selected)
            using (var b = new SolidBrush(Cc2Theme.Cyan)) g.FillRectangle(b, 0, 0, 3, Height);

        Color fg = Selected ? Cc2Theme.Cyan : _hover ? Cc2Theme.White : Cc2Theme.MidGrey;
        Cc2Theme.DrawPixelText(g, Glyph, Cc2Theme.PixelHead, fg, 14, (Height - 20) / 2);
        Cc2Theme.DrawPixelText(g, Text, Cc2Theme.Nav, fg, 40, (Height - 16) / 2);
    }
}

// ---------------------------------------------------------------------------
// StatTile — a small readout (label + big value) for the overview dashboard.
// ---------------------------------------------------------------------------
public sealed class StatTile : Control
{
    private string _label = "", _value = "—";
    public Color Accent { get; set; } = Cc2Theme.Cyan;
    public string Label { get => _label; set { _label = value; Invalidate(); } }
    public string Value { get => _value; set { _value = value; Invalidate(); } }

    public StatTile()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Cc2Theme.Black;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        using (var b = new SolidBrush(Cc2Theme.Black)) g.FillRectangle(b, ClientRectangle);
        using (var pen = new Pen(Cc2Theme.PanelBorder)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        using (var b = new SolidBrush(Accent)) g.FillRectangle(b, 0, 0, Width, 3);
        Cc2Theme.DrawPixelText(g, _label.ToUpperInvariant(), Cc2Theme.PixelSmall, Cc2Theme.MidGrey, 10, 12);
        Cc2Theme.DrawPixelText(g, _value, Cc2Theme.PixelTitle, Accent, 10, 30, shadow: true);
    }
}

// ---------------------------------------------------------------------------
// Styling helpers for framework controls.
// ---------------------------------------------------------------------------
public static class Style
{
    public static DataGridView Grid()
    {
        var grid = new DataGridView
        {
            BackgroundColor = Cc2Theme.Black,
            BorderStyle = BorderStyle.None,
            EnableHeadersVisualStyles = false,
            GridColor = Cc2Theme.Grid,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 26,
            RowTemplate = { Height = 22 },
            Font = Cc2Theme.Data,
        };
        grid.DefaultCellStyle.BackColor = Cc2Theme.Black;
        grid.DefaultCellStyle.ForeColor = Cc2Theme.White;
        grid.DefaultCellStyle.SelectionBackColor = Cc2Theme.ButtonHover;
        grid.DefaultCellStyle.SelectionForeColor = Cc2Theme.Cyan;
        grid.DefaultCellStyle.Font = Cc2Theme.Data;
        grid.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Cc2Theme.PureWhite;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Cc2Theme.Black;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Cc2Theme.PureWhite;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Cc2Theme.Black;
        grid.ColumnHeadersDefaultCellStyle.Font = Cc2Theme.DataBold;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Cc2Theme.Screen;
        grid.AlternatingRowsDefaultCellStyle.ForeColor = Cc2Theme.White;
        grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Cc2Theme.ButtonHover;
        grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = Cc2Theme.Cyan;
        return grid;
    }

    public static void ApplyDark(ComboBox combo)
    {
        combo.FlatStyle = FlatStyle.Flat;
        combo.BackColor = Cc2Theme.ButtonBg;
        combo.ForeColor = Cc2Theme.Cyan;
        combo.Font = Cc2Theme.Data;
        combo.DrawMode = DrawMode.OwnerDrawFixed;
        combo.DropDownStyle = ComboBoxStyle.DropDownList;
        combo.DrawItem += (s, e) =>
        {
            e.DrawBackground();
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var bg = new SolidBrush(sel ? Cc2Theme.ButtonHover : Cc2Theme.ButtonBg))
                e.Graphics.FillRectangle(bg, e.Bounds);
            if (e.Index >= 0)
            {
                var text = combo.GetItemText(combo.Items[e.Index]);
                TextRenderer.DrawText(e.Graphics, text, Cc2Theme.Data, e.Bounds,
                    sel ? Cc2Theme.Cyan : Cc2Theme.White, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        };
    }

    public static void ApplyDark(NumericUpDown num)
    {
        num.BorderStyle = BorderStyle.FixedSingle;
        num.BackColor = Cc2Theme.ButtonBg;
        num.ForeColor = Cc2Theme.Cyan;
        num.Font = Cc2Theme.Data;
    }

    public static void ApplyDark(TextBox box)
    {
        box.BorderStyle = BorderStyle.FixedSingle;
        box.BackColor = Cc2Theme.ButtonBg;
        box.ForeColor = Cc2Theme.Cyan;
        box.Font = Cc2Theme.Data;
    }

    public static Label Label(string text, Color color, Font? font = null) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = color,
        BackColor = Color.Transparent,
        Font = font ?? Cc2Theme.PixelBody,
    };
}
