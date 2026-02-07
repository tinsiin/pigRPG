using Effects.Integration;
using UnityEngine;

/// <summary>
/// フィールドエフェクトのテスト用コンポーネント。
/// 任意の GameObject にアタッチして使用。FieldEffectLayer 経由で再生する。
/// </summary>
public class FieldEffectTester : MonoBehaviour
{
    [Header("エフェクト設定")]
    [Tooltip("再生するエフェクト名（Resources/Effects/以下のJSONファイル名、拡張子なし）")]
    public string effectName = "test_fire";

    [Tooltip("ループ再生するか")]
    public bool loop = false;

    [Header("テスト操作")]
    [Tooltip("Play開始時に自動再生する")]
    public bool playOnStart = false;

    private void Start()
    {
        if (playOnStart)
        {
            PlayEffect();
        }
    }

    /// <summary>
    /// フィールドエフェクトを再生
    /// </summary>
    [ContextMenu("Play Field Effect")]
    public void PlayEffect()
    {
        if (string.IsNullOrEmpty(effectName))
        {
            Debug.LogError("[FieldEffectTester] effectName is not set!");
            return;
        }

        Debug.Log($"[FieldEffectTester] Playing field effect: {effectName}, loop: {loop}");
        var player = EffectManager.PlayField(effectName, loop);

        if (player != null)
        {
            Debug.Log("[FieldEffectTester] Field effect started successfully");
        }
        else
        {
            Debug.LogError("[FieldEffectTester] Failed to play field effect. Is FieldEffectLayer set up in the scene?");
        }
    }

    /// <summary>
    /// フィールドエフェクトを停止
    /// </summary>
    [ContextMenu("Stop Field Effect")]
    public void StopEffect()
    {
        Debug.Log($"[FieldEffectTester] Stopping field effect: {effectName}");
        EffectManager.StopField(effectName);
    }

    /// <summary>
    /// 全フィールドエフェクトを停止
    /// </summary>
    [ContextMenu("Stop All Field Effects")]
    public void StopAllEffects()
    {
        Debug.Log("[FieldEffectTester] Stopping all field effects");
        EffectManager.StopAllField();
    }
}
