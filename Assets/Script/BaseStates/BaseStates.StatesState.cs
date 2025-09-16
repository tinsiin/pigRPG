using System;
using System.Collections.Generic;
using UnityEngine;
using static CommonCalc;
using NRandom;
using Cysharp.Threading.Tasks;
using System.Linq;


//状態管理系ステータス
public abstract partial class BaseStates    
{

    //  ==============================================================================================================================
    //                                              キャラクター属性(精神属性)
    //  ==============================================================================================================================
    
    [SerializeField] //フィールドをシリアライズ
    private SpiritualProperty _myImpression;
    /// <summary>
    ///     このキャラクターの属性 精神属性が入る
    /// </summary>
    public SpiritualProperty MyImpression
    {
        get => _myImpression;       // 取得は公開
        protected set => _myImpression = value;  // 変更は継承クラス内のみ許可
    }

    /// <summary>
    ///     このキャラクターの"デフォルト"属性 精神属性が入る
    ///     一定数歩行するとMyImpressionがこれに戻る
    ///     当然この属性自体もゲーム中で変化する可能性はある。
    /// </summary>
    public SpiritualProperty DefaultImpression;
   
    /// <summary>
     /// このキャラの印象/キャラクタ属性と一致してるかどうか
     /// </summary>
    public bool HasCharacterImpression(SpiritualProperty imp)
    {
        // Inspector の "Everything" は -1 でシリアライズされるため、全許可として扱う
        if ((int)imp == -1) return true;
        return (MyImpression & imp) == imp;
    }
    /// <summary>
    /// 自分の持つ精神属性の数を取得する。
    /// 精神ポテンシャルとも言うよ
    /// </summary>
    public int GetMySpiritualPotential()
    {
        //重複しないコレクションを作成
        HashSet<SpiritualProperty> spiritualPropertyHashSet = new HashSet<SpiritualProperty>
        {
            //デフォルト精神属性
            DefaultImpression
        };

        //スキルの精神属性
        foreach (var skill in SkillList)
        {
            spiritualPropertyHashSet.Add(skill.SkillSpiritual);
        }

        //重複しない精神属性の数を返す
        return spiritualPropertyHashSet.Count;
    }

    //  ==============================================================================================================================
    //                                              種別
    //  ==============================================================================================================================
    /// <summary>
    ///     このキャラクターの種別
    /// </summary>
    public CharacterType MyType;

    /// <summary>
    /// このキャラの種別と一致してるかどうか
    /// </summary>
    public bool HasCharacterType(CharacterType type)
    {
        // Inspector の "Everything" は -1 でシリアライズされるため、全許可として扱う
        if ((int)type == -1) return true;
        return (MyType & type) == type;
    }

    //  ==============================================================================================================================
    //                                              回避率/攻撃率の落ち着き管理
    //  ==============================================================================================================================

    /// <summary>
    /// 落ち着きカウント
    /// 回避率や次攻撃者率　の平準化に用いられる
    /// </summary>
    int CalmDownCount = 0;
    /// <summary>
    /// 落ち着きカウントの最大値
    /// 影響された補正率がどの程度平準化されているかの計算に用いるために保存する。
    /// </summary>
    int CalmDownCountMax;
    /// <summary>
    /// 落ち着きカウントの最大値算出
    /// </summary>
    int CalmDownCountMaxRnd => RandomEx.Shared.NextInt(4, 8);
    /// <summary>
    /// 落ち着きカウントのカウント開始準備
    /// スキル回避率もセット
    /// </summary>
    public void CalmDownSet(float EvasionModifier = 1f, float AttackModifier = 1f)
    {
        CalmDownCountMax = CalmDownCountMaxRnd;//乱数から設定して、カウントダウンの最大値を設定
        CalmDownCount = CalmDownCountMax;//カウントダウンを最大値に設定
        CalmDownCount++;//NextTurnで即引かれるので調整　　落ち着き#カウント対処を参照して
        _skillEvasionModifier = EvasionModifier;//スキルにより影響された回避補正率をセット
        _skillAttackModifier = AttackModifier;//スキルにより影響された攻撃補正率をセット
    }
    /// <summary>
    /// 落ち着きカウントダウン
    /// </summary>
    void CalmDownCountDec()
    {
        CalmDownCount--;
    }
    /// <summary>
    /// 意図的に落ち着きカウントをゼロにすることにより、落ち着いた判定にする。
    /// </summary>
    void CalmDown()
    {
        CalmDownCount = 0;
    }
    //  ==============================================================================================================================
    //                                              パワー
    //  ==============================================================================================================================


    public ThePower NowPower = ThePower.medium;//初期値は中

    /// <summary>
    /// NowPowerが一段階上がる。
    /// </summary>
    void Power1Up()
    {
        NowPower = NowPower switch
            {
                ThePower.lowlow => ThePower.low,
                ThePower.low => ThePower.medium,
                ThePower.medium => ThePower.high,
                ThePower.high => ThePower.high, // 既に最高値の場合は変更なし
                _ => NowPower//ここはdefault句らしい
            };

    }

    /// <summary>
    /// NowPowerが一段階下がる。
    /// </summary>
    void Power1Down()
    {
        NowPower = NowPower switch
            {
                ThePower.high => ThePower.medium,
                ThePower.medium => ThePower.low,
                ThePower.low => ThePower.lowlow,
                ThePower.lowlow => ThePower.lowlow, // 既に最低値の場合は変更なし
                _ => NowPower//ここはdefault句らしい
            };
    }
    /// <summary>
    /// 殺した時にパワー上がるかな
    /// </summary>
    void HighNessChance(BaseStates deathEne)
    {
        var matchSkillCount = 0;
        foreach(var skill in deathEne.SkillList)//倒した敵のスキルで回す
        {
            if(skill.IsTLOA && skill.SkillSpiritual == MyImpression)//スキルがTLOAで自分の精神属性と一致するなら
            {
                matchSkillCount++;
            }
        }

        if(matchSkillCount > 0 && rollper(GetPowerUpChanceOnKillEnemy(matchSkillCount)))//合致数が一個以上あり、ハイネスチャンスの確率を通過すれば。
        {
            Power1Up();
        }
    }
    /// <summary>
    /// 敵を倒した時のパワー増加確率(%)を返す関数。 「ハイネスチャンスの確率」
    /// 精神属性ごとに分岐し、一致スキルの数 × 5% を加算する。
    /// </summary>
    float GetPowerUpChanceOnKillEnemy(int matchingSkillCount)
    {
        // 基礎確率を設定
        float baseChance = MyImpression switch
        {
            SpiritualProperty.kindergarden => 40f,
            SpiritualProperty.liminalwhitetile => 30f,
            _ => 20f
        };

        // 一致スキル数 × 5% を加算
        float totalChance = baseChance + matchingSkillCount * 5f;

        // 必要に応じて上限100%に丸めるなら下記をアンコメント
        // if (totalChance > 100f) totalChance = 100f;

        return totalChance;
    }

