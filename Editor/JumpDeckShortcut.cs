using System.Linq;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace JumpDeck.Editor
{
    public static class JumpDeckShortcut
    {
        internal const string Id = "JumpDeck/Open";
        public static string BindingLabel
        {
            get
            {
                string binding = ShortcutManager.instance.GetShortcutBinding(Id).ToString();
                return binding.Length == 0 ? "Unassigned" : binding;
            }
        }

        internal static VisualElement CreateField()
        {
            var manager = ShortcutManager.instance;
            var row = new VisualElement { name = "jumpdeck-shortcut-field" };
            row.style.flexDirection = FlexDirection.Row;
            row.style.height = 22;
            row.style.flexShrink = 0;
            var label = new Label("Open shortcut");
            label.style.minWidth = 120;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(label);
            bool listening = false;
            var button = new Button { name = "jumpdeck-shortcut", tooltip = "Click, then press a key combination. Escape cancels." };
            button.style.flexGrow = 1;
            row.Add(button);
            void Sync() => button.text = listening ? "Press shortcut…" : BindingLabel;
            button.clicked += () => { listening = true; button.Focus(); Sync(); };
            button.RegisterCallback<FocusOutEvent>(_ => { listening = false; Sync(); });
            button.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (!listening) return;
                evt.StopImmediatePropagation();
                if (evt.keyCode == KeyCode.Escape) { listening = false; Sync(); return; }
                if (evt.keyCode is KeyCode.None or KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.LeftControl
                    or KeyCode.RightControl or KeyCode.LeftShift or KeyCode.RightShift or KeyCode.LeftCommand or KeyCode.RightCommand) return;
                var modifiers = ShortcutModifiers.None;
                if (evt.altKey) modifiers |= ShortcutModifiers.Alt;
                if (evt.shiftKey) modifiers |= ShortcutModifiers.Shift;
                if (Application.platform == RuntimePlatform.OSXEditor)
                {
                    if (evt.commandKey) modifiers |= ShortcutModifiers.Action;
                    if (evt.ctrlKey) modifiers |= ShortcutModifiers.Control;
                }
                else if (evt.ctrlKey) modifiers |= ShortcutModifiers.Action;
                Rebind(new ShortcutBinding(new KeyCombination(evt.keyCode, modifiers)));
                listening = false;
                Sync();
            }, TrickleDown.TrickleDown);
            void BindingChanged(ShortcutBindingChangedEventArgs _) => Sync();
            void ProfileChanged(ActiveProfileChangedEventArgs _) => Sync();
            row.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                manager.shortcutBindingChanged += BindingChanged;
                manager.activeProfileChanged += ProfileChanged;
                Sync();
            });
            row.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                manager.shortcutBindingChanged -= BindingChanged;
                manager.activeProfileChanged -= ProfileChanged;
            });
            Sync();
            return row;
        }

        internal static void Rebind(ShortcutBinding binding)
        {
            var manager = ShortcutManager.instance;
            if (manager.IsProfileReadOnly(manager.activeProfileId))
            {
                string name = "JumpDeck";
                int suffix = 2;
                var profiles = manager.GetAvailableProfileIds().ToArray();
                while (profiles.Contains(name)) name = "JumpDeck " + suffix++;
                manager.CreateProfile(name);
                manager.activeProfileId = name;
            }
            manager.RebindShortcut(Id, binding);
        }
    }
}
