using System.Xml;

namespace CC2CheatGUI.Core;

public sealed class SaveSlot
{
    public required string DisplayName { get; init; }
    public required string SaveXmlPath { get; init; }
    public required string FolderName { get; init; }

    public override string ToString() => DisplayName;
}

/// <summary>Locates Carrier Command 2 save folders and slots on disk.</summary>
public static class SaveLocator
{
    // The game folder lives directly under %APPDATA% (Roaming).
    private static readonly string[] RootCandidates = { "Carrier Command 2" };

    // Different builds/guides reference these parent folder spellings.
    private static readonly string[] SavesDirCandidates = { "saved_games", "save_games", "saved_game" };

    public static string? FindRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (var name in RootCandidates)
        {
            var path = System.IO.Path.Combine(appData, name);
            if (Directory.Exists(path)) return path;
        }
        return null;
    }

    public static List<SaveSlot> FindSlots()
    {
        var slots = new List<SaveSlot>();
        var root = FindRoot();
        if (root == null) return slots;

        var names = ReadSlotNames(root);

        foreach (var savesDirName in SavesDirCandidates)
        {
            var savesDir = System.IO.Path.Combine(root, savesDirName);
            if (!Directory.Exists(savesDir)) continue;

            foreach (var slotDir in Directory.EnumerateDirectories(savesDir))
            {
                var savePath = LocateSaveXml(slotDir);
                if (savePath == null) continue;

                var folder = new DirectoryInfo(slotDir).Name;
                names.TryGetValue(folder, out var friendly);
                var display = string.IsNullOrWhiteSpace(friendly)
                    ? folder
                    : $"{friendly}  ({folder})";

                slots.Add(new SaveSlot
                {
                    DisplayName = display,
                    SaveXmlPath = savePath,
                    FolderName = folder,
                });
            }
        }

        slots.Sort((a, b) => string.CompareOrdinal(a.FolderName, b.FolderName));
        return slots;
    }

    private static string? LocateSaveXml(string slotDir)
    {
        var direct = System.IO.Path.Combine(slotDir, "save.xml");
        if (File.Exists(direct)) return direct;

        // Fall back to the largest .xml in the folder (covers oddly-named builds).
        string? best = null;
        long bestSize = -1;
        foreach (var xml in Directory.EnumerateFiles(slotDir, "*.xml"))
        {
            var size = new FileInfo(xml).Length;
            if (size > bestSize) { bestSize = size; best = xml; }
        }
        return best;
    }

    /// <summary>
    /// Best-effort map of slot-folder name -> friendly save name, read from the top-level
    /// persistent_data.xml (also spelled persistant_data.xml). Returns an empty map on any problem.
    /// </summary>
    private static Dictionary<string, string> ReadSlotNames(string root)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fileName in new[] { "persistent_data.xml", "persistant_data.xml" })
        {
            var path = System.IO.Path.Combine(root, fileName);
            if (!File.Exists(path)) continue;
            try
            {
                var doc = new XmlDocument();
                doc.Load(path);
                // Look for any element that references a slot folder and carries a display name.
                var all = doc.SelectNodes("//*");
                if (all == null) continue;
                foreach (XmlNode node in all)
                {
                    if (node is not XmlElement el) continue;
                    string folder = FirstAttr(el, "save_name", "folder", "path", "directory", "id");
                    string display = FirstAttr(el, "display_name", "name", "title");
                    if (folder.Length > 0 && display.Length > 0 && folder.Contains("slot"))
                        map[NormalizeFolder(folder)] = display;
                }
            }
            catch
            {
                // Ignore — friendly names are optional.
            }
        }
        return map;
    }

    private static string FirstAttr(XmlElement el, params string[] names)
    {
        foreach (var n in names)
        {
            var v = el.GetAttribute(n);
            if (v.Length > 0) return v;
        }
        return "";
    }

    private static string NormalizeFolder(string raw)
    {
        raw = raw.Replace('/', '\\');
        var idx = raw.LastIndexOf('\\');
        return idx >= 0 ? raw[(idx + 1)..] : raw;
    }
}
