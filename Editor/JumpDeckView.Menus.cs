using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace JumpDeck.Editor
{
    public sealed partial class JumpDeckView
    {
        private static readonly Vector2 GlobalSettingsSize = new(280, 116);
        private Vector2 SettingsSize => GlobalSettingsSize + new Vector2(0, extension?.SettingsHeight ?? 0);

        internal VisualElement CreateGlobalSettings(Action close = null)
        {
            var root = JumpDeckPopup.Root(SettingsSize);
            var title = new Label("JumpDeck");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);
            root.Add(new Button(() => { close?.Invoke(); CreateDeck(); }) { text = "Create deck", name = "jumpdeck-create-deck" });
            root.Add(new Button(() => { close?.Invoke(); ImportDeckFromDialog(); }) { text = "Import deck…", name = "jumpdeck-import" });
            var settings = JumpDeckViewSettings.instance;
            var counts = new Toggle("Show pin counts") { value = settings.ShowPinCounts, name = "jumpdeck-show-counts" };
            var minimize = new Toggle("Minimize headers") { value = settings.MinimizeHeaders, name = "jumpdeck-minimize" };
            counts.RegisterValueChangedCallback(evt => settings.UpdateGlobal(evt.newValue, settings.MinimizeHeaders));
            minimize.RegisterValueChangedCallback(evt => settings.UpdateGlobal(settings.ShowPinCounts, evt.newValue));
            root.Add(counts); root.Add(minimize);
            void Sync()
            {
                counts.SetValueWithoutNotify(settings.ShowPinCounts);
                minimize.SetValueWithoutNotify(settings.MinimizeHeaders);
            }
            root.RegisterCallback<AttachToPanelEvent>(_ => { JumpDeckViewSettings.Changed += Sync; Sync(); });
            root.RegisterCallback<DetachFromPanelEvent>(_ => JumpDeckViewSettings.Changed -= Sync);
            extension?.AppendSettings(root, close);
            return root;
        }

        private void ShowDeckMenu(JumpDeckCollection deck)
        {
            var anchor = this.Q("deck-" + deck.Id)?.Q<Button>("jumpdeck-deck-menu")?.worldBound ?? worldBound;
            UnityEditor.PopupWindow.Show(JumpDeckPopup.AnchorToRight(anchor),
                new JumpDeckSettingsPopup(data, deck, ExportDeck, DeleteDeck, value =>
                    JumpDeckQueryWindow.Open((name, query, scope) =>
                        JumpDeckService.AddQuery(data, value, name, query, scope))));
        }

        private void ShowPinMenu(JumpDeckCollection deck, JumpPin pin)
        {
            var anchor = this.Q("pin-" + pin.Id)?.Q<Button>("jumpdeck-pin-menu")?.worldBound ?? worldBound;
            UnityEditor.PopupWindow.Show(JumpDeckPopup.AnchorToRight(anchor),
                new JumpDeckActionsPopup(PinActionsSize(deck.Id, pin.Id), () => CreatePinActions(deck.Id, pin.Id)));
        }

        private Vector2 PinActionsSize(string deckId, string pinId)
        {
            var pin = data.Decks.FirstOrDefault(deck => deck.Id == deckId)?.Pins.FirstOrDefault(value => value.Id == pinId);
            if (pin == null) return new Vector2(300, 60);
            bool hasDestination = data.Decks.Any(deck => deck.Id != deckId && !deck.Locked);
            bool hasExtraAction = pin.Kind == JumpPinKind.SceneQuery || session.Resolve(pin).Status == JumpDeckReferenceStatus.SceneNotLoaded;
            return new Vector2(300, 104 + (hasDestination ? 20 : 0) + (hasExtraAction ? 21 : 0));
        }

        internal VisualElement CreatePinActions(string deckId, string pinId)
        {
            JumpDeckCollection Deck() => data.Decks.FirstOrDefault(value => value.Id == deckId);
            JumpPin Pin() => Deck()?.Pins.FirstOrDefault(value => value.Id == pinId);
            var root = JumpDeckPopup.Root(PinActionsSize(deckId, pinId));
            var pin = Pin();
            if (pin == null) { root.Add(new Label("Pin removed")); return root; }
            var title = new Label(pin.DisplayName);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);
            var edits = new VisualElement();
            var rename = new TextField("Name") { value = pin.DisplayName, isDelayed = true };
            rename.RegisterValueChangedCallback(evt =>
            {
                if (Pin() is { } current) JumpDeckService.RenamePin(data, current, evt.newValue);
            });
            edits.Add(rename);
            DropdownField click = null;
            if (pin.Kind == JumpPinKind.SceneQuery)
            {
                root.Add(new Button(() => { if (Pin() is { } current) OpenPin(current); }) { text = "Run query" });
                edits.Add(new Button(() =>
                {
                    if (Pin() is { } current) JumpDeckQueryWindow.Edit(current, (name, query, scope) =>
                    {
                        if (Pin() is { } edited) JumpDeckService.EditQuery(data, edited, name, query, scope);
                    });
                }) { text = "Edit query…" });
            }
            else
            {
                click = new DropdownField("Click action",
                    new List<string> { "Use type default", "Open/Inspect", "Ping", "Open/Inspect and Ping" },
                    pin.HasClickAction ? (int)pin.ClickAction + 1 : 0);
                click.RegisterValueChangedCallback(_ =>
                {
                    if (Pin() is { } current) JumpDeckService.SetClickAction(data, current,
                        click.index == 0 ? null : (JumpPinClickAction?)(click.index - 1));
                });
                edits.Add(click);
                var reference = session.Resolve(pin);
                if (reference.Status == JumpDeckReferenceStatus.SceneNotLoaded)
                    root.Add(new Button(() => OpenSceneAdditively(reference.ScenePath)) { text = "Open scene additively" });
            }
            var destinations = data.Decks.Where(value => value.Id != deckId && !value.Locked).ToList();
            if (destinations.Count > 0)
            {
                var choices = new List<string> { "Choose deck…" };
                choices.AddRange(destinations.Select(value => value.DisplayName));
                var move = new DropdownField("Move to", choices, 0);
                move.RegisterValueChangedCallback(_ =>
                {
                    int index = move.index - 1;
                    var source = Deck(); var item = Pin();
                    if (index < 0 || source == null || item == null) return;
                    var target = data.Decks.FirstOrDefault(value => value.Id == destinations[index].Id);
                    if (target != null) MovePinToDeck(source, target, item);
                });
                edits.Add(move);
            }
            edits.Add(new Button(() => { if (Pin() is { } current) RemovePin(Deck(), current); }) { text = "Remove pin" });
            root.Add(edits);
            void Sync()
            {
                var current = Pin();
                root.SetEnabled(current != null);
                if (current == null) { title.text = "Pin moved or removed"; return; }
                title.text = current.DisplayName;
                rename.SetValueWithoutNotify(current.DisplayName);
                if (click != null) click.SetValueWithoutNotify(click.choices[current.HasClickAction ? (int)current.ClickAction + 1 : 0]);
                edits.SetEnabled(!Deck().Locked);
            }
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            { data.Changed += Sync; Sync(); });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            { data.Changed -= Sync; });
            return root;
        }

        private void DeleteDeck(JumpDeckCollection deck)
        {
            if (deck.Locked) return;
            if (EditorUtility.DisplayDialog("Delete Deck?",
                $"Delete '{deck.DisplayName}' and its {deck.Pins.Count} pin(s)?", "Delete", "Cancel"))
                JumpDeckService.DeleteDeck(data, deck);
        }

        private void MovePinToDeck(JumpDeckCollection source, JumpDeckCollection target, JumpPin pin)
        {
            if (!JumpDeckService.MovePin(data, source, target, pin, target.Pins.Count))
                ShowNotification(new GUIContent(source.Locked || target.Locked ? "Deck is locked." : "That object is already pinned in this deck."));
        }

        private void RemovePin(JumpDeckCollection deck, JumpPin pin) =>
            JumpDeckService.RemovePin(data, deck, pin);

        private void OpenSceneAdditively(string path)
        {
            try { EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
            catch (Exception error) { ShowNotification(new GUIContent(error.Message)); }
        }
    }
}
