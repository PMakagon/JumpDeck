using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace JumpDeck.Editor.Tests
{
    internal sealed class JumpDeckInteractionTestWindow : EditorWindow
    {
        internal JumpDeckView View { get; private set; }
        internal readonly System.Collections.Generic.List<string> StartedDrags = new();
        public void CreateGUI()
        {
            if (View != null) return;
            View = new JumpDeckView(JumpDeckData.instance, null, null, title =>
            {
                Assert.That(Event.current?.type, Is.EqualTo(EventType.MouseDown).Or.EqualTo(EventType.MouseDrag),
                    "The native drag starter requires an unconsumed mouse event");
                StartedDrags.Add(title);
            });
            rootVisualElement.Add(View);
        }
        private void OnDisable() => View?.Dispose();
    }

    public sealed class JumpDeckTests
    {
        private JumpDeckData data;
        private string originalData;
        private string folder;
        private SceneSetup[] scenes;
        private (bool Counts, bool Minimize) originalGlobalSettings;

        [SetUp]
        public void SetUp()
        {
            var global = JumpDeckViewSettings.instance;
            originalGlobalSettings = (global.ShowPinCounts, global.MinimizeHeaders);
            global.UpdateGlobal(false, false);
            data = JumpDeckData.instance;
            data.EnsureInitialized();
            originalData = EditorJsonUtility.ToJson(data);
            scenes = EditorSceneManager.GetSceneManagerSetup();
            folder = "Assets/JumpDeckTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            var baseline = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(baseline, folder + "/Baseline.unity");
            data.Decks.Clear();
            data.Decks.Add(JumpDeckCollection.Create("Test"));
            Undo.ClearUndo(data);
        }

        [TearDown]
        public void TearDown()
        {
            JumpDeckViewSettings.instance.UpdateGlobal(originalGlobalSettings.Counts, originalGlobalSettings.Minimize);
            Selection.objects = Array.Empty<UnityEngine.Object>();
            if (scenes.Any(scene => scene.isLoaded && scene.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(scenes);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(folder);
            Undo.ClearUndo(data);
            EditorJsonUtility.FromJsonOverwrite(originalData, data);
            data.SaveData();
        }

        [Test]
        public void LegacyStorageMigratesIntoOneContainerWithoutChangingDecksOrPins()
        {
            var first = JumpDeckCollection.Create("Lighting");
            first.DisplaySettings.Update(48, true, false);
            first.Locked = true;
            first.Expanded = false;
            first.Pins.Add(JumpPin.ForObject("Asset", "stable-object", "stable-guid", 42, "Assets/Asset.asset", true, JumpPinClickAction.Ping));
            var second = JumpDeckCollection.Create("UI");
            second.Pins.Add(JumpPin.ForQuery("Lights", "h: t:Light", SceneQueryScope.ActiveScene));
            string legacy = "{\"deckSet\":{\"id\":\"\",\"displayName\":\"\",\"decks\":[]},\"decks\":[" + JsonUtility.ToJson(first) + "," + JsonUtility.ToJson(second) + "]}";
            EditorJsonUtility.FromJsonOverwrite("{\"MonoBehaviour\":" + legacy + "}", data);
            string beforeMigration = EditorJsonUtility.ToJson(data);
            Assert.That(beforeMigration, Does.Contain(first.Id), "Legacy fields must deserialize before migration: " + beforeMigration);
            data.EnsureInitialized();
            Assert.That(data.Set.Id, Is.Not.Empty);
            Assert.That(data.Decks.Select(deck => deck.Id), Is.EqualTo(new[] { first.Id, second.Id }),
                "Before: " + beforeMigration + "\nAfter: " + EditorJsonUtility.ToJson(data));
            Assert.That(data.Decks[0].Pins[0].Id, Is.EqualTo(first.Pins[0].Id));
            Assert.That(data.Decks[0].Pins[0].GlobalObjectId, Is.EqualTo("stable-object"));
            Assert.That(data.Decks[0].Pins[0].LocalFileId, Is.EqualTo(42));
            Assert.That(data.Decks[0].Pins[0].ClickAction, Is.EqualTo(JumpPinClickAction.Ping));
            Assert.That(data.Decks[0].Locked, Is.True);
            Assert.That(data.Decks[0].Expanded, Is.False);
            Assert.That(data.Decks[0].DisplaySettings.IconSize, Is.EqualTo(48));
            Assert.That(data.Decks[1].Pins[0].Id, Is.EqualTo(second.Pins[0].Id));
            Assert.That(data.Decks[1].Pins[0].QueryScope, Is.EqualTo(SceneQueryScope.ActiveScene));
            string setId = data.Set.Id;
            data.EnsureInitialized();
            Assert.That(data.Set.Id, Is.EqualTo(setId));
            Assert.That(data.Decks.Count, Is.EqualTo(2));
            string backup = Path.GetFullPath("UserSettings/JumpDeck.asset.before-set");
            Assert.That(File.Exists(backup), Is.True);
        }

        [Test]
        public void NestedStorageRoundtripPreservesContainerAndDeckIdentity()
        {
            var deck = data.Decks[0];
            deck.DisplaySettings.Update(32, false, true);
            deck.Pins.Add(JumpPin.ForQuery("Lights", "h: t:Light", SceneQueryScope.ActiveScene));
            string setId = data.Set.Id;
            string saved = EditorJsonUtility.ToJson(data);
            EditorJsonUtility.FromJsonOverwrite("{\"MonoBehaviour\":{\"deckSet\":{\"id\":\"\",\"displayName\":\"\",\"decks\":[]},\"decks\":[]}}", data);
            Assert.That(EditorJsonUtility.ToJson(data), Does.Not.Contain(deck.Id));
            EditorJsonUtility.FromJsonOverwrite(saved, data);
            data.EnsureInitialized();
            Assert.That(data.Set.Id, Is.EqualTo(setId));
            Assert.That(data.Decks[0].Id, Is.EqualTo(deck.Id));
            Assert.That(data.Decks[0].Pins[0].Id, Is.EqualTo(deck.Pins[0].Id));
            Assert.That(data.Decks[0].DisplaySettings.IconSize, Is.EqualTo(32));
            Assert.That(data.Decks[0].DisplaySettings.ShowOpenButton, Is.True);
        }

        [UnityTest]
        public IEnumerator QueryReferenceFiltersFindTheExpectedSceneObject()
        {
            var parent = new GameObject("Gameplay");
            var target = new GameObject("Enemies", typeof(Light)) { tag = "Respawn", layer = 8 };
            target.transform.SetParent(parent.transform);
            double settleUntil = EditorApplication.timeSinceStartup + .2;
            while (EditorApplication.timeSinceStartup < settleUntil) yield return null;
            foreach (string query in new[] {
                "h: t:Light", "h: tag:Respawn", "h: layer:8", "h: path:Gameplay/Enemies",
                "h: t:Light layer:8 -tag:EditorOnly", "h: t:Light or t:Camera" })
            {
                var pin = JumpPin.ForQuery("Reference", query, SceneQueryScope.AllLoadedScenes);
                data.Decks[0].Pins.Add(pin);
                using (var session = new JumpDeckSession(data))
                {
                    session.RunQuery(pin);
                    double deadline = EditorApplication.timeSinceStartup + 5;
                    while (session.RunningQueries.Contains(pin.Id) && EditorApplication.timeSinceStartup < deadline)
                        yield return null;
                    Assert.That(session.QueryErrors.ContainsKey(pin.Id), Is.False, query);
                    Assert.That(session.QueryResults.TryGetValue(pin.Id, out var results), Is.True, query);
                    Assert.That(results, Has.Member(target), query);
                }
            }
        }

        [Test]
        public void DeletedSubAssetNeverResolvesToMainAsset()
        {
            var main = ScriptableObject.CreateInstance<JumpDeckTestAsset>();
            var sub = ScriptableObject.CreateInstance<JumpDeckTestAsset>();
            sub.name = "SubAsset";
            string path = folder + "/Asset.asset";
            AssetDatabase.CreateAsset(main, path);
            AssetDatabase.AddObjectToAsset(sub, main);
            AssetDatabase.SaveAssets();
            Assert.That(JumpDeckObjectUtility.TryCreatePin(sub, out var pin, out _), Is.True);
            Assert.That(JumpDeckObjectUtility.Resolve(pin), Is.SameAs(sub));
            UnityEngine.Object.DestroyImmediate(sub, true);
            AssetDatabase.SaveAssets();
            var reference = JumpDeckObjectUtility.ResolveReference(pin);
            Assert.That(reference.Target, Is.Null);
            Assert.That(reference.Status, Is.EqualTo(JumpDeckReferenceStatus.Missing));
        }

        [Test]
        public void ScenePinDistinguishesClosedSceneFromDeletedObject()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var target = new GameObject("Pinned");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(target, scene);
            string path = folder + "/Scene.unity";
            EditorSceneManager.SaveScene(scene, path);
            Assert.That(JumpDeckObjectUtility.TryCreatePin(target, out var pin, out _), Is.True);
            EditorSceneManager.CloseScene(scene, true);
            Assert.That(JumpDeckObjectUtility.ResolveReference(pin).Status,
                Is.EqualTo(JumpDeckReferenceStatus.SceneNotLoaded));
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            Assert.That(JumpDeckObjectUtility.Resolve(pin), Is.Not.Null);
            UnityEngine.Object.DestroyImmediate(JumpDeckObjectUtility.Resolve(pin));
            Assert.That(JumpDeckObjectUtility.ResolveReference(pin).Status,
                Is.EqualTo(JumpDeckReferenceStatus.Missing));
            EditorSceneManager.CloseScene(scene, true);
        }

        [Test]
        public void RenameDeckSupportsUndoAndRedo()
        {
            JumpDeckService.RenameDeck(data, data.Decks[0], "Lighting");
            Undo.FlushUndoRecordObjects();
            Assert.That(data.Decks[0].DisplayName, Is.EqualTo("Lighting"));
            Undo.PerformUndo();
            Assert.That(data.Decks[0].DisplayName, Is.EqualTo("Test"));
            Undo.PerformRedo();
            Assert.That(data.Decks[0].DisplayName, Is.EqualTo("Lighting"));
        }

        [Test]
        public void UndoIsSavedOnceWithMultipleSessionsAndAfterTheyClose()
        {
            int saves = 0;
            void OnSaved() => saves++;
            JumpDeckService.RenameDeck(data, data.Decks[0], "Lighting");
            Undo.FlushUndoRecordObjects();
            JumpDeckData.Changed += OnSaved;
            try
            {
                using (var first = new JumpDeckSession(data))
                using (var second = new JumpDeckSession(data))
                {
                    Undo.PerformUndo();
                    Assert.That(saves, Is.EqualTo(1));
                    Assert.That(File.ReadAllText(Path.GetFullPath("UserSettings/JumpDeck.asset")), Does.Contain("displayName: Test"));
                }
                saves = 0;
                Undo.PerformRedo();
                Assert.That(saves, Is.EqualTo(1));
                Assert.That(File.ReadAllText(Path.GetFullPath("UserSettings/JumpDeck.asset")), Does.Contain("displayName: Lighting"));
            }
            finally { JumpDeckData.Changed -= OnSaved; }
        }

        [Test]
        public void TransientAndDestroyedObjectsCannotCreateUnusablePins()
        {
            var transient = ScriptableObject.CreateInstance<JumpDeckTestAsset>();
            transient.name = "Transient";
            try
            {
                Assert.That(JumpDeckObjectUtility.TryCreatePin(transient, out _, out var error), Is.False);
                Assert.That(error, Does.Contain("cannot save a reference"));
            }
            finally { UnityEngine.Object.DestroyImmediate(transient); }
            Assert.That(JumpDeckObjectUtility.TryCreatePin(transient, out _, out _), Is.False);
        }

        [Test]
        public void EditQueryPreservesIdentityAndSupportsUndo()
        {
            var pin = JumpPin.ForQuery("Lights", "h: t:Light", SceneQueryScope.AllLoadedScenes);
            data.Decks[0].Pins.Add(pin);
            string id = pin.Id;
            JumpDeckService.EditQuery(data, pin, "Cameras", "h: t:Camera", SceneQueryScope.ActiveScene);
            Undo.FlushUndoRecordObjects();
            Assert.That(pin.Id, Is.EqualTo(id));
            Undo.PerformUndo();
            var restored = data.Decks[0].Pins[0];
            Assert.That(restored.Id, Is.EqualTo(id));
            Assert.That(restored.Query, Is.EqualTo("h: t:Light"));
            Assert.That(restored.QueryScope, Is.EqualTo(SceneQueryScope.AllLoadedScenes));
        }

        [Test]
        public void MovePinUsesInsertionIndexInBothDirections()
        {
            var deck = data.Decks[0];
            var a = JumpPin.ForQuery("A", "h: a", SceneQueryScope.AllLoadedScenes);
            var b = JumpPin.ForQuery("B", "h: b", SceneQueryScope.AllLoadedScenes);
            var c = JumpPin.ForQuery("C", "h: c", SceneQueryScope.AllLoadedScenes);
            deck.Pins.AddRange(new[] { a, b, c });
            Assert.That(JumpDeckService.MovePin(data, deck, deck, a, 3), Is.True);
            Assert.That(deck.Pins.Select(pin => pin.DisplayName), Is.EqualTo(new[] { "B", "C", "A" }));
            Assert.That(JumpDeckService.MovePin(data, deck, deck, a, 0), Is.True);
            Assert.That(deck.Pins.Select(pin => pin.DisplayName), Is.EqualTo(new[] { "A", "B", "C" }));
        }

        [Test]
        public void MoveToDeckRejectsDuplicateObject()
        {
            var source = data.Decks[0];
            var destination = JumpDeckCollection.Create("Other");
            data.Decks.Add(destination);
            var pin = JumpPin.ForObject("Object", "identity", "guid", 12, "path");
            source.Pins.Add(pin);
            destination.Pins.Add(JumpPin.ForObject("Same", "identity", "guid", 12, "path"));
            Assert.That(JumpDeckService.MovePin(data, source, destination, pin, 1), Is.False);
            Assert.That(source.Pins, Does.Contain(pin));
            Assert.That(destination.Pins.Count, Is.EqualTo(1));
        }

        [Test]
        public void ExportImportPreservesActionsAndQueriesWithFreshPinIds()
        {
            var deck = data.Decks[0];
            JumpDeckService.SetDisplaySettings(data, deck, 48, true, false);
            deck.Pins.Add(JumpPin.ForObject("Asset", "", "missing-guid", 42, "Assets/Old.asset", true, JumpPinClickAction.Ping));
            deck.Pins.Add(JumpPin.ForQuery("Lights", "h: t:Light", SceneQueryScope.ActiveScene));
            deck.Locked = true;
            string path = Path.GetFullPath(folder + "/Deck.jumpdeck");
            File.WriteAllText(path, JumpDeckFileCodec.Serialize(deck));
            Assert.That(JumpDeckFileCodec.TryReadFile(path, out var file, out var error), Is.True, error);
            Assert.That(JumpDeckFileCodec.TryBuildDeck(file, path, out var imported, out _, out error), Is.True, error);
            Assert.That(imported.Locked, Is.True);
            Assert.That(imported.DisplaySettings.IconSize, Is.EqualTo(48));
            Assert.That(imported.DisplaySettings.ShowPingButton, Is.True);
            Assert.That(imported.DisplaySettings.ShowOpenButton, Is.False);
            Assert.That(imported.Pins.Count, Is.EqualTo(2));
            Assert.That(imported.Pins[0].Id, Is.Not.EqualTo(deck.Pins[0].Id));
            Assert.That(imported.Pins[0].ClickAction, Is.EqualTo(JumpPinClickAction.Ping));
            Assert.That(imported.Pins[0].LocalFileId, Is.EqualTo(42));
            Assert.That(imported.Pins[1].QueryScope, Is.EqualTo(SceneQueryScope.ActiveScene));
        }

        [Test]
        public void DeckDisplayChangesSupportUndoAndDoNotAffectOtherDecks()
        {
            var other = JumpDeckCollection.Create("UI");
            data.Decks.Add(other);
            JumpDeckService.SetDisplaySettings(data, data.Decks[0], 64, true, true);
            Undo.FlushUndoRecordObjects();
            Assert.That(other.DisplaySettings.IconSize, Is.EqualTo(16));
            Assert.That(other.DisplaySettings.ShowPingButton, Is.False);
            Undo.PerformUndo();
            Assert.That(data.Decks[0].DisplaySettings.IconSize, Is.EqualTo(16));
            Undo.PerformRedo();
            Assert.That(data.Decks[0].DisplaySettings.IconSize, Is.EqualTo(64));
            Assert.That(data.Decks[1].DisplaySettings.IconSize, Is.EqualTo(16));
            JumpDeckService.SetDisplaySettings(data, data.Decks[0], 16, false, false);
            Assert.That(data.Decks[0].DisplaySettings.ShowOpenButton, Is.False);
        }

        [Test]
        public void LegacyExportUsesDefaultDisplayAndImportedSizeIsClamped()
        {
            string path = Path.GetFullPath(folder + "/Legacy.jumpdeck");
            File.WriteAllText(path, "{\"formatVersion\":1,\"deckName\":\"Legacy\",\"pins\":[]}");
            Assert.That(JumpDeckFileCodec.TryReadFile(path, out var file, out var error), Is.True, error);
            Assert.That(JumpDeckFileCodec.TryBuildDeck(file, path, out var imported, out _, out error), Is.True, error);
            Assert.That(imported.DisplaySettings.IconSize, Is.EqualTo(16));
            Assert.That(imported.DisplaySettings.ShowOpenButton, Is.False);
            File.WriteAllText(path, "{\"formatVersion\":1,\"pins\":[],\"displaySettings\":{\"iconSize\":999,\"showPingButton\":true}}");
            Assert.That(JumpDeckFileCodec.TryReadFile(path, out file, out error), Is.True, error);
            Assert.That(JumpDeckFileCodec.TryBuildDeck(file, path, out imported, out _, out error), Is.True, error);
            Assert.That(imported.DisplaySettings.IconSize, Is.EqualTo(64));
            Assert.That(imported.DisplaySettings.ShowPingButton, Is.True);
        }

        [Test]
        public void UnsupportedDeckFormatIsRejected()
        {
            string path = Path.GetFullPath(folder + "/Future.jumpdeck");
            File.WriteAllText(path, "{\"formatVersion\":99,\"pins\":[]}");
            Assert.That(JumpDeckFileCodec.TryReadFile(path, out _, out var error), Is.False);
            Assert.That(error, Does.Contain("99"));
        }

        [Test]
        public void MissingFormatAndUnknownQueryScopeAreRejectedWithoutChangingStoredDecks()
        {
            string path = Path.GetFullPath(folder + "/Invalid.jumpdeck");
            File.WriteAllText(path, "{\"deckName\":\"Unrelated JSON\",\"pins\":[]}");
            Assert.That(JumpDeckFileCodec.TryReadFile(path, out _, out _), Is.False);
            File.WriteAllText(path, "{\"formatVersion\":1,\"pins\":[{\"kind\":\"sceneQuery\",\"query\":\"h: t:Light\",\"queryScope\":99}]}");
            Assert.That(JumpDeckFileCodec.TryReadFile(path, out var file, out var error), Is.True, error);
            Assert.That(JumpDeckFileCodec.TryBuildDeck(file, path, out _, out _, out error), Is.False);
            Assert.That(error, Does.Contain("scope"));
            Assert.That(data.Decks.Count, Is.EqualTo(1));
            Assert.That(data.Decks[0].Pins, Is.Empty);
        }

        [UnityTest]
        public IEnumerator InvalidQueryShowsAnErrorAndStopsRunning()
        {
            var pin = JumpPin.ForQuery("Broken", "h: (t:Light", SceneQueryScope.AllLoadedScenes);
            data.Decks[0].Pins.Add(pin);
            using (var session = new JumpDeckSession(data))
            {
                session.RunQuery(pin);
                double deadline = EditorApplication.timeSinceStartup + 5;
                while (session.RunningQueries.Contains(pin.Id) && EditorApplication.timeSinceStartup < deadline)
                    yield return null;
                Assert.That(session.RunningQueries.Contains(pin.Id), Is.False);
                Assert.That(session.QueryErrors.ContainsKey(pin.Id), Is.True);
                Assert.That(session.QueryResults.ContainsKey(pin.Id), Is.False);
            }
        }

        [UnityTest]
        public IEnumerator RemovingAQueryCannotRestoreItsResultOrSelectionLater()
        {
            var deck = data.Decks[0];
            var pin = JumpPin.ForQuery("Search", "h: t:Transform", SceneQueryScope.AllLoadedScenes);
            deck.Pins.Add(pin);
            using (var session = new JumpDeckSession(data))
            {
                session.RunQuery(pin);
                JumpDeckService.RemovePin(data, deck, pin);
                Selection.objects = Array.Empty<UnityEngine.Object>();
                for (int i = 0; i < 8; i++) yield return null;
                Assert.That(session.QueryResults.ContainsKey(pin.Id), Is.False);
                Assert.That(session.RunningQueries.Contains(pin.Id), Is.False);
                Assert.That(Selection.objects, Is.Empty);
            }
        }

        [UnityTest]
        public IEnumerator PanelCanDetachAndReattachWithoutKeepingSearchSubscriptions()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var panel = new JumpDeckView(data);
            int changes = 0;
            panel.DataChanged += () => changes++;
            try
            {
                window.rootVisualElement.Add(panel);
                window.Show();
                yield return null;
                panel.RemoveFromHierarchy();
                JumpDeckService.RenameDeck(data, data.Decks[0], "Detached");
                Assert.That(changes, Is.EqualTo(0));
                window.rootVisualElement.Add(panel);
                yield return null;
                JumpDeckService.RenameDeck(data, data.Decks[0], "Renamed");
                double deadline = EditorApplication.timeSinceStartup + 2;
                while (panel.Q<Label>(className: "deck-title").text != "Renamed" && EditorApplication.timeSinceStartup < deadline)
                    yield return null;
                Assert.That(changes, Is.EqualTo(1));
                Assert.That(panel.Q<Label>(className: "deck-title").text, Is.EqualTo("Renamed"));
                panel.Dispose();
                panel.Dispose();
                JumpDeckService.RenameDeck(data, data.Decks[0], "Disposed");
                Assert.That(changes, Is.EqualTo(1));
            }
            finally { panel.Dispose(); window.Close(); }
        }

        [UnityTest]
        public IEnumerator MenuButtonFocusDoesNotChangeRowSelection()
        {
            var deck = data.Decks[0];
            deck.Pins.Add(JumpPin.ForQuery("A", "h: a", SceneQueryScope.AllLoadedScenes));
            deck.Pins.Add(JumpPin.ForQuery("B", "h: b", SceneQueryScope.AllLoadedScenes));
            var window = OpenTestView(out var view);
            try
            {
                yield return null;
                var first = view.Q("pin-" + deck.Pins[0].Id);
                var second = view.Q("pin-" + deck.Pins[1].Id);
                first.Focus();
                yield return null;
                Assert.That(first.ClassListContains("selected-pin"), Is.True);
                second.Q<Button>("jumpdeck-pin-menu").Focus();
                yield return null;
                Assert.That(first.ClassListContains("selected-pin"), Is.True);
                Assert.That(second.ClassListContains("selected-pin"), Is.False);
            }
            finally { view.Dispose(); window.Close(); }
        }

        [UnityTest]
        public IEnumerator DisplaySettingsControlsUpdateIconSizeAndSeparateButtons()
        {
            AddTestObjectPin();
            var other = JumpDeckCollection.Create("Compact UI");
            other.Pins.Add(JumpPin.ForQuery("UI", "h: ui", SceneQueryScope.AllLoadedScenes));
            data.Decks.Add(other);
            var window = OpenTestView(out var view);
            VisualElement settingsContent = null;
            try
            {
                yield return null;
                Assert.That(view.Q<Button>("jumpdeck-pin-ping"), Is.Null);
                Assert.That(view.Q<Button>("jumpdeck-pin-open"), Is.Null);
                var gear = view.Q<Button>("jumpdeck-settings");
                Assert.That(gear, Is.Not.Null);
                settingsContent = new JumpDeckSettingsPopup(data, data.Decks[0]).CreateContent();
                settingsContent.style.position = Position.Absolute;
                window.rootVisualElement.Add(settingsContent);
                var slider = settingsContent.Q<SliderInt>("jumpdeck-icon-size");
                Assert.That(slider, Is.Not.Null);
                var toggles = settingsContent.Query<Toggle>().ToList();
                var ping = toggles.Single(toggle => toggle.label == "Show Ping button");
                var open = toggles.Single(toggle => toggle.label == "Show Open/Inspect button");
                slider.value = 48;
                ping.value = true;
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That(view.Q<Image>(className: "pin-icon").resolvedStyle.width, Is.EqualTo(48));
                Assert.That(view.Query<Image>(className: "pin-icon").ToList()[1].resolvedStyle.width, Is.EqualTo(16));
                Assert.That(view.Q(className: "pin-row").resolvedStyle.height, Is.GreaterThanOrEqualTo(56));
                var pingButton = view.Q<Button>("jumpdeck-pin-ping");
                Assert.That(pingButton, Is.Not.Null);
                Assert.That(pingButton.resolvedStyle.width, Is.EqualTo(54));
                Assert.That(pingButton.Q<JumpDeckActionIcon>().resolvedStyle.width, Is.EqualTo(46));
                Assert.That(view.Q<Button>("jumpdeck-pin-menu").resolvedStyle.width, Is.EqualTo(54));
                Assert.That(view.Q<Button>("jumpdeck-pin-menu").resolvedStyle.fontSize, Is.EqualTo(36));
                Assert.That(view.Q<Button>("jumpdeck-pin-open"), Is.Null);
                ping.value = false;
                open.value = true;
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That(view.Q<Button>("jumpdeck-pin-ping"), Is.Null);
                Assert.That(view.Q<Button>("jumpdeck-pin-open").resolvedStyle.width, Is.EqualTo(54));
                settingsContent.RemoveFromHierarchy();
                view.Dispose();
                view.RemoveFromHierarchy();
                view = new JumpDeckView(data);
                window.rootVisualElement.Add(view);
                yield return null;
                Assert.That(view.Q<Image>(className: "pin-icon").style.width.value.value, Is.EqualTo(48));
                Assert.That(view.Q<Button>("jumpdeck-pin-open"), Is.Not.Null);
            }
            finally { settingsContent?.RemoveFromHierarchy(); view.Dispose(); window.Close(); }
        }

        [UnityTest]
        public IEnumerator PinRowCanReorderAndMoveBetweenDecksWithMouseEvents()
        {
            var deck = data.Decks[0];
            var a = JumpPin.ForQuery("A", "h: a", SceneQueryScope.AllLoadedScenes);
            var b = JumpPin.ForQuery("B", "h: b", SceneQueryScope.AllLoadedScenes);
            var c = JumpPin.ForQuery("C", "h: c", SceneQueryScope.AllLoadedScenes);
            deck.Pins.AddRange(new[] { a, b, c });
            var other = JumpDeckCollection.Create("Other");
            data.Decks.Add(other);
            var window = OpenTestView(out var view);
            try
            {
                for (int i = 0; i < 3; i++) yield return null;
                double settleUntil = EditorApplication.timeSinceStartup + .5;
                while (EditorApplication.timeSinceStartup < settleUntil) yield return null;
                yield return BeginMouseDrag(window, () => view.Q("pin-" + a.Id));
                Assert.That(DragAndDrop.GetGenericData("JumpDeck.Pin"), Is.Not.Null, "The gesture must start a pin drag");
                var last = view.Q("pin-" + c.Id).worldBound;
                yield return DropAt(window, new Vector2(last.center.x, last.yMax - 2));
                for (int i = 0; i < 3; i++) yield return null;
                Assert.That(deck.Pins.Select(pin => pin.DisplayName), Is.EqualTo(new[] { "B", "C", "A" }));
                yield return BeginMouseDrag(window, () => view.Q("pin-" + a.Id));
                var otherHeader = view.Query<VisualElement>(className: "deck-header").ToList()[1];
                yield return DropAt(window, otherHeader.worldBound.center);
                yield return null;
                Assert.That(deck.Pins, Has.No.Member(a));
                Assert.That(other.Pins, Does.Contain(a));
            }
            finally { EndMouseDrag(window); view.Dispose(); window.Close(); }
        }

        [UnityTest]
        public IEnumerator AdjacentPinsAndDecksExchangePlacesInBothDirections()
        {
            var deck = data.Decks[0];
            var a = JumpPin.ForQuery("A", "h: a", SceneQueryScope.AllLoadedScenes);
            var b = JumpPin.ForQuery("B", "h: b", SceneQueryScope.AllLoadedScenes);
            deck.Pins.AddRange(new[] { a, b });
            var other = JumpDeckCollection.Create("Other");
            data.Decks.Add(other);
            var window = OpenTestView(out var view);
            try
            {
                for (int i = 0; i < 3; i++) yield return null;
                double settleUntil = EditorApplication.timeSinceStartup + .5;
                while (EditorApplication.timeSinceStartup < settleUntil) yield return null;
                // All four halves must work, including the two previously no-op positions.
                foreach (bool lowerHalf in new[] { false, true })
                {
                    yield return BeginMouseDrag(window, () => view.Q("pin-" + a.Id));
                    var bounds = view.Q("pin-" + b.Id).worldBound;
                    yield return DropAt(window, new Vector2(bounds.center.x, lowerHalf ? bounds.yMax - 3 : bounds.yMin + 3));
                    Assert.That(deck.Pins.Select(pin => pin.DisplayName), Is.EqualTo(new[] { "B", "A" }), "down, lowerHalf=" + lowerHalf);
                    yield return BeginMouseDrag(window, () => view.Q("pin-" + a.Id));
                    bounds = view.Q("pin-" + b.Id).worldBound;
                    yield return DropAt(window, new Vector2(bounds.center.x, lowerHalf ? bounds.yMax - 3 : bounds.yMin + 3));
                    Assert.That(deck.Pins.Select(pin => pin.DisplayName), Is.EqualTo(new[] { "A", "B" }), "up, lowerHalf=" + lowerHalf);
                }
                var headers = view.Query<VisualElement>(className: "deck-header").ToList();
                yield return BeginMouseDrag(window, () => view.Query<VisualElement>(className: "deck-header").ToList()[0]);
                yield return DropAt(window, view.Query<VisualElement>(className: "deck-header").ToList()[1].worldBound.center);
                Assert.That(data.Decks, Is.EqualTo(new[] { other, deck }));
                headers = view.Query<VisualElement>(className: "deck-header").ToList();
                yield return BeginMouseDrag(window, () => view.Query<VisualElement>(className: "deck-header").ToList()[1]);
                yield return DropAt(window, view.Query<VisualElement>(className: "deck-header").ToList()[0].worldBound.center);
                Assert.That(data.Decks, Is.EqualTo(new[] { deck, other }));
            }
            finally { EndMouseDrag(window); view.Dispose(); window.Close(); }
        }

        [UnityTest]
        public IEnumerator PopupResetAffectsOnlyItsDeckAndSurvivesUndo()
        {
            var deck = data.Decks[0];
            var other = JumpDeckCollection.Create("UI");
            data.Decks.Add(other);
            deck.DisplaySettings.Update(48, true, true);
            other.DisplaySettings.Update(24, true, false);
            var window = OpenTestView(out var view);
            var content = new JumpDeckSettingsPopup(data, deck).CreateContent();
            content.style.position = Position.Absolute;
            window.rootVisualElement.Add(content);
            try
            {
                yield return null;
                var reset = content.Q<Button>("jumpdeck-reset-display");
                using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = reset; reset.SendEvent(evt); }
                yield return null;
                Undo.FlushUndoRecordObjects();
                Assert.That(data.Decks[0].DisplaySettings.IconSize, Is.EqualTo(16));
                Assert.That(data.Decks[1].DisplaySettings.IconSize, Is.EqualTo(24));
                Undo.PerformUndo();
                yield return null;
                Assert.That(content.Q<SliderInt>().value, Is.EqualTo(48));
                content.Q<SliderInt>().value = 32;
                Assert.That(data.Decks[0].DisplaySettings.IconSize, Is.EqualTo(32));
                Assert.That(data.Decks[1].DisplaySettings.ShowPingButton, Is.True);
                var anchor = JumpDeckSettingsPopup.AnchorToRight(new Rect(10, 20, 22, 21));
                Assert.That(anchor.x, Is.GreaterThan(32));
            }
            finally { content.RemoveFromHierarchy(); view.Dispose(); window.Close(); }
        }

        [UnityTest]
        public IEnumerator DraggingPinIconProvidesObjectWithoutActivatingIt()
        {
            var target = AddTestObjectPin();
            var window = OpenTestView(out var view);
            try
            {
                for (int i = 0; i < 3; i++) yield return null;
                Selection.activeObject = null;
                var icon = view.Q<Image>(className: "pin-icon");
                yield return BeginMouseDrag(window, () => view.Q<Image>(className: "pin-icon"));
                Assert.That(DragAndDrop.objectReferences, Is.EqualTo(new UnityEngine.Object[] { target }));
                Assert.That(Selection.activeObject, Is.Null, "Starting a drag must not inspect the pin");
                EndMouseDrag(window);
                yield return null;
                SendMouse(window, EventType.MouseDown, icon.worldBound.center);
                yield return null;
                SendMouse(window, EventType.MouseUp, icon.worldBound.center);
                yield return null;
                Assert.That(Selection.activeObject, Is.SameAs(target), "A new click after a drag must work");
            }
            finally { EndMouseDrag(window); view.Dispose(); window.Close(); }
        }

        [Test]
        public void LockedDeckRejectsEditsButCanBeUnlockedWithUndo()
        {
            var deck = data.Decks[0];
            var pin = JumpPin.ForQuery("A", "h: a", SceneQueryScope.AllLoadedScenes);
            deck.Pins.Add(pin);
            var other = JumpDeckCollection.Create("Other");
            var otherPin = JumpPin.ForQuery("B", "h: b", SceneQueryScope.AllLoadedScenes);
            other.Pins.Add(otherPin);
            data.Decks.Add(other);
            JumpDeckService.SetLocked(data, deck, true);
            Undo.FlushUndoRecordObjects();
            JumpDeckService.RenameDeck(data, deck, "Changed");
            JumpDeckService.RenamePin(data, pin, "Changed");
            JumpDeckService.EditQuery(data, pin, "Changed", "h: b", SceneQueryScope.ActiveScene);
            JumpDeckService.SetDisplaySettings(data, deck, 48, true, true);
            JumpDeckService.AddQuery(data, deck, "New", "h: new", SceneQueryScope.ActiveScene);
            JumpDeckService.RemovePin(data, deck, pin);
            JumpDeckService.DeleteDeck(data, deck);
            JumpDeckService.MoveDeck(data, deck, 2);
            Assert.That(JumpDeckService.MovePin(data, deck, other, pin, 0), Is.False);
            Assert.That(JumpDeckService.MovePin(data, other, deck, otherPin, 0), Is.False);
            Assert.That(JumpDeckService.AddObjects(data, deck, Array.Empty<UnityEngine.Object>(), out _), Is.EqualTo(0));
            Assert.That(deck.DisplayName, Is.EqualTo("Test"));
            Assert.That(pin.DisplayName, Is.EqualTo("A"));
            Assert.That(deck.DisplaySettings.IconSize, Is.EqualTo(16));
            Assert.That(deck.Pins.Count, Is.EqualTo(1));
            Assert.That(data.Decks[0], Is.SameAs(deck));
            Undo.PerformUndo();
            Assert.That(data.Decks[0].Locked, Is.False);
            Undo.PerformRedo();
            Assert.That(data.Decks[0].Locked, Is.True);
            JumpDeckService.SetLocked(data, data.Decks[0], false);
            Assert.That(data.Decks[0].Locked, Is.False);
        }

        [UnityTest]
        public IEnumerator GlobalPopupControlsCountsAndMinimizesOnlyHeaders()
        {
            data.Decks[0].Pins.Add(JumpPin.ForQuery("A", "h: a", SceneQueryScope.AllLoadedScenes));
            var window = OpenTestView(out var view);
            var content = view.CreateGlobalSettings();
            content.style.position = Position.Absolute;
            content.style.left = 500;
            window.rootVisualElement.Add(content);
            try
            {
                yield return WaitForViewLayout(window);
                Assert.That(view.Q<Button>("jumpdeck-settings"), Is.Not.Null);
                AssertPopupFitsContent(content);
                var toolbar = view.Q(className: "jumpdeck-toolbar");
                foreach (var button in toolbar.Query<Button>().ToList())
                    Assert.That(button.worldBound.center.y, Is.EqualTo(toolbar.worldBound.center.y).Within(.5f));
                Assert.That(view.Query<Button>("jumpdeck-settings").ToList().Count, Is.EqualTo(1));
                Assert.That(view.Q<Button>("jumpdeck-deck-menu"), Is.Not.Null);
                Assert.That(view.Q("jumpdeck-drag-handle"), Is.Null);
                Assert.That(view.Q(className: "count-badge"), Is.Null);
                Assert.That(content.Q<Button>("jumpdeck-import"), Is.Not.Null);
                content.Q<Toggle>("jumpdeck-show-counts").value = true;
                yield return WaitForViewLayout(window);
                Assert.That(view.Q<Label>(className: "count-badge").text, Is.EqualTo("1"));
                float headerHeight = view.Q(className: "deck-header").resolvedStyle.height;
                content.Q<Toggle>("jumpdeck-minimize").value = true;
                yield return WaitForViewLayout(window);
                Assert.That(view.Q(className: "deck-header").resolvedStyle.height, Is.LessThan(headerHeight));
                Assert.That(view.Q(className: "deck-title").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                Assert.That(view.Q(className: "pin-row").worldBound.height, Is.GreaterThan(0));
                Assert.That(data.Decks[0].Expanded, Is.True);
                content.Q<Toggle>("jumpdeck-minimize").value = false;
                yield return WaitForViewLayout(window);
                Assert.That(view.Q(className: "deck-title").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            }
            finally { content.RemoveFromHierarchy(); view.Dispose(); window.Close(); }
        }

        [UnityTest]
        public IEnumerator DeckPopupLockDisablesEditingButAllowsUnlock()
        {
            var window = OpenTestView(out var view);
            var content = new JumpDeckSettingsPopup(data, data.Decks[0], _ => { }, _ => { }, _ => { }).CreateContent();
            content.style.position = Position.Absolute;
            window.rootVisualElement.Add(content);
            try
            {
                yield return null;
                AssertPopupFitsContent(content);
                content.Q<Toggle>("jumpdeck-deck-lock").value = true;
                yield return null;
                Assert.That(data.Decks[0].Locked, Is.True);
                Assert.That(content.Q<TextField>("jumpdeck-deck-name").enabledInHierarchy, Is.False);
                Assert.That(content.Q<SliderInt>().enabledInHierarchy, Is.False);
                Assert.That(content.Q<Toggle>("jumpdeck-deck-lock").enabledInHierarchy, Is.True);
                content.Q<Toggle>("jumpdeck-deck-lock").value = false;
                yield return null;
                Assert.That(content.Q<TextField>("jumpdeck-deck-name").enabledInHierarchy, Is.True);
            }
            finally { content.RemoveFromHierarchy(); view.Dispose(); window.Close(); }
        }

        [UnityTest]
        public IEnumerator PinActionPopupUpdatesAfterUndoAndRespectsLock()
        {
            AddTestObjectPin();
            var deck = data.Decks[0];
            var window = OpenTestView(out var view);
            var content = view.CreatePinActions(deck.Id, deck.Pins[0].Id);
            content.style.position = Position.Absolute;
            window.rootVisualElement.Add(content);
            try
            {
                yield return null;
                var click = content.Q<DropdownField>();
                AssertPopupFitsContent(content);
                click.index = 2;
                Undo.FlushUndoRecordObjects();
                Assert.That(data.Decks[0].Pins[0].ClickAction, Is.EqualTo(JumpPinClickAction.Ping));
                Undo.PerformUndo();
                yield return null;
                Assert.That(click.index, Is.EqualTo(0));
                JumpDeckService.SetLocked(data, data.Decks[0], true);
                yield return null;
                Assert.That(content.Q<TextField>().enabledInHierarchy, Is.False);
                Assert.That(click.enabledInHierarchy, Is.False);
            }
            finally { content.RemoveFromHierarchy(); view.Dispose(); window.Close(); }
        }

        [UnityTest]
        public IEnumerator PingButtonDoesNotActivateRowAndLockedPinsStillDragObjectsOut()
        {
            var target = AddTestObjectPin();
            var deck = data.Decks[0];
            JumpDeckService.SetDisplaySettings(data, deck, 16, true, false);
            var window = OpenTestView(out var view);
            try
            {
                yield return WaitForViewLayout(window);
                var icon = view.Q<Image>(className: "pin-icon");
                SendMouse(window, EventType.MouseDown, icon.worldBound.center);
                yield return null;
                SendMouse(window, EventType.MouseUp, icon.worldBound.center);
                yield return null;
                Assert.That(Selection.activeObject, Is.SameAs(target));
                Selection.activeObject = null;
                var ping = view.Q<Button>("jumpdeck-pin-ping");
                SendMouse(window, EventType.MouseDown, ping.worldBound.center);
                yield return null;
                SendMouse(window, EventType.MouseUp, ping.worldBound.center);
                yield return null;
                Assert.That(Selection.activeObject, Is.Null, "Ping must not invoke the row's Inspect click action");
                JumpDeckService.SetLocked(data, data.Decks[0], true);
                yield return BeginMouseDrag(window, () => view.Q<Image>(className: "pin-icon"));
                Assert.That(DragAndDrop.objectReferences, Is.EqualTo(new UnityEngine.Object[] { target }));
                Assert.That(DragAndDrop.GetGenericData("JumpDeck.Pin"), Is.Null);
                Assert.That(Selection.activeObject, Is.Null);
            }
            finally { EndMouseDrag(window); view.Dispose(); window.Close(); }
        }

        [UnityTest]
        public IEnumerator GlobalGearOpensNativePopupAndCreatesDeckWithMinimizeEnabled()
        {
            JumpDeckViewSettings.instance.UpdateGlobal(false, true);
            var window = OpenTestView(out var view);
            EditorWindow popup = null;
            try
            {
                yield return WaitForViewLayout(window);
                var gear = view.Q<Button>("jumpdeck-settings");
                gear.Focus();
                yield return null;
                window.Focus();
                using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = gear; gear.SendEvent(evt); }
                // A popup closes on lost OS focus; inspect its content before yielding to other windows.
                popup = Resources.FindObjectsOfTypeAll<EditorWindow>()
                    .SingleOrDefault(item => item.rootVisualElement.Q<Button>("jumpdeck-create-deck") != null);
                if (popup == null)
                {
                    yield return null;
                    popup = Resources.FindObjectsOfTypeAll<EditorWindow>()
                        .SingleOrDefault(item => item.rootVisualElement.Q<Button>("jumpdeck-create-deck") != null);
                }
                Assert.That(popup, Is.Not.Null, "The gear must open the native settings popup");
                var create = popup.rootVisualElement.Q<Button>("jumpdeck-create-deck");
                popup.Focus();
                create.Focus();
                using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = create; create.SendEvent(evt); }
                yield return WaitForViewLayout(window);
                Assert.That(data.Decks.Count, Is.EqualTo(2));
                var header = view.Q("deck-" + data.Decks[1].Id).Q(className: "deck-header");
                var rename = header.Q<TextField>();
                if (rename != null)
                {
                    Assert.That(header.ClassListContains("minimized-header"), Is.False, "The new deck's rename field must fit");
                    Assert.That(header.resolvedStyle.height, Is.GreaterThanOrEqualTo(rename.resolvedStyle.height));
                }
                else
                    Assert.That(header.ClassListContains("minimized-header"), Is.True, "After committing the name, the header minimizes again");
            }
            finally { if (popup != null) popup.Close(); view.Dispose(); window.Close(); }
        }

        private JumpDeckTestAsset AddTestObjectPin()
        {
            var target = ScriptableObject.CreateInstance<JumpDeckTestAsset>();
            AssetDatabase.CreateAsset(target, folder + "/Pinned.asset");
            Assert.That(JumpDeckObjectUtility.TryCreatePin(target, out var pin, out var error), Is.True, error);
            data.Decks[0].Pins.Add(pin);
            return target;
        }

        private static EditorWindow OpenTestView(out JumpDeckView view)
        {
            var window = ScriptableObject.CreateInstance<JumpDeckInteractionTestWindow>();
            window.position = new Rect(100, 100, 460, 420);
            window.Show();
            window.Focus();
            window.CreateGUI();
            view = window.View;
            return window;
        }

        private static void AssertPopupFitsContent(VisualElement content)
        {
            float bottomGap = content.worldBound.yMax - content.Children().Last().worldBound.yMax;
            Assert.That(bottomGap, Is.InRange(8f, 18f), "The last control must fit with only the popup's bottom padding");
        }

        private static void SendMouse(EditorWindow window, EventType type, Vector2 position) =>
            window.SendEvent(new Event { type = type, mousePosition = position, button = 0,
                clickCount = 1, delta = type == EventType.MouseDrag ? new Vector2(8, 0) : Vector2.zero });

        private static IEnumerator BeginMouseDrag(EditorWindow window, Func<VisualElement> resolveSource)
        {
            yield return WaitForViewLayout(window);
            var source = resolveSource();
            Assert.That(source?.panel, Is.Not.Null, "Drag must start on a currently attached element");
            int started = ((JumpDeckInteractionTestWindow)window).StartedDrags.Count;
            int downs = 0, moves = 0;
            int pressed = 0;
            source.RegisterCallback<MouseDownEvent>(_ => downs++);
            source.RegisterCallback<MouseMoveEvent>(evt => { moves++; pressed = evt.pressedButtons; });
            DragAndDrop.PrepareStartDrag();
            Assert.That(source.worldBound.width, Is.GreaterThan(0));
            Vector2 start = source.ClassListContains("deck-header")
                ? new Vector2(source.worldBound.xMin + 45, source.worldBound.center.y) : source.worldBound.center;
            var picked = source.panel.Pick(start);
            Assert.That(picked == source || source.Contains(picked), Is.True, "The source must be reachable by the mouse");
            SendMouse(window, EventType.MouseDown, start);
            yield return null;
            ((JumpDeckInteractionTestWindow)window).View.Refresh();
            yield return null;
            Assert.That(source.panel, Is.Not.Null, "Refresh must not remove a pin while its mouse gesture is armed");
            SendMouse(window, EventType.MouseDrag, start + new Vector2(8, 0));
            yield return null;
            Assert.That(((JumpDeckInteractionTestWindow)window).StartedDrags.Count, Is.EqualTo(started + 1),
                $"down={downs}, move={moves}, pressed={pressed}, attached={source.panel != null}");
            // Refresh may have rebuilt rows after releasing capture; wait for their layout.
            yield return WaitForViewLayout(window);
        }

        private static IEnumerator WaitForViewLayout(EditorWindow window)
        {
            double settleUntil = EditorApplication.timeSinceStartup + .1;
            double deadline = EditorApplication.timeSinceStartup + 2;
            bool ready;
            do
            {
                window.Repaint();
                yield return null;
                var view = ((JumpDeckInteractionTestWindow)window).View;
                ready = view.Query<VisualElement>(className: "pin-row").ToList()
                    .Concat(view.Query<VisualElement>(className: "deck-header").ToList())
                    .All(element => !float.IsNaN(element.worldBound.x) && !float.IsNaN(element.worldBound.y)
                        && element.worldBound.width > 0 && element.worldBound.height > 0);
            } while ((EditorApplication.timeSinceStartup < settleUntil || !ready)
                && EditorApplication.timeSinceStartup < deadline);
            Assert.That(ready, Is.True, "The refreshed rows must have a usable layout");
        }

        private static IEnumerator DropAt(EditorWindow window, Vector2 position)
        {
            Assert.That(float.IsNaN(position.x) || float.IsNaN(position.y), Is.False,
                "Drop coordinates require a completed UI layout");
            var root = window.rootVisualElement;
            int updates = 0, performs = 0;
            string updateTarget = null, performTarget = null;
            EventCallback<DragUpdatedEvent> onUpdate = evt => { updates++; updateTarget = (evt.target as VisualElement)?.name; };
            EventCallback<DragPerformEvent> onPerform = evt => { performs++; performTarget = (evt.target as VisualElement)?.name; };
            root.RegisterCallback(onUpdate, TrickleDown.TrickleDown);
            root.RegisterCallback(onPerform, TrickleDown.TrickleDown);
            SendMouse(window, EventType.DragUpdated, position);
            yield return null;
            SendMouse(window, EventType.DragPerform, position);
            root.UnregisterCallback(onUpdate, TrickleDown.TrickleDown);
            root.UnregisterCallback(onPerform, TrickleDown.TrickleDown);
            Assert.That(updates, Is.GreaterThan(0), "No drag update reached " + updateTarget);
            Assert.That(performs, Is.GreaterThan(0), "No drop reached " + performTarget);
            yield return null;
            SendMouse(window, EventType.MouseUp, position);
            yield return WaitForViewLayout(window);
        }

        private static void EndMouseDrag(EditorWindow window)
        {
            SendMouse(window, EventType.DragExited, Vector2.zero);
            SendMouse(window, EventType.MouseUp, Vector2.zero);
            DragAndDrop.PrepareStartDrag();
        }
    }
}
