using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;

public abstract partial class BaseStates    
{

    List<BaseVitalLayer> _vitalLayerList = new();
    /// <summary>
    /// 初期所持のVitalLayerのIDリスト
    /// </summary>
    [Space]
    [Header("追加HP(バリア層) 初期設定")]
    [Tooltip("初期所持するVitalLayerのIDリスト（初期化時に適用）")]
    [SerializeField] List<int> InitVitalLaerIDList = new();
    public IReadOnlyList<BaseVitalLayer> VitalLayers => _vitalLayerList;
    /// <summary>
    /// インスペクタ上で設定されたIDを通じて特定の追加HPを持ってるか調べる
    /// </summary>
    public bool HasVitalLayer(int id)
    {
        return _vitalLayerList.Any(vit => vit.id == id);
    }
    public void RemoveVitalLayer(int id)
    {
        int index = _vitalLayerList.FindIndex(l => l.id == id);
        if (index >= 0)
        {
            _vitalLayerList.RemoveAt(index);
        }
    }
    /// <summary>
    ///追加HPを適用  passiveと違い適合条件がないからvoid
    /// </summary>
    public void ApplyVitalLayer(int id)
    {
        //リスト内に同一の物があるか判定する。
        var sameHP = _vitalLayerList.FirstOrDefault(lay => lay.id == id);
        if (sameHP != null)
        {
            sameHP.ReplenishHP();//同一の為リストにある側を再補充する。
        }
        else//初物の場合
        {
            var newLayer = VitalLayerManager.Instance.GetAtID(id).DeepCopy();//マネージャーから取得 当然ディープコピー
            newLayer.ReplenishHP();//maxにしないとね
            //優先順位にリスト内で並び替えつつ追加
            // _vitalLaerList 内で、新しいレイヤーの Priority より大きい最初の要素のインデックスを探す
            int insertIndex = _vitalLayerList.FindIndex(v => v.Priority > newLayer.Priority);

            if (insertIndex < 0)
            {
                // 該当する要素が見つからなかった場合（全ての要素が新しいレイヤー以下の Priority ）
                // リストの末尾に追加
                _vitalLayerList.Add(newLayer);
            }
            else
            {
                // 新しいレイヤーを適切な位置に挿入
                _vitalLayerList.Insert(insertIndex, newLayer);
            }
        }
    }
    /// <summary>
    /// 追加HPを消す
    /// </summary>
    public void RemoveVitalLayerByID(int id)
    {
        var layer = _vitalLayerList.FirstOrDefault(lay => lay.id == id);
        if (layer != null)//あったら消す
        {
            _vitalLayerList.Remove(layer);
        }
        else
        {
            Debug.Log("RemoveVitalLayer nothing. id:" + id);
        }
    }

    /// <summary>
    /// vitalLayerでHPに到達する前に攻撃値を請け負う処理
    /// </summary>
    public void BarrierLayers(ref StatesPowerBreakdown dmg, ref StatesPowerBreakdown mentalDmg,BaseStates atker)
    {

        // 1) VitalLayer の順番どおりにダメージを適用していく
        //    ここでは「Priority が低い方(手前)が先に処理される想定」を前提に
        //    _vitalLaerList がすでに正しい順序でソートされていることを期待。

        for (int i = 0; i < _vitalLayerList.Count;)
        {
            var layer = _vitalLayerList[i];
            var skillPhy = atker.NowUseSkill.SkillPhysical;
            // 2) このレイヤーに貫通させて、返り値を「残りダメージ」とする
            layer.PenetrateLayer(ref dmg, ref mentalDmg, skillPhy);

            if (layer.LayerHP <= 0f)
            {
                // このレイヤーは破壊された
                _vitalLayerList.RemoveAt(i);
                // リストを削除したので、 i はインクリメントしない（要注意）
                //破壊慣れまたは破壊負け
                var kerekere = atker.TenDayValues(true).GetValueOrZero(TenDayAbility.KereKere);
                if (skillPhy == PhysicalProperty.heavy)//暴断なら破壊慣れ
                {
                    dmg += dmg * 0.015f * kerekere;
                }
                if (skillPhy == PhysicalProperty.volten)//vol天なら破壊負け
                {
                    dmg -= dmg * 0.022f * (atker.b_ATK.Total - kerekere);
                    //b_atk < kerekereになった際、減らずに逆に威力が増えるので、そういう場合の演出差分が必要
                }
            }
            else
            {
                // レイヤーが残ったら i を進める
                i++;
            }

            // 3) dmg が 0 以下になったら、もうこれ以上削る必要ない
            if (dmg.Total <= 0f)
            {
                break;
            }
        }

    }
    

}
