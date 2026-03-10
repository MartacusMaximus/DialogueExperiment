using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// Put this file in an Editor folder (Assets/Editor) so Unity compiles it as editor code.
public class DialogueCSVImporter : EditorWindow
{
    string csvPath = "Assets/dialogue.csv";
    DefaultAsset outputFolder;
    bool overwriteExisting = false;

    [MenuItem("Tools/Dialogue CSV Importer")]
    static void OpenWindow()
    {
        var window = GetWindow<DialogueCSVImporter>("Dialogue CSV Importer");
        window.minSize = new Vector2(620f, 320f);
    }

    void OnGUI()
    {
        GUILayout.Label("CSV -> DialogueNode importer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "Supported CSV columns:\n" +
            "bubbleId/Event Name, Speaker Name, Spoken Text\n" +
            "RespondsTo01, FollowUp01, SceneTriggers01\n" +
            "RespondsTo02, FollowUp02, SceneTriggers02 ... (scales to N)\n" +
            "Optional global fallback columns: Scene Triggers, Follow Up Event\n" +
            "Machine generation rule: RespondsToXX must include an R_... ID to become a machine option.",
            MessageType.Info);

        EditorGUILayout.Space();

        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("CSV file", EditorStyles.label);
        csvPath = EditorGUILayout.TextField(csvPath);

        if (GUILayout.Button("Browse CSV"))
        {
            string selected = EditorUtility.OpenFilePanel("Select dialogue CSV", Application.dataPath, "csv");
            if (!string.IsNullOrEmpty(selected))
            {
                if (selected.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
                    csvPath = "Assets" + selected.Substring(Application.dataPath.Length);
                else
                    csvPath = selected;
            }
        }

        EditorGUILayout.Space();
        GUILayout.Label("Output folder (Assets/...)", EditorStyles.label);
        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(outputFolder, typeof(DefaultAsset), false);
        if (outputFolder == null)
        {
            EditorGUILayout.HelpBox("Choose a folder inside Assets (for example: Assets/Dialogue/Imported).", MessageType.Warning);
        }
        else
        {
            string folderPath = AssetDatabase.GetAssetPath(outputFolder);
            if (!AssetDatabase.IsValidFolder(folderPath))
                EditorGUILayout.HelpBox("Selected object is not a folder asset.", MessageType.Error);
        }

        overwriteExisting = EditorGUILayout.Toggle("Overwrite existing assets", overwriteExisting);
        EditorGUILayout.EndVertical();

        GUILayout.FlexibleSpace();
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Import CSV", GUILayout.Width(160f), GUILayout.Height(40f)))
        {
            try
            {
                ValidateInputs();
                ImportCsv();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorUtility.DisplayDialog("Import Failed", ex.Message, "OK");
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    void ValidateInputs()
    {
        if (!File.Exists(GetFullCsvPath()))
            throw new Exception("CSV file not found: " + csvPath);

        if (outputFolder == null)
            throw new Exception("Select an output folder inside Assets.");

        string folderPath = AssetDatabase.GetAssetPath(outputFolder);
        if (!AssetDatabase.IsValidFolder(folderPath))
            throw new Exception("Selected output is not a valid Assets folder.");
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
        List<string[]> rows = ParseCsv(csvText);

        if (rows.Count == 0)
            throw new Exception("CSV contains no rows.");

        ColumnSchema schema;
        int startRow;
        BuildColumnSchema(rows, out schema, out startRow);

        List<ImportRow> dataRows = BuildDataRows(rows, schema, startRow);
        if (dataRows.Count == 0)
            throw new Exception("No data rows found in CSV.");

        for (int i = 0; i < dataRows.Count; i++)
            dataRows[i].GroupKey = ComputeGroupKey(dataRows[i].EventName);

        var groups = dataRows
            .GroupBy(r => r.GroupKey)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.OriginalRowIndex).ToList());

        string outFolder = AssetDatabase.GetAssetPath(outputFolder);
        if (!AssetDatabase.IsValidFolder(outFolder))
            throw new Exception("Selected output is not a valid folder.");

        int createdCount = 0;
        int updatedCount = 0;

        foreach (var kvp in groups)
        {
            string groupKey = kvp.Key;
            List<ImportRow> groupRows = kvp.Value;

            string assetName = SanitizeFileName(groupKey);
            if (string.IsNullOrWhiteSpace(assetName))
                assetName = "Dialogue_" + Guid.NewGuid().ToString("N").Substring(0, 6);

            string assetPath = Path.Combine(outFolder, assetName + ".asset").Replace("\\", "/");

            DialogueNode node = AssetDatabase.LoadAssetAtPath<DialogueNode>(assetPath);
            if (node != null && !overwriteExisting)
            {
                assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                node = null;
            }

            bool creating = node == null;
            if (creating)
                node = ScriptableObject.CreateInstance<DialogueNode>();

            node.eventGroupId = groupKey;

            List<SpeechBubble> bubbles = new List<SpeechBubble>(groupRows.Count);
            for (int i = 0; i < groupRows.Count; i++)
            {
                ImportRow row = groupRows[i];

                SpeechBubble bubble = new SpeechBubble
                {
                    bubbleId = row.EventName,
                    speakerName = row.SpeakerName,
                    text = row.SpokenText,
                    responseBranches = row.ResponseBranches,
                    appropriateResponses = row.LegacyAppropriateResponses,
                    sceneTriggers = row.GlobalSceneTriggers,
                    followUpEvent = row.GlobalFollowUpEvent,
                    autoFadeTime = 0f,
                    revealSpeed = 60f,
                    postRevealDelay = 0.25f
                };

                bubbles.Add(bubble);
            }

            node.bubbles = bubbles.ToArray();

            if (creating)
            {
                AssetDatabase.CreateAsset(node, assetPath);
                createdCount++;
            }
            else
            {
                EditorUtility.SetDirty(node);
                updatedCount++;
            }

            Debug.Log($"Dialogue CSV importer: {(creating ? "Created" : "Updated")} {assetPath} ({node.bubbles.Length} bubbles)");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Import Complete",
            $"Imported {groups.Count} DialogueNode(s).\nCreated: {createdCount}\nUpdated: {updatedCount}",
            "OK");
    }

    static void BuildColumnSchema(List<string[]> rows, out ColumnSchema schema, out int startRow)
    {
        schema = new ColumnSchema();
        startRow = 0;

        if (rows.Count == 0)
            return;

        Dictionary<string, int> header = BuildHeaderMap(rows[0]);
        bool looksLikeHeader =
            FindColumnIndex(header, "bubbleid", "bubble id", "event name", "event", "id") >= 0 &&
            FindColumnIndex(header, "speaker name", "speaker") >= 0 &&
            FindColumnIndex(header, "spoken text", "text", "dialogue text", "line", "message") >= 0;

        if (!looksLikeHeader)
        {
            schema.EventIndex = 0;
            schema.SpeakerIndex = 1;
            schema.TextIndex = 2;
            schema.LegacyResponsesIndex = 3;
            schema.GlobalSceneTriggerIndex = 4;
            schema.GlobalFollowUpIndex = 5;
            startRow = 0;
            return;
        }

        schema.EventIndex = FindColumnIndex(header, "bubbleid", "bubble id", "event name", "event", "id");
        schema.SpeakerIndex = FindColumnIndex(header, "speaker name", "speaker");
        schema.TextIndex = FindColumnIndex(header, "spoken text", "text", "dialogue text", "line", "message");

        schema.LegacyResponsesIndex = FindColumnIndex(header, "appropriate responses", "responses", "response options");
        schema.GlobalSceneTriggerIndex = FindColumnIndex(header, "scene triggers", "scene trigger", "triggers");
        schema.GlobalFollowUpIndex = FindColumnIndex(header, "follow up event", "followup event", "follow-up event", "follow up", "next event");

        foreach (var kvp in header)
        {
            string compact = CompactHeader(kvp.Key);
            int columnIndex = kvp.Value;

            Match respondsToMatch = Regex.Match(compact, @"^respondsto(\d+)$", RegexOptions.IgnoreCase);
            if (respondsToMatch.Success)
            {
                int branchIndex = ParseBranchIndex(respondsToMatch.Groups[1].Value);
                if (branchIndex > 0)
                    schema.RespondsToColumns[branchIndex] = columnIndex;
                continue;
            }

            Match followUpMatch = Regex.Match(compact, @"^followup(\d+)$", RegexOptions.IgnoreCase);
            if (followUpMatch.Success)
            {
                int branchIndex = ParseBranchIndex(followUpMatch.Groups[1].Value);
                if (branchIndex > 0)
                    schema.BranchFollowUpColumns[branchIndex] = columnIndex;
                continue;
            }

            Match sceneBranchMatch = Regex.Match(compact, @"^scenetriggers?(\d+)$", RegexOptions.IgnoreCase);
            if (sceneBranchMatch.Success)
            {
                int branchIndex = ParseBranchIndex(sceneBranchMatch.Groups[1].Value);
                if (branchIndex > 0)
                    schema.BranchSceneTriggerColumns[branchIndex] = columnIndex;
            }
        }

        startRow = 1;
    }

    static Dictionary<string, int> BuildHeaderMap(string[] firstRow)
    {
        var header = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < firstRow.Length; i++)
        {
            string normalized = NormalizeHeader(firstRow[i]);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!header.ContainsKey(normalized))
                header[normalized] = i;
        }

        return header;
    }

