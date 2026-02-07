using System;
using Effects.Core;
using Effects.Rendering;
using UnityEngine;
using UnityEngine.UI;

namespace Effects.Playback
{
    /// <summary>
    /// エフェクト再生を制御するコンポーネント
    /// </summary>
    public class EffectPlayer : MonoBehaviour
    {
        private EffectDefinition _definition;
        private EffectRenderer _renderer;
        private RawImage _rawImage;
        private Texture2D _texture;

        private int _currentFrameIndex;
        private int _lastRenderedFrame = -1;
        private float _frameTimer;
        private bool _isPlaying;
        private bool _loop;

        /// <summary>
        /// エフェクト名
        /// </summary>
        public string EffectName => _definition?.Name;

        /// <summary>
        /// 再生中かどうか
        /// </summary>
        public bool IsPlaying => _isPlaying;

        /// <summary>
        /// ループ再生かどうか
        /// </summary>
        public bool IsLoop => _loop;

        /// <summary>
        /// 再生完了時のコールバック
        /// </summary>
        public event Action OnComplete;

        /// <summary>
        /// 初期化
        /// </summary>
        public void Initialize(EffectDefinition definition, RawImage rawImage, bool loop)
        {
            _definition = definition;
            _rawImage = rawImage;
            _loop = loop;

            // 定義を正規化
            _definition.Normalize();

            // レンダラー作成
            _renderer = new EffectRenderer(_definition.Canvas);

            // テクスチャ作成
            _texture = _renderer.CreateTexture();
            _rawImage.texture = _texture;
            _rawImage.enabled = true;

            // 初期状態
            _currentFrameIndex = 0;
            _frameTimer = 0f;
            _isPlaying = false;
        }

        /// <summary>
        /// 再生開始
        /// </summary>
        public void Play()
        {
            if (_definition == null || _definition.Frames.Count == 0)
            {
                Debug.LogWarning("[EffectPlayer] No frames to play");
                return;
            }

            _currentFrameIndex = 0;
            _lastRenderedFrame = -1;
            _frameTimer = 0f;
            _isPlaying = true;

            // 最初のフレームを描画
            RenderCurrentFrame();
            _lastRenderedFrame = 0;
        }

        /// <summary>
        /// 再生停止
        /// </summary>
        public void Stop()
        {
            _isPlaying = false;
            if (_rawImage != null)
            {
                _rawImage.enabled = false;
            }
        }

        /// <summary>
        /// 一時停止
        /// </summary>
        public void Pause()
        {
            _isPlaying = false;
        }

        /// <summary>
        /// 再開
        /// </summary>
        public void Resume()
        {
            _isPlaying = true;
        }

        private void Update()
        {
            if (!_isPlaying || _definition == null) return;

            // ラグスパイク（ContextMenuや一時停止等）時のフレームスキップを防止
            // 1回のUpdateで最大2フレーム分のキャッチアップに制限
            float maxDeltaTime = 2f / Mathf.Max(1, _definition.Fps);
            _frameTimer += Mathf.Min(Time.deltaTime, maxDeltaTime);

            var currentFrame = _definition.Frames[_currentFrameIndex];
            float frameDuration = currentFrame.Duration > 0 ? currentFrame.Duration : (1f / _definition.Fps);

            // フレーム切り替え判定（ラグスパイク時の過剰ループ防止）
            int maxAdvance = _definition.Frames.Count * 2;
            while (_frameTimer >= frameDuration && maxAdvance-- > 0)
            {
                _frameTimer -= frameDuration;
                _currentFrameIndex++;

                if (_currentFrameIndex >= _definition.Frames.Count)
                {
                    if (_loop)
                    {
                        _currentFrameIndex = 0;
                    }
                    else
                    {
                        // 再生完了
                        _isPlaying = false;
                        OnComplete?.Invoke();
                        return;
                    }
                }

                // 新しいフレームの duration を取得
                currentFrame = _definition.Frames[_currentFrameIndex];
                frameDuration = currentFrame.Duration > 0 ? currentFrame.Duration : (1f / _definition.Fps);
            }

            // フレーム変化時のみ描画（CPU描画のコスト回避）
            if (_currentFrameIndex != _lastRenderedFrame)
            {
                RenderCurrentFrame();
                _lastRenderedFrame = _currentFrameIndex;
            }
        }

        private void RenderCurrentFrame()
        {
            if (_definition == null || _texture == null) return;

            var frame = _definition.Frames[_currentFrameIndex];
            _renderer.RenderFrame(_texture, frame, _currentFrameIndex);
        }

        private void OnDestroy()
        {
            // テクスチャを破棄
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
        }

        /// <summary>
        /// リソースを解放
        /// </summary>
        public void Cleanup()
        {
            Stop();

            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }

            if (_rawImage != null)
            {
                _rawImage.texture = null;
            }

            _definition = null;
            _renderer = null;
        }
    }
}
