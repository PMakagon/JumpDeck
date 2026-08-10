using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace JumpDeck.Editor
{
    internal enum JumpPinKind
    {
        Object,
        SceneQuery
    }

    internal enum SceneQueryScope
    {
        AllLoadedScenes,
        ActiveScene
    }

    internal enum JumpPinClickAction
    {
        InspectOrOpen,
        Ping,
        InspectOrOpenAndPing
    }

    [Serializable]
    internal sealed class JumpPin
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private JumpPinKind kind;
        [SerializeField] private string globalObjectId;
        [SerializeField] private string assetGuid;
        [SerializeField] private long localFileId;
        [SerializeField] private string fallbackPath;
        [SerializeField] private string query;
        [SerializeField] private SceneQueryScope queryScope;
        [SerializeField] private bool hasClickAction;
        [SerializeField] private JumpPinClickAction clickAction;

        internal string Id => id;
        internal string DisplayName { get => displayName; set => displayName = value; }
        internal JumpPinKind Kind => kind;
        internal string GlobalObjectId => globalObjectId;
        internal string AssetGuid => assetGuid;
        internal long LocalFileId => localFileId;
        internal string FallbackPath => fallbackPath;
        internal string Query => query;
        internal SceneQueryScope QueryScope => queryScope;
        internal bool HasClickAction => hasClickAction;
        internal JumpPinClickAction ClickAction => clickAction;

        internal static JumpPin ForObject(
            string name,
            string serializedGlobalObjectId,
            string guid,
            long fileId,
            string path,
            bool hasExplicitClickAction = false,
            JumpPinClickAction explicitClickAction = JumpPinClickAction.InspectOrOpen)
        {
            return new JumpPin
            {
                id = Guid.NewGuid().ToString("N"),
                displayName = name,
                kind = JumpPinKind.Object,
                globalObjectId = serializedGlobalObjectId,
                assetGuid = guid,
                localFileId = fileId,
                fallbackPath = path,
                hasClickAction = hasExplicitClickAction,
                clickAction = explicitClickAction
            };
        }

        internal static JumpPin ForQuery(string name, string searchQuery, SceneQueryScope scope)
        {
            return new JumpPin
            {
                id = Guid.NewGuid().ToString("N"),
                displayName = name,
                kind = JumpPinKind.SceneQuery,
                query = searchQuery,
                queryScope = scope
            };
        }

        internal void SetClickAction(JumpPinClickAction value)
        {
            clickAction = value;
            hasClickAction = true;
        }

        internal void UseTypeDefaultClickAction()
        {
            hasClickAction = false;
        }
    }

    [Serializable]
    internal sealed class JumpDeckCollection
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private bool expanded = true;
        [SerializeField] private List<JumpPin> pins = new();

        internal string Id => id;
        internal string DisplayName { get => displayName; set => displayName = value; }
        internal bool Expanded { get => expanded; set => expanded = value; }
        internal List<JumpPin> Pins => pins;

        internal static JumpDeckCollection Create(string name)
        {
            return new JumpDeckCollection
            {
                id = Guid.NewGuid().ToString("N"),
                displayName = name
            };
        }
    }

    [FilePath("UserSettings/JumpDeck.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class JumpDeckData : ScriptableSingleton<JumpDeckData>
    {
        [SerializeField] private List<JumpDeckCollection> decks = new();

        internal List<JumpDeckCollection> Decks => decks;

        internal void EnsureInitialized()
        {
            decks ??= new List<JumpDeckCollection>();

            if (decks.Count > 0)
                return;

            decks.Add(JumpDeckCollection.Create("My Deck"));
            SaveData();
        }

        internal void SaveData()
        {
            EditorUtility.SetDirty(this);
            Save(true);
        }
    }
}
