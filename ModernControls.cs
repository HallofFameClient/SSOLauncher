using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SSOLauncher;

/// Segoe MDL2 Assets code points, built from numbers so the source file stays pure ASCII.
internal static class Glyphs
{
    private static string G(int cp) => ((char)cp).ToString();

    public static readonly string Discover = G(0xE80F);    // home
    public static readonly string News = G(0xE7C3);        // page
    public static readonly string Shop = G(0xE7BF);        // shopping cart
    public static readonly string Account = G(0xE77B);     // contact
    public static readonly string Settings = G(0xE713);    // gear
    public static readonly string Play = G(0xE768);
    public static readonly string ChevronDown = G(0xE70D);
    public static readonly string CheckMark = G(0xE73E);
    public static readonly string Eye = G(0xE7B3);
    public static readonly string Shield = G(0xEA18);
    public static readonly string Horse = "♞";        // chess knight - MDL2 has no horse
    public static readonly string Delete = G(0xE74D);
    public static readonly string Folder = G(0xE838);
    public static readonly string Mail = G(0xE715);
    public static readonly string Lock = G(0xE72E);
    public static readonly string Globe = G(0xE774);
    public static readonly string Coin = G(0xE735);        // filled star
    public static readonly string Minimize = G(0xE921);
    public static readonly string Maximize = G(0xE922);
    public static readonly string Restore = G(0xE923);
    public static readonly string Close = G(0xE8BB);
}

/// GDI+ drawing helpers shared by the custom controls below.
internal static class Draw
{
    public const string IconFont = "Segoe MDL2 Assets";

    public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
                                        | TextFormatFlags.SingleLine;
    public const TextFormatFlags Mid = TextFormatFlags.VerticalCenter | TextFormatFlags.Left
                                     | TextFormatFlags.SingleLine;

    public static GraphicsPath Round(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        if (d <= 0) { p.AddRectangle(r); return p; }
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    public static void Fill(Graphics g, Rectangle r, int radius, Color c)
    {
        if (r.Width <= 0 || r.Height <= 0) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var p = Round(r, radius);
        using var b = new SolidBrush(c);
        g.FillPath(b, p);
    }

    public static void Stroke(Graphics g, Rectangle r, int radius, Color c, float width = 1f)
    {
        if (r.Width <= 1 || r.Height <= 1) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var p = Round(new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), radius);
        using var pen = new Pen(c, width);
        g.DrawPath(pen, p);
    }

    /// Soft drop shadow painted in the margin around <paramref name="face"/>.
    public static void Shadow(Graphics g, Rectangle face, int radius, int spread)
    {
        if (face.Width <= 0 || face.Height <= 0) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var b = new SolidBrush(Color.FromArgb(ThemeColors.ShadowStrength, ThemeColors.Shadow));
        for (int i = spread; i >= 1; i--)
        {
            var r = new Rectangle(face.X - i, face.Y - i + 2, face.Width + i * 2, face.Height + i * 2);
            using var p = Round(r, radius + i);
            g.FillPath(b, p);
        }
    }

    public static void Text(Graphics g, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        => TextRenderer.DrawText(g, text, font, bounds, color, flags);

    /// The colour actually visible behind a control, so custom painters can fake transparency.
    /// Children of a <see cref="Card"/> sit on its white face, not on the card's own ground.
    public static Color Ground(Control c)
    {
        var parent = c.Parent;
        if (parent is Card) return ThemeColors.Card;
        if (parent == null || parent.BackColor.A == 0) return ThemeColors.Background;
        return parent.BackColor;
    }
}

/// Cached fonts - created once, live for the app's lifetime.
internal static class Ui
{
    public static readonly Font H1 = new("Segoe UI", 19F, FontStyle.Bold);
    public static readonly Font H2 = new("Segoe UI", 13F, FontStyle.Bold);
    public static readonly Font H3 = new("Segoe UI", 11F, FontStyle.Bold);
    public static readonly Font Body = new("Segoe UI", 10F);
    public static readonly Font BodyBold = new("Segoe UI", 10F, FontStyle.Bold);
    public static readonly Font Small = new("Segoe UI", 8.75F);
    public static readonly Font Caps = new("Segoe UI", 8.25F, FontStyle.Bold);
    public static readonly Font Nav = new("Segoe UI", 11F);
    public static readonly Font Play = new("Segoe UI", 14F, FontStyle.Bold);
    public static readonly Font Icon = new(Draw.IconFont, 13F);
    public static readonly Font IconSmall = new(Draw.IconFont, 9F);
    public static readonly Font IconTiny = new(Draw.IconFont, 7F);
    public static readonly Font Symbol = new("Segoe UI Symbol", 11F, FontStyle.Bold);

