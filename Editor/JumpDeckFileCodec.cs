using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace JumpDeck.Editor
{
    public static class JumpDeckFileCodec
    {
        private const int CurrentFormatVersion = 1;
        private const long MaximumFileSize = 2 * 1024 * 1024;
        private const string ObjectKind = "object";
        private const string SceneQueryKind = "sceneQuery";

        public static string Serialize(JumpDeckCollection deck) => JsonUtility.ToJson(CreateFile(deck), true);

        public static bool TryDeserialize(string json, out JumpDeckCollection deck, out string error)
        {
            deck = null;
            if (!TryReadJson(json, out var file, out error)) return false;
            if (!TryBuildDeck(file, "Imported Deck.jumpdeck", out var imported, out _, out error)) return false;
            deck = imported;
            return true;
        }

        [Serializable]
        public sealed class DeckFile
        {
            public int formatVersion;
            public string deckName;
            public bool locked;
            public JumpDeckDisplaySettings displaySettings;
            public List<PinFile> pins = new();
        }

        [Serializable]
        public sealed class PinFile
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

        private static DeckFile CreateFile(JumpDeckCollection deck)
        {
            var file = new DeckFile
            {
                formatVersion = CurrentFormatVersion,
                deckName = deck.DisplayName,
                locked = deck.Locked,
                displaySettings = deck.DisplaySettings
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

        internal static bool TryReadFile(
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

                return TryReadJson(File.ReadAllText(fullPath, Encoding.UTF8), out file, out error);
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool TryReadJson(string json, out DeckFile file, out string error)
        {
            file = null;
            error = null;
            try
            {
                file = JsonUtility.FromJson<DeckFile>(json);
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

        internal static bool TryBuildDeck(
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
            if (file.displaySettings != null)
                deck.DisplaySettings.Update(file.displaySettings.IconSize,
                    file.displaySettings.ShowPingButton, file.displaySettings.ShowOpenButton);
            deck.Locked = file.locked;
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

                    if (!Enum.IsDefined(typeof(SceneQueryScope), source.queryScope))
                    {
                        error = $"Live Pin \"{displayName}\" has an unsupported scene scope.";
                        return false;
                    }
                    deck.Pins.Add(JumpPin.ForQuery(displayName, source.query.Trim(), (SceneQueryScope)source.queryScope));
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

        internal struct ImportSummary
        {
            internal int ObjectPins;
            internal int QueryPins;
            internal int UnresolvedObjects;
        }
    }
}
