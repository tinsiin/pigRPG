using System.Collections.Generic;
using Effects.Core;
using Effects.Playback;
using Newtonsoft.Json;
using UnityEngine;

namespace Effects.Integration
{
    /// <summary>
    /// エフェクトシステムの全体管理クラス
    /// エフェクト定義の読み込み・キャッシュ、再生/停止API、SE再生を統合
    /// </summary>
    public class EffectManager : MonoBehaviour
    {
        private static EffectManager _instance;

        /// <summary>
        /// シングルトンインスタンス
        /// </summary>
        public static EffectManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    // シーン内を検索
                    _instance = FindFirstObjectByType<EffectManager>();

                    // 見つからなければ自動生成
                    if (_instance == null)
                    {
                        var go = new GameObject("EffectManager");
                        _instance = go.AddComponent<EffectManager>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// エフェクト定義キャッシュ
        /// </summary>
        private readonly Dictionary<string, EffectDefinition> _definitionCache = new Dictionary<string, EffectDefinition>();

        /// <summary>
        /// SE用AudioSource
        /// </summary>
        private AudioSource _audioSource;


        private void Awake()
        {
            // シングルトン重複チェック
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            // AudioSourceを追加
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        #region 静的API

        /// <summary>
        /// エフェクトを再生
        /// </summary>
        /// <param name="effectName">エフェクト名（JSONファイル名、拡張子なし）</param>
        /// <param name="battleIconUI">表示先のBattleIconUI</param>
        /// <param name="loop">ループ再生するか</param>
        /// <returns>再生中のEffectPlayer（エラー時はnull）</returns>
        public static EffectPlayer Play(string effectName, BattleIconUI battleIconUI, bool loop = false)
        {
            if (Instance == null)
            {
                Debug.LogError("[EffectManager] Instance is null");
                return null;
            }
            return Instance.PlayInternal(effectName, battleIconUI, loop);
        }

        /// <summary>
        /// 指定エフェクトを停止
        /// </summary>
        /// <param name="battleIconUI">対象のBattleIconUI</param>
        /// <param name="effectName">停止するエフェクト名</param>
        public static void Stop(BattleIconUI battleIconUI, string effectName)
        {
            if (Instance == null) return;
            Instance.StopInternal(battleIconUI, effectName);
        }

        /// <summary>
        /// 指定BattleIconUIの全エフェクトを停止
        /// </summary>
        /// <param name="battleIconUI">対象のBattleIconUI</param>
        public static void StopAll(BattleIconUI battleIconUI)
        {
            if (Instance == null) return;
            Instance.StopAllInternal(battleIconUI);
        }

        /// <summary>
        /// 指定エフェクトが再生中か確認
        /// </summary>
        public static bool IsPlaying(BattleIconUI battleIconUI, string effectName)
        {
            if (Instance == null) return false;
            return Instance.IsPlayingInternal(battleIconUI, effectName);
        }

        /// <summary>
        /// フィールドエフェクトを再生
        /// </summary>
        /// <param name="effectName">エフェクト名（JSONファイル名、拡張子なし）</param>
        /// <param name="loop">ループ再生するか</param>
        /// <returns>再生中のEffectPlayer（エラー時はnull）</returns>
        public static EffectPlayer PlayField(string effectName, bool loop = false)
        {
            if (Instance == null)
            {
                Debug.LogError("[EffectManager] Instance is null");
                return null;
            }
            return Instance.PlayFieldInternal(effectName, loop);
        }

        /// <summary>
        /// 指定フィールドエフェクトを停止
        /// </summary>
        public static void StopField(string effectName)
        {
            if (Instance == null) return;
            Instance.StopFieldInternal(effectName);
        }

        /// <summary>
        /// 全フィールドエフェクトを停止
        /// </summary>
        public static void StopAllField()
        {
            if (Instance == null) return;
            Instance.StopAllFieldInternal();
        }

        /// <summary>
        /// キャッシュをクリア
        /// </summary>
        public static void ClearCache()
        {
            if (Instance == null) return;
            Instance._definitionCache.Clear();
        }

        #endregion

        #region 内部実装

        // フィールドエフェクトレイヤーのキャッシュ（ViewportArea 内に1つ）
        private EffectLayer _fieldEffectLayer;

        private EffectPlayer PlayInternal(string effectName, BattleIconUI battleIconUI, bool loop)
        {
            if (battleIconUI == null)
            {
                Debug.LogError("[EffectManager] BattleIconUI is null");
                return null;
            }

            if (string.IsNullOrEmpty(effectName))
            {
                Debug.LogWarning("[EffectManager] Effect name is null or empty");
                return null;
            }

            // エフェクト定義を読み込み
            var definition = LoadDefinition(effectName);
            if (definition == null)
            {
                return null;
            }

            // EffectLayerを取得または作成
            var effectLayer = GetOrCreateEffectLayer(battleIconUI);
            if (effectLayer == null)
            {
                Debug.LogError("[EffectManager] Failed to create EffectLayer");
                return null;
            }

            // target チェック
            if (definition.Target == "field")
            {
                Debug.LogWarning($"[EffectManager] '{effectName}' has target=\"field\" but was played via Play (icon API). Use PlayField instead.");
            }

            // 既存ループの再呼び出しかを判定（SE重複再生防止のため）
            var existingPlayer = effectLayer.FindPlayer(effectName);
            bool isExistingLoopReuse = existingPlayer != null && loop && existingPlayer.IsLoop;

            // エフェクト再生
            var player = effectLayer.PlayEffect(definition, loop);

            // SE再生（既存ループの再利用時のみスキップ、それ以外は常に再生）
            if (!isExistingLoopReuse && player != null)
            {
                PlaySe(definition.Se);
            }

            return player;
        }

        private void StopInternal(BattleIconUI battleIconUI, string effectName)
        {
            if (battleIconUI == null) return;

            var effectLayer = battleIconUI.GetComponentInChildren<EffectLayer>();
            if (effectLayer != null)
            {
                effectLayer.StopEffect(effectName);
            }
        }

        private void StopAllInternal(BattleIconUI battleIconUI)
        {
            if (battleIconUI == null) return;

            var effectLayer = battleIconUI.GetComponentInChildren<EffectLayer>();
            if (effectLayer != null)
            {
                effectLayer.StopAllEffects();
            }
        }

        private bool IsPlayingInternal(BattleIconUI battleIconUI, string effectName)
        {
            if (battleIconUI == null) return false;

            var effectLayer = battleIconUI.GetComponentInChildren<EffectLayer>();
            if (effectLayer != null)
            {
                return effectLayer.IsPlaying(effectName);
            }
            return false;
        }

        /// <summary>
        /// エフェクト定義を読み込み（キャッシュあり）
        /// </summary>
        private EffectDefinition LoadDefinition(string effectName)
        {
            // キャッシュを確認
            if (_definitionCache.TryGetValue(effectName, out var cached))
            {
                return cached;
            }

            // Resourcesから読み込み
            var path = $"{EffectConstants.EffectsResourcePath}{effectName}";
            var textAsset = Resources.Load<TextAsset>(path);

            if (textAsset == null)
            {
                Debug.LogError($"[EffectManager] Effect file not found: {path}");
                return null;
            }

            // JSONパース
            EffectDefinition definition;
            try
            {
                definition = KfxCompiler.LoadFromJson(textAsset.text);
            }
            catch (JsonException e)
            {
                Debug.LogError($"[EffectManager] Failed to parse effect JSON '{effectName}': {e.Message}");
                return null;
            }

            // バリデーション
            if (definition == null)
            {
                Debug.LogError($"[EffectManager] Effect definition is null: {effectName}");
                return null;
            }

            if (definition.Frames == null || definition.Frames.Count == 0)
            {
                Debug.LogError($"[EffectManager] Effect has no frames: {effectName}");
                return null;
            }

            if (definition.Canvas <= 0 || definition.Canvas > EffectConstants.CanvasMax)
            {
                Debug.LogError($"[EffectManager] Invalid canvas size: {definition.Canvas} (must be 1-{EffectConstants.CanvasMax})");
                return null;
            }

            // 名前を常にファイル名で上書き（Stop()との一貫性のため）
            // JSONの"name"フィールドはデバッグ表示用として無視
            definition.Name = effectName;

            // キャッシュに追加
            _definitionCache[effectName] = definition;

            return definition;
        }

        /// <summary>
        /// EffectLayerを取得または作成
        /// </summary>
        private EffectLayer GetOrCreateEffectLayer(BattleIconUI battleIconUI)
        {
            // 既存のEffectLayerを検索
            var existingLayer = battleIconUI.GetComponentInChildren<EffectLayer>();
            if (existingLayer != null)
            {
                return existingLayer;
            }

            // EffectLayerを新規作成
            var layerGo = new GameObject("EffectLayer");
            layerGo.transform.SetParent(battleIconUI.transform, false);

            // RectTransformを設定（親と同じサイズ、全体をカバー）
            var rectTransform = layerGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            // EffectLayerコンポーネントを追加
            var effectLayer = layerGo.AddComponent<EffectLayer>();

            return effectLayer;
        }

        private EffectPlayer PlayFieldInternal(string effectName, bool loop)
        {
            if (string.IsNullOrEmpty(effectName))
            {
                Debug.LogWarning("[EffectManager] Effect name is null or empty");
                return null;
            }

            var definition = LoadDefinition(effectName);
            if (definition == null) return null;

            // target チェック
            if (definition.Target != null && definition.Target != "field")
            {
                Debug.LogWarning($"[EffectManager] '{effectName}' has target=\"{definition.Target}\" but was played via PlayField. Use Play instead.");
            }

            var layer = GetFieldEffectLayer();
            if (layer == null)
            {
                Debug.LogError("[EffectManager] FieldEffectLayer not found in scene");
                return null;
            }

            var existingPlayer = layer.FindPlayer(effectName);
            bool isExistingLoopReuse = existingPlayer != null && loop && existingPlayer.IsLoop;

            var player = layer.PlayEffect(definition, loop);

            if (!isExistingLoopReuse && player != null)
            {
                PlaySe(definition.Se);
            }

            return player;
        }

        private void StopFieldInternal(string effectName)
        {
            GetFieldEffectLayer()?.StopEffect(effectName);
        }

        private void StopAllFieldInternal()
        {
            GetFieldEffectLayer()?.StopAllEffects();
        }

        /// <summary>
        /// フィールドエフェクトレイヤーを取得（遅延検索 + キャッシュ）
        /// </summary>
        private EffectLayer GetFieldEffectLayer()
        {
            if (_fieldEffectLayer == null)
            {
                var go = GameObject.Find("FieldEffectLayer");
                if (go != null)
                    _fieldEffectLayer = go.GetComponent<EffectLayer>();
            }
            return _fieldEffectLayer;
        }

        /// <summary>
        /// SE再生
        /// </summary>
        private void PlaySe(string seName)
        {
            if (string.IsNullOrEmpty(seName))
            {
                return;
            }

            if (_audioSource == null)
            {
                Debug.LogWarning("[EffectManager] AudioSource is null, cannot play SE");
                return;
            }

            // Resourcesからクリップを読み込み
            var path = $"{EffectConstants.AudioResourcePath}{seName}";
            var clip = Resources.Load<AudioClip>(path);

            if (clip == null)
            {
                Debug.LogWarning($"[EffectManager] SE not found: {path}");
                return;
            }

            _audioSource.PlayOneShot(clip);
        }

        #endregion

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