    public static Label Label(string text, Font font, Color color, ContentAlignment align = ContentAlignment.TopLeft)
        => new()
        {
            Text = text,
            Font = font,
            ForeColor = color,
            BackColor = Color.Transparent,
            TextAlign = align,
            AutoSize = false,
            UseMnemonic = false,
        };
}

/// Panel that never flickers - used for the page containers.
internal class BufferedPanel : Panel
{
    public BufferedPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
    }

    /// The panels cover the whole window, so the form would never see a hit test near its
    /// edges. Reporting HTTRANSPARENT there hands those pixels back to the form's resize grips.
    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x0084;
        const int grip = 6;
        if (m.Msg == WM_NCHITTEST)
        {
            var form = FindForm();
            if (form is { WindowState: FormWindowState.Normal })
            {
                int sx = unchecked((short)(long)m.LParam);
                int sy = unchecked((short)((long)m.LParam >> 16));
                var p = form.PointToClient(new Point(sx, sy));
                if (p.X <= grip || p.Y <= grip
                    || p.X >= form.ClientSize.Width - grip || p.Y >= form.ClientSize.Height - grip)
                {
                    m.Result = -1; // HTTRANSPARENT
                    return;
                }
            }
        }
        base.WndProc(ref m);
    }
}

/// Rounded, softly shadowed card. The control is <see cref="Inset"/> px larger than its
/// visible face on every side so the shadow has room; position children against <see cref="Face"/>.
internal sealed class Card : BufferedPanel
{
    public const int Inset = 10;

    public int Radius { get; set; } = 18;
    public Color Ground { get; set; } = Color.Empty;   // defaults to the theme background
    public bool Hoverable { get; set; }

    private bool _hot;

    public Card()
    {
        BackColor = ThemeColors.Background;
    }

    public Rectangle Face => new(Inset, Inset, Width - Inset * 2, Height - Inset * 2);

    public void SetFace(int x, int y, int w, int h)
        => SetBounds(x - Inset, y - Inset, w + Inset * 2, h + Inset * 2);

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        if (Hoverable) { _hot = true; Invalidate(); }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (Hoverable) { _hot = false; Invalidate(); }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Ground.IsEmpty ? ThemeColors.Background : Ground);
        var face = Face;
        if (face.Width <= 0 || face.Height <= 0) return;
        Draw.Shadow(g, face, Radius, _hot ? 8 : 4);
        Draw.Fill(g, face, Radius, ThemeColors.Card);
        Draw.Stroke(g, face, Radius, _hot ? ThemeColors.Primary : ThemeColors.Line);
        base.OnPaint(e);
    }
}

/// Rounded button used for every action in the launcher.
internal sealed class PillButton : Control, IButtonControl
{
    public enum Kind { Primary, Ghost, Quiet }

    private bool _hot, _down;

    public Kind Style { get; set; } = Kind.Primary;
    public int Radius { get; set; } = -1;          // -1 = full pill
    public string Glyph { get; set; } = "";
    public Font GlyphFont { get; set; } = Ui.Icon;

