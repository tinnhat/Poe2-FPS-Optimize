using LibBundle3.Nodes;
using PoeRedux.Services;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PoeRedux.Patches;

public sealed class Corpse : IPatch
{
    public string Name => "Corpse Patch";
    public object Description => "Hides corpses for monster types that override their own death callbacks, including small monsters.";

    public void Apply(DirectoryNode root)
    {
        var monsters = NavigateTo(root, "metadata", "monsters") ??
            throw new InvalidDataException("Could not find metadata/monsters in the selected game data.");

        int callbackFiles = 0;
        PatchDirectory(monsters, ref callbackFiles);

        var monsterFile = monsters.Children.OfType<FileNode>()
            .FirstOrDefault(f => f.Name.Equals("monster.ot", StringComparison.OrdinalIgnoreCase)) ??
            throw new InvalidDataException("Could not find metadata/monsters/monster.ot.");

        if (callbackFiles == 0 && !Read(monsterFile).Contains("PoeRedux: hide corpses", StringComparison.Ordinal))
            throw new InvalidDataException("No compatible monster death callbacks were found; no data was written.");
    }

    private static void PatchDirectory(DirectoryNode directory, ref int callbackFiles)
    {
        foreach (var node in directory.Children)
        {
            if (node is DirectoryNode subdirectory)
            {
                PatchDirectory(subdirectory, ref callbackFiles);
                continue;
            }

            if (node is not FileNode file || !file.Name.EndsWith(".ot", StringComparison.OrdinalIgnoreCase))
                continue;

            string data = Read(file);
            string patched = PatchCallbacks(data);

            // PoE 2's base monster.ot currently has no Life block. Add the fallback there;
            // child object types with explicit callbacks are handled above.
            if (file.Name.Equals("monster.ot", StringComparison.OrdinalIgnoreCase) &&
                !patched.Contains("PoeRedux: hide corpses", StringComparison.Ordinal))
            {
                patched = patched.TrimEnd() +
                    "\n\nLife\n{\n\t// PoeRedux: hide corpses\n" +
                    "\ton_spawned_dead = { RemoveEffects(); DisableRendering(); }\n" +
                    "\ton_death = { Delay( 0.2, { RemoveEffects(); DisableRendering(); } ); }\n}\n";
            }

            if (patched == data)
                continue;

            BackupManager.RecordOriginal(file.Record);
            file.Record.Write(Encoding.Unicode.GetBytes(patched));
            callbackFiles++;
        }
    }

    internal static string PatchCallbacks(string data)
    {
        if (data.Contains("PoeRedux: hide corpse callbacks", StringComparison.Ordinal))
            return data;

        string patched = PatchBracedEvent(data, "on_death",
            " Delay( 0.2, { RemoveEffects(); DisableRendering(); } ); ");
        patched = PatchBracedEvent(patched, "on_spawned_dead",
            " RemoveEffects(); DisableRendering(); ");

        patched = Regex.Replace(patched,
            "(?m)^(?<prefix>\\s*on_death\\s*=\\s*\")(?<body>[^\"]*)(?<suffix>\")",
            "${prefix}${body} Delay( 0.2, { RemoveEffects(); DisableRendering(); } ); ${suffix}");
        patched = Regex.Replace(patched,
            "(?m)^(?<prefix>\\s*on_spawned_dead\\s*=\\s*\")(?<body>[^\"]*)(?<suffix>\")",
            "${prefix}${body} RemoveEffects(); DisableRendering(); ${suffix}");

        if (patched != data)
            patched += "\n// PoeRedux: hide corpse callbacks\n";
        return patched;
    }

    private static string PatchBracedEvent(string data, string eventName, string command)
    {
        var matches = Regex.Matches(data, $@"(?m)^\s*{Regex.Escape(eventName)}\s*=\s*\{{")
            .Cast<Match>().Reverse().ToArray();

        foreach (Match match in matches)
        {
            int open = data.IndexOf('{', match.Index);
            int close = FindMatchingBrace(data, open);
            if (close < 0) continue;
            string body = data[(open + 1)..close];
            if (body.Contains("DisableRendering", StringComparison.Ordinal)) continue;
            data = data.Insert(close, command);
        }
        return data;
    }

    private static int FindMatchingBrace(string text, int open)
    {
        int depth = 0;
        bool quoted = false;
        for (int i = open; i < text.Length; i++)
        {
            if (text[i] == '"' && (i == 0 || text[i - 1] != '\\')) quoted = !quoted;
            if (quoted) continue;
            if (text[i] == '{') depth++;
            else if (text[i] == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static string Read(FileNode file) => Encoding.Unicode.GetString(file.Record.Read().ToArray());

    private static DirectoryNode? NavigateTo(DirectoryNode root, params string[] path)
    {
        DirectoryNode current = root;
        foreach (string name in path)
        {
            var next = current.Children.OfType<DirectoryNode>()
                .FirstOrDefault(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (next is null) return null;
            current = next;
        }
        return current;
    }
}
