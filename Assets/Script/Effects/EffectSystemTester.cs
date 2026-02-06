using Effects.Integration;
using UnityEngine;

/// <summary>
/// エフェクトシステムのテスト用コンポーネント
/// BattleIconUIを持つGameObjectにアタッチして使用
/// </summary>
public class EffectSystemTester : MonoBehaviour
{
    [Header("テスト対象")]
    [Tooltip("テスト対象のBattleIconUI（未指定の場合は自身から取得）")]
    public BattleIconUI targetIcon;

    [Header("エフェクト設定")]
    [Tooltip("再生するエフェクト名（Resources/Effects/以下のJSONファイル名、拡張子なし）")]
    public string effectName = "test_fire";

    [Tooltip("ループ再生するか")]
    public bool loop = false;

    [Header("テスト操作")]
    [Tooltip("Inspectorから再生ボタン代わりに使用")]
    public bool playOnStart = false;

    private void Start()
    {
        // targetIconが未指定なら自身から取得
        if (targetIcon == null)
        {
            targetIcon = GetComponent<BattleIconUI>();
        }

        if (playOnStart && targetIcon != null)
        {
            PlayEffect();
        }
    }

    /// <summary>
    /// エフェクトを再生（Inspectorのコンテキストメニューから呼び出し可能）
    /// </summary>
    [ContextMenu("Play Effect")]
    public void PlayEffect()
    {
        if (targetIcon == null)
        {
            Debug.LogError("[EffectSystemTester] targetIcon is not set!");
            return;
        }

        if (string.IsNullOrEmpty(effectName))
        {
            Debug.LogError("[EffectSystemTester] effectName is not set!");
            return;
        }

        Debug.Log($"[EffectSystemTester] Playing effect: {effectName}, loop: {loop}");
        var player = EffectManager.Play(effectName, targetIcon, loop);

        if (player != null)
        {
            Debug.Log($"[EffectSystemTester] Effect started successfully");
        }
        else
        {
            Debug.LogError($"[EffectSystemTester] Failed to play effect");
        }
    }

    /// <summary>
    /// エフェクトを停止
    /// </summary>
    [ContextMenu("Stop Effect")]
    public void StopEffect()
    {
        if (targetIcon == null) return;

        Debug.Log($"[EffectSystemTester] Stopping effect: {effectName}");
        EffectManager.Stop(targetIcon, effectName);
    }

    /// <summary>
    /// 全エフェクトを停止
    /// </summary>
    [ContextMenu("Stop All Effects")]
    public void StopAllEffects()
    {
        if (targetIcon == null) return;

        Debug.Log("[EffectSystemTester] Stopping all effects");
        EffectManager.StopAll(targetIcon);
    }

    /// <summary>
    /// 再生中か確認
    /// </summary>
    [ContextMenu("Check Is Playing")]
    public void CheckIsPlaying()
    {
        if (targetIcon == null) return;

        bool isPlaying = EffectManager.IsPlaying(targetIcon, effectName);
        Debug.Log($"[EffectSystemTester] IsPlaying({effectName}): {isPlaying}");
    }
}
