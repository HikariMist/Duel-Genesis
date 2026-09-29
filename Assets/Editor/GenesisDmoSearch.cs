#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>DEV: finds files in the recovered export (and the project's card-art folder) whose name or text mentions given card names.</summary>
    public static class GenesisDmoSearch
    {
        private const string ExportRoot = @"C:\Games\DMO\DMO_Recovered\ExportedProject";
        private static readonly string[] Terms =
        {
            "Relinquish", "Performance of Sword", "Flame Lord", "Skull Guardian", "Dokurorider", "Shinato", "Crab Turtle", "Paladin of White",
            "Black Illusion", "Commencement Dance", "Incandescent", "Novox", "Turtle Oath", "White Dragon Ritual"
        };

        [MenuItem("Duel Genesis/DEV/Search Recovered Export For Ritual Cards")]
        public static void Search()
        {
            var sb = new StringBuilder();
            string project = Directory.GetParent(Application.dataPath).FullName;
            foreach (string root in new[] { ExportRoot, Path.Combine(project, "Cards") })
            {
                if (!Directory.Exists(root)) { sb.AppendLine("missing: " + root); continue; }
                sb.AppendLine("== file names under " + root);
                string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
                sb.AppendLine($"({files.Length} files)");
                foreach (string f in files)
                {
                    string name = Path.GetFileName(f).Replace('_', ' ');
                    if (Terms.Any(t => name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0))
                        sb.AppendLine(f + "  (" + new FileInfo(f).Length + " bytes)");
                }
                // Card lists / databases inside text assets.
                sb.AppendLine("== text mentions under " + root);
                foreach (string f in files.Where(f => f.EndsWith(".json") || f.EndsWith(".txt") || f.EndsWith(".csv") || f.EndsWith(".asset") || f.EndsWith(".bytes")))
                {
                    var info = new FileInfo(f);
                    if (info.Length > 30_000_000) continue;
                    string text;
                    try { text = File.ReadAllText(f); } catch { continue; }
                    var hits = Terms.Where(t => text.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    if (hits.Count > 0) sb.AppendLine($"{f} ({info.Length} bytes): {string.Join(", ", hits)}");
                }
            }
            Directory.CreateDirectory(Path.Combine(project, "Logs"));
            File.WriteAllText(Path.Combine(project, "Logs", "DG-DmoSearch.txt"), sb.ToString());
            Debug.Log("Duel: Genesis search finished: Logs/DG-DmoSearch.txt");
        }
    }
}
#endif