    static List<ImportRow> BuildDataRows(List<string[]> rows, ColumnSchema schema, int startRow)
    {
        var dataRows = new List<ImportRow>();
        var firstRowByBubbleId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int i = startRow; i < rows.Count; i++)
        {
            string[] row = rows[i];

            string eventName = ReadColumn(row, schema.EventIndex);
            string speakerName = ReadColumn(row, schema.SpeakerIndex);
            string spokenText = ReadColumn(row, schema.TextIndex);

            string globalSceneRaw = ReadColumn(row, schema.GlobalSceneTriggerIndex);
            string globalFollowUpRaw = ReadColumn(row, schema.GlobalFollowUpIndex);
            string legacyResponsesRaw = ReadColumn(row, schema.LegacyResponsesIndex);

            if (string.IsNullOrWhiteSpace(eventName) &&
                string.IsNullOrWhiteSpace(speakerName) &&
                string.IsNullOrWhiteSpace(spokenText))
            {
                continue;
            }

            string bubbleIdKey = (eventName ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(bubbleIdKey))
            {
                if (firstRowByBubbleId.TryGetValue(bubbleIdKey, out int firstRowIndex))
                {
                    Debug.LogWarning(
                        $"Dialogue CSV importer: duplicate bubbleId '{bubbleIdKey}' at CSV row {i + 1}. " +
                        $"First occurrence was row {firstRowIndex + 1}. Duplicate IDs cause ambiguous follow-ups and lookup collisions.");
                }
                else
                {
                    firstRowByBubbleId[bubbleIdKey] = i;
                }
            }

            string[] legacyResponses = ParseTokenList(legacyResponsesRaw);
            string[] globalSceneTriggers = ParseTokenList(globalSceneRaw);
            string globalFollowUp = (globalFollowUpRaw ?? string.Empty).Trim();

            DialogueResponseBranch[] branches = BuildBranchesForRow(row, schema, legacyResponses, globalFollowUp, globalSceneTriggers);

            dataRows.Add(new ImportRow
            {
                EventName = eventName,
                SpeakerName = speakerName,
                SpokenText = spokenText,
                LegacyAppropriateResponses = legacyResponses,
                GlobalSceneTriggers = globalSceneTriggers,
                GlobalFollowUpEvent = globalFollowUp,
                ResponseBranches = branches,
                OriginalRowIndex = i
            });
        }

