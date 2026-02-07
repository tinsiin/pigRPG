using Effects.Integration;
using UnityEditor;
using UnityEngine;

namespace EffectsEditor
{
    /// <summary>
    /// ViewportArea にフィールドエフェクトレイヤーをセットアップするエディタユーティリティ。
    /// FrontFixedContainer の直後に1つだけ配置する（敵・味方アイコンの両方より上に描画される）。
    /// </summary>
    public static class FieldEffectLayerSetup
    {
        private const string LayerName = "FieldEffectLayer";
        private const string InsertAfter = "FrontFixedContainer";

        [MenuItem("Tools/Effects/Setup Field Effect Layer")]
        public static void Setup()
        {
            var viewport = GameObject.Find("AlwaysCanvas/EyeArea/ViewportArea");
            if (viewport == null)
            {
                Debug.LogError("[FieldEffectLayerSetup] ViewportArea not found");
                return;
            }

            // 既に存在する場合はスキップ
            var existing = viewport.transform.Find(LayerName);
            if (existing != null)
            {
                Debug.Log($"[FieldEffectLayerSetup] {LayerName} already exists");
                return;
            }

            // 旧3層構造の残骸を削除
            CleanupOldLayers(viewport.transform);

            Undo.RegisterFullObjectHierarchyUndo(viewport, "Setup Field Effect Layer");

            var afterTransform = viewport.transform.Find(InsertAfter);
            if (afterTransform == null)
            {
                Debug.LogError($"[FieldEffectLayerSetup] {InsertAfter} not found under ViewportArea");
                return;
            }

            int targetIndex = afterTransform.GetSiblingIndex() + 1;

            var go = new GameObject(LayerName);
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(viewport.transform, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var effectLayer = go.AddComponent<EffectLayer>();
            effectLayer.IsFieldLayer = true;

            go.transform.SetSiblingIndex(targetIndex);

            EditorUtility.SetDirty(viewport);
            Debug.Log($"[FieldEffectLayerSetup] Created {LayerName} at sibling index {targetIndex}");
        }

        private static void CleanupOldLayers(Transform viewport)
        {
            string[] oldNames = { "FieldEffectBack", "FieldEffectMiddle", "FieldEffectFront" };
            foreach (var name in oldNames)
            {
                var old = viewport.Find(name);
                if (old != null)
                {
                    Undo.DestroyObjectImmediate(old.gameObject);
                    Debug.Log($"[FieldEffectLayerSetup] Removed old layer: {name}");
                }
            }
        }
    }
}
