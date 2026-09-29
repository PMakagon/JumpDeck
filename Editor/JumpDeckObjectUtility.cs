using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JumpDeck.Editor
{
    internal enum JumpDeckReferenceStatus { Available, SceneNotLoaded, Missing, Invalid }

    internal readonly struct JumpDeckReference
    {
        internal readonly Object Target;
        internal readonly JumpDeckReferenceStatus Status;
        internal readonly string ScenePath;
        internal JumpDeckReference(Object target, JumpDeckReferenceStatus status, string scenePath = null)
        {
            Target = target;
            Status = status;
            ScenePath = scenePath;
        }
        internal string Description => Status switch
        {
            JumpDeckReferenceStatus.Available => "Available",
            JumpDeckReferenceStatus.SceneNotLoaded => "Scene is not loaded",
            JumpDeckReferenceStatus.Missing => "Object no longer exists",
            _ => "Invalid object reference"
        };
    }

    internal static class JumpDeckObjectUtility
    {
        internal static bool TryCreatePin(Object target, out JumpPin pin, out string error)
        {
            pin = null;
            error = null;

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

            if (globalId.identifierType == 0 && string.IsNullOrEmpty(guid))
            {
                error = $"JumpDeck cannot save a reference to '{target.name}'.";
                return false;
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
            return ResolveReference(pin).Target;
        }

        internal static JumpDeckReference ResolveReference(JumpPin pin)
        {
            if (pin == null || pin.Kind != JumpPinKind.Object)
                return new JumpDeckReference(null, JumpDeckReferenceStatus.Invalid);

            if (!string.IsNullOrEmpty(pin.GlobalObjectId) &&
                GlobalObjectId.TryParse(pin.GlobalObjectId, out GlobalObjectId globalId))
            {
                Object resolved = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                if (resolved != null)
                    return new JumpDeckReference(resolved, JumpDeckReferenceStatus.Available);

                if (globalId.identifierType == 2)
                {
                    string scenePath = AssetDatabase.GUIDToAssetPath(globalId.assetGUID.ToString());
                    if (string.IsNullOrEmpty(scenePath))
                        return new JumpDeckReference(null, JumpDeckReferenceStatus.Missing);
                    Scene scene = SceneManager.GetSceneByPath(scenePath);
                    return new JumpDeckReference(null,
                        scene.IsValid() && scene.isLoaded
                            ? JumpDeckReferenceStatus.Missing
                            : JumpDeckReferenceStatus.SceneNotLoaded, scenePath);
                }
            }

            if (string.IsNullOrEmpty(pin.AssetGuid))
                return new JumpDeckReference(null, JumpDeckReferenceStatus.Invalid);

            string path = AssetDatabase.GUIDToAssetPath(pin.AssetGuid);
            if (string.IsNullOrEmpty(path))
                return new JumpDeckReference(null, JumpDeckReferenceStatus.Missing);

            foreach (Object candidate in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out _, out long fileId) &&
                    fileId == pin.LocalFileId)
                {
                    return new JumpDeckReference(candidate, JumpDeckReferenceStatus.Available);
                }
            }

            return new JumpDeckReference(null, JumpDeckReferenceStatus.Missing);
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
            if (target == null || !EditorUtility.IsPersistent(target))
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

        internal static string HierarchyPath(Transform transform)
        {
            var path = new StringBuilder(transform.name);
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
                path.Insert(0, parent.name + "/");
            return path.ToString();
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

            string suffix = target is Component component
                ? $" ({component.GetType().Name})"
                : string.Empty;

            return $"{gameObject.scene.path}|{HierarchyPath(gameObject.transform)}{suffix}";
        }
    }
}
