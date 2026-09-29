using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace JumpDeck.Editor
{
    public sealed partial class JumpDeckView
    {
        private const int MaxVisibleQueryResults = 200;

        private VisualElement BuildPin(JumpDeckCollection deck, JumpPin pin)
        {
            var container = new VisualElement();

            var row = new VisualElement();
            row.name = "pin-" + pin.Id;
            row.AddToClassList("pin-row");
            var settings = deck.DisplaySettings;
            row.style.minHeight = Mathf.Max(25, settings.IconSize + 8);
            row.focusable = true;
            row.tabIndex = 0;
            row.EnableInClassList("selected-pin", pin.Id == selectedPinId);
            rows.Add((pin, row));
            row.RegisterCallback<FocusInEvent>(evt =>
            {
                if (evt.target == row) SelectRow(pin);
            });
            row.RegisterCallback<ContextClickEvent>(evt =>
            { ShowPinMenu(deck, pin); evt.StopPropagation(); });
            Func<bool> clicked = RegisterPinDrag(row, deck, pin);
            RegisterPinDropTarget(row, deck, pin);

            Object resolvedObject = pin.Kind == JumpPinKind.Object
                ? session.Resolve(pin).Target
                : null;

            var targetArea = new VisualElement();
            targetArea.AddToClassList("pin-target");
            row.Add(targetArea);

            var icon = new Image
            {
                image = GetPinIcon(pin, resolvedObject),
                tooltip = pin.Kind == JumpPinKind.SceneQuery ? "Live Pin" : pin.FallbackPath
            };
            icon.AddToClassList("pin-icon");
            icon.scaleMode = ScaleMode.ScaleToFit;
            icon.style.width = icon.style.height = settings.IconSize;
            LoadPinPreview(icon, resolvedObject, settings.IconSize);
            targetArea.Add(icon);

            var name = new Label(pin.DisplayName)
            {
                tooltip = BuildPinTooltip(pin, resolvedObject)
            };
            name.AddToClassList("pin-name");
            if (resolvedObject == null && pin.Kind == JumpPinKind.Object)
                name.AddToClassList("missing-pin");

            row.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 0 || !clicked()) return;
                SelectRow(pin);
                row.Focus();
                if (evt.clickCount == 2) OpenPin(pin);
                else ActivatePin(pin);
                evt.StopPropagation();
            });
            targetArea.Add(name);

            if (pin.Kind == JumpPinKind.SceneQuery)
            {
                bool isRunning = session.RunningQueries.Contains(pin.Id);
                bool hasResults = session.QueryResults.TryGetValue(pin.Id, out List<GameObject> results);
                if (isRunning || hasResults)
                {
                    var count = new Label(isRunning ? "…" : results.Count.ToString());
                    count.AddToClassList("query-count");
                    row.Add(count);
                }

                row.Add(CreateRowIconButton(
                    () => RunQuery(pin),
                    "Run query and select results", JumpDeckIconKind.Ping));
            }
            else
            {
                if (settings.ShowPingButton)
                {
                    Button pingButton = CreateRowIconButton(
                        () => PingPin(pin), "Ping in Project or Hierarchy", JumpDeckIconKind.Ping);
                    pingButton.name = "jumpdeck-pin-ping";
                    pingButton.SetEnabled(resolvedObject != null);
                    row.Add(pingButton);
                }
                if (settings.ShowOpenButton)
                {
                    Button openButton = CreateRowIconButton(
                        () => OpenOrInspectPin(pin), "Open or show in Inspector", JumpDeckIconKind.Inspector);
                    openButton.name = "jumpdeck-pin-open";
                    openButton.SetEnabled(resolvedObject != null);
                    row.Add(openButton);
                }
            }

            var menuButton = new Button(() => ShowPinMenu(deck, pin))
            {
                text = "⋮",
                tooltip = "Pin actions"
            };
            menuButton.AddToClassList("icon-button");
            menuButton.name = "jumpdeck-pin-menu";
            row.Add(menuButton);
            foreach (var button in row.Children().OfType<Button>())
                ScalePinButton(button, settings.IconSize);
            container.Add(row);

            if (pin.Kind == JumpPinKind.SceneQuery &&
                session.QueryResults.TryGetValue(pin.Id, out List<GameObject> queryObjects))
            {
                container.Add(BuildQueryResults(queryObjects));
            }

            if (pin.Kind == JumpPinKind.Object && resolvedObject == null)
            {
                var reference = session.Resolve(pin);
                var status = new Label(reference.Description);
                status.AddToClassList("reference-status");
                container.Add(status);
            }
            if (session.QueryErrors.TryGetValue(pin.Id, out string error))
                container.Add(new HelpBox(error, HelpBoxMessageType.Error));
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
                    tooltip = $"{gameObject.scene.name}/{JumpDeckObjectUtility.HierarchyPath(gameObject.transform)}"
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
            session.RunQuery(pin);
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

            if (objectActivator?.Invoke(pin, target) != true) ExecuteClickAction(pin, target);
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

            if (objectActivator?.Invoke(pin, target) != true) OpenOrInspectTarget(target);
        }

        private Object ResolvePinOrNotify(JumpPin pin)
        {
            var reference = session.Resolve(pin);
            if (reference.Target != null)
                return reference.Target;

            ShowNotification(new GUIContent($"{pin.DisplayName}: {reference.Description}"));
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

        private static Texture GetPinIcon(JumpPin pin, Object resolvedObject)
        {
            if (pin.Kind == JumpPinKind.SceneQuery)
                return EditorGUIUtility.IconContent("Search Icon").image;

            if (resolvedObject != null)
                return AssetPreview.GetMiniThumbnail(resolvedObject);

            return EditorGUIUtility.IconContent("console.warnicon.sml").image;
        }

        private static void LoadPinPreview(Image icon, Object target, int size)
        {
            if (target == null || size <= 24) return;
            var preview = AssetPreview.GetAssetPreview(target);
            if (preview != null) { icon.image = preview; return; }
            if (!IsPreviewLoading(target)) return;

            double deadline = EditorApplication.timeSinceStartup + 5;
            IVisualElementScheduledItem pending = null;
            pending = icon.schedule.Execute(() =>
            {
                if (target == null) { pending.Pause(); return; }
                preview = AssetPreview.GetAssetPreview(target);
                if (preview != null) icon.image = preview;
                if (preview != null || !IsPreviewLoading(target) ||
                    EditorApplication.timeSinceStartup >= deadline) pending.Pause();
            }).Every(100);
        }

        private static bool IsPreviewLoading(Object target)
        {
#if UNITY_6000_5_OR_NEWER
            return AssetPreview.IsLoadingAssetPreview(target.GetEntityId());
#else
            return AssetPreview.IsLoadingAssetPreview(target.GetInstanceID());
#endif
        }

        private string BuildPinTooltip(JumpPin pin, Object resolvedObject)
        {
            if (pin.Kind == JumpPinKind.SceneQuery)
                return $"{pin.Query}\nScope: {pin.QueryScope}";

            return resolvedObject != null
                ? pin.FallbackPath
                : $"{session.Resolve(pin).Description}\n{pin.FallbackPath}";
        }
    }
}
