Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Text;
public class Shot {
    public delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageW(IntPtr h, uint msg, IntPtr wp, IntPtr lp);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

    public static IntPtr MainWindow(int pid) {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint p;
            GetWindowThreadProcessId(h, out p);
            if ((int)p != pid) return true;
            if (!IsWindowVisible(h)) return true;
            StringBuilder sb = new StringBuilder(300);
            GetWindowTextW(h, sb, 300);
            if (sb.Length == 0) return true;
            found = h;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    public static string[] ChildButtons(IntPtr parent) {
        List<string> list = new List<string>();
        EnumChildWindows(parent, delegate(IntPtr h, IntPtr l) {
            StringBuilder cn = new StringBuilder(200);
            GetClassNameW(h, cn, 200);
            if (cn.ToString().IndexOf("Button", StringComparison.OrdinalIgnoreCase) < 0) return true;
            StringBuilder tb = new StringBuilder(300);
            GetWindowTextW(h, tb, 300);
            if (tb.Length == 0) return true;
            list.Add(h.ToInt64() + "|" + tb.ToString());
            return true;
        }, IntPtr.Zero);
        return list.ToArray();
    }

    public static string ClickByText(IntPtr parent, string text) {
        foreach (string row in ChildButtons(parent)) {
            string[] parts = row.Split('|');
            if (parts.Length < 2) continue;
            if (parts[1] != text) continue;
            SendMessageW(new IntPtr(long.Parse(parts[0])), 0x00F5, IntPtr.Zero, IntPtr.Zero);  // BM_CLICK
            return "clicked [" + text + "]";
        }
        return "button not found: [" + text + "]";
    }

    public static string Capture(IntPtr h, string path) {
        RECT r;
        GetWindowRect(h, out r);
        int w = r.R - r.L;
        int hh = r.B - r.T;
        if (w <= 0 || hh <= 0) return "bad rect " + w + "x" + hh;
        using (Bitmap bmp = new Bitmap(w, hh)) {
            using (Graphics g = Graphics.FromImage(bmp)) {
                IntPtr hdc = g.GetHdc();
                bool ok = PrintWindow(h, hdc, 2);
                g.ReleaseHdc(hdc);
                if (!ok) return "PrintWindow returned false";
            }
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
        return "ok " + w + "x" + hh;
    }
}
"@

$dir  = 'D:\harness gzq\009\miaohua\pc'
$exe  = Join-Path $dir 'Miaohua.exe'
$mode = if ($args.Count -ge 1) { $args[0] } else { 'fresh' }
$text = if ($args.Count -ge 2) { $args[1] } else { '' }
$out  = if ($args.Count -ge 3) { $args[2] } else { Join-Path $dir 'shot.png' }

if ($mode -eq 'keep') {
    $p = @(Get-Process -Name Miaohua -ErrorAction SilentlyContinue) | Select-Object -First 1
    if (-not $p) { "no running instance"; exit 1 }
} else {
    Get-Process -Name Miaohua -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill() } catch { } }
    Start-Sleep -Milliseconds 900
    $p = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds 5
}

$h = [Shot]::MainWindow($p.Id)
if ($h -eq [IntPtr]::Zero) { "no main window found"; exit 1 }

"buttons  : " + (([Shot]::ChildButtons($h)) -replace '^\d+\|', '' -join ', ')

if ($text -ne '') {
    $res = [Shot]::ClickByText($h, $text)
    "action   : $res"
    Start-Sleep -Milliseconds 900
}

"capture  : $([Shot]::Capture($h, $out))"
"shot     : $out"
