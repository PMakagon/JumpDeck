using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace JumpDeck.Editor
{
    // A view owns a session. Detaching a view cancels its searches and editor subscriptions.
    internal sealed class JumpDeckSession : IDisposable
    {
        private readonly IJumpDeckStorage data;
        private readonly Dictionary<string, JumpDeckReference> references = new();
        private readonly Dictionary<string, SearchContext> searches = new();
        private readonly Dictionary<string, string> querySignatures = new();
        private bool disposed;
        internal readonly Dictionary<string, List<GameObject>> QueryResults = new();
        internal readonly HashSet<string> RunningQueries = new();
        internal readonly Dictionary<string, string> QueryErrors = new();
        internal event Action Changed;

        internal JumpDeckSession(IJumpDeckStorage data)
        {
            this.data = data;
            data.Changed += HandleDataChanged;
            EditorApplication.projectChanged += HandleEditorChanged;
            EditorApplication.hierarchyChanged += HandleEditorChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode += HandleActiveSceneChanged;
            Undo.undoRedoPerformed += HandleUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
        }

        internal JumpDeckReference Resolve(JumpPin pin)
        {
            if (references.TryGetValue(pin.Id, out var reference))
            {
                if (reference.Status != JumpDeckReferenceStatus.Available || reference.Target != null)
                    return reference;
            }
            reference = JumpDeckObjectUtility.ResolveReference(pin);
            references[pin.Id] = reference;
            return reference;
        }

        internal void RunQuery(JumpPin pin)
        {
            if (disposed || !JumpDeckService.Contains(data, pin) || RunningQueries.Contains(pin.Id)) return;
            Scene activeScene = SceneManager.GetActiveScene();
            string signature = Signature(pin);
            SearchContext context = null;
            querySignatures[pin.Id] = signature;
            RunningQueries.Add(pin.Id);
            QueryResults.Remove(pin.Id);
            QueryErrors.Remove(pin.Id);
            try
            {
                context = SearchService.CreateContext(pin.Query);
                searches[pin.Id] = context;
                Changed?.Invoke();
                SearchService.Request(context, (_, items) =>
                {
                    if (!IsCurrent(pin, context, signature)) return;
                    try
                    {
                        var errors = JumpDeckSearchDiagnostics.GetErrors(context);
                        if (errors.Any())
                        {
                            QueryErrors[pin.Id] = string.Join("\n", errors.Select(error => error.reason));
                            return;
                        }
                        var objects = new HashSet<GameObject>();
                        foreach (var item in items)
                        {
                            var value = item.ToObject<GameObject>();
                            if (value == null || !value.scene.IsValid() || !value.scene.isLoaded) continue;
                            if (pin.QueryScope == SceneQueryScope.ActiveScene && value.scene != activeScene) continue;
                            objects.Add(value);
                        }
                        var results = objects.OrderBy(value => JumpDeckObjectUtility.HierarchyPath(value.transform), StringComparer.Ordinal).ToList();
                        QueryResults[pin.Id] = results;
                        if (results.Count > 0)
                        {
                            Selection.objects = results.Cast<Object>().ToArray();
                            EditorGUIUtility.PingObject(results[0]);
                            if (results.Count == 1) SceneView.lastActiveSceneView?.FrameSelected();
                        }
                    }
                    catch (Exception error) { QueryErrors[pin.Id] = error.Message; }
                    finally
                    {
                        if (searches.TryGetValue(pin.Id, out var current) && current == context)
                            searches.Remove(pin.Id);
                        RunningQueries.Remove(pin.Id);
                        context.Dispose();
                        if (!disposed) Changed?.Invoke();
                    }
                });
            }
            catch (Exception error)
            {
                searches.Remove(pin.Id);
                RunningQueries.Remove(pin.Id);
                QueryErrors[pin.Id] = error.Message;
                context?.Dispose();
                Changed?.Invoke();
            }
        }

        private bool IsCurrent(JumpPin pin, SearchContext context, string signature) =>
            !disposed && JumpDeckService.Contains(data, pin) && Signature(pin) == signature &&
            searches.TryGetValue(pin.Id, out var current) && current == context;

        private static string Signature(JumpPin pin) => $"{pin.QueryScope}:{pin.Query}";

        private void HandleDataChanged()
        {
            var pins = data.Decks.SelectMany(deck => deck.Pins).ToDictionary(pin => pin.Id);
            foreach (string id in querySignatures.Keys.ToArray())
            {
                if (pins.TryGetValue(id, out var pin) && Signature(pin) == querySignatures[id]) continue;
                Cancel(id);
                QueryResults.Remove(id);
                QueryErrors.Remove(id);
                querySignatures.Remove(id);
            }
            foreach (string id in references.Keys.Where(id => !pins.ContainsKey(id)).ToArray()) references.Remove(id);
            Changed?.Invoke();
        }

        private void Cancel(string id)
        {
            if (searches.TryGetValue(id, out var context))
            {
                searches.Remove(id);
                context.Dispose();
            }
            RunningQueries.Remove(id);
        }

        private void HandleEditorChanged()
        {
            references.Clear();
            foreach (string id in searches.Keys.ToArray()) Cancel(id);
            QueryResults.Clear();
            QueryErrors.Clear();
            querySignatures.Clear();
            Changed?.Invoke();
        }
        private void HandlePlayModeChanged(PlayModeStateChange _) => HandleEditorChanged();
        private void HandleActiveSceneChanged(Scene _, Scene __) => HandleEditorChanged();
        private void HandleUndoRedo() => HandleEditorChanged();

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            data.Changed -= HandleDataChanged;
            EditorApplication.projectChanged -= HandleEditorChanged;
            EditorApplication.hierarchyChanged -= HandleEditorChanged;
            EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= HandleActiveSceneChanged;
            Undo.undoRedoPerformed -= HandleUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            foreach (string id in searches.Keys.ToArray()) Cancel(id);
            references.Clear();
            QueryResults.Clear();
            QueryErrors.Clear();
            querySignatures.Clear();
            Changed = null;
        }
    }
}
