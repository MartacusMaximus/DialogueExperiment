// Place in Assets/Editor/DialogueCSVImporter.cs

using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System;

/// IMPORTANT: This script expects your runtime types to already exist:
/// - DialogueNode (ScriptableObject)
/// - SpeechBubble (serializable class)
/// - ExpectedResponseType (enum)
///
/// Put this file in an Editor folder (Assets/Editor) so Unity compiles it as editor code.
public class DialogueCSVImporter : EditorWindow
{
    string csvPath = "Assets/dialogue.csv";
    DefaultAsset outputFolder;
    bool overwriteExisting = false;
    Vector2 scroll;

    [MenuItem("Tools/Dialogue CSV Importer")]
    static void OpenWindow()
    {
        var w = GetWindow<DialogueCSVImporter>("Dialogue CSV Importer");
        w.minSize = new Vector2(520, 260);
    }

    void OnGUI()
    {
        GUILayout.Label("CSV -> DialogueNode importer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "CSV columns (header names expected):\n" +
            "Event Name, Speaker Name, Spoken Text, Expected Response\n\n" +
            "Event grouping rule: leading 'D_' removed and trailing digits stripped\n" +
            "Example: D_Example0, D_Example1 -> Example (one DialogueNode with two bubbles).",
            MessageType.Info);

        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("CSV file", EditorStyles.label);
        csvPath = EditorGUILayout.TextField(csvPath);

        if (GUILayout.Button("Browse CSV"))
        {
            string p = EditorUtility.OpenFilePanel("Select dialogue CSV", Application.dataPath, "csv");
            if (!string.IsNullOrEmpty(p))
            {
                // Convert absolute path to project relative if possible
                if (p.StartsWith(Application.dataPath))
                    csvPath = "Assets" + p.Substring(Application.dataPath.Length);
                else
                    csvPath = p;
            }
        }

        EditorGUILayout.Space();
        GUILayout.Label("Output folder (Assets/...)", EditorStyles.label);
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(outputFolder, typeof(DefaultAsset), false);
        if (outputFolder == null)
        {
            EditorGUILayout.HelpBox("Choose a folder inside Assets (e.g. Assets/Dialogue/Imported).", MessageType.Warning);
        }
        else
        {
            string folderPath = AssetDatabase.GetAssetPath(outputFolder);
            if (!AssetDatabase.IsValidFolder(folderPath))
                EditorGUILayout.HelpBox("Selected object is not a folder. Choose a folder asset.", MessageType.Error);
        }

        GUILayout.Space(6);
        overwriteExisting = EditorGUILayout.Toggle("Overwrite existing assets", overwriteExisting);

        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Import CSV", GUILayout.Width(140), GUILayout.Height(36)))
        {
            if (!File.Exists(GetFullCsvPath()))
            {
                EditorUtility.DisplayDialog("Error", "CSV file not found: " + csvPath, "OK");
            }
            else if (outputFolder == null)
            {
                EditorUtility.DisplayDialog("Error", "Select an output folder inside Assets.", "OK");
            }
            else
            {
                try
                {
                    ImportCsv();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog("Import Failed", ex.Message, "OK");
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    string GetFullCsvPath()
    {
        if (Path.IsPathRooted(csvPath)) return csvPath;
        return Path.Combine(Directory.GetCurrentDirectory(), csvPath);
    }

    void ImportCsv()
    {
        string fullPath = GetFullCsvPath();
        string csvText = File.ReadAllText(fullPath, Encoding.UTF8);
        var rows = ParseCsv(csvText);

        if (rows.Count == 0)
            throw new Exception("CSV contains no rows.");

        // header detection
        var header = rows[0].Select(s => s.Trim()).ToArray();
        int startRow = 1;
        bool hasHeader = false;
        string[] expectedHeaders = new[] { "Event Name", "Speaker Name", "Spoken Text", "Expected Response" };
        // Basic header match
        if (header.Length >= 4 && expectedHeaders.All(h => header.Any(c => string.Equals(c, h, StringComparison.OrdinalIgnoreCase))))
        {
            hasHeader = true;
            startRow = 1;
        }
        else
        {
            // assume no header, use row 0 as first data row
            startRow = 0;
        }

        // Build list of structured rows
        var dataRows = new List<ImportRow>();
        for (int i = startRow; i < rows.Count; i++)
        {
            var r = rows[i];
            // ensure at least 4 columns available
            string eventName = r.Length > 0 ? r[0].Trim() : "";
            string speakerName = r.Length > 1 ? r[1].Trim() : "";
            string spokenText = r.Length > 2 ? r[2].Trim() : "";
            string expectedResp = r.Length > 3 ? r[3].Trim() : "";

            // skip completely empty rows
            if (string.IsNullOrWhiteSpace(eventName) && string.IsNullOrWhiteSpace(speakerName) && string.IsNullOrWhiteSpace(spokenText))
                continue;

            dataRows.Add(new ImportRow()
            {
                EventName = eventName,
                SpeakerName = speakerName,
                SpokenText = spokenText,
                ExpectedResponse = expectedResp,
                OriginalRowIndex = i
            });
        }

        if (dataRows.Count == 0)
            throw new Exception("No data rows found in CSV.");

        // Grouping: map event name -> group key (strip leading D_ and trailing digits)
        foreach (var r in dataRows)
            r.GroupKey = ComputeGroupKey(r.EventName);

        // Group and sort rows in each group by original EventName and row order (to keep sequence)
        var groups = dataRows
            .GroupBy(r => r.GroupKey)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.EventName).ThenBy(x => x.OriginalRowIndex).ToList());

        // Determine output folder path
        string outFolder = AssetDatabase.GetAssetPath(outputFolder);
        if (!AssetDatabase.IsValidFolder(outFolder))
        {
            throw new Exception("Selected output is not a valid folder.");
        }

        // For each group, create or update a DialogueNode asset
        foreach (var kvp in groups)
        {
            string groupKey = kvp.Key;
            var rowsForGroup = kvp.Value;

            string assetName = SanitizeFileName(groupKey);
            if (string.IsNullOrEmpty(assetName))
                assetName = "Dialogue_" + Guid.NewGuid().ToString("N").Substring(0, 6);

            string assetPath = Path.Combine(outFolder, assetName + ".asset");
            assetPath = assetPath.Replace("\\", "/");

            // If exists and not allowed to overwrite, create unique
            if (File.Exists(assetPath) && !overwriteExisting)
            {
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
            }

            // Create DialogueNode
            var node = ScriptableObject.CreateInstance<DialogueNode>();

            // Response rules left empty for now
            // Build SpeechBubble[] from rowsForGroup
            var bubbles = new List<SpeechBubble>();
            foreach (var row in rowsForGroup)
            {
                var sb = new SpeechBubble();
                sb.speakerName = row.SpeakerName;
                sb.text = row.SpokenText;

                // default bubbleType
                sb.bubbleType = DialogueBubbleType.Monologue;

                // parse expected response string
                sb.expectedResponse = ParseExpectedResponse(row.ExpectedResponse);

                // defaults
                sb.autoFadeTime = 5f;
                sb.revealSpeed = 60f;
                sb.postRevealDelay = 0.25f;
                sb.followUp = null;

                bubbles.Add(sb);
            }

            node.bubbles = bubbles.ToArray();

            // Create or overwrite asset
            AssetDatabase.CreateAsset(node, assetPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created DialogueNode asset: {assetPath} with {node.bubbles.Length} bubbles.");
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Import Complete", $"Imported {groups.Count} DialogueNode(s).", "OK");
    }

    static string ComputeGroupKey(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName)) return "EmptyEvent";

        // remove leading D_ or d_
        string s = Regex.Replace(eventName.Trim(), @"^D_", "", RegexOptions.IgnoreCase);

        // remove trailing digits
        s = Regex.Replace(s, @"\d+$", "");

        // trim separators and whitespace
        s = s.Trim('_', '-', ' ');

        // fallback
        if (string.IsNullOrEmpty(s)) return eventName.Trim();

        return s;
    }

    static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "_");
        return name.Trim();
    }

