using System;
using System.Diagnostics;
using System.IO;

namespace VectorPowerHub.Tools {
    /// <summary>
    /// VectorPowerHub MSI Builder CLI Wrapper.
    /// Compiles with: csc.exe /out:bin\BuildMsi.exe tools\BuildMsi.cs
    /// </summary>
    public static class BuildMsiProgram {
        public static int Main(string[] args) {
            Console.WriteLine("[BuildMsi] Launching VectorPowerHub MSI generation pipeline...");
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string scriptPath = Path.Combine(baseDir, "..", "tools", "Build-Msi.ps1");
            if (!File.Exists(scriptPath)) {
                scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "tools", "Build-Msi.ps1");
            }
            scriptPath = Path.GetFullPath(scriptPath);

            if (!File.Exists(scriptPath)) {
                Console.Error.WriteLine("[BuildMsi] Error: Cannot locate tools/Build-Msi.ps1 at " + scriptPath);
                return 1;
            }

            string psExe = "powershell.exe";
            string pwshCandidate = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
            if (File.Exists(pwshCandidate)) {
                psExe = pwshCandidate;
            }

            string escapedArgs = string.Join(" ", args);
            ProcessStartInfo psi = new ProcessStartInfo {
                FileName = psExe,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\" " + escapedArgs,
                UseShellExecute = false,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                WorkingDirectory = Directory.GetCurrentDirectory()
            };

            try {
                using (Process proc = Process.Start(psi)) {
                    proc.WaitForExit();
                    return proc.ExitCode;
                }
            } catch (Exception ex) {
                Console.Error.WriteLine("[BuildMsi] Execution error: " + ex.Message);
                return 2;
            }
        }
    }
}
