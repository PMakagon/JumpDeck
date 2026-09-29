using System;
using UnityEditor;
using UnityEngine;

namespace JumpDeck.Editor
{
    internal sealed class JumpDeckQueryWindow : EditorWindow
    {
        private Action<string, string, SceneQueryScope> onCreate;
        private string queryName = "New Live Pin";
        private string query = "h: ";
        private SceneQueryScope scope = SceneQueryScope.AllLoadedScenes;
        private bool editing;
        private static readonly Vector2 WindowSize = new(430f, 250f);
        private const string QueryDocumentation = "https://docs.unity3d.com/6000.0/Documentation/Manual/search-scene.html";

        // The creation callback cannot survive an assembly reload.
        private void OnEnable() => AssemblyReloadEvents.beforeAssemblyReload += Close;
        private void OnDisable() => AssemblyReloadEvents.beforeAssemblyReload -= Close;

        internal static void Open(Action<string, string, SceneQueryScope> callback)
        {
            var window = CreateInstance<JumpDeckQueryWindow>();
            window.titleContent = new GUIContent("Create Live Pin");
            window.onCreate = callback;
            window.minSize = WindowSize;
            window.maxSize = window.minSize;
            window.ShowUtility();
            window.CenterOnMainWindow();
        }

        internal static void Edit(JumpPin pin, Action<string, string, SceneQueryScope> callback)
        {
            var window = CreateInstance<JumpDeckQueryWindow>();
            window.titleContent = new GUIContent("Edit Live Pin");
            window.editing = true;
            window.queryName = pin.DisplayName;
            window.query = pin.Query;
            window.scope = pin.QueryScope;
            window.onCreate = callback;
            window.minSize = WindowSize;
            window.maxSize = window.minSize;
            window.ShowUtility();
            window.CenterOnMainWindow();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Live Pin", EditorStyles.boldLabel);
            EditorGUILayout.Space(3f);

            queryName = EditorGUILayout.TextField("Name", queryName);
            query = EditorGUILayout.TextField("Hierarchy query", query);
            scope = (SceneQueryScope)EditorGUILayout.EnumPopup("Scope", scope);

            EditorGUILayout.Space(8f);
            QueryExample("t:Light layer:8", "Lights on layer 8");
            QueryExample("tag:Enemy", "Objects tagged Enemy");
            QueryExample("path:Gameplay/Enemies", "Objects under this path");
            QueryExample("t:Light -tag:EditorOnly", "Lights except EditorOnly");
            EditorGUILayout.Space(3f);
            if (GUILayout.Button("Unity query reference ↗", EditorStyles.linkLabel))
                Application.OpenURL(QueryDocumentation);

            GUILayout.FlexibleSpace();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Cancel", GUILayout.Width(90f)))
                    Close();

                using (new EditorGUI.DisabledScope(
                           string.IsNullOrWhiteSpace(queryName) ||
                           string.IsNullOrWhiteSpace(RemoveProviderPrefix(query))))
                {
                    if (GUILayout.Button(editing ? "Save" : "Create", GUILayout.Width(90f)))
                    {
                        string normalizedQuery = NormalizeQuery(query);
                        onCreate?.Invoke(queryName.Trim(), normalizedQuery, scope);
                        Close();
                    }
                }
            }

            EditorGUILayout.Space(8f);
        }

        private static void QueryExample(string example, string explanation)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(8f);
                GUILayout.Label(example, EditorStyles.miniLabel, GUILayout.Width(160f));
                GUILayout.Space(16f);
                GUILayout.Label(explanation, EditorStyles.miniLabel);
            }
        }

        private static string NormalizeQuery(string value)
        {
            string trimmed = value.Trim();
            return trimmed.StartsWith("h:", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : $"h: {trimmed}";
        }

        private static string RemoveProviderPrefix(string value)
        {
            string trimmed = value.Trim();
            return trimmed.StartsWith("h:", StringComparison.OrdinalIgnoreCase)
                ? trimmed.Substring(2).Trim()
                : trimmed;
        }

        private void CenterOnMainWindow()
        {
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            position = new Rect(
                main.x + (main.width - minSize.x) * 0.5f,
                main.y + (main.height - minSize.y) * 0.5f,
                minSize.x,
                minSize.y);
        }
    }
}
