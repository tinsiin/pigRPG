using System.Collections.Generic;
using Effects.Core;
using Effects.Playback;
using UnityEngine;
using UnityEngine.UI;

namespace Effects.Integration
{
    /// <summary>
    /// エフェクト表示レイヤー。
    /// BattleIconUI の子として使用（icon モード）、または ViewportArea の子として使用（field モード）。
    /// 複数のエフェクトを重ねて表示可能。
    /// </summary>
    public class EffectLayer : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private readonly List<EffectPlayerEntry> _activePlayers = new List<EffectPlayerEntry>();

        /// <summary>
        /// field モード時に true を設定。キャンバスを親RectTransform全体に引き延ばす。
        /// </summary>
        [HideInInspector]
        public bool IsFieldLayer;

        /// <summary>
        /// エフェクトプレイヤーのエントリ
        /// </summary>
        private class EffectPlayerEntry
        {
            public string EffectName;
            public EffectPlayer Player;
            public RawImage RawImage;
            public GameObject Container;
        }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null)
            {
                _rectTransform = gameObject.AddComponent<RectTransform>();
            }
        }

        /// <summary>
        /// エフェクトを再生
        /// </summary>
        public EffectPlayer PlayEffect(EffectDefinition definition, bool loop)
        {
            if (definition == null)
            {
                Debug.LogError("[EffectLayer] Definition is null");
                return null;
            }

            // 既に同じエフェクトが再生中の場合
            var existing = FindPlayer(definition.Name);
            if (existing != null)
            {
                if (loop && existing.IsLoop)
                {
                    // 同じループエフェクトは重複再生しない
                    return existing;
                }
                else
                {
                    // ループモードが異なる、または非ループは既存を停止して新規再生
                    StopEffect(definition.Name);
                }
            }

            // コンテナとRawImageを作成
            var container = new GameObject($"Effect_{definition.Name}");
            container.transform.SetParent(transform, false);

            var containerRT = container.AddComponent<RectTransform>();
            SetupRectTransform(containerRT, definition);

            var rawImage = container.AddComponent<RawImage>();
            rawImage.raycastTarget = false;

            // EffectPlayerを作成
            var player = container.AddComponent<EffectPlayer>();
            player.Initialize(definition, rawImage, loop);

            // エントリを追加
            var entry = new EffectPlayerEntry
            {
                EffectName = definition.Name,
                Player = player,
                RawImage = rawImage,
                Container = container
            };
            _activePlayers.Add(entry);

            // 再生完了時のクリーンアップ（非ループのみ）
            if (!loop)
            {
                player.OnComplete += () => OnEffectComplete(entry);
            }

            // 再生開始
            player.Play();

            return player;
        }

        /// <summary>
        /// 指定エフェクトを停止
        /// </summary>
        public void StopEffect(string effectName)
        {
            var entry = _activePlayers.Find(e => e.EffectName == effectName);
            if (entry != null)
            {
                RemoveEntry(entry);
            }
        }

        /// <summary>
        /// 全エフェクトを停止
        /// </summary>
        public void StopAllEffects()
        {
            foreach (var entry in _activePlayers.ToArray())
            {
                RemoveEntry(entry);
            }
        }

        /// <summary>
        /// 指定エフェクトが再生中か
        /// </summary>
        public bool IsPlaying(string effectName)
        {
            var player = FindPlayer(effectName);
            return player != null && player.IsPlaying;
        }

        /// <summary>
        /// 指定エフェクトのプレイヤーを取得
        /// </summary>
        public EffectPlayer FindPlayer(string effectName)
        {
            var entry = _activePlayers.Find(e => e.EffectName == effectName);
            return entry?.Player;
        }

        /// <summary>
        /// アクティブなエフェクト数
        /// </summary>
        public int ActiveEffectCount => _activePlayers.Count;

        private void SetupRectTransform(RectTransform rt, EffectDefinition def)
        {
            int canvas = def.Canvas;

            if (IsFieldLayer)
            {
                // field モード: 親（ViewportArea）全体に引き延ばし
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                return;
            }

            // icon モード: アイコンの実サイズを取得
            Vector2 iconSize = GetIconActualSize();

            if (def.IconRect != null && def.IconRect.Width > 0 && def.IconRect.Height > 0)
            {
                // icon_rect ベースのスケーリング
                var ir = def.IconRect;
                float scale = Mathf.Min(iconSize.x / ir.Width, iconSize.y / ir.Height);
                float displaySize = canvas * scale;

                float canvasCenter = canvas / 2f;
                float irCenterX = ir.X + ir.Width / 2f;
                float irCenterY = ir.Y + ir.Height / 2f;

                // キャンバスY下向き → RectTransformY上向き 変換
                float offsetX = (canvasCenter - irCenterX) * scale;
                float offsetY = (irCenterY - canvasCenter) * scale;

                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(displaySize, displaySize);
                rt.anchoredPosition = new Vector2(offsetX, offsetY);
            }
            else
            {
                // icon_rect 省略: アイコン短辺にフィット（従来動作）
                float shortSide = Mathf.Min(iconSize.x, iconSize.y);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(shortSide, shortSide);
                rt.anchoredPosition = Vector2.zero;
            }
        }

        /// <summary>
        /// 親の BattleIconUI の Icon RectTransform からアイコンの実サイズを取得。
        /// BattleIconUI が見つからない場合は親 RectTransform のサイズにフォールバック。
        /// </summary>
        private Vector2 GetIconActualSize()
        {
            // 親階層から BattleIconUI を探す
            var battleIcon = GetComponentInParent<BattleIconUI>();
            if (battleIcon != null && battleIcon.Icon != null)
            {
                var iconRT = battleIcon.Icon.rectTransform;
                float w = iconRT.rect.width;
                float h = iconRT.rect.height;
                if (w > 0 && h > 0)
                    return new Vector2(w, h);
            }

            // フォールバック: 親の EffectLayer 自体のサイズ
            float parentW = _rectTransform.rect.width;
            float parentH = _rectTransform.rect.height;
            if (parentW <= 0) parentW = 100;
            if (parentH <= 0) parentH = 100;
            return new Vector2(parentW, parentH);
        }

        private void OnEffectComplete(EffectPlayerEntry entry)
        {
            // 次のフレームで削除（コールバック中に削除するのを避ける）
            if (gameObject.activeInHierarchy)
            {
                StartCoroutine(RemoveEntryDelayed(entry));
            }
            else
            {
                RemoveEntry(entry);
            }
        }

        private System.Collections.IEnumerator RemoveEntryDelayed(EffectPlayerEntry entry)
        {
            yield return null;
            RemoveEntry(entry);
        }

        private void RemoveEntry(EffectPlayerEntry entry)
        {
            if (entry == null) return;

            _activePlayers.Remove(entry);

            if (entry.Player != null)
            {
                entry.Player.Cleanup();
            }

            if (entry.Container != null)
            {
                Destroy(entry.Container);
            }
        }

        private void OnDestroy()
        {
            StopAllEffects();
        }
    }
}
