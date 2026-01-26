using System;
using UnityEngine;

/// <summary>
/// ズーム時のフォーカス領域を指定する。
/// プリセットまたはカスタム領域を選択可能。
/// </summary>
[Serializable]
public struct FocusArea
{
    [Tooltip("フォーカスプリセット")]
    public FocusPreset Preset;

    [Tooltip("Custom選択時のカスタム領域 (0-1の相対座標)")]
    public Rect CustomRect;

    /// <summary>
    /// 実際のフォーカス領域を取得する。
    /// </summary>
    public Rect GetRect()
    {
        return Preset switch
        {
            FocusPreset.Full => new Rect(0f, 0f, 1f, 1f),
            FocusPreset.UpperHalf => new Rect(0f, 0.5f, 1f, 0.5f),
            FocusPreset.UpperThird => new Rect(0f, 0.67f, 1f, 0.33f),
            FocusPreset.LowerHalf => new Rect(0f, 0f, 1f, 0.5f),
            FocusPreset.Center => new Rect(0.25f, 0.25f, 0.5f, 0.5f),
            FocusPreset.CenterWide => new Rect(0.1f, 0.25f, 0.8f, 0.5f),
            FocusPreset.Custom => CustomRect,
            _ => new Rect(0f, 0f, 1f, 1f)
        };
    }

    /// <summary>
    /// デフォルト値（全体表示）を作成。
    /// </summary>
    public static FocusArea Default => new FocusArea
    {
        Preset = FocusPreset.Full,
        CustomRect = new Rect(0f, 0f, 1f, 1f)
    };

    /// <summary>
    /// プリセットから作成。
    /// </summary>
    public static FocusArea FromPreset(FocusPreset preset) => new FocusArea
    {
        Preset = preset,
        CustomRect = new Rect(0f, 0f, 1f, 1f)
    };

    /// <summary>
    /// カスタム領域から作成。
    /// </summary>
    public static FocusArea FromCustom(Rect rect) => new FocusArea
    {
        Preset = FocusPreset.Custom,
        CustomRect = rect
    };
}

/// <summary>
/// フォーカス領域のプリセット。
/// </summary>
public enum FocusPreset
{
    /// <summary>全体を表示</summary>
    Full,

    /// <summary>上半分（顔周りなど）</summary>
    UpperHalf,

    /// <summary>上1/3（顔のみ）</summary>
    UpperThird,

    /// <summary>下半分</summary>
    LowerHalf,

    /// <summary>中央部分（正方形）</summary>
    Center,

    /// <summary>中央部分（横長）</summary>
    CenterWide,

    /// <summary>カスタム領域を使用</summary>
    Custom
}
