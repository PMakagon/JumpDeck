using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace JumpDeck.Editor
{
    internal sealed class JumpDeckSettingsPopup : JumpDeckPopup
    {
        private readonly IJumpDeckStorage data;
        private readonly string deckId;
        private readonly Action<JumpDeckCollection> export;
        private readonly Action<JumpDeckCollection> delete;
        private readonly Action<JumpDeckCollection> addQuery;
        private JumpDeckCollection Deck => data.Decks.FirstOrDefault(deck => deck.Id == deckId);

        internal JumpDeckSettingsPopup(IJumpDeckStorage data, JumpDeckCollection deck, Action<JumpDeckCollection> export = null,
            Action<JumpDeckCollection> delete = null, Action<JumpDeckCollection> addQuery = null)
        {
            this.data = data;
            deckId = deck.Id;
            this.export = export;
            this.delete = delete;
            this.addQuery = addQuery;
        }

        public override Vector2 GetWindowSize() => new(300, 256
            - (addQuery == null ? 21 : 0) - (delete == null ? 21 : 0) - (export == null ? 21 : 0));

        private void UpdateDisplay(int? iconSize = null, bool? ping = null, bool? open = null)
        {
            var deck = Deck;
            if (deck == null) return;
            var settings = deck.DisplaySettings;
            JumpDeckService.SetDisplaySettings(data, deck, iconSize ?? settings.IconSize,
                ping ?? settings.ShowPingButton, open ?? settings.ShowOpenButton);
        }

        internal override VisualElement CreateContent()
        {
            var root = Root(GetWindowSize());
            var initialDeck = Deck;
            if (initialDeck == null)
            {
                root.Add(new Label("Deck removed"));
                return root;
            }
            var settings = initialDeck.DisplaySettings;
            var title = new Label(initialDeck.DisplayName);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 10;
            root.Add(title);
            var locked = new Toggle("Lock deck") { value = initialDeck.Locked, name = "jumpdeck-deck-lock" };
            locked.RegisterValueChangedCallback(evt =>
            {
                var deck = Deck;
                if (deck != null) JumpDeckService.SetLocked(data, deck, evt.newValue);
            });
            root.Add(locked);
            var edits = new VisualElement();
            root.Add(edits);
            var rename = new TextField("Name") { value = initialDeck.DisplayName, isDelayed = true, name = "jumpdeck-deck-name" };
            rename.RegisterValueChangedCallback(evt =>
            {
                var deck = Deck;
                if (deck != null) JumpDeckService.RenameDeck(data, deck, evt.newValue);
            });
            edits.Add(rename);

            var size = new SliderInt("Icon size", JumpDeckDisplaySettings.MinIconSize, JumpDeckDisplaySettings.MaxIconSize)
            {
                name = "jumpdeck-icon-size",
                value = settings.IconSize,
                showInputField = true
            };
            size.style.marginBottom = 10;
            size.RegisterValueChangedCallback(evt => UpdateDisplay(iconSize: evt.newValue));
            edits.Add(size);

            var ping = new Toggle("Show Ping button") { value = settings.ShowPingButton };
            ping.RegisterValueChangedCallback(evt => UpdateDisplay(ping: evt.newValue));
            edits.Add(ping);
            var open = new Toggle("Show Open/Inspect button") { value = settings.ShowOpenButton };
            open.RegisterValueChangedCallback(evt => UpdateDisplay(open: evt.newValue));
            edits.Add(open);

            edits.Add(new Button(() =>
            {
                UpdateDisplay(JumpDeckDisplaySettings.DefaultIconSize, false, false);
            }) { text = "Reset to defaults", name = "jumpdeck-reset-display" });
            edits.Add(new Button(() =>
            {
                var deck = Deck;
                if (deck != null) JumpDeckService.AddObjects(data, deck, Selection.objects, out _);
            }) { text = "Add current selection" });
            if (addQuery != null) edits.Add(new Button(() => { if (Deck is { } deck) addQuery(deck); }) { text = "Add Live Pin…" });
            if (delete != null) edits.Add(new Button(() => { if (Deck is { } deck) delete(deck); }) { text = "Delete deck…" });
            if (export != null) root.Add(new Button(() => { if (Deck is { } deck) export(deck); }) { text = "Export deck…" });
            void Sync()
            {
                var deck = Deck;
                root.SetEnabled(deck != null);
                title.text = deck?.DisplayName ?? "Deck removed";
                if (deck == null) return;
                locked.SetValueWithoutNotify(deck.Locked);
                edits.SetEnabled(!deck.Locked);
                rename.SetValueWithoutNotify(deck.DisplayName);
                size.SetValueWithoutNotify(deck.DisplaySettings.IconSize);
                ping.SetValueWithoutNotify(deck.DisplaySettings.ShowPingButton);
                open.SetValueWithoutNotify(deck.DisplaySettings.ShowOpenButton);
            }
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                data.Changed += Sync;
                Sync();
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                data.Changed -= Sync;
            });
            return root;
        }
    }
}
