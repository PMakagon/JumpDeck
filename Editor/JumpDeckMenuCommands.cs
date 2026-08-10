using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JumpDeck.Editor
{
    internal static class JumpDeckMenuCommands
    {
        private const string AssetMenuPath = "Assets/JumpDeck/Pin Selection";
        private const string ImportDeckMenuPath = "Assets/JumpDeck/Import Deck File";
        private const string GameObjectMenuPath = "GameObject/JumpDeck/Pin Selection";

        [MenuItem(AssetMenuPath, false, 1900)]
        private static void PinAssetSelection()
        {
            PinSelection();
        }

        [MenuItem(AssetMenuPath, true)]
        private static bool ValidatePinAssetSelection()
        {
            return Selection.objects.Any(EditorUtility.IsPersistent);
        }

        [MenuItem(GameObjectMenuPath, false, 49)]
        private static void PinHierarchySelection()
        {
            PinSelection();
        }

        [MenuItem(GameObjectMenuPath, true)]
        private static bool ValidatePinHierarchySelection()
        {
            return Selection.gameObjects.Length > 0;
        }

        [MenuItem(ImportDeckMenuPath, false, 1901)]
        private static void ImportSelectedDeckFile()
        {
            string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (string.IsNullOrEmpty(assetPath))
                return;

            JumpDeckData data = JumpDeckData.instance;
            data.EnsureInitialized();
            string fullPath = System.IO.Path.GetFullPath(assetPath);
            if (!JumpDeckFileService.ImportDeck(data, fullPath, out JumpDeckCollection importedDeck))
                return;

            JumpDeckWindow.RefreshOpenWindows();
            Debug.Log($"JumpDeck: imported deck '{importedDeck.DisplayName}' from {assetPath}.");
        }

        [MenuItem(ImportDeckMenuPath, true)]
        private static bool ValidateImportSelectedDeckFile()
        {
            return JumpDeckFileService.IsDeckFile(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        private static void PinSelection()
        {
            JumpDeckData data = JumpDeckData.instance;
            data.EnsureInitialized();

            int added = JumpDeckService.AddObjects(
                data,
                data.Decks[0],
                Selection.objects,
                out List<string> errors);

            JumpDeckWindow.RefreshOpenWindows();

            if (added == 0 && errors.Count > 0)
                Debug.LogWarning($"JumpDeck: {errors[0]}");
        }
    }
}
