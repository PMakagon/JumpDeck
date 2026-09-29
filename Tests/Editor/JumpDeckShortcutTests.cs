using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace JumpDeck.Editor.Tests
{
    public sealed class JumpDeckShortcutTests
    {
        [UnityTest]
        public IEnumerator SettingsRecordShortcutAndFollowUnityProfileChanges()
        {
            var manager = ShortcutManager.instance;
            string previousProfile = manager.activeProfileId;
            string profile = "JumpDeck test " + Guid.NewGuid().ToString("N");
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            manager.CreateProfile(profile);
            manager.activeProfileId = profile;
            window.Show();
            var row = JumpDeckShortcut.CreateField();
            window.rootVisualElement.Add(row);
            var button = row.Q<Button>("jumpdeck-shortcut");
            try
            {
                yield return null;
                window.Focus();
                button.Focus();
                using (var submit = NavigationSubmitEvent.GetPooled())
                { submit.target = button; button.SendEvent(submit); }
                Assert.That(button.text, Is.EqualTo("Press shortcut…"));
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.F11,
                    modifiers = EventModifiers.Control | EventModifiers.Alt | EventModifiers.Shift });
                yield return null;
                var combination = manager.GetShortcutBinding(JumpDeckShortcut.Id).keyCombinationSequence.Single();
                Assert.That(combination.keyCode, Is.EqualTo(KeyCode.F11));
                Assert.That(combination.modifiers, Is.EqualTo(ShortcutModifiers.Action | ShortcutModifiers.Alt | ShortcutModifiers.Shift));
                Assert.That(button.text, Is.EqualTo(JumpDeckShortcut.BindingLabel));
                var second = new ShortcutBinding(new KeyCombination(KeyCode.F10, ShortcutModifiers.Alt));
                manager.RebindShortcut(JumpDeckShortcut.Id, second);
                Assert.That(button.text, Is.EqualTo(second.ToString()));
                manager.activeProfileId = ShortcutManager.defaultProfileId;
                Assert.That(button.text, Is.EqualTo(JumpDeckShortcut.BindingLabel));
                using (var submit = NavigationSubmitEvent.GetPooled())
                { submit.target = button; button.SendEvent(submit); }
                window.SendEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
                Assert.That(manager.activeProfileId, Is.EqualTo(ShortcutManager.defaultProfileId), "Cancelling must not create a profile");
                Assert.That(button.text, Is.EqualTo(JumpDeckShortcut.BindingLabel));
            }
            finally
            {
                window.Close();
                manager.activeProfileId = previousProfile;
                manager.DeleteProfile(profile);
            }
        }

        [Test]
        public void RebindingDefaultCreatesEditableProfileAndPreservesOtherBindings()
        {
            var manager = ShortcutManager.instance;
            string previous = manager.activeProfileId;
            manager.activeProfileId = ShortcutManager.defaultProfileId;
            var bindings = manager.GetAvailableShortcutIds().Where(id => id != JumpDeckShortcut.Id)
                .ToDictionary(id => id, manager.GetShortcutBinding);
            string created = null;
            try
            {
                JumpDeckShortcut.Rebind(new ShortcutBinding(new KeyCombination(KeyCode.F11, ShortcutModifiers.Alt)));
                created = manager.activeProfileId;
                Assert.That(manager.IsProfileReadOnly(created), Is.False);
                foreach (var binding in bindings)
                    Assert.That(manager.GetShortcutBinding(binding.Key), Is.EqualTo(binding.Value), binding.Key);
            }
            finally
            {
                manager.activeProfileId = previous;
                if (created != null) manager.DeleteProfile(created);
            }
        }
    }
}