    public PillButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Color.Transparent;
        Font = Ui.BodyBold;
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hot = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); Focus(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { _hot = _down = false; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Space or Keys.Enter || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter) PerformClick();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Draw.Ground(this));
        var r = new Rectangle(0, 0, Width, Height);
        int radius = Radius < 0 ? Height / 2 : Radius;

        Color fill, text, border = Color.Empty;
        if (Style == Kind.Primary)
        {
            fill = !Enabled ? ThemeColors.Line
                 : _down ? ThemeColors.PrimaryPressed
                 : _hot ? ThemeColors.PrimaryHover
                 : ThemeColors.Primary;
            text = Enabled ? ThemeColors.OnPrimary : ThemeColors.Faint;
            if (Enabled)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using var glow = new SolidBrush(Color.FromArgb(_hot ? 18 : 10, ThemeColors.Primary));
                for (int i = 6; i >= 1; i--)
                {
                    var gr = new Rectangle(r.X - i, r.Y - i + 2, r.Width + i * 2, r.Height + i * 2);
                    using var gp = Draw.Round(gr, radius + i);
                    g.FillPath(glow, gp);
                }
            }
        }
        else if (Style == Kind.Ghost)
        {
            fill = _down ? ThemeColors.Highlight : _hot ? ThemeColors.HighlightSoft : ThemeColors.Card;
            text = Enabled ? ThemeColors.Ink : ThemeColors.Faint;
            border = _hot ? ThemeColors.Primary : ThemeColors.Line;
        }
        else
        {
            fill = _hot ? ThemeColors.HighlightSoft : Color.Transparent;
            text = Enabled ? ThemeColors.Muted : ThemeColors.Faint;
        }

        if (fill != Color.Transparent) Draw.Fill(g, r, radius, fill);
        if (!border.IsEmpty) Draw.Stroke(g, r, radius, border);
        if (Focused && Enabled) Draw.Stroke(g, Rectangle.Inflate(r, -3, -3), Math.Max(2, radius - 3), ThemeColors.Primary, 1.4f);

        var flags = Draw.Center | TextFormatFlags.EndEllipsis;
        if (Glyph.Length == 0) { Draw.Text(g, Text, Font, r, text, flags); return; }
        if (Text.Length == 0) { Draw.Text(g, Glyph, GlyphFont, r, text, flags); return; }

        Size glyphSize = TextRenderer.MeasureText(g, Glyph, GlyphFont, Size, TextFormatFlags.SingleLine);
        Size textSize = TextRenderer.MeasureText(g, Text, Font, Size, TextFormatFlags.SingleLine);
        const int gap = 10;
        int x = r.X + (r.Width - (glyphSize.Width + gap + textSize.Width)) / 2;
        Draw.Text(g, Glyph, GlyphFont, new Rectangle(x, r.Y, glyphSize.Width, r.Height), text, Draw.Center);
        Draw.Text(g, Text, Font, new Rectangle(x + glyphSize.Width + gap, r.Y, textSize.Width, r.Height), text, Draw.Center);
    }

    // IButtonControl - lets the form use this as AcceptButton.
    public DialogResult DialogResult { get; set; }
    public void NotifyDefault(bool value) { }
    public void PerformClick() { if (Enabled) OnClick(EventArgs.Empty); }
}

/// Sidebar entry: icon + label, with a rounded highlight when selected.
internal sealed class NavItem : Control
{
    private bool _hot;
    private bool _selected;

    public string Glyph { get; set; } = "";

    public bool Selected
    {
        get => _selected;
        set { if (_selected != value) { _selected = value; Invalidate(); } }
    }

    public NavItem()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Font = Ui.Nav;
        Cursor = Cursors.Hand;
        TabStop = false;
    }

    protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hot = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Draw.Ground(this));
        var r = new Rectangle(0, 0, Width, Height);

        if (_selected) Draw.Fill(g, r, Height / 2, ThemeColors.Highlight);
        else if (_hot) Draw.Fill(g, r, Height / 2, ThemeColors.HighlightSoft);

        Color fg = _selected ? ThemeColors.Ink : ThemeColors.Muted;
        Draw.Text(g, Glyph, Ui.Icon, new Rectangle(r.X + 18, r.Y, 26, r.Height),
            _selected ? ThemeColors.Primary : fg, Draw.Mid);
        Draw.Text(g, Text, _selected ? Ui.BodyBold : Font, new Rectangle(r.X + 52, r.Y, r.Width - 62, r.Height),
            fg, Draw.Mid | TextFormatFlags.EndEllipsis);
    }
}

/// Rounded frame drawn behind a TextBox/ComboBox so the plain Win32 control looks modern.
internal sealed class FieldHost : BufferedPanel
{
    private bool _focused;

