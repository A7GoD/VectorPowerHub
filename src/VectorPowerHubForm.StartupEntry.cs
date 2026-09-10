using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VectorPowerHub {
    public partial class VectorPowerHubForm : Form {
        // -----------------------------------------------------------------------------------------
        [STAThread]
        public static void Main(string[] args) {
            int initialTab = 0;
            bool startMinimized = false;
            if (args != null && args.Length > 0) {
                for (int i = 0; i < args.Length; i++) {
                    string a = args[i].ToLowerInvariant();
                    if (a == "/minimized" || a == "--minimized" || a == "/tray" || a == "--tray" || a == "/startup" || a == "--startup") {
                        startMinimized = true;
                    }
                }
                if (args[0] == "/test" || args[0] == "--test") {
                    Console.WriteLine("[TEST] Starting VectorPowerHubForm headless verification...");
                    using (VectorPowerHubForm form = new VectorPowerHubForm()) {
                        Thread.Sleep(1200);
                        form.OnTelemetryTick(null, EventArgs.Empty);
                        HubTelemetrySnapshot s = form.currentSnapshot;
                        Console.WriteLine("[TEST] Telemetry Snapshot:");
                        Console.WriteLine(string.Format("  In-Game FPS: {0:0.0}", s.Fps));
                        Console.WriteLine(string.Format("  CPU Package Power: {0:0.0} W (P-Core: {1:0.00} GHz | E-Core: {2:0.00} GHz)", s.CpuPowerW, s.PCoreGhz, s.ECoreGhz));
                        Console.WriteLine(string.Format("  GPU Dynamic Draw: {0:0.0} W (Clock: {1} MHz | Temp: {2} C | Util: {3}%)", s.GpuPowerW, s.GpuClockMhz, s.GpuTempC, s.GpuUtilPct));
                        Console.WriteLine(string.Format("  Total Platform Draw: {0:0.0} W / 215.0 W Ceiling", s.TotalPlatformPowerW));
                        Console.WriteLine(string.Format("  GPU Status Badge: {0}", s.GpuStatus));
                        Console.WriteLine(string.Format("  Display Attached: {0} ({1})", s.IsNvidiaDisplayAttached, s.NvidiaMonitorName));
                        Console.WriteLine(string.Format("  Active Profile: {0}", form.currentSelectedProfile));
                        Console.WriteLine("[TEST] VectorPowerHubForm verification completed successfully!");
                    }
                    return;
                }
                if (args[0] == "/render" || args[0] == "/screenshot") {
                    string outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "previews");
                    if (!Directory.Exists(outDir)) { Directory.CreateDirectory(outDir); }
                    for (int t = 0; t <= 2; t++) {
                        using (VectorPowerHubForm form = new VectorPowerHubForm(t)) {
                            form.StartPosition = FormStartPosition.Manual;
                            form.Location = new Point(-2000, -2000);
                            form.Show();
                            Application.DoEvents();
                            Thread.Sleep(2200);
                            form.OnTelemetryTick(null, EventArgs.Empty);
                            Application.DoEvents();

                            Bitmap bmp = new Bitmap(form.Width, form.Height);
                            using (Graphics g = Graphics.FromImage(bmp)) {
                                g.Clear(VectorPowerHubForm.ColorBgMain);
                                RenderControlHierarchy(form, g, Point.Empty);
                            }
                            string outName = Path.Combine(outDir, string.Format("tab{0}_preview.png", t));
                            bmp.Save(outName, System.Drawing.Imaging.ImageFormat.Png);
                            Console.WriteLine(string.Format("[SCREENSHOT] Saved tab {0} to {1}", t, outName));
                            form.Close();
                        }
                    }
                    return;
                }
                if (args[0] == "/tab2" || args[0] == "/topology" || (args.Length > 1 && args[0] == "/tab" && args[1] == "2")) {
                    initialTab = 2;
                } else if (args[0] == "/tab1" || args[0] == "/bench" || (args.Length > 1 && args[0] == "/tab" && args[1] == "1")) {
                    initialTab = 1;
                }
            }
            bool createdNew = false;
            using (Mutex appMutex = new Mutex(true, "VectorPowerHub_SingleInstance_Mutex", out createdNew)) {
                if (!createdNew) {
                    // Another instance is already running!
                    if (!startMinimized) {
                        // 1. Signal named EventWaitHandle to restore and activate primary instance
                        try {
                            using (EventWaitHandle wakeEvent = EventWaitHandle.OpenExisting("VectorPowerHub_WakeEvent")) {
                                wakeEvent.Set();
                            }
                        } catch { }

                        // 2. Broadcast registered window message
                        try {
                            if (WM_SHOW_HUB != 0) {
                                PostMessage((IntPtr)HWND_BROADCAST, WM_SHOW_HUB, IntPtr.Zero, IntPtr.Zero);
                            }
                        } catch { }

                        // 3. Bring existing process window to foreground
                        try {
                            Process current = Process.GetCurrentProcess();
                            foreach (Process p in Process.GetProcessesByName(current.ProcessName)) {
                                if (p.Id != current.Id) {
                                    if (p.MainWindowHandle != IntPtr.Zero) {
                                        ShowWindow(p.MainWindowHandle, SW_RESTORE);
                                        SetForegroundWindow(p.MainWindowHandle);
                                    }
                                    break;
                                }
                            }
                        } catch { }
                    }

                    // Exit immediately without creating duplicate windows or tray icons
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new VectorPowerHubForm(initialTab, startMinimized));
                GC.KeepAlive(appMutex);
            }
        }

        private static void RenderControlHierarchy(Control parent, Graphics g, Point origin) {
            foreach (Control c in parent.Controls) {
                if (!c.Visible || c.Width <= 0 || c.Height <= 0) continue;
                Point p = new Point(origin.X + c.Left, origin.Y + c.Top);
                using (Bitmap childBmp = new Bitmap(c.Width, c.Height)) {
                    c.DrawToBitmap(childBmp, new Rectangle(0, 0, c.Width, c.Height));
                    g.DrawImage(childBmp, p);
                }
                if (c.Controls.Count > 0) {
                    RenderControlHierarchy(c, g, p);
                }
            }
        }

    }
}
