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
    [SerializeField]
    float _defAtk;
    
    /// <summary>
    /// 連続攻撃時にはそれ用の、それ以外はスキル自体の防御無視率が返ります。
    /// </summary>
    public float DEFATK{
        get{
            
            if(NowConsecutiveATKFromTheSecondTimeOnward())//連続攻撃中で、
            {
                if(!IsSingleHitAtk)//単回攻撃でないなら
                {
                    return NowAimDefATK();//連続攻撃に設定されているDEFATKを乗算する
                }
            }

            return _defAtk;
        }
    }
    /// <summary>
    /// 現在のムーブセットでのDEFATKを、現在の攻撃回数から取得する
    /// 初回攻撃を指定したらエラー出るます
    /// </summary>
    float NowAimDefATK()
    {
        //Debug.Log("NowMoveSetState.GetAtDEFATK(_atkCountUP - 1) = " +(_atkCountUP-1));
        return NowMoveSetState.GetAtDEFATK(_atkCountUP - 1); 
        //-1してる理由　ムーブセットListは二回目以降から指定されるので。
        //リストのインデックスでしっかり初回から参照されるように二回目前提として必ず-1をする。
    }

}