    public Control? Field { get; private set; }
    public string Glyph { get; set; } = "";
    public int LeftPad { get; set; } = 14;

    public FieldHost()
    {
        BackColor = Color.Transparent;
    }

    /// Puts <paramref name="field"/> inside the frame and keeps it vertically centred.
    public void Host(Control field)
    {
        Field = field;
        field.BackColor = ThemeColors.Card;
        field.ForeColor = ThemeColors.Ink;
        field.GotFocus += (_, _) => { _focused = true; Invalidate(); };
        field.LostFocus += (_, _) => { _focused = false; Invalidate(); };
        Controls.Add(field);
        LayoutField();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutField();
    }

    private void LayoutField()
    {
        if (Field == null || Width <= 0) return;
        int pad = LeftPad + (Glyph.Length > 0 ? 24 : 0);
        int h = Math.Min(Field.Height, Math.Max(10, Height - 8));
        Field.SetBounds(pad, (Height - h) / 2, Math.Max(10, Width - pad - 12), h);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Draw.Ground(this));
        var r = new Rectangle(0, 0, Width, Height);
        int radius = Math.Min(14, Height / 2);
        Draw.Fill(g, r, radius, ThemeColors.Card);
        Draw.Stroke(g, r, radius, _focused ? ThemeColors.Primary : ThemeColors.Line, _focused ? 1.6f : 1f);
        if (Glyph.Length > 0)
            Draw.Text(g, Glyph, Ui.IconSmall, new Rectangle(LeftPad, 0, 20, Height), ThemeColors.Faint, Draw.Center);
        base.OnPaint(e);
    }
}

/// ComboBox without the Win32 border, with a themed drop-down list and chevron.
internal sealed class FlatCombo : ComboBox
{
    public FlatCombo()
    {
        FlatStyle = FlatStyle.Flat;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 26;
        Font = Ui.Body;
        BackColor = ThemeColors.Card;
        ForeColor = ThemeColors.Ink;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= Items.Count) return;
        bool selected = (e.State & DrawItemState.Selected) != 0;
        using (var b = new SolidBrush(selected ? ThemeColors.Highlight : ThemeColors.Card))
            e.Graphics.FillRectangle(b, e.Bounds);
        Draw.Text(e.Graphics, Items[e.Index]?.ToString() ?? "", Font,
            new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 14, e.Bounds.Height),
            ThemeColors.Ink, Draw.Mid);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        const int WM_PAINT = 0x000F;
        if (m.Msg != WM_PAINT || IsDisposed || !IsHandleCreated) return;
        using var g = Graphics.FromHwnd(Handle);
        using var b = new SolidBrush(BackColor);
        // The flat ComboBox paints its own border a few pixels inside the control - and
        // picks a light system colour, which is glaring on the dark theme. Paint the whole
        // rim over rather than tracing it, so the FieldHost frame is the only border shown.
        const int rim = 3;
        g.FillRectangle(b, 0, 0, Width, rim);
        g.FillRectangle(b, 0, Height - rim, Width, rim);
        g.FillRectangle(b, 0, 0, rim, Height);
        g.FillRectangle(b, Width - rim, 0, rim, Height);
        var button = new Rectangle(Width - 24, 0, 24, Height);
        g.FillRectangle(b, button);
        Draw.Text(g, Glyphs.ChevronDown, Ui.IconTiny, button, ThemeColors.Muted, Draw.Center);
    }
}

/// CheckBox drawn as a rounded, brand-coloured tick box.
internal sealed class ModernCheck : CheckBox
{
    private bool _hot;

    public ModernCheck()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        AutoSize = false;
        BackColor = Color.Transparent;
        Font = Ui.Small;
        ForeColor = ThemeColors.Muted;
        Cursor = Cursors.Hand;
        Height = 26;
    }

    protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hot = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Draw.Ground(this));
        const int box = 19;
        var r = new Rectangle(0, (Height - box) / 2, box, box);
        if (Checked)
        {
            Draw.Fill(g, r, 6, Enabled ? ThemeColors.Primary : ThemeColors.Faint);
            Draw.Text(g, Glyphs.CheckMark, Ui.IconTiny, r, ThemeColors.OnPrimary, Draw.Center);
        }
        else
        {
            Draw.Fill(g, r, 6, _hot ? ThemeColors.HighlightSoft : ThemeColors.Card);
            Draw.Stroke(g, r, 6, _hot ? ThemeColors.Primary : ThemeColors.Line, 1.4f);
        }
        Draw.Text(g, Text, Font, new Rectangle(box + 10, 0, Width - box - 10, Height),
            Enabled ? ForeColor : ThemeColors.Faint, Draw.Mid | TextFormatFlags.EndEllipsis);
    }
}

