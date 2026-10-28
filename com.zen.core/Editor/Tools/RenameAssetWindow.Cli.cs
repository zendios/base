using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Base.Tools
{
    /// <summary>
    /// Command-line entries of Base > Rename Assets (same ZPAS rules, same suggestions), for agents and CI (ZPAS.md §6):
    /// <code>
    /// Unity -batchmode -quit -nographics -projectPath . -executeMethod Base.Tools.RenameAssetWindow.ValidateCli [-zpasRoot Assets/Production]
    /// Unity -batchmode -quit -nographics -projectPath . -executeMethod Base.Tools.RenameAssetWindow.RenameCli [-zpasRoot ...] [-zpasApply]
    /// </code>
    /// ValidateCli writes Logs/Zpas/naming.csv and exits 1 while names fail. RenameCli writes Logs/Zpas/rename.csv (old, suggested,
    /// check of the suggestion, names still in code); with -zpasApply it renames the rows whose suggestion passes and whose old name
    /// is not in any .cs file, and exits 1 while names still fail.
    /// </summary>
    public partial class RenameAssetWindow
    {
        private const string ReportFolder = "Logs/Zpas";

        public static void ValidateCli()
        {
            var rows = CliRows();
            WriteReport("naming.csv", rows, null);
            Debug.Log($"[Base Rename] {rows.Count} name(s) fail the ZPAS check, report {ReportFolder}/naming.csv");
            if (rows.Count > 0)
                EditorApplication.Exit(1);
        }

        public static void RenameCli()
        {
            var rows = CliRows();
            bool apply = System.Environment.GetCommandLineArgs().Contains("-zpasApply");
            var safe = rows.Where(r => r.Issue == null && r.NewName != r.OldName && r.CodeHits == null).ToList();
            var errors = new StringBuilder();
            if (apply && safe.Count > 0)
                Rename(safe, errors);
            WriteReport("rename.csv", rows, apply ? safe : null);
            Debug.Log($"[Base Rename] {rows.Count} to fix, {safe.Count} safe to rename" + (apply ? ", renamed" : " (dry run, add -zpasApply)")
                      + (errors.Length > 0 ? "\n" + errors : ""));
            if (errors.Length > 0 || rows.Count > (apply ? safe.Count : 0))
                EditorApplication.Exit(1);
        }

        /// <summary>The window's rows (every asset failing ZPAS with its suggestion) under -zpasRoot (default Assets/).</summary>
        private static List<Row> CliRows()
        {
            var args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-zpasRoot");
            var root = index >= 0 && index + 1 < args.Length ? args[index + 1].TrimEnd('/') + "/" : "Assets/";
            var window = CreateInstance<RenameAssetWindow>();   // OnEnable scans
            var rows = window._rows.Where(r => r.Path.StartsWith(root)).ToList();
            DestroyImmediate(window);
            return rows;
        }

        private static void WriteReport(string file, List<Row> rows, List<Row> renamed)
        {
            var lines = new List<string> { "path,old,suggested,suggestion_check,in_code,renamed" };
            lines.AddRange(rows.Select(r => string.Join(",", Quote(r.Path), Quote(r.OldName), Quote(r.NewName), Quote(r.Issue ?? "ok"),
                Quote(r.CodeHits ?? ""), renamed != null && renamed.Contains(r) ? "yes" : "no")));
            Directory.CreateDirectory(ReportFolder);
            File.WriteAllLines(Path.Combine(ReportFolder, file), lines, new UTF8Encoding(false));
        }

        private static string Quote(string value) => "\"" + value.Replace("\"", "'") + "\"";
    }
}
