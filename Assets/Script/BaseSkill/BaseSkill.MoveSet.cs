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

    /// <summary>
    /// 現在のムーブセット
    /// </summary>
    [NonSerialized]
    MoveSet NowMoveSetState=new();

    /// <summary>
    /// A-MoveSetのList<MoveSet>から現在のMoveSetをランダムに取得する
    /// なにもない場合はreturnで終わる。つまり単体攻撃前提ならmovesetが決まらない。
    /// aOrB 0:A 1:B
    /// </summary>
    public void DecideNowMoveSet_A0_B1(int aOrB)
    {
        if(aOrB == 0)
        {
            if(A_MoveSet_Cash.Count == 0)
            {
                NowMoveSetState = new();
                return;
            }
            NowMoveSetState = A_MoveSet_Cash[RandomSource.NextInt(A_MoveSet_Cash.Count)];
        }
        else if(aOrB == 1)
        {
            if(B_MoveSet_Cash.Count == 0)
            {
                NowMoveSetState = new();
                return;
            }
            NowMoveSetState = B_MoveSet_Cash[RandomSource.NextInt(B_MoveSet_Cash.Count)];
        }
    }

    /// <summary>
    /// キャッシュ用(スキルレベルのメモ参照)
    /// </summary>
    List<MoveSet> A_MoveSet_Cash = new();
    /// <summary>
    /// キャッシュ用(スキルレベルのメモ参照)
    /// </summary>
    List<MoveSet> B_MoveSet_Cash=new();
    /// <summary>
    /// 連続攻撃中にスキルレベル成長によるムーブセット変更を防ぐために、
    /// 連続攻撃開始時にムーブセットをキャッシュし使い続ける
    /// Phase 2: SkillLevelDataから直接読み取り
    /// </summary>
    public void CashMoveSet()
    {
        A_MoveSet_Cash = FixedSkillLevelData[_levelIndex].A_MoveSet ?? new();
        B_MoveSet_Cash = FixedSkillLevelData[_levelIndex].B_MoveSet ?? new();
    }


}


/// <summary>
/// インスペクタで表示可能なAimStyleのリストの一つのステータスとDEFATKのペア
/// </summary>
[Serializable]
public class MoveSet: ISerializationCallbackReceiver
{
    public List<AimStyle> States = new List<AimStyle>();
    public List<float> DEFATKList = new List<float>();
    public AimStyle GetAtState(int index)
    {
        return States[index];
    }
    public float GetAtDEFATK(int index)
    {
        return DEFATKList[index];
    }
    public MoveSet()
    {
        States.Clear();
        DEFATKList.Clear();
    }
    //Unityインスペクタ上で新しく防御無視率を設定した際に、デフォルトで何も起こらない(=1.0f)値が入るようにするための処理。

    // 旧サイズを保持しておくための変数
    [NonSerialized]
    private int oldSizeDEFATK = 0;

// シリアライズ直前に呼ばれる
    public void OnBeforeSerialize()
    {
        // 新規追加があった場合、そのぶんだけ1.0fを代入
        if (DEFATKList.Count > oldSizeDEFATK)
        {
            for (int i = oldSizeDEFATK; i < DEFATKList.Count; i++)
            {
                // 新しく挿入された分
                DEFATKList[i] = 1.0f;
            }
        }

        // 今回のリストサイズを保存
        oldSizeDEFATK = DEFATKList.Count;
    }

    // デシリアライズ後に呼ばれる
    public void OnAfterDeserialize()
    {
        // 特に何もしない場合は空でOK
    }
    public MoveSet DeepCopy()
    {
        // 新しい MoveSet を生成
        var copy = new MoveSet();

        // List<AimStyle> の中身をまるごとコピー
        // (AimStyle は enum なので値コピーで OK)
        copy.States = new List<AimStyle>(this.States);

        // List<float> の中身をまるごとコピー
        copy.DEFATKList = new List<float>(this.DEFATKList);

        // oldSizeDEFATK は NonSerializedなので
        // 新しい copy では 0 に初期化されるが
        // OnBeforeSerialize が動くときにまた
        // 適切に更新されるので問題なし

        return copy;
    }



}

