   using R3;
using RandomExtensions;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
public partial class BaseSkill
{
    //  ==============================================================================================================================
    //                                            スキルのパッシブ効果のインスペクタ上フィールド部分
    //  ==============================================================================================================================

    [Header("パッシブ付与スキルの付与パッシブ")]
    /// <summary>
    /// SubEffectsの基本的な奴
    /// </summary>
    [SerializeField] List<int> subEffects;
    [Header("追加HP付与スキルの付与追加HP")]
    /// <summary>
    /// スキル実行時に付与する追加HP(Passive由来でない)　ID指定
    /// </summary>
    public List<int> subVitalLayers;
    [Header("除去スキルとして消せるパッシブ")]
    /// <summary>
    /// 除去スキルとして消せるパッシブのID範囲
    /// </summary>
    public List<int> canEraceEffectIDs;
    [Header("消せる数")]
    /// <summary>
    /// 除去スキルとして使用する際に指定する消せるパッシブの数
    /// 除去スキルでないと参照されない
    /// </summary>
    public int CanEraceEffectCount;

    [Header("除去スキルとして消せる追加HP")]
    /// <summary>
    /// 除去スキルとして消せる追加HPのID範囲
    /// </summary>
    public List<int> canEraceVitalLayerIDs;
    [Header("消せる数")]
    /// <summary>
    /// 除去スキルとして使用する際に指定する消せる追加HPの数
    /// 除去スキルでないと参照されない
    /// </summary>
    public int CanEraceVitalLayerCount;

    //  ==============================================================================================================================
    //                                            スキルのパッシブ効果の処理関数など
    //  ==============================================================================================================================

    /// <summary>
    /// 現在の消せるパッシブの数
    /// ReactionSkill内で除去する度に減っていく値
    /// </summary>
    [NonSerialized]
    public int Now_CanEraceEffectCount;
    /// <summary>
    /// 現在の消せる追加HPの数
    /// ReactionSkill内で除去する度に減っていく値
    /// </summary>
    [NonSerialized]
    public int Now_CanEraceVitalLayerCount;
    /// <summary>
    /// 除去可能数をReactionSkill冒頭で補充する。
    /// </summary>
    public void RefilCanEraceCount()
    {
        Now_CanEraceEffectCount = CanEraceEffectCount;
        Now_CanEraceVitalLayerCount = CanEraceVitalLayerCount;
    }


    /// <summary>
    /// スキル実行時に付与する状態異常とか ID指定
    /// </summary>
    public List<int> SubEffects
    {
        get { return (subEffects ?? Enumerable.Empty<int>())
                        .Concat(bufferSubEffects ?? Enumerable.Empty<int>()).ToList(); }
    }

    //  ==============================================================================================================================
    //                                            パッシブ付与スキル用のバッファ
    //  ==============================================================================================================================


    /// <summary>
    /// SubEffectsのバッファ 主にパッシブによる追加適用用など
    /// </summary>
    List<int> bufferSubEffects = new();
    /// <summary>
    /// スキルのパッシブ付与効果に追加適用する。　
    /// バッファーのリストに追加する。
    /// </summary>
    /// <param name="subEffects"></param>
    public void SetBufferSubEffects(List<int> subEffects)
    {
        bufferSubEffects = subEffects;
    }
    /// <summary>
    /// スキルのパッシブ付与効果の追加適用を消す。
    /// </summary>
    public void EraseBufferSubEffects()
    {
        if (bufferSubEffects == null)
        {
            // 安全対策：未初期化なら初期化して終了
            bufferSubEffects = new List<int>();
            return;
        }
        bufferSubEffects.Clear();
    }



}