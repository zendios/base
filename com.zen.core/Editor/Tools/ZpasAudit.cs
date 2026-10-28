using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Base.Tools
{
    /// <summary>
    /// Read-only ZPAS audit (ZPAS.md §6.2): names with the rules of Base > Rename Assets, folder casing, and the import
    /// settings of §5 (texture size / Read-Write / sprite mipmaps / Android format / data-map sRGB, audio load type, mesh
    /// Read-Write). Writes Logs/Zpas/audit.csv (one row per finding) and Logs/Zpas/summary.txt (counts per category).
    /// <code>Unity -batchmode -quit -nographics -projectPath . -executeMethod Base.Tools.ZpasAudit.Run [-zpasRoot Assets/Production]</code>
    /// </summary>
    public static class ZpasAudit
    {
        private const string ReportFolder = "Logs/Zpas";
        private static readonly Regex FolderName = new Regex("^[A-Z][A-Za-z0-9]*(_[A-Z0-9][A-Za-z0-9]*)*$");

        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-zpasRoot");
            var root = index >= 0 && index + 1 < args.Length ? args[index + 1].TrimEnd('/') + "/" : "Assets/";

            var rows = new List<string> { "path,category,detail" };
            var counts = new SortedDictionary<string, int>();
            void Add(string path, string category, string detail)
            {
                rows.Add($"\"{path}\",{category},\"{detail.Replace("\"", "'")}\"");
                counts[category] = counts.TryGetValue(category, out var n) ? n + 1 : 1;
            }

            var paths = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith(root) && !RenameAssetWindow.IsExcluded(p) && !p.Contains("/Editor/")).OrderBy(p => p).ToList();
            foreach (var path in paths)
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (AssetDatabase.IsValidFolder(path))
                {
                    if (!FolderName.IsMatch(name)) Add(path, "name_folder", name);
                    continue;
                }
                var issue = RenameAssetWindow.Check(RenameAssetWindow.KindOf(path), path, name);
                if (issue != null) Add(path, "name", issue);

                switch (AssetImporter.GetAtPath(path))
                {
                    case TextureImporter texture: AuditTexture(path, texture, Add); break;
                    case AudioImporter audio: AuditAudio(path, audio, Add); break;
                    case ModelImporter model when model.isReadable: Add(path, "mesh_read_write_on", ""); break;
                }
            }

            Directory.CreateDirectory(ReportFolder);
            File.WriteAllLines(Path.Combine(ReportFolder, "audit.csv"), rows, new UTF8Encoding(false));
            var summary = new StringBuilder($"root={root} assets={paths.Count} findings={rows.Count - 1}\n");
            foreach (var pair in counts) summary.AppendLine($"{pair.Key}={pair.Value}");
            File.WriteAllText(Path.Combine(ReportFolder, "summary.txt"), summary.ToString());
            Debug.Log("[Base ZPAS] audit\n" + summary);
        }

        private static void AuditTexture(string path, TextureImporter importer, System.Action<string, string, string> add)
        {
            importer.GetSourceTextureWidthAndHeight(out var width, out var height);
            if (!Mathf.IsPowerOfTwo(width) || !Mathf.IsPowerOfTwo(height)) add(path, "texture_not_pot", $"{width}x{height}");
            if (importer.isReadable) add(path, "texture_read_write_on", "");
            if (importer.mipmapEnabled && importer.textureType == TextureImporterType.Sprite) add(path, "texture_sprite_mipmaps_on", "");
            // without an Android override Unity picks the format from Android Texture Compression (ASTC by default in Unity 6)
            var android = importer.GetPlatformTextureSettings("Android");
            var format = android.overridden ? android.format : importer.GetAutomaticFormat("Android");
            if (!format.ToString().StartsWith("ASTC")) add(path, "texture_android_not_astc", (android.overridden ? "" : "automatic ") + format);
            if ((path.Contains("_normal") || path.Contains("_orm")) && importer.sRGBTexture) add(path, "texture_srgb_on_data_map", "");
        }

        private static void AuditAudio(string path, AudioImporter importer, System.Action<string, string, string> add)
        {
            var loadType = importer.defaultSampleSettings.loadType;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var length = clip != null ? clip.length : 0f;
            var music = path.Contains("bgm") || length > 20f;
            if (music && loadType == AudioClipLoadType.DecompressOnLoad) add(path, "audio_music_decompress_on_load", $"{length:F1}s");
            if (!music && loadType == AudioClipLoadType.Streaming) add(path, "audio_sfx_streaming", $"{length:F1}s");
        }
    }
}
