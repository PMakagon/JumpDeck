using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Search;
using UnityEditor.ShortcutManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace JumpDeck.Editor
{
    internal sealed class JumpDeckWindow : EditorWindow, IHasCustomMenu
    {
        private const int MaxVisibleQueryResults = 200;

        private readonly Dictionary<string, List<GameObject>> queryResults = new();
        private readonly HashSet<string> runningQueries = new();
        private readonly HashSet<string> expandedQueries = new();

        private JumpDeckData data;
        private ScrollView deckScroll;
        private ToolbarSearchField searchField;
        private string filter = string.Empty;
        private string renamingDeckId;
        private string renamingPinId;

        [MenuItem("Window/JumpDeck", false, 1800)]
        internal static void ShowWindow()
        {
            JumpDeckWindow window = GetWindow<JumpDeckWindow>();
            window.titleContent = new GUIContent("JumpDeck", EditorGUIUtility.IconContent("Favorite").image);
            window.minSize = new Vector2(210f, 220f);
            window.Show();
        }

        internal static void RefreshOpenWindows()
        {
            foreach (JumpDeckWindow window in Resources.FindObjectsOfTypeAll<JumpDeckWindow>())
                window.RebuildDecks();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
            menu.AddItem(new GUIContent("Import Deck..."), false, ImportDeckFromDialog);
        }

        [Shortcut("JumpDeck/Open", KeyCode.J, ShortcutModifiers.Alt)]
        private static void OpenShortcut()
        {
            ShowWindow();
        }

        private void OnEnable()
        {
            data = JumpDeckData.instance;
            data.EnsureInitialized();
            EditorApplication.projectChanged += HandleProjectChanged;
            EditorApplication.hierarchyChanged += HandleHierarchyChanged;
            Undo.undoRedoPerformed += HandleUndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= HandleProjectChanged;
            EditorApplication.hierarchyChanged -= HandleHierarchyChanged;
            Undo.undoRedoPerformed -= HandleUndoRedo;
        }

        public void CreateGUI()
        {
            data = JumpDeckData.instance;
            data.EnsureInitialized();

            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("jumpdeck-root");

            StyleSheet styleSheet = LoadStyleSheet();
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            BuildToolbar();

            deckScroll = new ScrollView(ScrollViewMode.Vertical);
            deckScroll.AddToClassList("deck-scroll");
            rootVisualElement.Add(deckScroll);

            rootVisualElement.RegisterCallback<DragUpdatedEvent>(HandleRootDragUpdated);
            rootVisualElement.RegisterCallback<DragPerformEvent>(HandleRootDragPerform);

            RebuildDecks();
        }

        private StyleSheet LoadStyleSheet()
        {
            MonoScript script = MonoScript.FromScriptableObject(this);
            string scriptPath = AssetDatabase.GetAssetPath(script);
            string editorDirectory = Path.GetDirectoryName(scriptPath)?.Replace('\\', '/');

            return string.IsNullOrEmpty(editorDirectory)
                ? null
                : AssetDatabase.LoadAssetAtPath<StyleSheet>($"{editorDirectory}/JumpDeckWindow.uss");
        }

        private void BuildToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.AddToClassList("jumpdeck-toolbar");

            searchField = new ToolbarSearchField
            {
                tooltip = "Filter pins"
            };
            searchField.AddToClassList("jumpdeck-search");
            searchField.RegisterValueChangedCallback(evt =>
            {
                filter = evt.newValue?.Trim() ?? string.Empty;
                RebuildDecks();
            });
            toolbar.Add(searchField);

            toolbar.Add(CreateToolbarIconButton(
                AddSelectionToFirstDeck,
                "Pin current selection",
                "★",
                "Favorite",
                "d_Favorite"));
            toolbar.Add(CreateToolbarIconButton(
                OpenQueryCreator,
                "Create Live Pin",
                "⌕",
                "d_Search Icon",
                "Search Icon"));
            toolbar.Add(CreateToolbarIconButton(
                AddDeck,
                "Create Deck",
                "+",
                "Folder Icon",
                "d_Folder Icon"));

            rootVisualElement.Add(toolbar);
        }

        private static ToolbarButton CreateToolbarIconButton(
            Action action,
            string tooltip,
            string fallbackGlyph,
            params string[] iconNames)
        {
            var button = new ToolbarButton(action)
            {
                tooltip = tooltip
            };
            button.AddToClassList("toolbar-icon-button");
            SetButtonIcon(button, fallbackGlyph, iconNames);
            return button;
        }

        private static Button CreateRowIconButton(
            Action action,
            string tooltip,
            string fallbackGlyph,
            params string[] iconNames)
        {
            var button = new Button(action)
            {
                tooltip = tooltip
            };
            button.AddToClassList("row-icon-button");
            SetButtonIcon(button, fallbackGlyph, iconNames);
            return button;
        }

        private static void SetButtonIcon(Button button, string fallbackGlyph, params string[] iconNames)
        {
            Texture icon = iconNames
                .Select(name => EditorGUIUtility.IconContent(name).image)
                .FirstOrDefault(image => image != null);

            if (icon == null)
            {
                button.text = fallbackGlyph;
                return;
            }

            var image = new Image
            {
                image = icon,
                scaleMode = ScaleMode.ScaleToFit
            };
            image.AddToClassList("button-icon");
            button.Add(image);
        }

        private void RebuildDecks()
        {
            if (deckScroll == null || data == null)
                return;

            deckScroll.Clear();

            if (data.Decks.Count == 0)
            {
                AddEmptyState();
                return;
            }

            for (int deckIndex = 0; deckIndex < data.Decks.Count; deckIndex++)
                deckScroll.Add(BuildDeck(data.Decks[deckIndex], deckIndex));
        }

        private VisualElement BuildDeck(JumpDeckCollection deck, int deckIndex)
        {
            var deckElement = new VisualElement();
            deckElement.AddToClassList("deck");

            var header = new VisualElement();
            header.AddToClassList("deck-header");

            var expandedToggle = new Button(() =>
            {
                deck.Expanded = !deck.Expanded;
                data.SaveData();
                RebuildDecks();
            })
            {
                text = deck.Expanded ? "▼" : "▶",
                tooltip = deck.Expanded ? "Collapse deck" : "Expand deck"
            };
            expandedToggle.AddToClassList("deck-foldout");
            header.Add(expandedToggle);

            if (renamingDeckId == deck.Id)
            {
                TextField renameField = CreateRenameField(
                    deck.DisplayName,
                    value =>
                    {
                        deck.DisplayName = string.IsNullOrWhiteSpace(value) ? "Untitled Deck" : value.Trim();
                        renamingDeckId = null;
                        data.SaveData();
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
                    if (evt.clickCount != 2 || evt.button != 0)
                        return;

                    renamingDeckId = deck.Id;
                    RebuildDecks();
                    evt.StopPropagation();
                });
                header.Add(title);
            }

            var count = new Label(deck.Pins.Count.ToString());
            count.AddToClassList("count-badge");
            header.Add(count);

            var menuButton = new Button(() => ShowDeckMenu(deck, deckIndex))
            {
                text = "⋮",
                tooltip = "Deck actions"
            };
            menuButton.AddToClassList("icon-button");
            header.Add(menuButton);

            deckElement.Add(header);

            if (deck.Expanded)
            {
                var content = new VisualElement();
                content.AddToClassList("deck-content");

                List<(JumpPin Pin, int Index)> visiblePins = deck.Pins
                    .Select((pin, index) => (Pin: pin, Index: index))
                    .Where(value => MatchesFilter(value.Pin))
                    .ToList();

                foreach ((JumpPin pin, int pinIndex) in visiblePins)
                    content.Add(BuildPin(deck, deckIndex, pin, pinIndex));

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

        private VisualElement BuildPin(
            JumpDeckCollection deck,
            int deckIndex,
            JumpPin pin,
            int pinIndex)
        {
            var container = new VisualElement();

            var row = new VisualElement();
            row.AddToClassList("pin-row");

            Object resolvedObject = pin.Kind == JumpPinKind.Object
                ? JumpDeckObjectUtility.Resolve(pin)
                : null;

            var icon = new Image
            {
                image = GetPinIcon(pin, resolvedObject),
                tooltip = pin.Kind == JumpPinKind.SceneQuery ? "Live Pin" : pin.FallbackPath
            };
            icon.AddToClassList("pin-icon");
            row.Add(icon);

            if (renamingPinId == pin.Id)
            {
                TextField renameField = CreateRenameField(
                    pin.DisplayName,
                    value =>
                    {
                        pin.DisplayName = string.IsNullOrWhiteSpace(value) ? "Untitled Pin" : value.Trim();
                        renamingPinId = null;
                        data.SaveData();
                        RebuildDecks();
                    });
                row.Add(renameField);
            }
            else
            {
                var name = new Label(pin.DisplayName)
                {
                    tooltip = BuildPinTooltip(pin, resolvedObject)
                };
                name.AddToClassList("pin-name");
                if (resolvedObject == null && pin.Kind == JumpPinKind.Object)
                    name.AddToClassList("missing-pin");

                name.RegisterCallback<MouseDownEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;

                    if (evt.clickCount == 2)
                        OpenPin(pin);
                    else
                        ActivatePin(pin);

                    evt.StopPropagation();
                });
                row.Add(name);
            }

            if (pin.Kind == JumpPinKind.SceneQuery)
            {
                bool isRunning = runningQueries.Contains(pin.Id);
                bool hasResults = queryResults.TryGetValue(pin.Id, out List<GameObject> results);
                if (isRunning || hasResults)
                {
                    var count = new Label(isRunning ? "…" : results.Count.ToString());
                    count.AddToClassList("query-count");
                    row.Add(count);
                }

                row.Add(CreateRowIconButton(
                    () => RunQuery(pin),
                    "Run query and select results",
                    "▶",
                    "d_Search Icon",
                    "Search Icon"));
            }
            else
            {
                Button pingButton = CreateRowIconButton(
                    () => PingPin(pin),
                    "Ping in Project or Hierarchy",
                    "◎",
                    "d_ViewToolZoom",
                    "ViewToolZoom");
                pingButton.SetEnabled(resolvedObject != null);
                row.Add(pingButton);

                Button openButton = CreateRowIconButton(
                    () => OpenOrInspectPin(pin),
                    "Open or show in Inspector",
                    "↗",
                    "d_UnityEditor.InspectorWindow",
                    "UnityEditor.InspectorWindow");
                openButton.SetEnabled(resolvedObject != null);
                row.Add(openButton);
            }

            var menuButton = new Button(() => ShowPinMenu(deck, deckIndex, pin, pinIndex))
            {
                text = "⋮",
                tooltip = "Pin actions"
            };
            menuButton.AddToClassList("icon-button");
            row.Add(menuButton);
            container.Add(row);

            if (pin.Kind == JumpPinKind.SceneQuery &&
                expandedQueries.Contains(pin.Id) &&
                queryResults.TryGetValue(pin.Id, out List<GameObject> queryObjects))
            {
                container.Add(BuildQueryResults(queryObjects));
            }

            return container;
        }

        private VisualElement BuildQueryResults(List<GameObject> results)
        {
            var list = new VisualElement();
            list.AddToClassList("query-results");

            if (results.Count == 0)
            {
                var empty = new Label("No objects found");
                empty.AddToClassList("query-empty");
                list.Add(empty);
                return list;
            }

            foreach (GameObject gameObject in results.Take(MaxVisibleQueryResults))
            {
                if (gameObject == null)
                    continue;

                var resultButton = new Button(() =>
                    JumpDeckObjectUtility.SelectAndPing(gameObject, true))
                {
                    text = gameObject.name,
                    tooltip = $"{gameObject.scene.name}/{BuildHierarchyPath(gameObject.transform)}"
                };
                resultButton.AddToClassList("query-result");
                list.Add(resultButton);
            }

            if (results.Count > MaxVisibleQueryResults)
            {
                var remainder = new Label($"+ {results.Count - MaxVisibleQueryResults} more results");
                remainder.AddToClassList("query-empty");
                list.Add(remainder);
            }

            return list;
        }

        private void RunQuery(JumpPin pin)
        {
            if (runningQueries.Contains(pin.Id))
                return;

            runningQueries.Add(pin.Id);
            expandedQueries.Add(pin.Id);
            RebuildDecks();

            SearchService.Request(pin.Query, (_, items) =>
            {
                var objects = new List<GameObject>();
                Scene activeScene = SceneManager.GetActiveScene();

                foreach (SearchItem item in items)
                {
                    GameObject gameObject = item.ToObject<GameObject>();
                    if (gameObject == null)
                        continue;

                    if (pin.QueryScope == SceneQueryScope.ActiveScene && gameObject.scene != activeScene)
                        continue;

                    if (!objects.Contains(gameObject))
                        objects.Add(gameObject);
                }

                objects.Sort((left, right) =>
                    string.CompareOrdinal(BuildHierarchyPath(left.transform), BuildHierarchyPath(right.transform)));

                queryResults[pin.Id] = objects;
                runningQueries.Remove(pin.Id);

                if (objects.Count > 0)
                {
                    Selection.objects = objects.Cast<Object>().ToArray();
                    EditorGUIUtility.PingObject(objects[0]);

                    if (objects.Count == 1)
                        SceneView.lastActiveSceneView?.FrameSelected();
                }

                RebuildDecks();
            });
        }

        private void ActivatePin(JumpPin pin)
        {
            if (pin.Kind == JumpPinKind.SceneQuery)
            {
                RunQuery(pin);
                return;
            }

            Object target = ResolvePinOrNotify(pin);
            if (target == null)
                return;

            ExecuteClickAction(pin, target);
        }

        private void OpenPin(JumpPin pin)
        {
            if (pin.Kind == JumpPinKind.SceneQuery)
            {
                RunQuery(pin);
                return;
            }

            OpenOrInspectPin(pin);
        }

        private void PingPin(JumpPin pin)
        {
            Object target = ResolvePinOrNotify(pin);
            if (target == null)
                return;

            EditorGUIUtility.PingObject(target);
        }

        private void OpenOrInspectPin(JumpPin pin)
        {
            Object target = ResolvePinOrNotify(pin);
            if (target == null)
                return;

            OpenOrInspectTarget(target);
        }

        private Object ResolvePinOrNotify(JumpPin pin)
        {
            Object target = JumpDeckObjectUtility.Resolve(pin);
            if (target != null)
                return target;

            ShowNotification(new GUIContent($"Missing: {pin.DisplayName}"));
            return null;
        }

        private static void ExecuteClickAction(JumpPin pin, Object target)
        {
            JumpPinClickAction action = JumpDeckObjectUtility.GetEffectiveClickAction(pin, target);
            if (action == JumpPinClickAction.Ping || action == JumpPinClickAction.InspectOrOpenAndPing)
                EditorGUIUtility.PingObject(target);

            if (action == JumpPinClickAction.InspectOrOpen ||
                action == JumpPinClickAction.InspectOrOpenAndPing)
            {
                OpenOrInspectTarget(target);
            }
        }

        private static void OpenOrInspectTarget(Object target)
        {
            if (JumpDeckObjectUtility.UsesOpenAction(target))
            {
                AssetDatabase.OpenAsset(target);
                return;
            }

            Selection.activeObject = target;
        }

        private void AddDeck()
        {
            Undo.RecordObject(data, "Add JumpDeck Deck");
            JumpDeckCollection deck = JumpDeckCollection.Create($"Deck {data.Decks.Count + 1}");
            data.Decks.Add(deck);
            renamingDeckId = deck.Id;
            data.SaveData();
            RebuildDecks();
        }

        private void AddSelectionToFirstDeck()
        {
            if (data.Decks.Count == 0)
                AddDeck();

            AddObjects(data.Decks[0], Selection.objects);
        }

        private void OpenQueryCreator()
        {
            if (data.Decks.Count == 0)
                AddDeck();

            JumpDeckQueryWindow.Open((name, query, scope) =>
            {
                JumpDeckCollection targetDeck = data.Decks[0];
                Undo.RecordObject(data, "Add JumpDeck Live Pin");
                targetDeck.Pins.Add(JumpPin.ForQuery(name, query, scope));
                data.SaveData();
                RebuildDecks();
            });
        }

        private void AddObjects(JumpDeckCollection deck, IEnumerable<Object> objects)
        {
            int added = JumpDeckService.AddObjects(data, deck, objects, out List<string> errors);

            if (added > 0)
                RebuildDecks();

            if (errors.Count > 0)
                ShowNotification(new GUIContent(errors[0]));
        }

        private void ImportDeckFromDialog()
        {
            if (!JumpDeckFileService.ImportDeckFromDialog(data, out JumpDeckCollection importedDeck))
                return;

            RebuildDecks();
            ShowNotification(new GUIContent($"Imported: {importedDeck.DisplayName}"));
        }

        private void ExportDeck(JumpDeckCollection deck)
        {
            if (!JumpDeckFileService.ExportDeck(deck, out string exportedPath))
                return;

            ShowNotification(new GUIContent($"Exported: {Path.GetFileName(exportedPath)}"));
        }

        private void ShowDeckMenu(JumpDeckCollection deck, int deckIndex)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Rename"), false, () =>
            {
                renamingDeckId = deck.Id;
                RebuildDecks();
            });

            if (deckIndex > 0)
                menu.AddItem(new GUIContent("Move Up"), false, () => MoveDeck(deckIndex, -1));
            else
                menu.AddDisabledItem(new GUIContent("Move Up"));

            if (deckIndex < data.Decks.Count - 1)
                menu.AddItem(new GUIContent("Move Down"), false, () => MoveDeck(deckIndex, 1));
            else
                menu.AddDisabledItem(new GUIContent("Move Down"));

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Add Current Selection"), false, () =>
                AddObjects(deck, Selection.objects));
            menu.AddItem(new GUIContent("Add Live Pin"), false, () =>
                JumpDeckQueryWindow.Open((name, query, scope) =>
                {
                    Undo.RecordObject(data, "Add JumpDeck Live Pin");
                    deck.Pins.Add(JumpPin.ForQuery(name, query, scope));
                    data.SaveData();
                    RebuildDecks();
                }));

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Export Deck..."), false, () => ExportDeck(deck));
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Delete Deck"), false, () => DeleteDeck(deck));
            menu.ShowAsContext();
        }

        private void ShowPinMenu(
            JumpDeckCollection deck,
            int deckIndex,
            JumpPin pin,
            int pinIndex)
        {
            var menu = new GenericMenu();
            if (pin.Kind == JumpPinKind.SceneQuery)
            {
                menu.AddItem(new GUIContent("Run"), false, () => OpenPin(pin));
            }
            else
            {
                Object target = JumpDeckObjectUtility.Resolve(pin);
                string primaryActionName = GetPrimaryActionName(target);
                AddClickActionItems(menu, pin, target, primaryActionName);
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Rename"), false, () =>
            {
                renamingPinId = pin.Id;
                RebuildDecks();
            });

            if (pinIndex > 0)
                menu.AddItem(new GUIContent("Move Up"), false, () => MovePin(deck, pinIndex, -1));
            else
                menu.AddDisabledItem(new GUIContent("Move Up"));

            if (pinIndex < deck.Pins.Count - 1)
                menu.AddItem(new GUIContent("Move Down"), false, () => MovePin(deck, pinIndex, 1));
            else
                menu.AddDisabledItem(new GUIContent("Move Down"));

            if (data.Decks.Count > 1)
            {
                menu.AddSeparator("Move To/");
                for (int index = 0; index < data.Decks.Count; index++)
                {
                    int targetIndex = index;
                    JumpDeckCollection targetDeck = data.Decks[targetIndex];
                    if (targetIndex == deckIndex)
                        menu.AddDisabledItem(new GUIContent($"Move To/{targetDeck.DisplayName}"));
                    else
                        menu.AddItem(new GUIContent($"Move To/{targetDeck.DisplayName}"), false, () =>
                            MovePinToDeck(deck, targetDeck, pin));
                }
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Remove Pin"), false, () => RemovePin(deck, pin));
            menu.ShowAsContext();
        }

        private void AddClickActionItems(
            GenericMenu menu,
            JumpPin pin,
            Object target,
            string primaryActionName)
        {
            JumpPinClickAction defaultAction = JumpDeckObjectUtility.GetDefaultClickAction(target);
            string defaultActionName = GetClickActionName(defaultAction, primaryActionName);

            menu.AddItem(
                new GUIContent($"Click Action/Use Type Default ({defaultActionName})"),
                !pin.HasClickAction,
                () => SetPinClickAction(pin, null));
            menu.AddSeparator("Click Action/");
            AddClickActionItem(
                menu,
                pin,
                JumpPinClickAction.InspectOrOpen,
                primaryActionName);
            AddClickActionItem(
                menu,
                pin,
                JumpPinClickAction.Ping,
                "Ping");
            AddClickActionItem(
                menu,
                pin,
                JumpPinClickAction.InspectOrOpenAndPing,
                $"{primaryActionName} and Ping");
        }

        private void AddClickActionItem(
            GenericMenu menu,
            JumpPin pin,
            JumpPinClickAction action,
            string label)
        {
            menu.AddItem(
                new GUIContent($"Click Action/{label}"),
                pin.HasClickAction && pin.ClickAction == action,
                () => SetPinClickAction(pin, action));
        }

        private void SetPinClickAction(JumpPin pin, JumpPinClickAction? action)
        {
            Undo.RecordObject(data, "Change JumpDeck Pin Click Action");
            if (action.HasValue)
                pin.SetClickAction(action.Value);
            else
                pin.UseTypeDefaultClickAction();

            data.SaveData();
            RebuildDecks();
        }

        private static string GetPrimaryActionName(Object target)
        {
            return JumpDeckObjectUtility.UsesOpenAction(target) ? "Open" : "Inspect";
        }

        private static string GetClickActionName(JumpPinClickAction action, string primaryActionName)
        {
            return action switch
            {
                JumpPinClickAction.Ping => "Ping",
                JumpPinClickAction.InspectOrOpenAndPing => $"{primaryActionName} and Ping",
                _ => primaryActionName
            };
        }

        private void MoveDeck(int index, int offset)
        {
            int targetIndex = index + offset;
            if (targetIndex < 0 || targetIndex >= data.Decks.Count)
                return;

            Undo.RecordObject(data, "Reorder JumpDeck Deck");
            (data.Decks[index], data.Decks[targetIndex]) = (data.Decks[targetIndex], data.Decks[index]);
            data.SaveData();
            RebuildDecks();
        }

        private void DeleteDeck(JumpDeckCollection deck)
        {
            if (!EditorUtility.DisplayDialog(
                    "Delete Deck?",
                    $"Delete '{deck.DisplayName}' and its {deck.Pins.Count} pin(s)?",
                    "Delete",
                    "Cancel"))
            {
                return;
            }

            Undo.RecordObject(data, "Delete JumpDeck Deck");
            data.Decks.Remove(deck);
            if (data.Decks.Count == 0)
                data.Decks.Add(JumpDeckCollection.Create("My Deck"));
            data.SaveData();
            RebuildDecks();
        }

        private void MovePin(JumpDeckCollection deck, int index, int offset)
        {
            int targetIndex = index + offset;
            if (targetIndex < 0 || targetIndex >= deck.Pins.Count)
                return;

            Undo.RecordObject(data, "Reorder JumpDeck Pin");
            (deck.Pins[index], deck.Pins[targetIndex]) = (deck.Pins[targetIndex], deck.Pins[index]);
            data.SaveData();
            RebuildDecks();
        }

        private void MovePinToDeck(
            JumpDeckCollection sourceDeck,
            JumpDeckCollection targetDeck,
            JumpPin pin)
        {
            Undo.RecordObject(data, "Move JumpDeck Pin");
            sourceDeck.Pins.Remove(pin);
            targetDeck.Pins.Add(pin);
            targetDeck.Expanded = true;
            data.SaveData();
            RebuildDecks();
        }

        private void RemovePin(JumpDeckCollection deck, JumpPin pin)
        {
            Undo.RecordObject(data, "Remove JumpDeck Pin");
            deck.Pins.Remove(pin);
            queryResults.Remove(pin.Id);
            expandedQueries.Remove(pin.Id);
            data.SaveData();
            RebuildDecks();
        }

        private void RegisterDeckDropTarget(VisualElement target, JumpDeckCollection deck)
        {
            target.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                if (DragAndDrop.objectReferences.Length == 0)
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                target.AddToClassList("drag-over");
                evt.StopPropagation();
            });

            target.RegisterCallback<DragLeaveEvent>(_ => target.RemoveFromClassList("drag-over"));
            target.RegisterCallback<DragPerformEvent>(evt =>
            {
                target.RemoveFromClassList("drag-over");
                DragAndDrop.AcceptDrag();
                AddObjects(deck, DragAndDrop.objectReferences);
                evt.StopPropagation();
            });
        }

        private void HandleRootDragUpdated(DragUpdatedEvent evt)
        {
            if (DragAndDrop.objectReferences.Length > 0)
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        }

        private void HandleRootDragPerform(DragPerformEvent evt)
        {
            if (DragAndDrop.objectReferences.Length == 0 || data.Decks.Count == 0)
                return;

            DragAndDrop.AcceptDrag();
            AddObjects(data.Decks[0], DragAndDrop.objectReferences);
            evt.StopPropagation();
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
                    renamingPinId = null;
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

        private static Texture GetPinIcon(JumpPin pin, Object resolvedObject)
        {
            if (pin.Kind == JumpPinKind.SceneQuery)
                return EditorGUIUtility.IconContent("d_Search Icon").image;

            if (resolvedObject != null)
                return AssetPreview.GetMiniThumbnail(resolvedObject);

            return EditorGUIUtility.IconContent("console.warnicon.sml").image;
        }

        private static string BuildPinTooltip(JumpPin pin, Object resolvedObject)
        {
            if (pin.Kind == JumpPinKind.SceneQuery)
                return $"{pin.Query}\nScope: {pin.QueryScope}";

            return resolvedObject != null
                ? pin.FallbackPath
                : $"Missing or scene not loaded\n{pin.FallbackPath}";
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }

        private void MoveDeckOrPinCleanup()
        {
            var knownIds = new HashSet<string>(data.Decks.SelectMany(deck => deck.Pins).Select(pin => pin.Id));
            foreach (string id in queryResults.Keys.Where(id => !knownIds.Contains(id)).ToArray())
                queryResults.Remove(id);
            expandedQueries.RemoveWhere(id => !knownIds.Contains(id));
            runningQueries.RemoveWhere(id => !knownIds.Contains(id));
        }

        private void HandleProjectChanged()
        {
            MoveDeckOrPinCleanup();
            RebuildDecks();
        }

        private void HandleHierarchyChanged()
        {
            queryResults.Clear();
            runningQueries.Clear();
            RebuildDecks();
        }

        private void HandleUndoRedo()
        {
            data?.SaveData();
            RebuildDecks();
        }

        private void AddEmptyState()
        {
            var empty = new VisualElement();
            empty.AddToClassList("empty-state");
            empty.Add(new Label("Your JumpDeck is empty"));

            var button = new Button(AddDeck)
            {
                text = "Create Deck"
            };
            empty.Add(button);
            deckScroll.Add(empty);
        }
    }
}
