using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using static JumpDeck.Editor.JumpDeckFileCodec;

namespace JumpDeck.Editor
{
    internal static class JumpDeckFileService
    {
        internal const string FileExtension = "jumpdeck";

        private const string ExportDirectoryKey = "JumpDeck.ExportDirectory";
        private const string ImportDirectoryKey = "JumpDeck.ImportDirectory";

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
                string json = Serialize(deck);
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
            IJumpDeckStorage data,
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
            IJumpDeckStorage data,
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

            Undo.RecordObject(data.UndoTarget, "Import JumpDeck Deck");
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

        private static string MakeUniqueDeckName(IJumpDeckStorage data, string requestedName)
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
    }
}
