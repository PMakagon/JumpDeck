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

        internal static void Open(Action<string, string, SceneQueryScope> callback)
        {
            var window = CreateInstance<JumpDeckQueryWindow>();
            window.titleContent = new GUIContent("Create Live Pin");
            window.onCreate = callback;
            window.minSize = new Vector2(430f, 205f);
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

            EditorGUILayout.Space(5f);
            EditorGUILayout.HelpBox(
                "Examples: h: t:Light, h: tag:Enemy, h: path:/Gameplay/Enemies",
                MessageType.Info);

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
                    if (GUILayout.Button("Create", GUILayout.Width(90f)))
                    {
                        string normalizedQuery = NormalizeQuery(query);
                        onCreate?.Invoke(queryName.Trim(), normalizedQuery, scope);
                        Close();
                    }
                }
            }

            EditorGUILayout.Space(8f);
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
