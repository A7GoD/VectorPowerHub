using System;
using System.Runtime.InteropServices;

public class T {
    [DllImport("ntdll.dll")]
    public static extern int NtQueryTimerResolution(out uint min, out uint max, out uint cur);

    public static void Main() {
        uint min = 0, max = 0, cur = 0;
        NtQueryTimerResolution(out min, out max, out cur);
        Console.WriteLine("CURRENT_RES_MS: " + (cur / 10000.0).ToString("F4"));
        Console.WriteLine("MIN_RES_MS: " + (min / 10000.0).ToString("F4"));
        Console.WriteLine("MAX_RES_MS: " + (max / 10000.0).ToString("F4"));
    }
}