    /// <summary>
    /// キャラクターのパワーが歩行によって変化する関数
    /// </summary>
    protected void TransitionPowerOnWalkByCharacterImpression()
    {
        switch(MyImpression)
        {
            case SpiritualProperty.doremis:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(35))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(25))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(6))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(6))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        if(rollper(2.7f))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(7.55f))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                }
                break;
            case SpiritualProperty.pillar:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(2.23f))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(5))
                        {
                            NowPower = ThePower.high;
                        }
                        if(rollper(20))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(6.09f))
                        {
                            NowPower = ThePower.medium;
                        }
                        if(rollper(15))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(8))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }

                break;
            case SpiritualProperty.kindergarden:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(25))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(31))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(28))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(25))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(20))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(30))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.liminalwhitetile:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(17))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(3))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(3.1f))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(13))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(2))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(40))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.sacrifaith:
                switch(NowPower)
                {
                    case ThePower.high:
                        //不変
                    case ThePower.medium:
                        if(rollper(14))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(20))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(26))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.cquiest:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(14))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(3))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(3.1f))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(13))
                        {
                            NowPower = ThePower.medium;
                        }

                        break;
                    case ThePower.lowlow:
                        if(rollper(4.3f))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.pysco:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(77.77f))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(6.7f))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(3))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(90))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(10))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(80))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.godtier:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(4.26f))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(3))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(30))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(28))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(8))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(100))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.baledrival:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(9))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(25))
                        {
                            NowPower = ThePower.high;
                        }
                        if(rollper(11))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(26.5f))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(8))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(50))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.devil:
                switch(NowPower)
                {
                    case ThePower.high:
                        if(rollper(5))
                        {
                            NowPower = ThePower.medium;
                        }
                        break;
                    case ThePower.medium:
                        if(rollper(6))
                        {
                            NowPower = ThePower.low;
                        }
                        if(rollper(4.1f))
                        {
                            NowPower = ThePower.high;
                        }
                        break;
                    case ThePower.low:
                        if(rollper(15))
                        {
                            NowPower = ThePower.medium;
                        }

                        if(rollper(7))
                        {
                            NowPower = ThePower.lowlow;
                        }
                        break;
                    case ThePower.lowlow:
                        if(rollper(22))
                        {
                            NowPower = ThePower.low;
                        }
                        break;
                }
                break;
        }
    }

    //  ==============================================================================================================================
    //                                              人間状況
    //  ==============================================================================================================================
    /// <summary>
    /// 現在のこのキャラの人間状況
    /// </summary>
    HumanConditionCircumstances NowCondition;
    /// <summary>
    /// 前回の人間状況　同じのが続いてるかの判断要
    /// </summary>
    HumanConditionCircumstances PreviousCondition;
    /// <summary>
    /// 人間状況の続いてるターン　想定連続ターン
    /// </summary>
    int ConditionConsecutiveTurn;
    /// <summary>
    /// 人間状況の累積連続ターン　強制変化用
    /// </summary>
    int TotalTurnsInSameCondition;
    /// <summary>
    /// 人間状況の短期継続ターンをリセットする
    /// </summary>
    void ResetConditionConsecutiveTurn()
    {
        ConditionConsecutiveTurn = 0;
    }
    /// <summary>
    /// 人間状況のターン変数をすべてリセット
    /// </summary>
    void ResetConditionTurns()
    {
        ConditionConsecutiveTurn = 0;
        TotalTurnsInSameCondition = 0;
    }
    /// <summary>
    /// 人間状況が変わった際に必要な処理
    /// 基本的にConditionInNextTurnで自動で処理されるから、各人間状況変化に個別には必要ない。
    /// ただし、時間変化の際は別途呼び出す必要がある。(ConditionInNextTurnを参照してください。)
    /// </summary>
    void ConditionTransition()
    {
            PreviousCondition = NowCondition;
            ResetConditionTurns();
    }
    /// <summary>
    /// 人間状況の次のターンへの変化
    /// </summary>
    void ConditionInNextTurn() 
    {
        // 状態が変わってたら
        if (PreviousCondition != NowCondition)
        {
            ConditionTransition();
        }else
        {//変わってなければターン経過
            ConditionConsecutiveTurn++;
            TotalTurnsInSameCondition++;
        }

        //ターン数が増えた後に時間変化の関数を実行  
        ApplyConditionChangeOnTimePass();
    }
    /* ---------------------------------
     * イベント変化
     * --------------------------------- 
     */

    /// <summary>
    /// 戦闘開始時に決まる人間状況の初期値
    /// </summary>
    public void ApplyConditionOnBattleStart(float eneTenDays)
    {
        var myTenDays = TenDayValuesSum(false);
        // 安全策として、0除算を避ける
        float ratio = (eneTenDays == 0) 
            ? 999999f // 敵が0なら自分が勝ってる扱い(∞倍勝ち)
            : myTenDays / eneTenDays;

        // パワー(NowPower)は ThePower 型 (lowlow, low, medium, high など)
        // MyImpression は精神属性

        // 初期値はとりあえず普調にしておいて、後で条件を満たせば上書きする
        NowCondition = HumanConditionCircumstances.Normal;

        switch (MyImpression)
        {
            //--------------------------------
            // 1) ベール (baledrival)
            //--------------------------------
            case SpiritualProperty.baledrival:
                // 「高揚」：パワーが高 && 2倍負け( ratio <= 0.5 )
                if (NowPower == ThePower.high && ratio <= 0.5f)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                }
                else
                {
                    // それ以外は「楽観的」
                    NowCondition = HumanConditionCircumstances.Optimistic;
                }
                break;

            //--------------------------------
            // 2) デビル (devil)
            //--------------------------------
            case SpiritualProperty.devil:
                // 「高揚」：1.8倍勝ち ( ratio >= 1.8 )
                if (ratio >= 1.8f)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                }
                else
                {
                    // それ以外 => 「普調」 (疑念にはならない)
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 3) 自己犠牲 (sacrifaith)
            //--------------------------------
            case SpiritualProperty.sacrifaith:
                // 覚悟：パワーが low より上(=low以上) かつ 2倍負け( ratio <= 0.5 )
                //   ※「パワーがlow“以上”」= (low, medium, highのいずれか)
                if (NowPower >= ThePower.low && ratio <= 0.5f)
                {
                    NowCondition = HumanConditionCircumstances.Resolved;
                }
                // 疑念：パワーがlowlow && 1.6倍負け( ratio <= 1/1.6≒0.625 )
                else if (NowPower == ThePower.lowlow && ratio <= 0.625f)
                {
                    NowCondition = HumanConditionCircumstances.Doubtful;
                }
                else
                {
                    // それ以外 => 普調
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 4) ゴッドティア (godtier)
            //--------------------------------
            case SpiritualProperty.godtier:
                // 「楽観的」: 総量2.5倍勝ち( ratio >= 2.5 )
                if (ratio >= 2.5f)
                {
                    NowCondition = HumanConditionCircumstances.Optimistic;
                }
                // 「覚悟」 : パワーがmedium以上 && 2倍負け( ratio <= 0.5 )
                else if (NowPower >= ThePower.medium && ratio <= 0.5f)
                {
                    NowCondition = HumanConditionCircumstances.Resolved;
                }
                else
                {
                    // それ以外 => 普調
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 5) リーミナルホワイトタイル (liminalwhitetile)
            //--------------------------------
            case SpiritualProperty.liminalwhitetile:
                // 「楽観的」: 総量2倍勝ち( ratio >= 2.0 )
                if (ratio >= 2.0f)
                {
                    NowCondition = HumanConditionCircumstances.Optimistic;
                }
                // 「疑念」 : 2倍負け( ratio <= 0.5 )
                else if (ratio <= 0.5f)
                {
                    NowCondition = HumanConditionCircumstances.Doubtful;
                }
                else
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 6) キンダーガーデン (kindergarden)
            //--------------------------------
            case SpiritualProperty.kindergarden:
                // 「楽観的」: 1.7倍勝ち
                if (ratio >= 1.7f)
                {
                    NowCondition = HumanConditionCircumstances.Optimistic;
                }
                // 「疑念」 : 1.5倍負け ( ratio <= 2/3 = 0.6667 )
                else if (ratio <= 0.6667f)
                {
                    NowCondition = HumanConditionCircumstances.Doubtful;
                }
                else
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                break;

            //--------------------------------
            // 7) 支柱 (pillar) 
            //    戦闘開始時は「普調」だけ
            //--------------------------------
            case SpiritualProperty.pillar:
                NowCondition = HumanConditionCircumstances.Normal;
                break;

            //--------------------------------
            // 8) サイコパス (pysco)
            //    戦闘開始時は常に落ち着く => 普調
            //--------------------------------
            case SpiritualProperty.pysco:
                NowCondition = HumanConditionCircumstances.Normal;
                break;

            //--------------------------------
            // 9) ドレミス, シークイエスト, etc. 
            //    仕様外 or 未指定なら一旦「普調」にする
            //--------------------------------
            default:
                NowCondition = HumanConditionCircumstances.Normal;
                break;
        }
    }
    /// <summary>
    /// 人間状況の時間変化
    /// </summary>
    void ApplyConditionChangeOnTimePass()
    {
        bool changed = false; // 状態が変化したかどうか

        switch (NowCondition)
        {
            case HumanConditionCircumstances.Resolved:
                // 覚悟 → 高揚 (想定17)
                if (ConditionConsecutiveTurn >= 17)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Angry:
                // 怒り → 普調 (想定10)
                if (ConditionConsecutiveTurn >= 10)
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                    changed = true;
                }
                // 怒り → 高揚 (累積23)
                else if (TotalTurnsInSameCondition >= 23)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Doubtful:
                // 疑念 → 楽観的 (想定11)
                if (ConditionConsecutiveTurn >= 11)
                {
                    NowCondition = HumanConditionCircumstances.Optimistic;
                    changed = true;
                }
                // 疑念 → 混乱 (累積19)
                else if (TotalTurnsInSameCondition >= 19)
                {
                    NowCondition = HumanConditionCircumstances.Confused;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Confused:
                // 混乱 → 普調 (想定11)
                if (ConditionConsecutiveTurn >= 11)
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                    changed = true;
                }
                // 混乱 → 高揚 (累積22)
                else if (TotalTurnsInSameCondition >= 22)
                {
                    NowCondition = HumanConditionCircumstances.Elated;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Elated:
                // 高揚 → 普調 (想定13)
                if (ConditionConsecutiveTurn >= 13)
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                    changed = true;
                }
                break;

            case HumanConditionCircumstances.Painful:
                // 辛い → 普調 (想定14)
                if (ConditionConsecutiveTurn >= 14)
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                    changed = true;
                }
                break;

            // 楽観的, 普調 などは今回の仕様では変化しないので何もしない
            default:
                break;
        }

        if (changed)
        {
            ConditionTransition();
        }
    }
        
    /// <summary>
    /// 相性値の高い仲間が死んだ際の人間状況の変化
    /// </summary>
    public void ApplyConditionChangeOnCloseAllyDeath(int deathCount)
    {
        if(MyType == CharacterType.Life)//基本的に生命のみ
        {
            switch (NowCondition)//死によって、どの状況からどの状況へ変化するか
            {
                case HumanConditionCircumstances.Painful://辛い
                    NowCondition = HumanConditionCircumstances.Confused;//辛いと誰でも混乱する
                    break;
                case HumanConditionCircumstances.Optimistic://楽観的
                    switch(MyImpression)
                    {
                        case SpiritualProperty.kindergarden:
                            if(rollper(36))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.pysco:
                            if(deathCount > 1)
                            {//二人なら危機感を感じて普調になる
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.baledrival:
                            if(deathCount > 1)
                            {//二人なら怒り
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                //そうでないなら変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.devil:
                        case SpiritualProperty.sacrifaith:
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.doremis:
                            NowCondition = HumanConditionCircumstances.Angry;
                        break;
                        case SpiritualProperty.liminalwhitetile:
                            if(deathCount>1 && rollper(10))
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                    }
                    break;
                case HumanConditionCircumstances.Elated:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(deathCount>1)
                            {//二人なら混乱
                                NowCondition = HumanConditionCircumstances.Confused;
                            }else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            NowCondition = HumanConditionCircumstances.Confused;
                        break;
                        case SpiritualProperty.doremis:
                            if(deathCount == 1)
                            {//一人なら混乱する
                                NowCondition = HumanConditionCircumstances.Confused;
                            }else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.sacrifaith:
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.devil:
                            NowCondition = HumanConditionCircumstances.Normal;
                        break;
                        case SpiritualProperty.baledrival:
                            if(deathCount == 1)
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし　
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.pillar:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし　
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        //シークイエストとサイコパスは変化なし
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pysco:
                            ResetConditionConsecutiveTurn();//変化なし
                            break;
                    }
                    break;
                case HumanConditionCircumstances.Resolved:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.devil:
                            if(deathCount>1)
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(44))
                            {
                                NowCondition =HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        //後は全て変化なし
                        default:
                            ResetConditionConsecutiveTurn();
                            break;
                    }
                    break;
                case HumanConditionCircumstances.Angry:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.devil:
                            if(rollper(66.66f))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(28))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.sacrifaith:
                            NowCondition = HumanConditionCircumstances.Resolved;
                        break;
                        //後は全て変化なし
                        default:
                            ResetConditionConsecutiveTurn();
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Doubtful:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.sacrifaith:
                            if(deathCount == 1)
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                        break;
                        case SpiritualProperty.liminalwhitetile:
                            if(deathCount > 1 && rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break; 
                        case SpiritualProperty.devil:  
                            if(deathCount > 1 && rollper(21.666f))
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.baledrival:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.godtier:
                            NowCondition = HumanConditionCircumstances.Resolved;
                        break;
                        case SpiritualProperty.doremis:
                            ResetConditionConsecutiveTurn();//変化なし
                        break;
                        case SpiritualProperty.pysco:
                            switch(RandomEx.Shared.NextInt(5))
                            {
                                case 0:
                                NowCondition = HumanConditionCircumstances.Optimistic;
                                break;
                                case 1:
                                NowCondition = HumanConditionCircumstances.Resolved;
                                break;
                                case 2:
                                //変化なし
                                ResetConditionConsecutiveTurn();
                                break;
                                case 3:
                                NowCondition = HumanConditionCircumstances.Doubtful;
                                break;
                                case 4:
                                NowCondition = HumanConditionCircumstances.Angry;
                                break;
                            }
                        break;
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.cquiest:
                            NowCondition = HumanConditionCircumstances.Normal;
                        break;
                    }
                    break;
                case HumanConditionCircumstances.Confused:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pillar:
                            NowCondition = HumanConditionCircumstances.Resolved;
                        break;   
                        case SpiritualProperty.devil:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.godtier:
                            if(deathCount == 1)
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        //後は全て変化なし
                        default:
                            ResetConditionConsecutiveTurn();
                            break;
                    }
                    break;
                case HumanConditionCircumstances.Normal:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.sacrifaith:
                            if(deathCount == 1)
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                        break;
                        case SpiritualProperty.liminalwhitetile:
                            if(deathCount > 1 && rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break; 
                        case SpiritualProperty.devil:  
                            if(deathCount > 1 && rollper(21.666f))
                            {
                                NowCondition = HumanConditionCircumstances.Angry;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.baledrival:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                        break;
                        case SpiritualProperty.godtier:
                            NowCondition = HumanConditionCircumstances.Resolved;
                        break;
                        case SpiritualProperty.doremis:
                            if(deathCount > 1)
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.pysco:
                            switch(RandomEx.Shared.NextInt(5))
                            {
                                case 0:
                                NowCondition = HumanConditionCircumstances.Optimistic;
                                break;
                                case 1:
                                NowCondition = HumanConditionCircumstances.Resolved;
                                break;
                                case 2:
                                //変化なし
                                ResetConditionConsecutiveTurn();
                                break;
                                case 3:
                                NowCondition = HumanConditionCircumstances.Doubtful;
                                break;
                                case 4:
                                NowCondition = HumanConditionCircumstances.Angry;
                                break;
                            }
                        break;
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pillar:
                            ResetConditionConsecutiveTurn();//変化なし
                            break;
                    }
                break;
            }

        }
    }
    /// <summary>
    /// 敵を倒した際の人間状況の変化
    /// </summary>
    public void ApplyConditionChangeOnKillEnemy(BaseStates ene)
    {
        //実行した瞬間にそのスキルによって変化した精神属性により変化してほしいので、スキルの精神属性を使う
        ////(スキル属性のキャラ代入のタイミングについて　を参照)
        var imp = NowUseSkill.SkillSpiritual;
        if (MyType == CharacterType.Life) // 基本的に生命のみ
        {
            switch (NowCondition)
            {
                case HumanConditionCircumstances.Painful:
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(66))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else if(rollper(10))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else if(rollper(10))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(33))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(57))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(33))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            var OptimisticPer = 0;//楽観的に行く確率
                            var eneKereKere = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.KereKere);
                            var eneWif = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.FlameBreathingWife);
                            var KereKere = TenDayValues(true).GetValueOrZero(TenDayAbility.KereKere);
                            var Wif = TenDayValues(true).GetValueOrZero(TenDayAbility.FlameBreathingWife);
                            if(KereKere >= eneKereKere && Wif > eneWif)
                            {
                                OptimisticPer = (int)(Wif - eneWif);
                            }
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(OptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(30))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            var NormalPer = 0;
                            var EneLeisure = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure);
                            var Leisure = TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure);
                            if(Leisure > EneLeisure)
                            {
                                NormalPer = (int)(Leisure - EneLeisure);
                            }
                            if(rollper(90 + NormalPer))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(15))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(9))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(30))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(35))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(7))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            var C_NormalEndWarPer = 0;
                            var C_NormalNightPer = 0;
                            var C_EneEndWar = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.HeavenAndEndWar);
                            var C_EneNight = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.NightInkKnight);
                            var C_EndWar = TenDayValues(true).GetValueOrZero(TenDayAbility.HeavenAndEndWar);
                            var C_Night = TenDayValues(true).GetValueOrZero(TenDayAbility.NightInkKnight);
                            if(C_EndWar > C_EneEndWar)
                            {
                                C_NormalEndWarPer = (int)(C_EndWar - C_EneEndWar);
                            }
                            if(C_Night > C_EneNight)
                            {
                                C_NormalNightPer = (int)(C_Night - C_EneNight);
                            }

                            if(rollper(80 + C_NormalEndWarPer + C_NormalNightPer))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(22))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(78))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(75))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(5))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            var VondPer = 0;
                            var Vond = TenDayValues(true).GetValueOrZero(TenDayAbility.Vond);
                            var EneVond = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Vond);
                            if(Vond > EneVond)
                            {
                                VondPer = (int)(Vond - EneVond);
                            }
                            if(rollper(97 + VondPer))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(4))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(30))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(25))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Optimistic:
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(11))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            var KinderOptimToElated_PersonaPer = TenDayValues(true).GetValueOrZero(TenDayAbility.PersonaDivergence);
                            if(KinderOptimToElated_PersonaPer> 776)KinderOptimToElated_PersonaPer = 776;//最低でも1%残るようにする
                            if(rollper(777 - KinderOptimToElated_PersonaPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            var SacrifaithOptimToElated_HumanKillerPer = TenDayValues(true).GetValueOrZero(TenDayAbility.HumanKiller);
                            if(rollper(-50 + SacrifaithOptimToElated_HumanKillerPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(5))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            var baledrivalOptimToElated_HumanKillerPer = TenDayValues(true).GetValueOrZero(TenDayAbility.HumanKiller);
                            if(rollper(3 + baledrivalOptimToElated_HumanKillerPer*2))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            var DevilOptimToElatedPer = TenDayValues(true).GetValueOrZero(TenDayAbility.TentVoid) - TenDayValues(true).GetValueOrZero(TenDayAbility.Enokunagi);
                            if(rollper(60 - DevilOptimToElatedPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(1))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(6))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(2))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(4))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(38)){
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        default:
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Elated:
                    //変わらない
                    ResetConditionConsecutiveTurn();
                    break;

                case HumanConditionCircumstances.Resolved:
                    var ResolvedToOptimisticPer = TenDayValues(true).GetValueOrZero(TenDayAbility.FlameBreathingWife) - ene.TenDayValues(false).GetValueOrZero(TenDayAbility.FlameBreathingWife);
                    if(ResolvedToOptimisticPer < 0)
                    {
                        ResolvedToOptimisticPer = 0;
                    }
                    ResolvedToOptimisticPer = Mathf.Sqrt(ResolvedToOptimisticPer) * 2;
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(11 + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            var ResolvedToOptimisticKinder_luck = TenDayValues(true).GetValueOrZero(TenDayAbility.Lucky);
                            if(rollper(77 + ResolvedToOptimisticKinder_luck + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            var ResolvedToOptimisticSacrifaith_UnextinguishedPath = TenDayValues(true).GetValueOrZero(TenDayAbility.UnextinguishedPath);
                            if(rollper(15 -ResolvedToOptimisticSacrifaith_UnextinguishedPath + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(10 + TenDayValues(true).GetValueOrZero(TenDayAbility.StarTersi) * 0.9f + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(40 + ResolvedToOptimisticPer + TenDayValues(true).GetValueOrZero(TenDayAbility.SpringWater)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            var ResolvedToOptimisticDevil_BalePer= TenDayValues(true).GetValueOrZero(TenDayAbility.Vail) - ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Vail);
                            var ResolvedToOptimisticDevil_FaceToHandPer= TenDayValues(true).GetValueOrZero(TenDayAbility.FaceToHand) - ene.TenDayValues(false).GetValueOrZero(TenDayAbility.FaceToHand);
                            if(rollper(40 + ResolvedToOptimisticPer + (ResolvedToOptimisticDevil_BalePer - ResolvedToOptimisticDevil_FaceToHandPer)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(12 + (TenDayValues(true).GetValueOrZero(TenDayAbility.SpringWater) - TenDayValues(true).GetValueOrZero(TenDayAbility.Taraiton)) + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(4 + ResolvedToOptimisticPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            // 変化なし
                            ResetConditionConsecutiveTurn();
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(7 + ResolvedToOptimisticPer))
                            {
                                NowCondition =HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Angry:
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(10))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            var AngryEneVail = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Vail);
                            var AngryVail = TenDayValues(true).GetValueOrZero(TenDayAbility.Vail);
                            var AngryEneWaterThunder = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.WaterThunderNerve);
                            var AngryWaterThunder = TenDayValues(true).GetValueOrZero(TenDayAbility.WaterThunderNerve);
                            var AngryToElated_KinderPer = AngryVail - AngryEneVail + (AngryWaterThunder - AngryEneWaterThunder);
                            if(rollper(50 + AngryToElated_KinderPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            if(rollper(30 - TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire)))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(TenDayValues(true).GetValueOrZero(TenDayAbility.HumanKiller)))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            const float Threshold = 37.5f;
                            var AngryToElated_BaledrivalPer = Threshold;
                            var AngryToElated_Baledrival_VailValue = TenDayValues(true).GetValueOrZero(TenDayAbility.Vail)/2;
                            if(AngryToElated_Baledrival_VailValue >Threshold)AngryToElated_BaledrivalPer = AngryToElated_Baledrival_VailValue;
                            if(rollper(AngryToElated_BaledrivalPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(40 + (20 - TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire))))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(19))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(14))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(2))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(27))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Doubtful:
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(30 + TenDayValues(true).GetValueOrZero(TenDayAbility.SpringNap)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(46))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(30))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(77))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            if(rollper(10))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(1 + TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) + TenDayValues(true).GetValueOrZero(TenDayAbility.Smiler)))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            var eneRainCoat = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Raincoat);
                            var EndWar = TenDayValues(true).GetValueOrZero(TenDayAbility.HeavenAndEndWar);
                            if(rollper(40 - (EndWar - eneRainCoat)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(44))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(80 + TenDayValues(true).GetValueOrZero(TenDayAbility.Rain)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(90 + TenDayValues(true).GetValueOrZero(TenDayAbility.ColdHeartedCalm) / 4))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) * 1.2f))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(32 + TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper((TenDayValues(true).GetValueOrZero(TenDayAbility.UnextinguishedPath)-2) / 5))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            var DoubtfulToOptimistic_CPer = 0f;
                            if(ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure) < TenDayValues(true).GetValueOrZero(TenDayAbility.NightInkKnight) * 0.3f)
                            {
                                DoubtfulToOptimistic_CPer = TenDayValues(true).GetValueOrZero(TenDayAbility.ElementFaithPower);
                            }

                            if(rollper(38 + DoubtfulToOptimistic_CPer))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(33))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper((TenDayValues(true).GetValueOrZero(TenDayAbility.HeavenAndEndWar) - 6) / 2))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(27 - TenDayValues(true).GetValueOrZero(TenDayAbility.NightInkKnight)))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(85))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(70))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            const float Threshold = 49f;
                            var DoubtfulToNorml_Doremis_nightDarknessAndVoidValue = TenDayValues(true).GetValueOrZero(TenDayAbility.NightDarkness) + TenDayValues(true).GetValueOrZero(TenDayAbility.TentVoid);
                            var DoubtfulToNorml_DoremisPer = Threshold;
                            if(DoubtfulToNorml_Doremis_nightDarknessAndVoidValue < Threshold) DoubtfulToNorml_DoremisPer = DoubtfulToNorml_Doremis_nightDarknessAndVoidValue;
                            if(rollper(TenDayValues(true).GetValueOrZero(TenDayAbility.NightDarkness) + 30))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(DoubtfulToNorml_DoremisPer))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(TenDayValues(true).GetValueOrZero(TenDayAbility.StarTersi) / 1.7f))
                            {
                                NowCondition = HumanConditionCircumstances.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Confused:
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(70))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(44))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            var ConfusedToPainful_Kindergarden_DokumamusiAndRainCoatAverage = 
                            (TenDayValues(true).GetValueOrZero(TenDayAbility.dokumamusi) + TenDayValues(true).GetValueOrZero(TenDayAbility.Raincoat)) / 2;
                            if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(80 - (ConfusedToPainful_Kindergarden_DokumamusiAndRainCoatAverage - TenDayValues(true).GetValueOrZero(TenDayAbility.ColdHeartedCalm))))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(20 + TenDayValues(true).GetValueOrZero(TenDayAbility.Raincoat) * 0.4f + TenDayValues(true).GetValueOrZero(TenDayAbility.dokumamusi) * 0.6f))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(70 + (TenDayValues(true).GetValueOrZero(TenDayAbility.Sort)-4)))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(60))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(80))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(11))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(34))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(75))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(6))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(27))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(40))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(64))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(2))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(7))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(60))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(60))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(3 - TenDayValues(true).GetValueOrZero(TenDayAbility.SpringWater)))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            if(rollper(90))
                            {
                                NowCondition = HumanConditionCircumstances.Painful;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Normal;
                            }else if(rollper(67))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Normal:
                    var y = TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure) - ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure);//余裕の差
                    switch (imp)
                    {
                        case SpiritualProperty.liminalwhitetile:
                            if(rollper(30 + y*0.8f))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(20 - y))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.kindergarden:
                            if(rollper(40 + y*2))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(70))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.sacrifaith:
                            if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pysco:
                            if(rollper(14))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(2))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.baledrival:
                            if(rollper(30 +y))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(80))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.devil:
                            if(rollper(35 + y*1.1f))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.cquiest:
                            if(rollper(30 + y*0.1f))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(20))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.godtier:
                            if(rollper(20 + y/4))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(15 + y * 0.95f))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.pillar:
                            if(rollper(12))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.doremis:
                            var NormalToElated_DoremisPer = 0f;
                            if(y > 0) NormalToElated_DoremisPer = TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) + TenDayValues(true).GetValueOrZero(TenDayAbility.Miza);
                            if(rollper(38 + y/2))
                            {
                                NowCondition = HumanConditionCircumstances.Optimistic;
                            }else if(rollper(20 + NormalToElated_DoremisPer))
                            {
                                NowCondition = HumanConditionCircumstances.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;
            }
        }
    }
    /// <summary>
    /// 相性値の高い味方が復活した際の人間状況の変化
    /// </summary>    
    public void ApplyConditionChangeOnCloseAllyAngel()
    {
        if (MyType == CharacterType.Life) // 基本的に生命のみ
        {
            switch (NowCondition)
            {
                case HumanConditionCircumstances.Painful:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.sacrifaith:
                        case SpiritualProperty.baledrival:
                            NowCondition = HumanConditionCircumstances.Elated;
                            break;
                        case SpiritualProperty.pysco:
                        case SpiritualProperty.liminalwhitetile:
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.doremis:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.devil:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Optimistic:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.godtier:
                            NowCondition = HumanConditionCircumstances.Elated;
                            break;
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.baledrival:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;    
                    }
                    break;

                case HumanConditionCircumstances.Elated:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.devil:
                        case SpiritualProperty.baledrival:
                        case SpiritualProperty.kindergarden:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        case SpiritualProperty.doremis:
                        case SpiritualProperty.cquiest:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case HumanConditionCircumstances.Resolved:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.liminalwhitetile:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        case SpiritualProperty.doremis:
                        case SpiritualProperty.baledrival:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case HumanConditionCircumstances.Angry:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.devil:
                        case SpiritualProperty.baledrival:
                        case SpiritualProperty.liminalwhitetile:
                            NowCondition = HumanConditionCircumstances.Elated;
                            break;
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.pysco:
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.pillar:
                        case SpiritualProperty.doremis:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case HumanConditionCircumstances.Doubtful:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.baledrival:
                        case SpiritualProperty.cquiest:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        default:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                    }
                    break;

                case HumanConditionCircumstances.Confused:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.liminalwhitetile:
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.sacrifaith:
                        case SpiritualProperty.baledrival:
                        case SpiritualProperty.devil:
                        case SpiritualProperty.cquiest:
                        case SpiritualProperty.godtier:
                        case SpiritualProperty.pillar:
                            NowCondition = HumanConditionCircumstances.Normal;
                            break;
                        case SpiritualProperty.doremis:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case HumanConditionCircumstances.Normal:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.pysco:
                        case SpiritualProperty.liminalwhitetile:
                        case SpiritualProperty.godtier:
                            NowCondition = HumanConditionCircumstances.Optimistic;
                            break;
                        case SpiritualProperty.kindergarden:
                        case SpiritualProperty.devil:
                            NowCondition = HumanConditionCircumstances.Elated;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;
            }
        }
    } 
    /// <summary>
    /// 死亡と復活の間は何もないも同然なので復活時の変化はなく、死亡時のみ。
    /// つまり==復活した直後にその人間状況のまま開始すること前提==で考える。
    /// </summary>
    public void ApplyConditionChangeOnDeath()
    {
        switch (NowCondition)
        {
            //------------------------------
            // 辛い (Painful)
            //------------------------------
            case HumanConditionCircumstances.Painful:
                // 普調 (一律50%)
                if (rollper(50))
                {
                    NowCondition = HumanConditionCircumstances.Normal;
                }
                else
                {
                    // 変化なし
                    ResetConditionConsecutiveTurn();
                }
                break;

            //------------------------------
            // 楽観的 (Optimistic)
            //------------------------------
            case HumanConditionCircumstances.Optimistic:
                switch (MyImpression)
                {
                    // 楽観的 → 辛い
                    case SpiritualProperty.devil:
                    case SpiritualProperty.sacrifaith:
                        NowCondition = HumanConditionCircumstances.Painful;
                        break;

                    // 楽観的 → 普調
                    case SpiritualProperty.pillar:
                    case SpiritualProperty.godtier:
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.kindergarden:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;

                    case SpiritualProperty.pysco:
                        // サイコパスは 50% 普調 / 50% 変化なし
                        if (rollper(50))
                        {
                            NowCondition = HumanConditionCircumstances.Normal;
                        }
                        else
                        {
                            // 変化なし
                            ResetConditionConsecutiveTurn();
                        }
                        break;

                    // 楽観的 → 変化なし
                    case SpiritualProperty.baledrival:
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.doremis:
                        ResetConditionConsecutiveTurn();
                        break;

                    default:
                        // 変化なし
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            //------------------------------
            // 高揚 (Elated)
            //------------------------------
            case HumanConditionCircumstances.Elated:
                switch (MyImpression)
                {
                    // 変化なし
                    case SpiritualProperty.sacrifaith:
                    case SpiritualProperty.godtier:
                    case SpiritualProperty.devil:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 普調
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.pillar:
                    case SpiritualProperty.kindergarden:
                    case SpiritualProperty.doremis:
                    case SpiritualProperty.pysco:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;

                    // 楽観的
                    case SpiritualProperty.baledrival:
                        NowCondition = HumanConditionCircumstances.Optimistic;
                        break;

                    // 辛いにはいかなそう => default で変化なし
                    default:
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            //------------------------------
            // 覚悟 (Resolved)
            //------------------------------
            case HumanConditionCircumstances.Resolved:
                switch (MyImpression)
                {
                    // 変化なし => ベール
                    case SpiritualProperty.baledrival:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 普調 => シークイエスト, ドレミス, デビル, ゴッドティア, キンダー
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.doremis:
                    case SpiritualProperty.devil:
                    case SpiritualProperty.godtier:
                    case SpiritualProperty.kindergarden:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;

                    // 辛い => 支柱, リーミナル
                    case SpiritualProperty.pillar:
                    case SpiritualProperty.liminalwhitetile:
                        NowCondition = HumanConditionCircumstances.Painful;
                        break;

                    // 疑念 => 自己犠牲, サイコ
                    case SpiritualProperty.sacrifaith:
                    case SpiritualProperty.pysco:
                        NowCondition = HumanConditionCircumstances.Doubtful;
                        break;

                    default:
                        // 変化なし
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            //------------------------------
            // 怒り (Angry)
            //------------------------------
            case HumanConditionCircumstances.Angry:
                switch (MyImpression)
                {
                    // 変化なし => リーミナル, 自己犠牲
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.sacrifaith:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 楽観的 => サイコ
                    case SpiritualProperty.pysco:
                        NowCondition = HumanConditionCircumstances.Optimistic;
                        break;

                    // 普調 => else
                    default:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;
                }
                break;

            //------------------------------
            // 疑念 (Doubtful)
            //------------------------------
            case HumanConditionCircumstances.Doubtful:
                switch (MyImpression)
                {
                    // 怒り => 自己犠牲, ベール, デビル
                    case SpiritualProperty.sacrifaith:
                    case SpiritualProperty.baledrival:
                    case SpiritualProperty.devil:
                        NowCondition = HumanConditionCircumstances.Angry;
                        break;

                    // 普調 => サイコ, リーミナル, 支柱
                    case SpiritualProperty.pysco:
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.pillar:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;

                    // 楽観的 => ドレミス, シークイエスト, キンダー, ゴッドティア
                    case SpiritualProperty.doremis:
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.kindergarden:
                    case SpiritualProperty.godtier:
                        NowCondition = HumanConditionCircumstances.Optimistic;
                        break;

                    // 辛いにはいかない => default => 変化なし
                    default:
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            //------------------------------
            // 混乱 (Confused)
            //------------------------------
            case HumanConditionCircumstances.Confused:
                switch (MyImpression)
                {
                    // 変化なし => キンダー
                    case SpiritualProperty.kindergarden:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 高揚 => ベール, リーミナル
                    case SpiritualProperty.baledrival:
                    case SpiritualProperty.liminalwhitetile:
                        NowCondition = HumanConditionCircumstances.Elated;
                        break;

                    // 普調 => else
                    default:
                        NowCondition = HumanConditionCircumstances.Normal;
                        break;
                }
                break;

            //------------------------------
            // 普調 (Normal)
            //------------------------------
            case HumanConditionCircumstances.Normal:
                switch (MyImpression)
                {
                    // 変化なし => 支柱, サイコ
                    case SpiritualProperty.pillar:
                    case SpiritualProperty.pysco:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 楽観的 => ゴッドティア
                    case SpiritualProperty.godtier:
                        NowCondition = HumanConditionCircumstances.Optimistic;
                        break;

                    // 疑念 => リーミナル, キンダー, デビル
                    case SpiritualProperty.liminalwhitetile:
                    case SpiritualProperty.kindergarden:
                    case SpiritualProperty.devil:
                        NowCondition = HumanConditionCircumstances.Doubtful;
                        break;

                    // 怒り => 自己犠牲, シークイエスト, ベール
                    case SpiritualProperty.sacrifaith:
                    case SpiritualProperty.cquiest:
                    case SpiritualProperty.baledrival:
                        NowCondition = HumanConditionCircumstances.Angry;
                        break;

                    default:
                        // 変化なし
                        ResetConditionConsecutiveTurn();
                        break;
                }
                break;

            default:
                // それ以外(例えば none) => 変化なし
                ResetConditionConsecutiveTurn();
                break;
        }
    }

    //  ==============================================================================================================================
    //                                              死亡
    //  ==============================================================================================================================

    /// <summary>
    ///     死を判定するオーバライド可能な関数
    /// </summary>
    /// <returns></returns>
    public virtual bool Death()
    {
        if (HP <= 0) 
        {
            if(!hasDied)
            {
            hasDied =true;
            DeathCallBack();
            }
            return true;
        }
        return false;
    }
    /// <summary>
    /// 死んだ瞬間を判断するためのフラグ
    /// </summary>
    bool hasDied =false;
    /* ---------------------------------
     * broken
     * --------------------------------- 
     */
    /// <summary>
    /// 完全死滅してるかどうか。
    /// </summary>
    [NonSerialized]
    public bool broken = false;
    [SerializeField]
    float _machineBrokenRate = 0.3f;//インスペクタで設定する際の初期デフォルト値
    const float _lifeBrokenRate = 0.1f;//生命の壊れる確率は共通の定数
    /// <summary>
    /// OverKillが発生した場合、壊れる確率
    /// </summary>
    float OverKillBrokenRate
    {
        get
        {
            if(MyType == CharacterType.Machine)
            {
                return _machineBrokenRate;
            }
            if(MyType == CharacterType.Life)
            {
                return _lifeBrokenRate;
            }
            // そのほかのタイプに対応していない場合は例外をスロー
            throw new NotImplementedException(
            $"OverKillBrokenRate is not implemented for CharacterType: {MyType}"
        );
        }
    }


    //  ==============================================================================================================================
    //                                              その他
    //  ==============================================================================================================================
    
    /// <summary>
    /// 「標準のロジック」の割り込みカウンターが発動するかのオプション
    /// AllyClassはUIから、Enemyは継承してシリアライズで設定する。
    /// とりあえずtrueで設定
    /// </summary>
    public virtual bool IsInterruptCounterActive => true;




}
/// <summary>
/// パワー、元気、気力値　歩行やその他イベントなどで短期的に上げ下げし、
/// 狙い流れ等の防ぎ方切り替え処理などで、さらに上下する値として導入されたりする。
/// ThePowerExtensionsで日本語に変更可能
/// </summary>
public enum ThePower
{
        /// <summary>たるい</summary>
    lowlow,
        /// <summary>低い</summary>
    low,
    /// <summary>普通</summary>
    medium,
    /// <summary>高い</summary>
    high
}
/// <summary>
/// 人間状況　全員持つけど例えばLife以外なんかは固定されてたりしたりする。
/// </summary>
public enum HumanConditionCircumstances
{
    /// <summary>
    /// 辛い状態を表します。
    /// </summary>
    Painful,
    /// <summary>
    /// 楽観的な状態を表します。
    /// </summary>
    Optimistic,
    /// <summary>
    /// 高揚した状態を表します。
    /// </summary>
    Elated,
    /// <summary>
    /// 覚悟を決めた状態を表します。
    /// </summary>
    Resolved,
    /// <summary>
    /// 怒りの状態を表します。
    /// </summary>
    Angry,
    /// <summary>
    /// 状況への疑念を抱いている状態を表します。
    /// </summary>
    Doubtful,
    /// <summary>
    /// 混乱した状態を表します。
    /// </summary>
    Confused,
    /// <summary>
    /// 普段の状態を表します。
    /// </summary>
    Normal
    }
/// <summary>
///     キャラクター達の種別
/// </summary>
[Flags]
public enum CharacterType
{
    TLOA = 1 << 0,
    Machine = 1 << 1,
    Life = 1 << 2 //TLOAそのもの、機械、生命
}
/// <summary>
///     精神属性、スキル、キャラクターに依存し、キャラクターは直前に使った物が適用される
///     だから精神属性同士で攻撃の通りは設定される。
/// </summary>
[Flags]
public enum SpiritualProperty
{
    doremis = 1 << 0,   // ビットパターン: 0000 0001  (1)
    pillar = 1 << 1,   // ビットパターン: 0000 0010  (2)
    kindergarden = 1 << 2,   // ビットパターン: 0000 0100  (4)
    liminalwhitetile = 1 << 3,   // ビットパターン: 0000 1000  (8)
    sacrifaith = 1 << 4,   // ビットパターン: 0001 0000  (16)
    cquiest = 1 << 5,   // ビットパターン: 0010 0000  (32)
    pysco = 1 << 6,   // ビットパターン: 0100 0000  (64)
    godtier = 1 << 7,   // ビットパターン: 1000 0000  (128)
    baledrival = 1 << 8,   // ビットパターン: 0001 0000 0000  (256)
    devil = 1 << 9,    // ビットパターン: 0010 0000 0000  (512)
    none = 1 << 10,    // ビットパターン: 0100 0000 0000  (1024)
    mvoid = 1 << 11,
    Galvanize = 1 << 12,
    air = 1 << 13,
    memento = 1 << 14
}


