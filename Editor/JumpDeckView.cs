using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace JumpDeck.Editor
{
    public readonly struct JumpDeckSummary
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int PinCount;
        internal JumpDeckSummary(JumpDeckCollection deck)
        {
            Id = deck.Id;
            Name = deck.DisplayName;
            PinCount = deck.Pins.Count;
        }
    }

    // Reusable editor panel; the host supplies placement and notifications.
    public sealed partial class JumpDeckView : VisualElement, IDisposable
    {
        private readonly IJumpDeckStorage data;
        private readonly Func<string> targetDeck;
        private readonly Func<JumpPin, Object, bool> objectActivator;
        private readonly Action<string> notify;
        private readonly Action<string> startDrag;
        private readonly IJumpDeckViewExtension extension;
        private JumpDeckSession session;
        private readonly ScrollView deckScroll;
        private string filter = string.Empty;
        private string renamingDeckId;
        private string selectedPinId;
        private readonly List<(JumpPin Pin, VisualElement Row)> rows = new();
        private Predicate<string> deckFilter;
        private bool refreshQueued;
        private int armedGestures;
        private bool refreshAfterGesture;
        private bool disposed;

        public event Action DataChanged;
        public event Action<string> DeckCreated;
        public VisualElement Toolbar { get; private set; }
        public IJumpDeckViewExtension Extension => extension;
        public IReadOnlyList<JumpDeckSummary> Decks => data.Decks.Select(deck => new JumpDeckSummary(deck)).ToArray();

        public JumpDeckView(Action<string> notification = null)
            : this(JumpDeckStorage.Default, notification, JumpDeckStorage.DefaultDestination, null) { }

        public JumpDeckView(IJumpDeckStorage storage, Action<string> notification = null, Func<string> targetDeck = null,
            Func<JumpPin, Object, bool> objectActivator = null)
            : this(storage, notification, targetDeck, null, objectActivator) { }

        // Native drag enters the OS input loop. Tests supply a starter while exercising the UI gesture and payload.
        internal JumpDeckView(IJumpDeckStorage storage, Action<string> notification, Func<string> targetDeck, Action<string> dragStarter,
            Func<JumpPin, Object, bool> objectActivator = null)
        {
            startDrag = dragStarter ?? DragAndDrop.StartDrag;
            data = storage ?? throw new ArgumentNullException(nameof(storage));
            this.targetDeck = targetDeck;
            this.objectActivator = objectActivator;
            notify = notification ?? (message => Debug.Log($"JumpDeck: {message}"));
            AddToClassList("jumpdeck-root");
            AddToClassList(EditorGUIUtility.isProSkin ? "jumpdeck-dark" : "jumpdeck-light");
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromScriptableObject(JumpDeckData.instance));
            var styles = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                $"{Path.GetDirectoryName(scriptPath)?.Replace('\\', '/')}/JumpDeckWindow.uss");
            if (styles != null) styleSheets.Add(styles);
            BuildToolbar();
            deckScroll = new ScrollView(ScrollViewMode.Vertical);
            deckScroll.AddToClassList("deck-scroll");
            Add(deckScroll);
            RegisterCallback<AttachToPanelEvent>(_ => AttachSession());
            RegisterCallback<DetachFromPanelEvent>(_ => DetachSession());
            RegisterCallback<DragUpdatedEvent>(HandleRootDragUpdated);
            RegisterCallback<DragPerformEvent>(HandleRootDragPerform);
            RegisterCallback<KeyDownEvent>(HandleNavigation);
            extension = JumpDeckStorage.Extend(data, this);
        }

        public void SetDeckFilter(Predicate<string> predicate)
        {
            deckFilter = predicate;
            Refresh();
        }

        public void PinSelection(string deckId)
        {
            var deck = data.Decks.FirstOrDefault(value => value.Id == deckId);
            if (deck != null) AddObjects(deck, Selection.objects);
        }

        private void AttachSession()
        {
            if (disposed || session != null) return;
            session = new JumpDeckSession(data);
            session.Changed += HandleChanged;
            JumpDeckViewSettings.Changed += Refresh;
            RebuildDecks();
        }
        private void DetachSession()
        {
            JumpDeckViewSettings.Changed -= Refresh;
            session?.Dispose();
            session = null;
        }
        private void HandleChanged()
        {
            Refresh();
            DataChanged?.Invoke();
        }
        public void Refresh()
        {
            if (disposed || refreshQueued || session == null) return;
            refreshQueued = true;
            schedule.Execute(() =>
            {
                refreshQueued = false;
                if (!disposed && session != null) RebuildDecks();
            });
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            extension?.Dispose();
            DetachSession();
            DataChanged = null;
            DeckCreated = null;
        }
        private void ShowNotification(GUIContent message) => notify(message.text);

        private void BuildToolbar()
        {
            var toolbar = new Toolbar();
            Toolbar = toolbar;
            toolbar.AddToClassList("jumpdeck-toolbar");

            var searchField = new ToolbarSearchField
            {
                tooltip = "Filter pins"
            };
            searchField.AddToClassList("jumpdeck-search");
            searchField.RegisterValueChangedCallback(evt =>
            {
                filter = evt.newValue?.Trim() ?? string.Empty;
                Refresh();
            });
            toolbar.Add(searchField);

            toolbar.Add(CreateToolbarIconButton(
                AddSelectionToFirstDeck,
                "Pin current selection", JumpDeckIconKind.PinSelection));
            toolbar.Add(CreateToolbarIconButton(
                OpenQueryCreator,
                "Create Live Pin", JumpDeckIconKind.LivePin));
            ToolbarButton settingsButton = null;
            settingsButton = CreateToolbarIconButton(
                () =>
                {
                    JumpDeckActionsPopup popup = null;
                    popup = new JumpDeckActionsPopup(SettingsSize,
                        () => CreateGlobalSettings(() => popup.editorWindow?.Close()));
                    UnityEditor.PopupWindow.Show(JumpDeckPopup.AnchorToRight(settingsButton.worldBound), popup);
                },
                "JumpDeck settings", JumpDeckIconKind.Settings);
            settingsButton.name = "jumpdeck-settings";
            toolbar.Add(settingsButton);

            Add(toolbar);
        }

        private static ToolbarButton CreateToolbarIconButton(
            Action action,
            string tooltip,
            JumpDeckIconKind iconKind)
        {
            var button = new ToolbarButton(action)
            {
                tooltip = tooltip
            };
            button.AddToClassList("toolbar-icon-button");
            button.Add(new JumpDeckActionIcon(iconKind));
            return button;
        }

        private static Button CreateRowIconButton(
            Action action,
            string tooltip,
            JumpDeckIconKind iconKind)
        {
            var button = new Button(action)
            {
                tooltip = tooltip
            };
            button.AddToClassList("row-icon-button");
            button.Add(new JumpDeckActionIcon(iconKind));
            return button;
        }

        private static void ScalePinButton(Button button, int iconSize)
        {
            float width = Mathf.Max(22, iconSize + 6);
            float height = Mathf.Max(21, iconSize + 6);
            button.style.width = button.style.minWidth = button.style.maxWidth = width;
            button.style.height = button.style.minHeight = button.style.maxHeight = height;
            button.style.fontSize = Mathf.Max(12, iconSize * .75f);
            button.style.paddingLeft = button.style.paddingRight = 3;
            button.style.paddingTop = button.style.paddingBottom = 3;
            foreach (var icon in button.Query<VisualElement>(className: "button-icon").ToList())
                icon.style.width = icon.style.height = Mathf.Max(14, iconSize - 2);
        }

        private void RebuildDecks()
        {
            if (session == null)
                return;

            if (armedGestures > 0)
            {
                refreshAfterGesture = true;
                return;
            }

            Vector2 offset = deckScroll.scrollOffset;
            bool restoreFocus = rows.Any(item => item.Row == panel?.focusController?.focusedElement);
            rows.Clear();
            deckScroll.Clear();

            foreach (var deck in data.Decks)
                if (deckFilter == null || deckFilter(deck.Id))
                    deckScroll.Add(BuildDeck(deck));
            deckScroll.scrollOffset = offset;
            if (restoreFocus) rows.FirstOrDefault(item => item.Pin.Id == selectedPinId).Row?.Focus();
        }

        private VisualElement BuildDeck(JumpDeckCollection deck)
        {
            var deckElement = new VisualElement();
            deckElement.AddToClassList("deck");
            deckElement.name = "deck-" + deck.Id;

            var header = new VisualElement();
            header.AddToClassList("deck-header");
            header.tooltip = deck.DisplayName + (deck.Locked ? " (locked)" : "");
            header.EnableInClassList("minimized-header",
                JumpDeckViewSettings.instance.MinimizeHeaders && renamingDeckId != deck.Id);

            var expandedToggle = new Button(() =>
            {
                deck.Expanded = !deck.Expanded;
                data.SaveData();
            })
            {
                text = deck.Expanded ? "▼" : "▶",
                tooltip = deck.Expanded ? "Collapse deck" : "Expand deck"
            };
            expandedToggle.AddToClassList("deck-foldout");
            header.Add(expandedToggle);
            RegisterDeckDrag(header, deck);
            RegisterDeckDropTarget(header, deck);

            if (renamingDeckId == deck.Id)
            {
                TextField renameField = CreateRenameField(
                    deck.DisplayName,
                    value =>
                    {
                        JumpDeckService.RenameDeck(data, deck, value);
                        renamingDeckId = null;
                        RebuildDecks();
                    });
                header.Add(renameField);
            }
            else
            {
                var title = new Label(deck.DisplayName);
                title.AddToClassList("deck-title");
                title.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (deck.Locked || evt.clickCount != 2 || evt.button != 0)
                        return;

                    renamingDeckId = deck.Id;
                    RebuildDecks();
                    evt.StopPropagation();
                });
                header.Add(title);
            }

            if (JumpDeckViewSettings.instance.ShowPinCounts)
            {
                var count = new Label(deck.Pins.Count.ToString());
                count.AddToClassList("count-badge");
                header.Add(count);
            }
            if (deck.Locked)
            {
                var locked = new JumpDeckActionIcon(JumpDeckIconKind.Lock) { tooltip = "Deck locked" };
                locked.AddToClassList("deck-lock-mark");
                header.Add(locked);
            }

            var menuButton = new Button(() => ShowDeckMenu(deck))
            {
                text = "⋮",
                tooltip = "Deck actions"
            };
            menuButton.AddToClassList("icon-button");
            menuButton.name = "jumpdeck-deck-menu";
            header.Add(menuButton);

            deckElement.Add(header);

            if (deck.Expanded)
            {
                var content = new VisualElement();
                content.AddToClassList("deck-content");

                List<JumpPin> visiblePins = deck.Pins.Where(MatchesFilter).ToList();

                foreach (JumpPin pin in visiblePins)
                    content.Add(BuildPin(deck, pin));

                if (visiblePins.Count == 0)
                {
                    var hint = new Label(string.IsNullOrEmpty(filter)
                        ? "Drop assets or scene objects here"
                        : "No matching pins");
                    hint.AddToClassList("drop-hint");
                    content.Add(hint);
                }

                RegisterDeckDropTarget(content, deck);
                deckElement.Add(content);
            }

            return deckElement;
        }

        public void CreateDeck()
        {
            var deck = JumpDeckService.AddDeck(data);
            renamingDeckId = deck.Id;
            DeckCreated?.Invoke(deck.Id);
            RebuildDecks();
        }

        private void AddSelectionToFirstDeck()
        {
            var deck = FirstVisibleDeck();
            if (deck != null) AddObjects(deck, Selection.objects);
        }

        private void OpenQueryCreator()
        {
            var deck = FirstVisibleDeck();
            if (deck == null || deck.Locked) return;
            JumpDeckQueryWindow.Open((name, query, scope) =>
                JumpDeckService.AddQuery(data, deck, name, query, scope));
        }

        private void AddObjects(JumpDeckCollection deck, IEnumerable<Object> objects)
        {
            JumpDeckService.AddObjects(data, deck, objects, out List<string> errors);

            if (errors.Count > 0)
                ShowNotification(new GUIContent(errors[0]));
        }

        public void ImportDeckFromDialog()
        {
            if (!JumpDeckFileService.ImportDeckFromDialog(data, out JumpDeckCollection importedDeck))
                return;

            DeckCreated?.Invoke(importedDeck.Id);
            RebuildDecks();
            ShowNotification(new GUIContent($"Imported: {importedDeck.DisplayName}"));
        }

        private void ExportDeck(JumpDeckCollection deck)
        {
            if (!JumpDeckFileService.ExportDeck(deck, out string exportedPath))
                return;

            ShowNotification(new GUIContent($"Exported: {Path.GetFileName(exportedPath)}"));
        }

        private TextField CreateRenameField(string initialValue, Action<string> commit)
        {
            var field = new TextField
            {
                value = initialValue
            };
            field.AddToClassList("rename-field");

            bool committed = false;
            void Commit()
            {
                if (committed)
                    return;

                committed = true;
                commit(field.value);
            }

            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    Commit();
                    evt.StopPropagation();
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    committed = true;
                    renamingDeckId = null;
                    RebuildDecks();
                    evt.StopPropagation();
                }
            });
            field.RegisterCallback<FocusOutEvent>(_ => Commit());
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.SelectAll();
            });
            return field;
        }

        private bool MatchesFilter(JumpPin pin)
        {
            if (string.IsNullOrEmpty(filter))
                return true;

            return Contains(pin.DisplayName, filter) ||
                   Contains(pin.FallbackPath, filter) ||
                   Contains(pin.Query, filter);
        }

        private static bool Contains(string value, string search)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private JumpDeckCollection FirstVisibleDeck()
        {
            string preferred = targetDeck?.Invoke();
            return data.Decks.FirstOrDefault(deck => deck.Id == preferred && (deckFilter == null || deckFilter(deck.Id)))
                ?? data.Decks.FirstOrDefault(deck => deckFilter == null || deckFilter(deck.Id));
        }
    }
}
