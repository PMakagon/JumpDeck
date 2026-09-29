using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JumpDeck.Editor
{
    public enum JumpPinKind
    {
        Object,
        SceneQuery
    }

    public enum SceneQueryScope
    {
        AllLoadedScenes,
        ActiveScene
    }

    public enum JumpPinClickAction
    {
        InspectOrOpen,
        Ping,
        InspectOrOpenAndPing
    }

    [Serializable]
    public sealed class JumpPin
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

        public string Id => id;
        public string DisplayName { get => displayName; set => displayName = value; }
        public JumpPinKind Kind => kind;
        public string GlobalObjectId => globalObjectId;
        public string AssetGuid => assetGuid;
        public long LocalFileId => localFileId;
        public string FallbackPath => fallbackPath;
        public string Query => query;
        public SceneQueryScope QueryScope => queryScope;
        public bool HasClickAction => hasClickAction;
        public JumpPinClickAction ClickAction => clickAction;

        public static JumpPin ForObject(
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

        public static JumpPin ForQuery(string name, string searchQuery, SceneQueryScope scope)
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

        internal void EditQuery(string name, string value, SceneQueryScope scope)
        {
            displayName = name;
            query = value;
            queryScope = scope;
        }
    }

    [Serializable]
    public sealed class JumpDeckCollection
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private bool expanded = true;
        [SerializeField] private bool locked;
        [SerializeField] private List<JumpPin> pins = new();
        [SerializeField] private JumpDeckDisplaySettings displaySettings;

        public string Id => id;
        public bool Locked { get => locked; set => locked = value; }
        public string DisplayName { get => displayName; set => displayName = value; }
        public bool Expanded { get => expanded; set => expanded = value; }
        public List<JumpPin> Pins => pins;
        public JumpDeckDisplaySettings DisplaySettings => displaySettings ??= new JumpDeckDisplaySettings();

        internal bool InitializeDisplaySettings(bool migrateLegacy)
        {
            if (displaySettings != null) return false;
            displaySettings = new JumpDeckDisplaySettings();
            if (migrateLegacy) JumpDeckViewSettings.instance.CopyTo(displaySettings);
            return true;
        }

        public static JumpDeckCollection Create(string name)
        {
            return new JumpDeckCollection
            {
                id = Guid.NewGuid().ToString("N"),
                displayName = name,
                displaySettings = new JumpDeckDisplaySettings()
            };
        }
    }

    // A passive storage container. The editor currently exposes only this one set.
    [Serializable]
    public sealed class JumpDeckSet
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private List<JumpDeckCollection> decks = new();

        public string Id => id;
        public string DisplayName { get => displayName; set => displayName = value; }
        public List<JumpDeckCollection> Decks => decks ??= new List<JumpDeckCollection>();

        public static JumpDeckSet FromDecks(List<JumpDeckCollection> source, string name = "Default") => new()
        {
            id = Guid.NewGuid().ToString("N"),
            displayName = name,
            decks = source ?? new List<JumpDeckCollection>()
        };
    }

    [FilePath("UserSettings/JumpDeck.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class JumpDeckData : ScriptableSingleton<JumpDeckData>, IJumpDeckStorage
    {
        internal static event Action Changed;
        [SerializeField] private JumpDeckSet deckSet;
        // Retain the old field name solely to read projects saved before the container existed.
        [SerializeField] private List<JumpDeckCollection> decks;
        [NonSerialized] private bool storageChanged;

        internal JumpDeckSet Set
        {
            get
            {
                InitializeStorage();
                return deckSet;
            }
        }
        internal List<JumpDeckCollection> Decks => Set.Decks;
        UnityEngine.Object IJumpDeckStorage.UndoTarget => this;
        List<JumpDeckCollection> IJumpDeckStorage.Decks => Decks;
        event Action IJumpDeckStorage.Changed { add => Changed += value; remove => Changed -= value; }
        void IJumpDeckStorage.SaveData() => SaveData();

        private void OnEnable() => Undo.undoRedoPerformed += HandleUndoRedo;
        private void OnDisable() => Undo.undoRedoPerformed -= HandleUndoRedo;
        private void HandleUndoRedo() => SaveData();

        private void InitializeStorage()
        {
            // Unity can deserialize a missing inline class as an empty instance instead of null.
            if (deckSet != null && !string.IsNullOrEmpty(deckSet.Id)) return;
            if (decks?.Count > 0)
            {
                string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), "UserSettings/JumpDeck.asset");
                string backup = path + ".before-set";
                if (File.Exists(path) && !File.Exists(backup)) File.Copy(path, backup);
            }
            var source = decks?.Count > 0 ? decks : deckSet?.Decks;
            deckSet = JumpDeckSet.FromDecks(source);
            decks = null;
            storageChanged = true;
        }

        internal void EnsureInitialized()
        {
            InitializeStorage();
            bool changed = storageChanged;
            bool hasLegacySettings = File.Exists(Path.Combine(
                Path.GetDirectoryName(Application.dataPath), "UserSettings/JumpDeckViewSettings.asset"));
            foreach (var deck in deckSet.Decks)
                changed |= deck.InitializeDisplaySettings(hasLegacySettings);
            if (deckSet.Decks.Count == 0)
            {
                deckSet.Decks.Add(JumpDeckCollection.Create("My Deck"));
                changed = true;
            }
            if (changed) SaveData();
        }

        internal void SaveData()
        {
            InitializeStorage();
            EditorUtility.SetDirty(this);
            Save(true);
            storageChanged = false;
            Changed?.Invoke();
        }
    }
}
