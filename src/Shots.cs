// "--shots <dir>": developer helper. Opens the window, visits every tab in both themes and saves screenshots.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

static class Shots
{
    public static void Attach(MainForm f, string dir)
    {
        Directory.CreateDirectory(dir);
        f.Shown += delegate
        {
            List<object[]> plan = new List<object[]>();
            foreach (bool dark in new bool[] { true, false })
                for (int tab = 0; tab < 3; tab++)
                    plan.Add(new object[] { dark, tab });
            int step = 0, phase = 0, wait = 0;
            Timer t = new Timer();
            t.Interval = 700;
            t.Tick += delegate
            {
                if (wait-- > 0) return;
                if (step >= plan.Count) { t.Stop(); f.Close(); return; }
                bool dark = (bool)plan[step][0];
                int tab = (int)plan[step][1];
                if (phase == 0)
                {
                    Theme.Set(dark);
                    f.SelectTab(tab);
                    f.Activate();
                    wait = tab == 1 ? 14 : 3;   // the capture tab runs its checks in the background
                    phase = 1;
                }
                else
                {
                    // renders the window itself (not the screen), so other windows never end up in the picture
                    using (Bitmap bmp = new Bitmap(f.Width, f.Height))
                    {
                        f.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                        bmp.Save(Path.Combine(dir, (dark ? "dark" : "light") + "-tab" + tab + ".png"), ImageFormat.Png);
                    }
                    step++; phase = 0;
                }
            };
            t.Start();
        };
    }
}
