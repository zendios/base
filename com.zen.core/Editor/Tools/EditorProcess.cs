using System.Diagnostics;

namespace Base.Tools
{
    /// <summary>Command-line tools run from the editor (tar, plutil).</summary>
    internal static class EditorProcess
    {
        /// <summary>
        /// Runs file with args and waits for it. Null when it exited with 0, else why not (exit code and stderr).
        /// output: its stdout.
        /// </summary>
        public static string Run(string file, string args, out string output)
        {
            var info = new ProcessStartInfo(file, args)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (var process = Process.Start(info))
            {
                // stderr read in the background: reading both streams one after the other can block on a full pipe.
                var error = process.StandardError.ReadToEndAsync();
                output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                return process.ExitCode == 0 ? null : $"{file} exit {process.ExitCode}: {error.Result.Trim()}";
            }
        }
    }
}
