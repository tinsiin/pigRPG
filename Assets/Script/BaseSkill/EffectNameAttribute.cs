using UnityEngine;

/// <summary>
/// string フィールドに付与すると、Inspector上で
/// Assets/Resources/Effects/*.json のドロップダウン選択UIに変わる。
/// targetFilter に "icon" / "field" を指定すると、該当targetのエフェクトのみ表示。
/// 未指定(null)なら全エフェクトを表示。
/// </summary>
public class EffectNameAttribute : PropertyAttribute
{
    public string TargetFilter { get; }

    public EffectNameAttribute() { TargetFilter = null; }
    public EffectNameAttribute(string targetFilter) { TargetFilter = targetFilter; }
}
