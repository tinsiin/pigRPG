using System;
using UnityEngine;

[Serializable]
public class AllySkill : BaseSkill
{
    [Header("スキルのidは必ず設定、また、下にあるValidSkillIDListにIDが入ってないと有効にならない\n(実際のプレイで成長するって要素があるから、これは初期値とデバック用の話ね)")]
    [SerializeField]
    int _iD;
    /// <summary>
    /// id　主に有効化されてるかどうか
    /// </summary>
    public int ID => _iD;

    /// <summary>
    /// allySkillにおける行使者はAllyClassなのでキャストして返す
    /// </summary>
    public new AllyClass Doer => (AllyClass)base.Doer;
    [Header("スキルの熟練度はプレイで成長するから、基本的にはゼロだけど、\n特殊なフレーバー要素で上げとくのもあり。")]
    /// <summary>
    /// スキル熟練度　単純な数のプロパティで、標準のロジックはスキル内ではないからここでは数だけ
    /// </summary>
    public float Proficiency;

    /// <summary>
    /// スキル使用回数をカウントアップする
    /// allySkillでは熟練度が加算される
    /// </summary>
    public override void DoSkillCountUp()
    {
        base.DoSkillCountUp();
        AddProficiency();
        AddEmotionalAttachmentSkillQuantity();
    }

    /// <summary>
    /// 熟練度を加算する（思い入れ量に応じてスケーリング）
    /// </summary>
    void AddProficiency()
    {
        // このスキルが思い入れスキルかどうかチェック
        if (Doer.EmotionalAttachmentSkillID == ID)
        {
            // 思い入れスキルの場合、思い入れ量に応じてスケーリング
            float multiplier = Doer.GetProficiencyMultiplierFromQuantity();
            Proficiency += multiplier;
            Debug.Log($"思い入れスキル熟練度加算: +{multiplier:F2} (思い入れ量: {Doer.EmotionalAttachmentSkillQuantity})");
        }
        else
        {
            // 通常スキルは1.0倍
            Proficiency += 1.0f;
        }
    }
    /// <summary>
    /// allySkillで継承したスキルパワー関数
    /// </summary>
    protected override float _skillPower(bool IsCradle)
    {
        //スキルの思い入れ由来のhp固定加算値
        return base._skillPower(IsCradle) + CurrentHPFixedAdditionSkillPower();
    }
    /// <summary>
    /// 現在HP固定加算をスキルパワーに加算する用の関数
    /// スキルの思い入れ由来の固定加算値
    /// </summary>
    float CurrentHPFixedAdditionSkillPower()
    {
        if(Doer.EmotionalAttachmentSkillID == ID)//思い入れスキルならhp固定加算が入る
        {
            // 思い入れ量から倍率を取得
            float multiplier = Doer.GetCurrentHPFixedAdditionMultiplier();
            
            // 基礎値×現在HP = SkillPower加算値
            float currentHP = Doer.HP;
            float additionPower = multiplier * currentHP;
            
            Debug.Log($"思い入れスキルHP固定加算: +{additionPower:F2} (倍率: {multiplier:F3}, 現在HP: {currentHP}, 思い入れ量: {Doer.EmotionalAttachmentSkillQuantity})");
            return additionPower;
        }
        return 0;
    }
    /// <summary>
    /// 思い入れ量を加算する用の関数
    /// </summary>
    void AddEmotionalAttachmentSkillQuantity()
    {
        Doer.EmotionalAttachmentSkillQuantity++;
    }
    /// <summary>
    /// TLOAの思い入れスキル用弱体化スキルパッシブを付与する専用の関数
    /// 重複を防ぐため既にあった場合、同名のパッシブを削除する
    /// </summary>
    /// <param name="passive">ここに渡すのは弱体化スキルパッシブ(スキルの思い入れ由来)専用</param>
    public void ApplyEmotionalAttachmentSkillQuantityChangeSkillWeakeningPassive(BaseSkillPassive passive)
    {
        var tuning = PlayersStatesHub.Tuning;
        var weakeningPassive = tuning?.EmotionalAttachmentSkillWeakeningPassiveRef;
        if (weakeningPassive == null)
        {
            Debug.LogError("ApplyEmotionalAttachmentSkillQuantityChangeSkillWeakeningPassive: Tuning が未設定です");
            return;
        }
        if(passive.Name == weakeningPassive.Name)
        {
            //弱体化スキルパッシブに該当するものが一つでもあったら、それを消す
            for (int i = ReactiveSkillPassiveList.Count - 1; i >= 0; i--)//回してるリストから削除するのでfor文で逆順に回すと安心
            {
                if(ReactiveSkillPassiveList[i].Name == passive.Name)
                {
                    ReactiveSkillPassiveList.RemoveAt(i);
                }
            }
            //思い入れ弱体化スキルパッシブを付与する
            ApplySkillPassive(passive);
        }else
        {
            Debug.LogError("思い入れ弱体化スキルパッシブ付与関数に\n渡されたパッシブがスキルの思い入れ専用の弱体化スキルパッシブではありません");
        }
    }
    
    public AllySkill InitAllyDeepCopy()
    {
        var clone = new AllySkill();
        InitDeepCopy(clone);

        clone._iD = _iD;
        clone.Proficiency = Proficiency;//熟練度
        return clone;
    }
}