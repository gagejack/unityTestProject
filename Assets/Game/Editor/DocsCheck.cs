using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Confirms that every file path and component named in the docs still exists.
    /// Rules (see AGENTS.md): paths are written as Markdown links or in backticks starting with a
    /// project folder (Assets/, docs/, Packages/, ProjectSettings/); a backticked single PascalCase
    /// word is treated as a Unity type name (component or asset type) and must exist.
    /// </summary>
    public static class DocsCheck
    {
        static readonly string[] PathRoots = { "Assets/", "docs/", "Packages/", "ProjectSettings/" };
        static readonly Regex Link = new Regex(@"\]\(([^)\s]+)\)");
        static readonly Regex Code = new Regex(@"`([^`\r\n]+)`");
        static readonly Regex TypeName = new Regex(@"^[A-Z][a-z0-9]+(?:[A-Z][A-Za-z0-9]*)+$");

        [MenuItem("Tools/Project/Check Docs")]
        public static void RunFromMenu()
        {
            var problems = Run();
            if (problems.Count == 0) Debug.Log("Check Docs: all paths and components named in the docs exist.");
            else Debug.LogError("Check Docs found " + problems.Count + " problem(s):\n" + string.Join("\n", problems));
        }

        /// <summary>Returns one line per problem; empty when the docs are consistent.</summary>
        public static List<string> Run()
        {
            string root = Path.GetDirectoryName(Application.dataPath);
            var docs = new List<string> { "README.md", "AGENTS.md", "CLAUDE.md" };
            docs.AddRange(Directory.GetFiles(Path.Combine(root, "docs"), "*.md", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
                .Where(p => p != "docs/HANDOFF_PROMPT.md"));
            var typeNames = new HashSet<string>(TypeCache.GetTypesDerivedFrom<Object>().Select(t => t.Name));
            // Game interfaces and namespaces count as known names too.
            foreach (var type in System.AppDomain.CurrentDomain.GetAssemblies()
                         .Where(a => a.GetName().Name.StartsWith("Game.") || a.GetName().Name.StartsWith("Assembly-CSharp"))
                         .SelectMany(a => a.GetTypes()))
            {
                typeNames.Add(type.Name);
                if (type.Namespace != null) foreach (string part in type.Namespace.Split('.')) typeNames.Add(part);
            }
            var problems = new List<string>();
            foreach (string doc in docs)
            {
                string full = Path.Combine(root, doc);
                if (!File.Exists(full)) { problems.Add(doc + ": file is missing"); continue; }
                string text = File.ReadAllText(full);
                string folder = Path.GetDirectoryName(full);
                foreach (Match m in Link.Matches(text))
                {
                    string target = m.Groups[1].Value.Split('#')[0];
                    if (target.Length == 0 || target.Contains("://") || target.StartsWith("mailto:")) continue;
                    if (!Exists(folder, System.Uri.UnescapeDataString(target))) problems.Add(doc + ": broken link " + target);
                }
                foreach (Match m in Code.Matches(text))
                {
                    string value = m.Groups[1].Value.Trim();
                    if (PathRoots.Any(value.StartsWith))
                    {
                        if (!Exists(root, value.TrimEnd('/'))) problems.Add(doc + ": missing path " + value);
                    }
                    else if (TypeName.IsMatch(value) && !typeNames.Contains(value))
                        problems.Add(doc + ": unknown component or type " + value);
                }
            }
            return problems;
        }

        static readonly char[] Invalid = Path.GetInvalidPathChars().Concat(new[] { '<', '>', '*', '?', '|', '"' }).ToArray();

        static bool Exists(string baseFolder, string relative)
        {
            if (relative.IndexOfAny(Invalid) >= 0) return false;
            string full = Path.GetFullPath(Path.Combine(baseFolder, relative));
            return File.Exists(full) || Directory.Exists(full);
        }
    }
}
