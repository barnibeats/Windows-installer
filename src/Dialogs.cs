// Themed message and confirmation dialogs, plus the progress sink shared by long operations.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

enum DlgKind { Info, Warn, Error }

class DlgForm : Form
{
    public DlgForm(string title)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        BackColor = Theme.Bg;
        Font = Theme.Body;
        DoubleBuffered = true;
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.DarkTitleBar(Handle, Theme.Dark); }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        Theme.PaintBackdrop(e.Graphics, ClientRectangle);
    }

    public static void Accent(Graphics g, Rectangle r, Color c)
    {
        Theme.Quality(g);
        using (GraphicsPath p = Theme.Round(r, 8)) using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
    }
}

static class Dlg
{
    static Color KindColor(DlgKind k) { return k == DlgKind.Error ? Theme.Bad : (k == DlgKind.Warn ? Theme.Warn : Theme.A2); }

    static string Glyph(DlgKind k) { return k == DlgKind.Error ? "✕" : (k == DlgKind.Warn ? "!" : "i"); }

    static Size Measure(string text, int width)
    {
        Size s = TextRenderer.MeasureText(text, Theme.Body, new Size(width, 2000), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        return new Size(width, Math.Min(s.Height + 4, 420));
    }

    static void Icon(DlgForm f, DlgKind kind)
    {
        Control icon = new Control();
        icon.SetBounds(22, 22, 36, 36);
        Color c = KindColor(kind);
        icon.Paint += delegate(object s, PaintEventArgs e)
        {
            Theme.Quality(e.Graphics);
            using (SolidBrush b = new SolidBrush(Theme.Alpha(c, 50))) e.Graphics.FillEllipse(b, 0, 0, 35, 35);
            using (Pen p = new Pen(c, 1.5f)) e.Graphics.DrawEllipse(p, 0, 0, 35, 35);
            TextRenderer.DrawText(e.Graphics, Glyph(kind), Theme.Title, new Rectangle(0, 0, 36, 36), c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        };
        f.Controls.Add(icon);
    }

    public static void Msg(IWin32Window owner, string text, DlgKind kind)
    {
        Show(owner, text, kind, false);
    }

    public static bool Ask(IWin32Window owner, string text, DlgKind kind)
    {
        return Show(owner, text, kind, true);
    }

    static bool Show(IWin32Window owner, string text, DlgKind kind, bool yesNo)
    {
        DlgForm f = new DlgForm(S.T("app.title"));
        Size ts = Measure(text, 420);
        int h = Math.Max(ts.Height, 40) + 100;
        f.ClientSize = new Size(500, h);
        Icon(f, kind);
        Txt body = new Txt(text);
        body.SetBounds(74, 24, 410, ts.Height);
        f.Controls.Add(body);

        PillButton ok = new PillButton();
        ok.Text = yesNo ? S.T("dlg.yes") : S.T("dlg.ok");
        ok.SetBounds(yesNo ? 500 - 22 - 110 - 8 - 110 : 500 - 22 - 110, h - 52, 110, 34);
        ok.Click += delegate { f.DialogResult = DialogResult.OK; f.Close(); };
        f.Controls.Add(ok);
        f.AcceptButton = null;
        if (yesNo)
        {
            PillButton no = new PillButton();
            no.Kind = BtnKind.Ghost;
            no.Text = S.T("dlg.no");
            no.SetBounds(500 - 22 - 110, h - 52, 110, 34);
            no.Click += delegate { f.DialogResult = DialogResult.Cancel; f.Close(); };
            f.Controls.Add(no);
        }
        f.Shown += delegate { ok.Focus(); };
        f.KeyPreview = true;
        f.KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) { f.DialogResult = DialogResult.Cancel; f.Close(); } };
        DialogResult r = owner == null ? f.ShowDialog() : f.ShowDialog(owner);
        f.Dispose();
        return r == DialogResult.OK;
    }

    // Dangerous action: the user has to type a word (a disk number, SYSPREP) to continue.
    public static bool ConfirmTyped(IWin32Window owner, string title, string body, string word, string okText)
    {
        DlgForm f = new DlgForm(title);
        Size ts = Measure(body, 440);
        int h = ts.Height + 150;
        f.ClientSize = new Size(520, h);
        Icon(f, DlgKind.Error);
        Txt t = new Txt(body);
        t.Tone = Theme.Bad;
        t.SetBounds(74, 24, 430, ts.Height);
        f.Controls.Add(t);

        Txt prompt = new Txt(S.F("dlg.type", word));
        prompt.IsMuted = true;
        prompt.SetBounds(74, h - 98, 300, 20);
        f.Controls.Add(prompt);
        PillBox box = new PillBox();
        box.SetBounds(380, h - 104, 118, 32);
        f.Controls.Add(box);

        PillButton ok = new PillButton();
        ok.Kind = BtnKind.Danger;
        ok.Text = okText;
        ok.SetBounds(520 - 22 - 8 - 110 - 190, h - 52, 190, 34);
        ok.Enabled = false;
        ok.Click += delegate { f.DialogResult = DialogResult.OK; f.Close(); };
        f.Controls.Add(ok);
        PillButton no = new PillButton();
        no.Kind = BtnKind.Ghost;
        no.Text = S.T("dlg.cancel");
        no.SetBounds(520 - 22 - 110, h - 52, 110, 34);
        no.Click += delegate { f.DialogResult = DialogResult.Cancel; f.Close(); };
        f.Controls.Add(no);
        box.TextChanged2 += delegate { ok.Enabled = box.Text.Trim() == word; };
        f.Shown += delegate { box.Edit.Focus(); };
        f.KeyPreview = true;
        f.KeyDown += delegate(object s, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) { f.DialogResult = DialogResult.Cancel; f.Close(); }
            else if (e.KeyCode == Keys.Enter && ok.Enabled) { f.DialogResult = DialogResult.OK; f.Close(); }
        };
        DialogResult r = owner == null ? f.ShowDialog() : f.ShowDialog(owner);
        f.Dispose();
        return r == DialogResult.OK;
    }
}

// Progress sink bound to a bar, a status line and a log box. Safe to call from worker threads.
class UiProgress : IProgress2
{
    readonly GradientBar bar;
    readonly Txt status;
    readonly LogBox log;

    public UiProgress(GradientBar bar, Txt status, LogBox log) { this.bar = bar; this.status = status; this.log = log; }

    public void Log(string line) { Ui.Post(delegate { log.AppendText(line + "\r\n"); }); }
    public void Percent(int percent) { Ui.Post(delegate { bar.Value = percent; }); }
    public void Indeterminate() { Ui.Post(delegate { bar.Marquee = true; }); }
    public void Status(string text) { Ui.Post(delegate { status.Tone = null; status.Text = text; }); }
}
