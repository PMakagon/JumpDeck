using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace JumpDeck.Editor
{
    public sealed partial class JumpDeckView
    {
        private const string PinDragKey = "JumpDeck.Pin";
        private const string DeckDragKey = "JumpDeck.Deck";
        private sealed class PinDrag
        {
            internal JumpDeckCollection Deck;
            internal JumpPin Pin;
            internal IJumpDeckStorage Data;
        }

        private void SelectRow(JumpPin pin)
        {
            selectedPinId = pin.Id;
            foreach (var item in rows) item.Row.EnableInClassList("selected-pin", item.Pin.Id == selectedPinId);
        }

        private void HandleNavigation(KeyDownEvent evt)
        {
            if (evt.target is TextField || evt.target is TextElement ||
                (evt.target is VisualElement element &&
                 (element.GetFirstAncestorOfType<TextField>() != null ||
                  element.GetFirstAncestorOfType<ToolbarSearchField>() != null))) return;
            if (rows.Count == 0) return;
            int index = rows.FindIndex(item => item.Pin.Id == selectedPinId);
            if (evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow)
            {
                int next = index < 0 ? 0 : Mathf.Clamp(index + (evt.keyCode == KeyCode.UpArrow ? -1 : 1), 0, rows.Count - 1);
                SelectRow(rows[next].Pin);
                rows[next].Row.Focus();
                deckScroll.ScrollTo(rows[next].Row);
            }
            else if ((evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) && index >= 0)
            {
                if (evt.target is Button) return;
                ActivatePin(rows[index].Pin);
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                selectedPinId = null;
                foreach (var item in rows) item.Row.RemoveFromClassList("selected-pin");
                Blur();
            }
            else return;
            evt.StopPropagation();
            evt.StopImmediatePropagation();
        }

        private void RegisterDeckDrag(VisualElement header, JumpDeckCollection deck)
        {
            RegisterDragGesture(header, () =>
            {
                if (deck.Locked) return;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(PinDragKey, null);
                DragAndDrop.SetGenericData(DeckDragKey, deck);
                DragAndDrop.objectReferences = Array.Empty<UnityEngine.Object>();
                startDrag(deck.DisplayName);
            });
        }

        private Func<bool> RegisterPinDrag(VisualElement row, JumpDeckCollection deck, JumpPin pin)
        {
            return RegisterDragGesture(row, () =>
            {
                var target = pin.Kind == JumpPinKind.Object ? session.Resolve(pin).Target : null;
                if (deck.Locked && target == null) return;
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.SetGenericData(DeckDragKey, null);
                DragAndDrop.SetGenericData(PinDragKey, deck.Locked ? null : new PinDrag { Data = data, Deck = deck, Pin = pin });
                DragAndDrop.objectReferences = target == null ? Array.Empty<UnityEngine.Object>() : new[] { target };
                startDrag(pin.DisplayName);
            });
        }

        private Func<bool> RegisterDragGesture(VisualElement handle, Action start)
        {
            bool armed = false;
            bool dragged = false;
            bool completedClick = false;
            Vector2 origin = default;
            void Disarm()
            {
                if (!armed) return;
                armed = false;
                armedGestures--;
                if (armedGestures == 0 && refreshAfterGesture)
                {
                    refreshAfterGesture = false;
                    Refresh();
                }
            }
            handle.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button != 0) return;
                completedClick = false;
                if (evt.target is VisualElement child &&
                    (child is Button || child.GetFirstAncestorOfType<Button>() != null ||
                     child is TextField || child.GetFirstAncestorOfType<TextField>() != null)) return;
                Disarm();
                dragged = false;
                armed = true;
                armedGestures++;
                origin = evt.mousePosition;
                handle.CaptureMouse();
                evt.StopPropagation();
            });
            handle.RegisterCallback<MouseMoveEvent>(evt =>
            {
                if (!armed || (evt.pressedButtons & 1) == 0 || Vector2.Distance(origin, evt.mousePosition) < 5f) return;
                dragged = true;
                var input = Event.current;
                EventType inputType = input?.type ?? EventType.Ignore;
                Disarm();
                handle.ReleaseMouse();
                // Releasing capture can consume the IMGUI event; StartDrag still requires its original mouse type.
                EventType releasedType = input?.type ?? EventType.Ignore;
                try
                {
                    if (input != null) input.type = inputType;
                    start();
                }
                finally
                {
                    if (input != null) input.type = releasedType;
                }
                evt.StopPropagation();
            });
            handle.RegisterCallback<MouseUpEvent>(evt =>
            {
                if (evt.button != 0) return;
                completedClick = armed && !dragged && handle.worldBound.Contains(evt.mousePosition);
                Disarm();
                handle.ReleaseMouse();
            });
            handle.RegisterCallback<MouseCaptureOutEvent>(_ =>
            {
                if (armed) completedClick = false;
                Disarm();
            });
            handle.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                completedClick = false;
                Disarm();
                handle.ReleaseMouse();
            });
            return () => completedClick;
        }

        private PinDrag CurrentPinDrag() => DragAndDrop.GetGenericData(PinDragKey) is PinDrag drag && ReferenceEquals(drag.Data, data)
            ? drag : null;

        private void RegisterPinDropTarget(VisualElement row, JumpDeckCollection deck, JumpPin pin)
        {
            row.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                var drag = CurrentPinDrag();
                if (drag == null) return;
                if (deck.Locked || drag.Deck.Locked)
                { DragAndDrop.visualMode = DragAndDropVisualMode.Rejected; evt.StopPropagation(); return; }
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                bool after = DropPinAfter(drag, deck, pin, row, evt.localMousePosition.y);
                row.EnableInClassList("drop-before", !after);
                row.EnableInClassList("drop-after", after);
                evt.StopPropagation();
            });
            row.RegisterCallback<DragLeaveEvent>(_ => ClearDrop(row));
            row.RegisterCallback<DragPerformEvent>(evt =>
            {
                var drag = CurrentPinDrag();
                if (drag == null) return;
                if (deck.Locked || drag.Deck.Locked) { ClearDrop(row); evt.StopPropagation(); return; }
                int index = deck.Pins.IndexOf(pin) + (DropPinAfter(drag, deck, pin, row, evt.localMousePosition.y) ? 1 : 0);
                ClearDrop(row);
                if (JumpDeckService.MovePin(data, drag.Deck, deck, drag.Pin, index)) DragAndDrop.AcceptDrag();
                else ShowNotification(new GUIContent("That object is already pinned in this deck."));
                evt.StopPropagation();
            });
        }

        private static bool DropPinAfter(PinDrag drag, JumpDeckCollection deck, JumpPin target,
            VisualElement row, float mouseY)
        {
            if (drag.Deck == deck)
            {
                int distance = deck.Pins.IndexOf(target) - deck.Pins.IndexOf(drag.Pin);
                // Either half of the neighbouring row should move the pin past that neighbour.
                if (distance == 1) return true;
                if (distance == -1) return false;
            }
            return mouseY > row.layout.height * .5f;
        }

        private static void ClearDrop(VisualElement target)
        {
            target.RemoveFromClassList("drag-over");
            target.RemoveFromClassList("drop-before");
            target.RemoveFromClassList("drop-after");
        }

        private void RegisterDeckDropTarget(VisualElement target, JumpDeckCollection deck)
        {
            target.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                bool deckDrag = DragAndDrop.GetGenericData(DeckDragKey) is JumpDeckCollection source && data.Decks.Contains(source);
                if (!deckDrag && CurrentPinDrag() == null && DragAndDrop.objectReferences.Length == 0) return;
                if (deck.Locked) { DragAndDrop.visualMode = DragAndDropVisualMode.Rejected; evt.StopPropagation(); return; }
                DragAndDrop.visualMode = deckDrag || CurrentPinDrag() != null ? DragAndDropVisualMode.Move : DragAndDropVisualMode.Copy;
                target.AddToClassList("drag-over");
                evt.StopPropagation();
            });
            target.RegisterCallback<DragLeaveEvent>(_ => ClearDrop(target));
            target.RegisterCallback<DragPerformEvent>(evt =>
            {
                ClearDrop(target);
                if (deck.Locked) { evt.StopPropagation(); return; }
                if (DragAndDrop.GetGenericData(DeckDragKey) is JumpDeckCollection source && data.Decks.Contains(source))
                {
                    int targetIndex = data.Decks.IndexOf(deck);
                    if (data.Decks.IndexOf(source) < targetIndex) targetIndex++;
                    JumpDeckService.MoveDeck(data, source, targetIndex);
                }
                else if (CurrentPinDrag() is PinDrag drag)
                {
                    if (!JumpDeckService.MovePin(data, drag.Deck, deck, drag.Pin, deck.Pins.Count))
                        ShowNotification(new GUIContent("That object is already pinned in this deck."));
                }
                else if (DragAndDrop.objectReferences.Length > 0) AddObjects(deck, DragAndDrop.objectReferences);
                else return;
                DragAndDrop.AcceptDrag();
                evt.StopPropagation();
            });
        }

        private void HandleRootDragUpdated(DragUpdatedEvent evt)
        {
            if (CurrentPinDrag() == null && DragAndDrop.objectReferences.Length > 0)
                DragAndDrop.visualMode = FirstVisibleDeck()?.Locked == true ? DragAndDropVisualMode.Rejected : DragAndDropVisualMode.Copy;
        }

        private void HandleRootDragPerform(DragPerformEvent evt)
        {
            var deck = FirstVisibleDeck();
            if (CurrentPinDrag() != null || DragAndDrop.objectReferences.Length == 0 || deck == null || deck.Locked) return;
            DragAndDrop.AcceptDrag();
            AddObjects(deck, DragAndDrop.objectReferences);
            evt.StopPropagation();
        }
    }
}
