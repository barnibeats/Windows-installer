// Custom-drawn controls (pill buttons, cards, tables). They read colors from Theme at paint time,
// so switching the theme only needs an Invalidate.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// A page: transparent-friendly background with the soft colored glows.
class Page : Panel
{
    public Page()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnPaintBackground(PaintEventArgs e) { Theme.PaintBackdrop(e.Graphics, ClientRectangle); }
}

// A rounded "card" with an optional numbered badge and title.
class Card : Panel
{
    public string Step = "";
    string title = "";
    public string Title { get { return title; } set { title = value; Invalidate(); } }

    public Card()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Quality(g);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = Theme.Round(r, 14))
        {
            using (SolidBrush b = new SolidBrush(Theme.Card)) g.FillPath(b, p);
            using (Pen pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
        }
        int x = 16;
        if (Step.Length > 0)
        {
            Rectangle badge = new Rectangle(x, 12, 24, 24);
            using (GraphicsPath bp = Theme.Round(badge, 7))
            using (LinearGradientBrush gb = Theme.Grad(badge, Theme.A1, Theme.A2, 45f)) g.FillPath(gb, bp);
            TextRenderer.DrawText(g, Step, Theme.Bold, badge, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            x += 34;
        }
        if (title.Length > 0)
            TextRenderer.DrawText(g, title, Theme.Title, new Rectangle(x, 10, Width - x - 12, 28), Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}

enum BtnKind { Primary, Danger, Ghost }

class PillButton : Control
{
    public BtnKind Kind = BtnKind.Primary;
    bool hover, down;
    public int MaxRadius = 40;

    public PillButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor | ControlStyles.StandardClick, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Height = 34;
        Font = Theme.Bold;
        BackColor = Color.Transparent;
    }

    public void PerformClick() { if (Enabled) OnClick(EventArgs.Empty); }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Focus(); Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override bool IsInputKey(Keys k) { return k == Keys.Enter || k == Keys.Space || base.IsInputKey(k); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { PerformClick(); e.Handled = true; }
        base.OnKeyDown(e);
    }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Quality(g);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = Math.Min(Height / 2, MaxRadius);
        Color textColor = Color.White;
        using (GraphicsPath p = Theme.Round(r, radius))
        {
            if (!Enabled)
            {
                using (SolidBrush b = new SolidBrush(Theme.Mix(Theme.Card, Theme.Bg, 0.3f))) g.FillPath(b, p);
                using (Pen pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
                textColor = Theme.Muted;
            }
            else if (Kind == BtnKind.Ghost)
            {
                Color fill = down ? Theme.Mix(Theme.Card, Theme.A1, 0.25f) : (hover ? Theme.CardHover : Theme.Card);
                using (SolidBrush b = new SolidBrush(fill)) g.FillPath(b, p);
                using (Pen pen = new Pen(hover || Focused ? Theme.A2 : Theme.Border)) g.DrawPath(pen, p);
                textColor = Theme.Text;
            }
            else
            {
                Color c1 = Kind == BtnKind.Danger ? Theme.Mix(Color.FromArgb(255, 80, 105), Theme.A3, 0.15f) : Theme.A1;
                Color c2 = Kind == BtnKind.Danger ? Theme.A3 : Theme.A2;
                if (hover) { c1 = Theme.Mix(c1, Color.White, 0.14f); c2 = Theme.Mix(c2, Color.White, 0.14f); }
                if (down) { c1 = Theme.Mix(c1, Color.Black, 0.15f); c2 = Theme.Mix(c2, Color.Black, 0.15f); }
                using (LinearGradientBrush gb = Theme.Grad(r, c1, c2, 20f)) g.FillPath(gb, p);
                if (Focused) using (Pen pen = new Pen(Color.White, 1.5f)) g.DrawPath(pen, p);
            }
        }
        TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}

// Pill-shaped tab strip.
class SegTabs : Control
{
    List<string> items = new List<string>();
    int selected;
    int hoverIdx = -1;
    public event Action SelectedChanged;

    public SegTabs()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 38;
        Font = Theme.Bold;
        Cursor = Cursors.Hand;
    }

    public int SelectedIndex
    {
        get { return selected; }
        set { if (value == selected) return; selected = value; Invalidate(); Action a = SelectedChanged; if (a != null) a(); }
    }

    public void SetItems(string[] texts) { items = new List<string>(texts); Invalidate(); }

    Rectangle[] LayoutItems()
    {
        List<Rectangle> res = new List<Rectangle>();
        int x = 0;
        foreach (string s in items)
        {
            int w = TextRenderer.MeasureText(s, Font).Width + 34;
            res.Add(new Rectangle(x, 2, w, Height - 5));
            x += w + 6;
        }
        return res.ToArray();
    }

    public int PreferredWidth
    {
        get { Rectangle[] r = LayoutItems(); return r.Length == 0 ? 0 : r[r.Length - 1].Right + 1; }
    }

    int HitTest(Point p)
    {
        Rectangle[] rs = LayoutItems();
        for (int i = 0; i < rs.Length; i++) if (rs[i].Contains(p)) return i;
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e) { int h = HitTest(e.Location); if (h != hoverIdx) { hoverIdx = h; Invalidate(); } base.OnMouseMove(e); }
    protected override void OnMouseLeave(EventArgs e) { hoverIdx = -1; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { int h = HitTest(e.Location); if (h >= 0) SelectedIndex = h; base.OnMouseDown(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Quality(g);
        Rectangle[] rs = LayoutItems();
        for (int i = 0; i < rs.Length; i++)
        {
            Rectangle r = new Rectangle(rs[i].X, rs[i].Y, rs[i].Width - 1, rs[i].Height - 1);
            using (GraphicsPath p = Theme.Round(r, r.Height / 2))
            {
                Color text;
                if (i == selected)
                {
                    using (LinearGradientBrush gb = Theme.Grad(r, Theme.A1, Theme.A2, 20f)) g.FillPath(gb, p);
                    text = Color.White;
                }
                else
                {
                    using (SolidBrush b = new SolidBrush(i == hoverIdx ? Theme.CardHover : Theme.Card)) g.FillPath(b, p);
                    using (Pen pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
                    text = i == hoverIdx ? Theme.Text : Theme.Muted;
                }
                TextRenderer.DrawText(g, items[i], Font, rs[i], text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }
    }
}

// Checkbox / radio button.
class CheckPill : Control
{
    bool isChecked;
    public bool Radio;
    public string Group = "";
    bool hover;
    public event Action CheckedChanged;

    public CheckPill()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        TabStop = true;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        Height = 24;
        Font = Theme.Body;
    }

    public bool Checked
    {
        get { return isChecked; }
        set
        {
            if (value == isChecked) return;
            if (Radio && !value) { return; }   // a radio button is cleared by its siblings
            isChecked = value;
            if (Radio && value && Parent != null)
                foreach (Control c in Parent.Controls)
                {
                    CheckPill o = c as CheckPill;
                    if (o != null && o != this && o.Radio && o.Group == Group && o.isChecked) { o.isChecked = false; o.Invalidate(); Action oa = o.CheckedChanged; if (oa != null) oa(); }
                }
            Invalidate();
            Action a = CheckedChanged; if (a != null) a();
        }
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnClick(EventArgs e) { if (Enabled) { if (Radio) Checked = true; else Checked = !isChecked; } base.OnClick(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override bool IsInputKey(Keys k) { return k == Keys.Space || base.IsInputKey(k); }
    protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Quality(g);
        Rectangle box = new Rectangle(0, (Height - 18) / 2, 17, 17);
        Color border = !Enabled ? Theme.Border : (isChecked ? Theme.A2 : (hover || Focused ? Theme.A2 : Theme.Muted));
        using (GraphicsPath p = Radio ? EllipsePath(box) : Theme.Round(box, 5))
        {
            if (isChecked && Enabled)
                using (LinearGradientBrush gb = Theme.Grad(box, Theme.A1, Theme.A2, 45f)) g.FillPath(gb, p);
            else
                using (SolidBrush b = new SolidBrush(Theme.Field)) g.FillPath(b, p);
            using (Pen pen = new Pen(border, 1.3f)) g.DrawPath(pen, p);
        }
        if (isChecked)
        {
            Color mark = Enabled ? Color.White : Theme.Muted;
            if (Radio) using (SolidBrush b = new SolidBrush(mark)) g.FillEllipse(b, box.X + 5, box.Y + 5, 7, 7);
            else using (Pen pen = new Pen(mark, 2f))
                {
                    pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, new Point[] { new Point(box.X + 4, box.Y + 9), new Point(box.X + 7, box.Y + 12), new Point(box.X + 13, box.Y + 5) });
                }
        }
        TextRenderer.DrawText(g, Text, Font, new Rectangle(25, 0, Width - 25, Height), Enabled ? Theme.Text : Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    static GraphicsPath EllipsePath(Rectangle r) { GraphicsPath p = new GraphicsPath(); p.AddEllipse(r); return p; }
}

// Text input in a rounded frame.
class PillBox : Control
{
    public readonly TextBox Edit = new TextBox();
    string placeholder = "";
    public string Placeholder { get { return placeholder; } set { placeholder = value ?? ""; if (Edit.IsHandleCreated) Native.SetCue(Edit.Handle, placeholder); } }

    public PillBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 32;
        Edit.BorderStyle = BorderStyle.None;
        Edit.Font = Theme.Body;
        Edit.GotFocus += delegate { Invalidate(); };
        Edit.LostFocus += delegate { Invalidate(); };
        Edit.TextChanged += delegate { Invalidate(); if (TextChanged2 != null) TextChanged2(); };
        Edit.HandleCreated += delegate { Native.SetCue(Edit.Handle, placeholder); };
        Controls.Add(Edit);
        ApplyTheme();
    }

    public event Action TextChanged2;

    public override string Text { get { return Edit.Text; } set { Edit.Text = value; } }
    public bool ReadOnly { get { return Edit.ReadOnly; } set { Edit.ReadOnly = value; ApplyTheme(); } }

    public void ApplyTheme()
    {
        Edit.BackColor = Theme.Field;
        Edit.ForeColor = Theme.Text;
        Invalidate();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int h = Edit.PreferredHeight;
        Edit.SetBounds(14, (Height - h) / 2, Math.Max(10, Width - 28), h);
    }

    protected override void OnEnabledChanged(EventArgs e) { Edit.Enabled = Enabled; Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Quality(g);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = Theme.Round(r, Height / 2))
        {
            using (SolidBrush b = new SolidBrush(Enabled ? Theme.Field : Theme.Mix(Theme.Field, Theme.Bg, 0.4f))) g.FillPath(b, p);
            using (Pen pen = new Pen(Edit.Focused ? Theme.A2 : Theme.Border, Edit.Focused ? 1.5f : 1f)) g.DrawPath(pen, p);
        }
    }
}

// Numeric field with - / + buttons.
class Stepper : Control
{
    public readonly TextBox Edit = new TextBox();
    int value = 60;
    public int Min = 20, Max = 100000, Step = 10;
    public event Action ValueChanged;

    public Stepper()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 32;
        Width = 130;
        Edit.BorderStyle = BorderStyle.None;
        Edit.Font = Theme.Body;
        Edit.TextAlign = HorizontalAlignment.Center;
        Edit.Text = value.ToString();
        Edit.TextChanged += delegate { int v; if (int.TryParse(Edit.Text.Trim(), out v)) { value = v; Fire(); } };
        Edit.Leave += delegate { Clamp(); };
        Edit.KeyPress += delegate(object s, KeyPressEventArgs e) { if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar)) e.Handled = true; };
        Edit.GotFocus += delegate { Invalidate(); };
        Edit.LostFocus += delegate { Invalidate(); };
        Controls.Add(Edit);
        ApplyTheme();
    }

    void Fire() { Action a = ValueChanged; if (a != null) a(); Invalidate(); }

    public int Value
    {
        get { return value; }
        set { this.value = Math.Max(Min, Math.Min(Max, value)); Edit.Text = this.value.ToString(); Fire(); }
    }

    void Clamp() { int v; if (!int.TryParse(Edit.Text.Trim(), out v)) v = value; Value = v; }

    public void ApplyTheme() { Edit.BackColor = Theme.Field; Edit.ForeColor = Theme.Text; Invalidate(); }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int h = Edit.PreferredHeight;
        Edit.SetBounds(Height, (Height - h) / 2, Math.Max(10, Width - Height * 2), h);
    }

    protected override void OnEnabledChanged(EventArgs e) { Edit.Enabled = Enabled; Invalidate(); base.OnEnabledChanged(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (!Enabled) return;
        if (e.X < Height) { Clamp(); Value = value - Step; }
        else if (e.X > Width - Height) { Clamp(); Value = value + Step; }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        Cursor = Enabled && (e.X < Height || e.X > Width - Height) ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Quality(g);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = Theme.Round(r, Height / 2))
        {
            using (SolidBrush b = new SolidBrush(Enabled ? Theme.Field : Theme.Mix(Theme.Field, Theme.Bg, 0.4f))) g.FillPath(b, p);
            using (Pen pen = new Pen(Edit.Focused ? Theme.A2 : Theme.Border, Edit.Focused ? 1.5f : 1f)) g.DrawPath(pen, p);
        }
        Color c = Enabled ? Theme.Muted : Theme.Border;
        TextRenderer.DrawText(g, "−", Theme.Title, new Rectangle(0, 0, Height, Height), c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "+", Theme.Title, new Rectangle(Width - Height, 0, Height, Height), c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

// Drop-down list drawn in the theme.
class DropPill : Control
{
    List<string> items = new List<string>();
    int selected = -1;
    bool hover;
    ToolStripDropDown drop;
    public event Action SelectedIndexChanged;
    public string Placeholder = "";

    public DropPill()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
        TabStop = true;
        BackColor = Color.Transparent;
        Height = 32;
        Cursor = Cursors.Hand;
        Font = Theme.Body;
    }

    public int SelectedIndex
    {
        get { return selected; }
        set { if (value == selected) return; selected = value; Invalidate(); Action a = SelectedIndexChanged; if (a != null) a(); }
    }

    public int Count { get { return items.Count; } }

    public void SetItems(IEnumerable<string> list)
    {
        items = new List<string>(list);
        selected = -1;
        Invalidate();
    }

    public void UpdateItems(IEnumerable<string> list)   // keeps the selection
    {
        items = new List<string>(list);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override bool IsInputKey(Keys k) { return k == Keys.Down || k == Keys.Up || base.IsInputKey(k); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down && selected < items.Count - 1) { SelectedIndex = selected + 1; e.Handled = true; }
        else if (e.KeyCode == Keys.Up && selected > 0) { SelectedIndex = selected - 1; e.Handled = true; }
        else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { Open(); e.Handled = true; }
        base.OnKeyDown(e);
    }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); Open(); base.OnMouseDown(e); }

    void Open()
    {
        if (!Enabled || items.Count == 0) return;
        ListBox lb = new ListBox();
        lb.BorderStyle = BorderStyle.None;
        lb.DrawMode = DrawMode.OwnerDrawFixed;
        lb.ItemHeight = 28;
        lb.BackColor = Theme.Card;
        lb.ForeColor = Theme.Text;
        lb.IntegralHeight = false;
        foreach (string s in items) lb.Items.Add(s);
        if (selected >= 0) lb.SelectedIndex = selected;
        lb.Width = Math.Max(Width, 220);
        lb.Height = Math.Min(items.Count * 28 + 2, 260);
        int hovered = -1;
        lb.DrawItem += delegate(object s, DrawItemEventArgs ev)
        {
            if (ev.Index < 0) return;
            Graphics g = ev.Graphics;
            Theme.Quality(g);
            bool sel = ev.Index == lb.SelectedIndex || ev.Index == hovered;
            using (SolidBrush b = new SolidBrush(Theme.Card)) g.FillRectangle(b, ev.Bounds);
            if (sel)
            {
                Rectangle rr = new Rectangle(ev.Bounds.X + 4, ev.Bounds.Y + 2, ev.Bounds.Width - 8, ev.Bounds.Height - 4);
                using (GraphicsPath p = Theme.Round(rr, 8)) using (SolidBrush b = new SolidBrush(Theme.Mix(Theme.Card, Theme.A1, ev.Index == lb.SelectedIndex ? 0.35f : 0.2f))) g.FillPath(b, p);
            }
            TextRenderer.DrawText(g, lb.Items[ev.Index].ToString(), Theme.Body, new Rectangle(ev.Bounds.X + 12, ev.Bounds.Y, ev.Bounds.Width - 20, ev.Bounds.Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        };
        lb.MouseMove += delegate(object s, MouseEventArgs ev)
        {
            int idx = lb.IndexFromPoint(ev.Location);
            if (idx != hovered) { hovered = idx; lb.Invalidate(); }
        };
        lb.MouseClick += delegate(object s, MouseEventArgs ev)
        {
            int idx = lb.IndexFromPoint(ev.Location);
            if (idx >= 0) { if (drop != null) drop.Close(); SelectedIndex = idx; }
        };
        ToolStripControlHost host = new ToolStripControlHost(lb);
        host.Margin = Padding.Empty; host.Padding = Padding.Empty; host.AutoSize = false; host.Size = lb.Size;
        drop = new ToolStripDropDown();
        drop.Padding = Padding.Empty;
        drop.BackColor = Theme.Card;
        drop.Items.Add(host);
        drop.Show(this, new Point(0, Height + 2));
        lb.Focus();
        Native.DarkControl(lb.Handle, Theme.Dark);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Quality(g);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = Theme.Round(r, Height / 2))
        {
            using (SolidBrush b = new SolidBrush(!Enabled ? Theme.Mix(Theme.Field, Theme.Bg, 0.4f) : (hover ? Theme.CardHover : Theme.Field))) g.FillPath(b, p);
            using (Pen pen = new Pen(Focused || hover ? Theme.A2 : Theme.Border)) g.DrawPath(pen, p);
        }
        string t = selected >= 0 && selected < items.Count ? items[selected] : Placeholder;
        TextRenderer.DrawText(g, t, Font, new Rectangle(14, 0, Width - 40, Height), selected >= 0 && Enabled ? Theme.Text : Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        int cx = Width - 20, cy = Height / 2;
        using (Pen pen = new Pen(Enabled ? Theme.Muted : Theme.Border, 1.8f))
        {
            pen.StartCap = LineCap.Round; pen.EndCap = LineCap.Round; pen.LineJoin = LineJoin.Round;
            g.DrawLines(pen, new Point[] { new Point(cx - 4, cy - 2), new Point(cx, cy + 2), new Point(cx + 4, cy - 2) });
        }
    }
}

// Table in the theme (details view with owner drawing).
class ThemedList : ListView
{
    int[] percents = new int[0];
    public int RowHeight = 30;

    public ThemedList()
    {
        View = View.Details;
        FullRowSelect = true;
        MultiSelect = false;
        HideSelection = false;
        HotTracking = false;
        BorderStyle = BorderStyle.None;
        OwnerDraw = true;
        DoubleBuffered = true;
        HeaderStyle = ColumnHeaderStyle.Nonclickable;
        ShowItemToolTips = true;
        Font = Theme.Body;
        ImageList il = new ImageList();
        il.ImageSize = new Size(1, RowHeight);
        SmallImageList = il;
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Field;
        ForeColor = Theme.Text;
        if (IsHandleCreated) Native.DarkControl(Handle, Theme.Dark);
        Invalidate();
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.DarkControl(Handle, Theme.Dark); }

    public void SetColumnPercents(params int[] p) { percents = p; FitColumns(); }

    void FitColumns()
    {
        if (percents.Length == 0 || Columns.Count != percents.Length || ClientSize.Width <= 0) return;
        int total = ClientSize.Width;
        int used = 0;
        for (int i = 0; i < Columns.Count; i++)
        {
            int w = i == Columns.Count - 1 ? total - used : total * percents[i] / 100;
            Columns[i].Width = Math.Max(20, w);
            used += w;
        }
    }

    protected override void OnResize(EventArgs e) { base.OnResize(e); Refit(); }
    protected override void OnClientSizeChanged(EventArgs e) { base.OnClientSizeChanged(e); Refit(); }

    // Re-fits the columns to the width and hides the horizontal scroll bar (the columns always fit).
    public void Refit()
    {
        FitColumns();
        if (IsHandleCreated) Native.HideHScroll(Handle);
    }

    protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
    {
        Graphics g = e.Graphics;
        using (SolidBrush b = new SolidBrush(Theme.Field)) g.FillRectangle(b, e.Bounds);
        using (Pen pen = new Pen(Theme.Border)) g.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        TextRenderer.DrawText(g, e.Header.Text, Theme.Small, new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height), Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }

    protected override void OnDrawItem(DrawListViewItemEventArgs e) { }   // everything is drawn per sub-item

    protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Quality(g);
        Rectangle b = e.Bounds;
        // every cell paints its own background (Windows may redraw a single cell, e.g. on mouse hover),
        // so the whole-row rounded highlight is drawn clipped to this cell
        Rectangle row = new Rectangle(2, b.Y + 1, ClientSize.Width - 4, b.Height - 2);
        using (SolidBrush bg = new SolidBrush(Theme.Field)) g.FillRectangle(bg, b);
        if (e.Item.Selected)
        {
            GraphicsState st = g.Save();
            g.SetClip(b);
            using (GraphicsPath p = Theme.Round(row, 8))
            {
                using (SolidBrush sb = new SolidBrush(Theme.Mix(Theme.Field, Theme.A1, Theme.Dark ? 0.30f : 0.18f))) g.FillPath(sb, p);
                using (Pen pen = new Pen(Theme.Alpha(Theme.A2, 120))) g.DrawPath(pen, p);
            }
            g.Restore(st);
        }
        // sub-items to the right of the first column are painted on top of the row background
        Color c = e.SubItem.ForeColor;
        if (c == Color.Empty || c.ToArgb() == SystemColors.WindowText.ToArgb()) c = Theme.Text;
        TextRenderer.DrawText(g, e.SubItem.Text, e.ColumnIndex == 0 ? Theme.Bold : Font, new Rectangle(b.X + 8, b.Y, b.Width - 10, b.Height), c,
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
    }
}

class GradientBar : Control
{
    int value;
    bool marquee;
    int phase;
    Timer timer = new Timer();

    public GradientBar()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Height = 10;
        timer.Interval = 30;
        timer.Tick += delegate { phase = (phase + 6) % 1000; Invalidate(); };
    }

    public int Value { get { return value; } set { this.value = Math.Max(0, Math.Min(100, value)); Marquee = false; Invalidate(); } }

    public bool Marquee
    {
        get { return marquee; }
        set { if (marquee == value) return; marquee = value; if (value) timer.Start(); else timer.Stop(); Invalidate(); }
    }

    protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Quality(g);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = Theme.Round(r, Height / 2))
        {
            using (SolidBrush b = new SolidBrush(Theme.Field)) g.FillPath(b, p);
            using (Pen pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
        }
        Rectangle fill;
        if (marquee)
        {
            int w = Math.Max(30, Width * 3 / 10);
            int total = Width + w;
            int x = (int)((long)phase * total / 1000) - w;
            int x0 = Math.Max(0, x), x1 = Math.Min(Width - 1, x + w);
            fill = new Rectangle(x0, 0, x1 - x0, Height - 1);
        }
        else fill = new Rectangle(0, 0, (int)((Width - 1) * (long)value / 100), Height - 1);
        if (fill.Width < 2) return;
        Region old = g.Clip;
        using (GraphicsPath clip = Theme.Round(r, Height / 2)) g.SetClip(clip);
        using (GraphicsPath fp = Theme.Round(fill, Height / 2))
        using (LinearGradientBrush gb = Theme.Grad(fill, Theme.A1, Theme.A2, 0f)) g.FillPath(gb, fp);
        g.Clip = old;
    }
}

// Read-only log box.
class LogBox : TextBox
{
    public LogBox()
    {
        Multiline = true;
        ReadOnly = true;
        ScrollBars = ScrollBars.Vertical;
        BorderStyle = BorderStyle.None;
        Font = Theme.Mono;
        ApplyTheme();
    }

    public void ApplyTheme()
    {
        BackColor = Theme.Field;
        ForeColor = Theme.Text;
        if (IsHandleCreated) Native.DarkControl(Handle, Theme.Dark);
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.DarkControl(Handle, Theme.Dark); }

    public void AppendLine(string s)
    {
        if (InvokeRequired) { Ui.Post(delegate { AppendLine(s); }); return; }
        AppendText(s + "\r\n");
    }
}

// A frame that gives a borderless control (list, log) the rounded outline of the theme.
class Frame : Panel
{
    public Frame()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Padding = new Padding(8, 6, 8, 6);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        Theme.Quality(g);
        Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = Theme.Round(r, 10))
        {
            using (SolidBrush b = new SolidBrush(Theme.Field)) g.FillPath(b, p);
            using (Pen pen = new Pen(Theme.Border)) g.DrawPath(pen, p);
        }
    }
}

// Plain themed text label (the standard Label draws with the wrong colors on transparent parents).
class Txt : Control
{
    public Color? Tone;       // null = normal text
    public bool IsMuted;
    public Txt()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        Font = Theme.Body;
        Height = 20;
    }
    public Txt(string text) : this() { Text = text; }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        Color c = Tone.HasValue ? Tone.Value : (IsMuted ? Theme.Muted : Theme.Text);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, c, TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
    }
}
