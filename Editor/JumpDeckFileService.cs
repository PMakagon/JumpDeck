using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace JumpDeck.Editor
{
    internal static class JumpDeckFileService
    {
        internal const string FileExtension = "jumpdeck";

        private const int CurrentFormatVersion = 1;
        private const long MaximumFileSize = 2 * 1024 * 1024;
        private const string ExportDirectoryKey = "JumpDeck.ExportDirectory";
        private const string ImportDirectoryKey = "JumpDeck.ImportDirectory";
        private const string ObjectKind = "object";
        private const string SceneQueryKind = "sceneQuery";

        [Serializable]
        private sealed class DeckFile
        {
            public int formatVersion = CurrentFormatVersion;
            public string deckName;
            public List<PinFile> pins = new();
        }

        [Serializable]
        private sealed class PinFile
        {
            public string kind;
            public string displayName;
            public string globalObjectId;
            public string assetGuid;
            public long localFileId;
            public string fallbackPath;
            public string query;
            public int queryScope;
            public bool hasClickAction;
            public int clickAction;
        }

        internal static bool ExportDeck(JumpDeckCollection deck, out string exportedPath)
        {
            exportedPath = null;
            if (deck == null)
                return false;

            string initialDirectory = GetExistingDirectory(
                EditorPrefs.GetString(ExportDirectoryKey, Application.dataPath),
                Application.dataPath);
            string path = EditorUtility.SaveFilePanel(
                "Export JumpDeck Deck",
                initialDirectory,
                SanitizeFileName(deck.DisplayName),
                FileExtension);

            if (string.IsNullOrEmpty(path))
                return false;

            try
            {
                DeckFile file = CreateFile(deck);
                string json = JsonUtility.ToJson(file, true);
                File.WriteAllText(path, json + Environment.NewLine, new UTF8Encoding(false));
                EditorPrefs.SetString(ExportDirectoryKey, Path.GetDirectoryName(path) ?? initialDirectory);
                ImportAssetIfInsideProject(path);
                exportedPath = path;
                return true;
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog(
                    "JumpDeck Export Failed",
                    exception.Message,
                    "OK");
                return false;
            }
        }

        internal static bool ImportDeckFromDialog(
            JumpDeckData data,
            out JumpDeckCollection importedDeck)
        {
            importedDeck = null;
            string initialDirectory = GetExistingDirectory(
                EditorPrefs.GetString(ImportDirectoryKey, Application.dataPath),
                Application.dataPath);
            string path = EditorUtility.OpenFilePanel(
                "Import JumpDeck Deck",
                initialDirectory,
                FileExtension);

            if (string.IsNullOrEmpty(path))
                return false;

            return ImportDeck(data, path, out importedDeck);
        }

        internal static bool ImportDeck(
            JumpDeckData data,
            string path,
            out JumpDeckCollection importedDeck)
        {
            importedDeck = null;

            if (!TryReadFile(path, out DeckFile file, out string error) ||
                !TryBuildDeck(file, path, out JumpDeckCollection deck, out ImportSummary summary, out error))
            {
                EditorUtility.DisplayDialog("JumpDeck Import Failed", error, "OK");
                return false;
            }

            deck.DisplayName = MakeUniqueDeckName(data, deck.DisplayName);
            string unresolvedLine = summary.UnresolvedObjects > 0
                ? $"\n{summary.UnresolvedObjects} unresolved object reference(s) will be kept for later resolution."
                : string.Empty;
            string message =
                $"Import \"{file.deckName}\" as \"{deck.DisplayName}\"?\n\n" +
                $"Object pins: {summary.ObjectPins}\n" +
                $"Live Pins: {summary.QueryPins}" +
                unresolvedLine;

            if (!EditorUtility.DisplayDialog(
                    "Import JumpDeck Deck",
                    message,
                    "Import as New Deck",
                    "Cancel"))
            {
                return false;
            }

            Undo.RecordObject(data, "Import JumpDeck Deck");
            data.Decks.Add(deck);
            data.SaveData();
            EditorPrefs.SetString(
                ImportDirectoryKey,
                Path.GetDirectoryName(Path.GetFullPath(path)) ?? Application.dataPath);
            importedDeck = deck;
            return true;
        }

        internal static bool IsDeckFile(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   string.Equals(Path.GetExtension(assetPath), $".{FileExtension}", StringComparison.OrdinalIgnoreCase);
        }

        private static DeckFile CreateFile(JumpDeckCollection deck)
        {
            var file = new DeckFile
            {
                deckName = deck.DisplayName
            };

            foreach (JumpPin pin in deck.Pins)
            {
                file.pins.Add(new PinFile
                {
                    kind = pin.Kind == JumpPinKind.SceneQuery ? SceneQueryKind : ObjectKind,
                    displayName = pin.DisplayName,
                    globalObjectId = pin.GlobalObjectId,
                    assetGuid = pin.AssetGuid,
                    localFileId = pin.LocalFileId,
                    fallbackPath = pin.FallbackPath,
                    query = pin.Query,
                    queryScope = (int)pin.QueryScope,
                    hasClickAction = pin.HasClickAction,
                    clickAction = (int)pin.ClickAction
                });
            }

            return file;
        }

        private static bool TryReadFile(
            string path,
            out DeckFile file,
            out string error)
        {
            file = null;
            error = null;

            try
            {
                string fullPath = Path.GetFullPath(path);
                var info = new FileInfo(fullPath);
                if (!info.Exists)
                {
                    error = $"File does not exist:\n{fullPath}";
                    return false;
                }

                if (info.Length > MaximumFileSize)
                {
                    error = "Deck file is larger than the supported 2 MB limit.";
                    return false;
                }

                file = JsonUtility.FromJson<DeckFile>(File.ReadAllText(fullPath, Encoding.UTF8));
                if (file == null)
                {
                    error = "The file does not contain a valid JumpDeck deck.";
                    return false;
                }

                if (file.formatVersion != CurrentFormatVersion)
                {
                    error =
                        $"Unsupported JumpDeck format version {file.formatVersion}. " +
                        $"This version supports format {CurrentFormatVersion}.";
                    return false;
                }

                file.pins ??= new List<PinFile>();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryBuildDeck(
            DeckFile file,
            string sourcePath,
            out JumpDeckCollection deck,
            out ImportSummary summary,
            out string error)
        {
            string fallbackName = Path.GetFileNameWithoutExtension(sourcePath);
            string deckName = string.IsNullOrWhiteSpace(file.deckName)
                ? fallbackName
                : file.deckName.Trim();
            deck = JumpDeckCollection.Create(string.IsNullOrWhiteSpace(deckName) ? "Imported Deck" : deckName);
            summary = default;
            error = null;

            for (int index = 0; index < file.pins.Count; index++)
            {
                PinFile source = file.pins[index];
                if (source == null)
                {
                    error = $"Pin #{index + 1} is invalid.";
                    return false;
                }

                string displayName = string.IsNullOrWhiteSpace(source.displayName)
                    ? "Untitled Pin"
                    : source.displayName.Trim();

                if (string.Equals(source.kind, ObjectKind, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(source.globalObjectId) && string.IsNullOrEmpty(source.assetGuid))
                    {
                        error = $"Object pin \"{displayName}\" has no object identifier.";
                        return false;
                    }

                    if (source.hasClickAction &&
                        !Enum.IsDefined(typeof(JumpPinClickAction), source.clickAction))
                    {
                        error = $"Object pin \"{displayName}\" has an unsupported click action.";
                        return false;
                    }

                    JumpPin pin = JumpPin.ForObject(
                        displayName,
                        source.globalObjectId,
                        source.assetGuid,
                        source.localFileId,
                        source.fallbackPath,
                        source.hasClickAction,
                        source.hasClickAction
                            ? (JumpPinClickAction)source.clickAction
                            : JumpPinClickAction.InspectOrOpen);
                    deck.Pins.Add(pin);
                    summary.ObjectPins++;
                    if (JumpDeckObjectUtility.Resolve(pin) == null)
                        summary.UnresolvedObjects++;
                }
                else if (string.Equals(source.kind, SceneQueryKind, StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrWhiteSpace(source.query))
                    {
                        error = $"Live Pin \"{displayName}\" has an empty query.";
                        return false;
                    }

                    SceneQueryScope scope = Enum.IsDefined(typeof(SceneQueryScope), source.queryScope)
                        ? (SceneQueryScope)source.queryScope
                        : SceneQueryScope.AllLoadedScenes;
                    deck.Pins.Add(JumpPin.ForQuery(displayName, source.query.Trim(), scope));
                    summary.QueryPins++;
                }
                else
                {
                    error = $"Pin \"{displayName}\" has unsupported kind \"{source.kind}\".";
                    return false;
                }
            }

            return true;
        }

        private static string MakeUniqueDeckName(JumpDeckData data, string requestedName)
        {
            var existingNames = new HashSet<string>(
                data.Decks.Select(deck => deck.DisplayName),
                StringComparer.OrdinalIgnoreCase);
            if (!existingNames.Contains(requestedName))
                return requestedName;

            for (int suffix = 2; ; suffix++)
            {
                string candidate = $"{requestedName} ({suffix})";
                if (!existingNames.Contains(candidate))
                    return candidate;
            }
        }

        private static string SanitizeFileName(string value)
        {
            string source = string.IsNullOrWhiteSpace(value) ? "JumpDeck" : value.Trim();
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            string sanitized = new string(source
                .Select(character => invalidCharacters.Contains(character) ? '_' : character)
                .ToArray());
            return string.IsNullOrWhiteSpace(sanitized) ? "JumpDeck" : sanitized;
        }

        private static string GetExistingDirectory(string candidate, string fallback)
        {
            return !string.IsNullOrEmpty(candidate) && Directory.Exists(candidate)
                ? candidate
                : fallback;
        }

        private static void ImportAssetIfInsideProject(string path)
        {
            string fullPath = Path.GetFullPath(path).Replace('\\', '/');
            string assetsPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/');
            if (!fullPath.StartsWith(assetsPath + "/", StringComparison.OrdinalIgnoreCase))
                return;

            string assetPath = "Assets" + fullPath.Substring(assetsPath.Length);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private struct ImportSummary
        {
            internal int ObjectPins;
            internal int QueryPins;
            internal int UnresolvedObjects;
        }
    }
}