/// Slim indeterminate progress bar - replaces the Win32 green ProgressBar.
internal sealed class Marquee : Control
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 16 };
    private float _phase;

    public Marquee()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Height = 6;
        _timer.Tick += (_, _) =>
        {
            _phase += 0.010f;
            if (_phase > 1f) _phase -= 1f;
            Invalidate();
        };
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        _timer.Enabled = Visible;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Draw.Ground(this));
        var track = new Rectangle(0, 0, Width, Height);
        Draw.Fill(g, track, Height / 2, ThemeColors.Highlight);
        int w = Math.Max(40, Width / 3);
        int x = (int)(_phase * (Width + w)) - w;
        var lit = Rectangle.Intersect(track, new Rectangle(x, 0, w, Height));
        if (lit.Width > 0) Draw.Fill(g, lit, Height / 2, ThemeColors.Primary);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}

/// Icon + value pair used for the sidebar's rider badge, star coins and horse count.
/// The glyph either sits in a filled circle (counters) or is drawn plain (badge).
internal sealed class StatChip : Control
{
    public string Glyph { get; set; } = "";
    public Font GlyphFont { get; set; } = Ui.IconSmall;
    public Color Accent { get; set; } = ThemeColors.Primary;
    public bool Bubble { get; set; } = true;

    public StatChip()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Ui.BodyBold;
        ForeColor = ThemeColors.Ink;
    }

    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnForeColorChanged(EventArgs e) { Invalidate(); base.OnForeColorChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Draw.Ground(this));
        if (Width <= 0 || Height <= 0) return;

        int diameter = Math.Min(Height, 26);
        bool hasGlyph = Glyph.Length > 0;
        int glyphWidth = !hasGlyph ? 0
            : Bubble ? diameter
            : TextRenderer.MeasureText(g, Glyph, GlyphFont, Size, TextFormatFlags.SingleLine).Width;
        Size textSize = TextRenderer.MeasureText(g, Text, Font, Size, TextFormatFlags.SingleLine);
        int gap = hasGlyph && Text.Length > 0 ? 8 : 0;

        int x = Math.Max(0, (Width - (glyphWidth + gap + textSize.Width)) / 2);
        if (hasGlyph)
        {
            var box = new Rectangle(x, (Height - diameter) / 2, glyphWidth, diameter);
            if (Bubble) Draw.Fill(g, box, diameter / 2, Accent);
            Draw.Text(g, Glyph, GlyphFont, box, Bubble ? ThemeColors.OnPrimary : Accent, Draw.Center);
        }
        Draw.Text(g, Text, Font, new Rectangle(x + glyphWidth + gap, 0, textSize.Width + 6, Height),
            ForeColor, Draw.Mid);
    }
}

/// Minimise / maximise / close button for the custom title bar.
internal sealed class CaptionButton : Control
{
    private bool _hot;

    public string Glyph { get; set; } = "";
    public bool Danger { get; set; }

    public CaptionButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
        Size = new Size(44, 30);
        Cursor = Cursors.Hand;
        TabStop = false;
    }

    protected override void OnMouseEnter(EventArgs e) { _hot = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hot = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Draw.Ground(this));
        var r = new Rectangle(0, 0, Width, Height);
        Color fg = ThemeColors.Muted;
        if (_hot)
        {
            Draw.Fill(g, r, 8, Danger ? ThemeColors.Error : ThemeColors.Highlight);
            fg = Danger ? Color.White : ThemeColors.Ink;
        }
        Draw.Text(g, Glyph, Ui.IconTiny, r, fg, Draw.Center);
    }
}
