using System.Text;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JumpDeck.Editor
{
    internal static class JumpDeckObjectUtility
    {
        internal static bool TryCreatePin(Object source, out JumpPin pin, out string error)
        {
            pin = null;
            error = null;

            Object target = NormalizeObject(source);
            if (target == null)
            {
                error = "JumpDeck cannot pin this object.";
                return false;
            }

            bool persistent = EditorUtility.IsPersistent(target);
            if (!persistent && TryGetSceneGameObject(target, out GameObject gameObject) &&
                string.IsNullOrEmpty(gameObject.scene.path))
            {
                error = $"Save the scene before pinning '{target.name}'.";
                return false;
            }

            GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(target);
            string serializedGlobalId = globalId.ToString();
            string guid = string.Empty;
            long localFileId = 0;
            string fallbackPath;

            if (persistent)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out guid, out localFileId);
                fallbackPath = AssetDatabase.GetAssetPath(target);
            }
            else
            {
                fallbackPath = BuildScenePath(target);
            }

            pin = JumpPin.ForObject(
                target.name,
                serializedGlobalId,
                guid,
                localFileId,
                fallbackPath);

            return true;
        }

        internal static Object Resolve(JumpPin pin)
        {
            if (pin == null || pin.Kind != JumpPinKind.Object)
                return null;

            if (!string.IsNullOrEmpty(pin.GlobalObjectId) &&
                GlobalObjectId.TryParse(pin.GlobalObjectId, out GlobalObjectId globalId))
            {
                Object resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                if (resolved != null)
                    return resolved;
            }

            if (string.IsNullOrEmpty(pin.AssetGuid))
                return null;

            string path = AssetDatabase.GUIDToAssetPath(pin.AssetGuid);
            if (string.IsNullOrEmpty(path))
                return null;

            foreach (Object candidate in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out _, out long fileId) &&
                    fileId == pin.LocalFileId)
                {
                    return candidate;
                }
            }

            return AssetDatabase.LoadMainAssetAtPath(path);
        }

        internal static void SelectAndPing(Object target, bool frameSceneObject)
        {
            if (target == null)
                return;

            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);

            if (frameSceneObject && !EditorUtility.IsPersistent(target))
                SceneView.lastActiveSceneView?.FrameSelected();
        }

        internal static string BuildIdentity(JumpPin pin)
        {
            if (!string.IsNullOrEmpty(pin.GlobalObjectId))
                return pin.GlobalObjectId;

            return $"{pin.AssetGuid}:{pin.LocalFileId}";
        }

        internal static JumpPinClickAction GetEffectiveClickAction(JumpPin pin, Object target)
        {
            return pin.HasClickAction
                ? pin.ClickAction
                : GetDefaultClickAction(target);
        }

        internal static JumpPinClickAction GetDefaultClickAction(Object target)
        {
            if (target == null)
                return JumpPinClickAction.InspectOrOpen;

            if (!EditorUtility.IsPersistent(target))
                return JumpPinClickAction.InspectOrOpen;

            string assetPath = AssetDatabase.GetAssetPath(target);
            if (AssetDatabase.IsValidFolder(assetPath) || IsImportedMediaAsset(assetPath))
                return JumpPinClickAction.Ping;

            return JumpPinClickAction.InspectOrOpen;
        }

        internal static bool UsesOpenAction(Object target)
        {
            if (target == null || !EditorUtility.IsPersistent(target))
                return false;

            if (target is SceneAsset || target is MonoScript)
                return true;

            if (target is not GameObject)
                return false;

            return string.Equals(
                Path.GetExtension(AssetDatabase.GetAssetPath(target)),
                ".prefab",
                System.StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryGetSceneGameObject(Object target, out GameObject gameObject)
        {
            switch (target)
            {
                case GameObject value:
                    gameObject = value;
                    return true;
                case Component value:
                    gameObject = value.gameObject;
                    return true;
                default:
                    gameObject = null;
                    return false;
            }
        }

        private static Object NormalizeObject(Object source)
        {
            if (source == null)
                return null;

            if (source is Component component && component == null)
                return null;

            return source;
        }

        private static bool IsImportedMediaAsset(string assetPath)
        {
            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            if (importer == null)
                return false;

            switch (importer.GetType().Name)
            {
                case "TextureImporter":
                case "ModelImporter":
                case "AudioImporter":
                case "VideoClipImporter":
                case "TrueTypeFontImporter":
                case "SpeedTreeImporter":
                case "PSDImporter":
                    return true;
                default:
                    return false;
            }
        }

        private static string BuildScenePath(Object target)
        {
            if (!TryGetSceneGameObject(target, out GameObject gameObject))
                return target.name;

            var hierarchyPath = new StringBuilder(gameObject.name);
            Transform parent = gameObject.transform.parent;
            while (parent != null)
            {
                hierarchyPath.Insert(0, '/');
                hierarchyPath.Insert(0, parent.name);
                parent = parent.parent;
            }

            string suffix = target is Component component
                ? $" ({component.GetType().Name})"
                : string.Empty;

            return $"{gameObject.scene.path}|{hierarchyPath}{suffix}";
        }
    }
}