    static ExpectedResponseType ParseExpectedResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return ExpectedResponseType.None;

        // try exact enum parse ignoring case
        if (Enum.TryParse(typeof(ExpectedResponseType), text, true, out var parsed))
            return (ExpectedResponseType)parsed;

        // common synonyms
        var t = text.Trim().ToLowerInvariant();
        switch (t)
        {
            case "none":
            case "n":
            case "-":
            case "0":
                return ExpectedResponseType.None;
            case "dialoguechoice":
            case "choice":
            case "choiceonly":
            case "dialogue_choice":
            case "dialogue choice":
                return ExpectedResponseType.DialogueChoice;
            case "performaction":
            case "action":
            case "perform_action":
                return ExpectedResponseType.PerformAction;
            case "timeronly":
            case "timer":
            case "timed":
                return ExpectedResponseType.TimerOnly;
            default:
                Debug.LogWarning($"CSV importer: unknown ExpectedResponse '{text}', defaulting to None.");
                return ExpectedResponseType.None;
        }
    }

    // Simple CSV parser that handles quotes and commas.
    static List<string[]> ParseCsv(string csv)
    {
        var lines = new List<string[]>();
        using (var reader = new StringReader(csv))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                lines.Add(ParseCsvLine(line));
            }
        }
        return lines;
    }

    // robust enough for typical Excel CSVs (handles quoted values with commas and double quotes)
    static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        if (line == null) return fields.ToArray();

        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                // if this is a double quote inside a quoted field, consume next quote
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++; // skip next
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Length = 0;
            }
            else
            {
                sb.Append(c);
            }
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }

    class ImportRow
    {
        public string EventName;
        public string SpeakerName;
        public string SpokenText;
        public string ExpectedResponse;
        public int OriginalRowIndex;
        public string GroupKey;
    }
}