        return dataRows;
    }

    static DialogueResponseBranch[] BuildBranchesForRow(
        string[] row,
        ColumnSchema schema,
        string[] legacyResponses,
        string legacyGlobalFollowUp,
        string[] legacyGlobalSceneTriggers)
    {
        var branches = new List<DialogueResponseBranch>();
        var branchIndexes = new SortedSet<int>();

        foreach (int key in schema.RespondsToColumns.Keys) branchIndexes.Add(key);
        foreach (int key in schema.BranchFollowUpColumns.Keys) branchIndexes.Add(key);
        foreach (int key in schema.BranchSceneTriggerColumns.Keys) branchIndexes.Add(key);

        foreach (int branchIndex in branchIndexes)
        {
            string respondsRaw = ReadColumn(row, GetValueOrDefault(schema.RespondsToColumns, branchIndex));
            string followRaw = ReadColumn(row, GetValueOrDefault(schema.BranchFollowUpColumns, branchIndex));
            string sceneRaw = ReadColumn(row, GetValueOrDefault(schema.BranchSceneTriggerColumns, branchIndex));

            string[] respondsTo = ParseTokenList(respondsRaw);
            string[] sceneTriggers = ParseTokenList(sceneRaw);
            string followUp = (followRaw ?? string.Empty).Trim();

            if (respondsTo.Length == 0 && sceneTriggers.Length == 0 && string.IsNullOrWhiteSpace(followUp))
                continue;

            branches.Add(new DialogueResponseBranch
            {
                branchKey = branchIndex.ToString("00"),
                respondsTo = respondsTo,
                followUpEvent = followUp,
                sceneTriggers = sceneTriggers
            });
        }

        // Legacy fallback: old "Appropriate Responses" column becomes branch 01.
        if (branches.Count == 0 && legacyResponses != null && legacyResponses.Length > 0)
        {
            branches.Add(new DialogueResponseBranch
            {
                branchKey = "01",
                respondsTo = legacyResponses,
                followUpEvent = legacyGlobalFollowUp,
                sceneTriggers = legacyGlobalSceneTriggers
            });
        }

        return branches.Count == 0 ? Array.Empty<DialogueResponseBranch>() : branches.ToArray();
    }

    static int GetValueOrDefault(Dictionary<int, int> map, int key)
    {
        if (map.TryGetValue(key, out int value))
            return value;
        return -1;
    }

    static int FindColumnIndex(Dictionary<string, int> header, params string[] aliases)
    {
        for (int i = 0; i < aliases.Length; i++)
        {
            string key = NormalizeHeader(aliases[i]);
            if (header.TryGetValue(key, out int idx))
                return idx;
        }

        return -1;
    }

    static int ParseBranchIndex(string digits)
    {
        if (!int.TryParse(digits, out int index))
            return -1;

        return index;
    }

    static string ReadColumn(string[] row, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= row.Length)
            return string.Empty;

        return row[columnIndex]?.Trim() ?? string.Empty;
    }

    static string NormalizeHeader(string value)
    {
        return (value ?? string.Empty)
            .Replace("\uFEFF", string.Empty)
            .Trim()
            .ToLowerInvariant();
    }

    static string CompactHeader(string normalized)
    {
        return Regex.Replace(normalized ?? string.Empty, @"[\s_\-]", string.Empty);
    }

    static string[] ParseTokenList(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        string[] tokens = value
            .Split(new[] { ',', ';', '|', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return tokens.Length == 0 ? Array.Empty<string>() : tokens;
    }

    static string ComputeGroupKey(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
            return "EmptyEvent";

        string id = eventName.Trim();
        id = Regex.Replace(id, @"^[A-Za-z]_", "", RegexOptions.IgnoreCase);
        id = Regex.Replace(id, @"\d+$", string.Empty);
        id = id.Trim('_', '-', ' ');

        if (string.IsNullOrEmpty(id))
            return eventName.Trim();

        return id;
    }

    static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c.ToString(), "_");

        return name.Trim();
    }

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
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
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

    class ColumnSchema
    {
        public int EventIndex = -1;
        public int SpeakerIndex = -1;
        public int TextIndex = -1;
        public int LegacyResponsesIndex = -1;
        public int GlobalSceneTriggerIndex = -1;
        public int GlobalFollowUpIndex = -1;

        public readonly Dictionary<int, int> RespondsToColumns = new Dictionary<int, int>();
        public readonly Dictionary<int, int> BranchFollowUpColumns = new Dictionary<int, int>();
        public readonly Dictionary<int, int> BranchSceneTriggerColumns = new Dictionary<int, int>();
    }

    class ImportRow
    {
        public string EventName;
        public string SpeakerName;
        public string SpokenText;

        public string[] LegacyAppropriateResponses;
        public string[] GlobalSceneTriggers;
        public string GlobalFollowUpEvent;

        public DialogueResponseBranch[] ResponseBranches;

        public int OriginalRowIndex;
        public string GroupKey;
    }
}
