using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace JumpDeck.Editor
{
    internal static class JumpDeckService
    {
        internal static void SetDisplaySettings(IJumpDeckStorage data, JumpDeckCollection deck,
            int size, bool ping, bool open)
        {
            if (!data.Decks.Contains(deck) || deck.Locked) return;
            size = Mathf.Clamp(size, JumpDeckDisplaySettings.MinIconSize, JumpDeckDisplaySettings.MaxIconSize);
            var settings = deck.DisplaySettings;
            if (settings.IconSize == size && settings.ShowPingButton == ping && settings.ShowOpenButton == open) return;
            Undo.RecordObject(data.UndoTarget, "Change JumpDeck Deck Display");
            settings.Update(size, ping, open);
            data.SaveData();
        }

        internal static JumpDeckCollection AddDeck(IJumpDeckStorage data)
        {
            Undo.RecordObject(data.UndoTarget, "Add JumpDeck Deck");
            var deck = JumpDeckCollection.Create($"Deck {data.Decks.Count + 1}");
            data.Decks.Add(deck);
            data.SaveData();
            return deck;
        }

        internal static void RenameDeck(IJumpDeckStorage data, JumpDeckCollection deck, string name)
        {
            if (!data.Decks.Contains(deck) || deck.Locked) return;
            string next = string.IsNullOrWhiteSpace(name) ? "Untitled Deck" : name.Trim();
            if (deck.DisplayName == next) return;
            Undo.RecordObject(data.UndoTarget, "Rename JumpDeck Deck");
            deck.DisplayName = next;
            data.SaveData();
        }

        internal static void RenamePin(IJumpDeckStorage data, JumpPin pin, string name)
        {
            if (!CanEdit(data, pin)) return;
            string next = string.IsNullOrWhiteSpace(name) ? "Untitled Pin" : name.Trim();
            if (pin.DisplayName == next) return;
            Undo.RecordObject(data.UndoTarget, "Rename JumpDeck Pin");
            pin.DisplayName = next;
            data.SaveData();
        }

        internal static void AddQuery(IJumpDeckStorage data, JumpDeckCollection deck,
            string name, string query, SceneQueryScope scope)
        {
            if (!data.Decks.Contains(deck) || deck.Locked) return;
            Undo.RecordObject(data.UndoTarget, "Add JumpDeck Live Pin");
            deck.Pins.Add(JumpPin.ForQuery(name, query, scope));
            data.SaveData();
        }

        internal static void EditQuery(IJumpDeckStorage data, JumpPin pin,
            string name, string query, SceneQueryScope scope)
        {
            if (!CanEdit(data, pin) || pin.Kind != JumpPinKind.SceneQuery) return;
            if (pin.DisplayName == name && pin.Query == query && pin.QueryScope == scope) return;
            Undo.RecordObject(data.UndoTarget, "Edit JumpDeck Live Pin");
            pin.EditQuery(name, query, scope);
            data.SaveData();
        }

        internal static void SetClickAction(IJumpDeckStorage data, JumpPin pin, JumpPinClickAction? action)
        {
            if (!CanEdit(data, pin)) return;
            if (pin.HasClickAction == action.HasValue && (!action.HasValue || pin.ClickAction == action.Value)) return;
            Undo.RecordObject(data.UndoTarget, "Change JumpDeck Pin Click Action");
            if (action.HasValue) pin.SetClickAction(action.Value);
            else pin.UseTypeDefaultClickAction();
            data.SaveData();
        }

        internal static void DeleteDeck(IJumpDeckStorage data, JumpDeckCollection deck)
        {
            if (!data.Decks.Contains(deck) || deck.Locked) return;
            Undo.RecordObject(data.UndoTarget, "Delete JumpDeck Deck");
            data.Decks.Remove(deck);
            if (data.Decks.Count == 0) data.Decks.Add(JumpDeckCollection.Create("My Deck"));
            data.SaveData();
        }

        internal static void RemovePin(IJumpDeckStorage data, JumpDeckCollection deck, JumpPin pin)
        {
            if (!data.Decks.Contains(deck) || deck.Locked || !deck.Pins.Contains(pin)) return;
            Undo.RecordObject(data.UndoTarget, "Remove JumpDeck Pin");
            deck.Pins.Remove(pin);
            data.SaveData();
        }

        internal static void MoveDeck(IJumpDeckStorage data, JumpDeckCollection deck, int insertionIndex)
        {
            int sourceIndex = data.Decks.IndexOf(deck);
            if (sourceIndex < 0 || deck.Locked) return;
            int targetIndex = Mathf.Clamp(insertionIndex, 0, data.Decks.Count);
            if (targetIndex > sourceIndex) targetIndex--;
            if (targetIndex == sourceIndex) return;
            Undo.RecordObject(data.UndoTarget, "Reorder JumpDeck Deck");
            data.Decks.RemoveAt(sourceIndex);
            data.Decks.Insert(targetIndex, deck);
            data.SaveData();
        }

        internal static bool MovePin(IJumpDeckStorage data, JumpDeckCollection source,
            JumpDeckCollection target, JumpPin pin, int insertionIndex)
        {
            if (!data.Decks.Contains(source) || !data.Decks.Contains(target) || source.Locked || target.Locked) return false;
            int sourceIndex = source.Pins.IndexOf(pin);
            if (sourceIndex < 0) return false;
            if (source != target && pin.Kind == JumpPinKind.Object && target.Pins.Any(value =>
                value.Kind == JumpPinKind.Object &&
                JumpDeckObjectUtility.BuildIdentity(value) == JumpDeckObjectUtility.BuildIdentity(pin))) return false;
            int targetIndex = Mathf.Clamp(insertionIndex, 0, target.Pins.Count);
            if (source == target && targetIndex > sourceIndex) targetIndex--;
            if (source == target && sourceIndex == targetIndex) return true;
            Undo.RecordObject(data.UndoTarget, "Move JumpDeck Pin");
            source.Pins.RemoveAt(sourceIndex);
            target.Pins.Insert(targetIndex, pin);
            target.Expanded = true;
            data.SaveData();
            return true;
        }

        internal static void SetLocked(IJumpDeckStorage data, JumpDeckCollection deck, bool locked)
        {
            if (!data.Decks.Contains(deck) || deck.Locked == locked) return;
            Undo.RecordObject(data.UndoTarget, "Lock JumpDeck Deck");
            deck.Locked = locked;
            data.SaveData();
        }

        private static bool CanEdit(IJumpDeckStorage data, JumpPin pin) =>
            data.Decks.Any(deck => !deck.Locked && deck.Pins.Contains(pin));

        internal static bool Contains(IJumpDeckStorage data, JumpPin pin) =>
            data.Decks.Any(deck => deck.Pins.Contains(pin));

        internal static int AddObjects(
            IJumpDeckStorage data,
            JumpDeckCollection deck,
            IEnumerable<Object> objects,
            out List<string> errors)
        {
            errors = new List<string>();
            if (!data.Decks.Contains(deck) || deck.Locked) return 0;
            var additions = new List<JumpPin>();
            var existingIdentities = new HashSet<string>(
                deck.Pins
                    .Where(pin => pin.Kind == JumpPinKind.Object)
                    .Select(JumpDeckObjectUtility.BuildIdentity));

            foreach (Object value in objects.Where(value => value != null).Distinct())
            {
                if (!JumpDeckObjectUtility.TryCreatePin(value, out JumpPin pin, out string error))
                {
                    if (!string.IsNullOrEmpty(error))
                        errors.Add(error);
                    continue;
                }

                string identity = JumpDeckObjectUtility.BuildIdentity(pin);
                if (!existingIdentities.Add(identity))
                    continue;

                additions.Add(pin);
            }

            if (additions.Count == 0)
                return 0;

            Undo.RecordObject(data.UndoTarget, "Add JumpDeck Pins");
            deck.Pins.AddRange(additions);
            deck.Expanded = true;
            data.SaveData();
            return additions.Count;
        }
    }
}
