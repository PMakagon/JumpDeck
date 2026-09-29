using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

namespace JumpDeck.Editor
{
    internal sealed class JumpDeckWindow : EditorWindow
    {
        private JumpDeckView view;

        private void OnEnable() => JumpDeckStorage.DefaultChanged += HandleStorageChanged;
        private void HandleStorageChanged()
        {
            if (view != null) CreateGUI();
        }

        [MenuItem("Window/JumpDeck", false, 1800)]
        internal static void ShowWindow()
        {
            var window = GetWindow<JumpDeckWindow>();
            window.titleContent = new GUIContent("JumpDeck", EditorGUIUtility.IconContent("Favorite").image);
            window.minSize = new Vector2(210f, 220f);
            window.Show();
        }

        [Shortcut("JumpDeck/Open", KeyCode.J, ShortcutModifiers.Alt)]
        private static void OpenShortcut()
        {
            if (!JumpDeckStorage.TryOpen()) ShowWindow();
        }

        public void CreateGUI()
        {
            view?.Dispose();
            rootVisualElement.Clear();
            view = new JumpDeckView(message => ShowNotification(new GUIContent(message)));
            rootVisualElement.Add(view);
        }

        private void OnDisable()
        {
            JumpDeckStorage.DefaultChanged -= HandleStorageChanged;
            view?.Dispose();
            view = null;
        }
    }
}
