using System.Collections.Generic;
using Effects.Core;
using Effects.Playback;
using UnityEngine;
using UnityEngine.UI;

namespace Effects.Integration
{
    /// <summary>
    /// BattleIconUIに追加されるエフェクト表示レイヤー
    /// 複数のエフェクトを重ねて表示可能
    /// </summary>
    public class EffectLayer : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private readonly List<EffectPlayerEntry> _activePlayers = new List<EffectPlayerEntry>();

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
            SetupRectTransform(containerRT, definition.Canvas);

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

        private void SetupRectTransform(RectTransform rt, int canvasSize)
        {
            // 親のサイズを取得
            var parentRT = _rectTransform;
            float parentWidth = parentRT.rect.width;
            float parentHeight = parentRT.rect.height;

            // 親がまだサイズ未設定の場合はデフォルト値を使用
            if (parentWidth <= 0) parentWidth = 100;
            if (parentHeight <= 0) parentHeight = 100;

            // 等比スケール（短辺に合わせる）
            float shortSide = Mathf.Min(parentWidth, parentHeight);

            // 中央配置
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(shortSide, shortSide);
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
