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
    
    [Space]
    [Header("精神属性")]
    [Tooltip("このキャラクターの現在の精神属性（ビットフラグ）")]
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
    [Tooltip("デフォルト精神属性。一定歩数の歩行で MyImpression がこの属性に戻る")]
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
    [Space]
    [Header("種別")]
    [Tooltip("キャラクターの種別（ビットフラグ）")]
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
    int CalmDownCountMaxRnd => RandomSource.NextInt(4, 8);
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
        _skillEvasionModifier = _BaseEvasionModifier;//補正率を無しに。
        _skillAttackModifier = _BaseAttackModifier;

    }
    //  ==============================================================================================================================
    //                                              パワー
    //  ==============================================================================================================================


    [Space]
    [Header("パワー")]
    [Tooltip("キャラクターの現在のパワー段階（初期値: medium）")]
    public PowerLevel NowPower = PowerLevel.Medium;//初期値は中

    /// <summary>
    /// NowPowerが一段階上がる。
    /// </summary>
    void Power1Up()
    {
        NowPower = NowPower switch
            {
                PowerLevel.VeryLow => PowerLevel.Low,
                PowerLevel.Low => PowerLevel.Medium,
                PowerLevel.Medium => PowerLevel.High,
                PowerLevel.High => PowerLevel.High, // 既に最高値の場合は変更なし
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
                PowerLevel.High => PowerLevel.Medium,
                PowerLevel.Medium => PowerLevel.Low,
                PowerLevel.Low => PowerLevel.VeryLow,
                PowerLevel.VeryLow => PowerLevel.VeryLow, // 既に最低値の場合は変更なし
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
            SpiritualProperty.Kindergarten => 40f,
            SpiritualProperty.LiminalWhiteTile => 30f,
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
            case SpiritualProperty.Doremis:
                switch(NowPower)
                {
                    case PowerLevel.High:
                        if(rollper(35))
                        {
                            NowPower = PowerLevel.Medium;
                        }
                        break;
                    case PowerLevel.Medium:
                        if(rollper(25))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        if(rollper(6))
                        {
                            NowPower = PowerLevel.High;
                        }
                        break;
                    case PowerLevel.Low:
                        if(rollper(6))
                        {
                            NowPower = PowerLevel.VeryLow;
                        }
                        if(rollper(2.7f))
                        {
                            NowPower = PowerLevel.High;
                        }
                        break;
                    case PowerLevel.VeryLow:
                        if(rollper(7.55f))
                        {
                            NowPower = PowerLevel.Medium;
                        }
                        break;
                }
                break;
            case SpiritualProperty.Pillar:
                switch(NowPower)
                {
                    case PowerLevel.High:
                        if(rollper(2.23f))
                        {
                            NowPower = PowerLevel.Medium;
                        }
                        break;
                    case PowerLevel.Medium:
                        if(rollper(5))
                        {
                            NowPower = PowerLevel.High;
                        }
                        if(rollper(20))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        break;
                    case PowerLevel.Low:
                        if(rollper(6.09f))
                        {
                            NowPower = PowerLevel.Medium;
                        }
                        if(rollper(15))
                        {
                            NowPower = PowerLevel.VeryLow;
                        }
                        break;
                    case PowerLevel.VeryLow:
                        if(rollper(8))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        break;
                }

                break;
            case SpiritualProperty.Kindergarten:
                switch(NowPower)
                {
                    case PowerLevel.High:
                        if(rollper(25))
                        {
                            NowPower = PowerLevel.Medium;
                        }
                        break;
                    case PowerLevel.Medium:
                        if(rollper(31))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        if(rollper(28))
                        {
                            NowPower = PowerLevel.High;
                        }
                        break;
                    case PowerLevel.Low:
                        if(rollper(25))
                        {
                            NowPower = PowerLevel.Medium;
                        }

                        if(rollper(20))
                        {
                            NowPower = PowerLevel.VeryLow;
                        }
                        break;
                    case PowerLevel.VeryLow:
                        if(rollper(30))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.LiminalWhiteTile:
                switch(NowPower)
                {
                    case PowerLevel.High:
                        if(rollper(17))
                        {
                            NowPower = PowerLevel.Medium;
                        }
                        break;
                    case PowerLevel.Medium:
                        if(rollper(3))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        if(rollper(3.1f))
                        {
                            NowPower = PowerLevel.High;
                        }
                        break;
                    case PowerLevel.Low:
                        if(rollper(13))
                        {
                            NowPower = PowerLevel.Medium;
                        }

                        if(rollper(2))
                        {
                            NowPower = PowerLevel.VeryLow;
                        }
                        break;
                    case PowerLevel.VeryLow:
                        if(rollper(40))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.Sacrifaith:
                switch(NowPower)
                {
                    case PowerLevel.High:
                        //不変
                    case PowerLevel.Medium:
                        if(rollper(14))
                        {
                            NowPower = PowerLevel.High;
                        }
                        break;
                    case PowerLevel.Low:
                        if(rollper(20))
                        {
                            NowPower = PowerLevel.Medium;
                        }
                        break;
                    case PowerLevel.VeryLow:
                        if(rollper(26))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.Cquiest:
                switch(NowPower)
                {
                    case PowerLevel.High:
                        if(rollper(14))
                        {
                            NowPower = PowerLevel.Medium;
                        }
                        break;
                    case PowerLevel.Medium:
                        if(rollper(3))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        if(rollper(3.1f))
                        {
                            NowPower = PowerLevel.High;
                        }
                        break;
                    case PowerLevel.Low:
                        if(rollper(13))
                        {
                            NowPower = PowerLevel.Medium;
                        }

                        break;
                    case PowerLevel.VeryLow:
                        if(rollper(4.3f))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.Psycho:
                switch(NowPower)
                {
                    case PowerLevel.High:
                        if(rollper(77.77f))
                        {
                            NowPower = PowerLevel.Medium;
                        }
                        break;
                    case PowerLevel.Medium:
                        if(rollper(6.7f))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        if(rollper(3))
                        {
                            NowPower = PowerLevel.High;
                        }
                        break;
                    case PowerLevel.Low:
                        if(rollper(90))
                        {
                            NowPower = PowerLevel.Medium;
                        }

                        if(rollper(10))
                        {
                            NowPower = PowerLevel.VeryLow;
                        }
                        break;
                    case PowerLevel.VeryLow:
                        if(rollper(80))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.GodTier:
                switch(NowPower)
                {
                    case PowerLevel.High:
                        if(rollper(4.26f))
                        {
                            NowPower = PowerLevel.Medium;
                        }
                        break;
                    case PowerLevel.Medium:
                        if(rollper(3))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        if(rollper(30))
                        {
                            NowPower = PowerLevel.High;
                        }
                        break;
                    case PowerLevel.Low:
                        if(rollper(28))
                        {
                            NowPower = PowerLevel.Medium;
                        }

                        if(rollper(8))
                        {
                            NowPower = PowerLevel.VeryLow;
                        }
                        break;
                    case PowerLevel.VeryLow:
                        if(rollper(100))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.BaleDrival:
                switch(NowPower)
                {
                    case PowerLevel.High:
                        if(rollper(9))
                        {
                            NowPower = PowerLevel.Medium;
                        }
                        break;
                    case PowerLevel.Medium:
                        if(rollper(25))
                        {
                            NowPower = PowerLevel.High;
                        }
                        if(rollper(11))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        break;
                    case PowerLevel.Low:
                        if(rollper(26.5f))
                        {
                            NowPower = PowerLevel.Medium;
                        }

                        if(rollper(8))
                        {
                            NowPower = PowerLevel.VeryLow;
                        }
                        break;
                    case PowerLevel.VeryLow:
                        if(rollper(50))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        break;
                }
                break;
            case SpiritualProperty.Devil:
                switch(NowPower)
                {
                    case PowerLevel.High:
                        if(rollper(5))
                        {
                            NowPower = PowerLevel.Medium;
                        }
                        break;
                    case PowerLevel.Medium:
                        if(rollper(6))
                        {
                            NowPower = PowerLevel.Low;
                        }
                        if(rollper(4.1f))
                        {
                            NowPower = PowerLevel.High;
                        }
                        break;
                    case PowerLevel.Low:
                        if(rollper(15))
                        {
                            NowPower = PowerLevel.Medium;
                        }

                        if(rollper(7))
                        {
                            NowPower = PowerLevel.VeryLow;
                        }
                        break;
                    case PowerLevel.VeryLow:
                        if(rollper(22))
                        {
                            NowPower = PowerLevel.Low;
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
    Demeanor NowCondition;
    /// <summary>
    /// 前回の人間状況　同じのが続いてるかの判断要
    /// </summary>
    Demeanor PreviousCondition;
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

        // パワー(NowPower)は PowerLevel 型 (VeryLow, low, medium, high など)
        // MyImpression は精神属性

        // 初期値はとりあえず普調にしておいて、後で条件を満たせば上書きする
        NowCondition = Demeanor.Normal;

        switch (MyImpression)
        {
            //--------------------------------
            // 1) ベール (BaleDrival)
            //--------------------------------
            case SpiritualProperty.BaleDrival:
                // 「高揚」：パワーが高 && 2倍負け( ratio <= 0.5 )
                if (NowPower == PowerLevel.High && ratio <= 0.5f)
                {
                    NowCondition = Demeanor.Elated;
                }
                else
                {
                    // それ以外は「楽観的」
                    NowCondition = Demeanor.Optimistic;
                }
                break;

            //--------------------------------
            // 2) デビル (Devil)
            //--------------------------------
            case SpiritualProperty.Devil:
                // 「高揚」：1.8倍勝ち ( ratio >= 1.8 )
                if (ratio >= 1.8f)
                {
                    NowCondition = Demeanor.Elated;
                }
                else
                {
                    // それ以外 => 「普調」 (疑念にはならない)
                    NowCondition = Demeanor.Normal;
                }
                break;

            //--------------------------------
            // 3) 自己犠牲 (Sacrifaith)
            //--------------------------------
            case SpiritualProperty.Sacrifaith:
                // 覚悟：パワーが low より上(=low以上) かつ 2倍負け( ratio <= 0.5 )
                //   ※「パワーがlow“以上”」= (low, medium, highのいずれか)
                if (NowPower >= PowerLevel.Low && ratio <= 0.5f)
                {
                    NowCondition = Demeanor.Resolved;
                }
                // 疑念：パワーがVeryLow && 1.6倍負け( ratio <= 1/1.6≒0.625 )
                else if (NowPower == PowerLevel.VeryLow && ratio <= 0.625f)
                {
                    NowCondition = Demeanor.Doubtful;
                }
                else
                {
                    // それ以外 => 普調
                    NowCondition = Demeanor.Normal;
                }
                break;

            //--------------------------------
            // 4) ゴッドティア (GodTier)
            //--------------------------------
            case SpiritualProperty.GodTier:
                // 「楽観的」: 総量2.5倍勝ち( ratio >= 2.5 )
                if (ratio >= 2.5f)
                {
                    NowCondition = Demeanor.Optimistic;
                }
                // 「覚悟」 : パワーがmedium以上 && 2倍負け( ratio <= 0.5 )
                else if (NowPower >= PowerLevel.Medium && ratio <= 0.5f)
                {
                    NowCondition = Demeanor.Resolved;
                }
                else
                {
                    // それ以外 => 普調
                    NowCondition = Demeanor.Normal;
                }
                break;

            //--------------------------------
            // 5) リーミナルホワイトタイル (LiminalWhiteTile)
            //--------------------------------
            case SpiritualProperty.LiminalWhiteTile:
                // 「楽観的」: 総量2倍勝ち( ratio >= 2.0 )
                if (ratio >= 2.0f)
                {
                    NowCondition = Demeanor.Optimistic;
                }
                // 「疑念」 : 2倍負け( ratio <= 0.5 )
                else if (ratio <= 0.5f)
                {
                    NowCondition = Demeanor.Doubtful;
                }
                else
                {
                    NowCondition = Demeanor.Normal;
                }
                break;

            //--------------------------------
            // 6) キンダーガーデン (Kindergarten)
            //--------------------------------
            case SpiritualProperty.Kindergarten:
                // 「楽観的」: 1.7倍勝ち
                if (ratio >= 1.7f)
                {
                    NowCondition = Demeanor.Optimistic;
                }
                // 「疑念」 : 1.5倍負け ( ratio <= 2/3 = 0.6667 )
                else if (ratio <= 0.6667f)
                {
                    NowCondition = Demeanor.Doubtful;
                }
                else
                {
                    NowCondition = Demeanor.Normal;
                }
                break;

            //--------------------------------
            // 7) 支柱 (Pillar) 
            //    戦闘開始時は「普調」だけ
            //--------------------------------
            case SpiritualProperty.Pillar:
                NowCondition = Demeanor.Normal;
                break;

            //--------------------------------
            // 8) サイコパス (Psycho)
            //    戦闘開始時は常に落ち着く => 普調
            //--------------------------------
            case SpiritualProperty.Psycho:
                NowCondition = Demeanor.Normal;
                break;

            //--------------------------------
            // 9) ドレミス, シークイエスト, etc. 
            //    仕様外 or 未指定なら一旦「普調」にする
            //--------------------------------
            default:
                NowCondition = Demeanor.Normal;
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
            case Demeanor.Resolved:
                // 覚悟 → 高揚 (想定17)
                if (ConditionConsecutiveTurn >= 17)
                {
                    NowCondition = Demeanor.Elated;
                    changed = true;
                }
                break;

            case Demeanor.Angry:
                // 怒り → 普調 (想定10)
                if (ConditionConsecutiveTurn >= 10)
                {
                    NowCondition = Demeanor.Normal;
                    changed = true;
                }
                // 怒り → 高揚 (累積23)
                else if (TotalTurnsInSameCondition >= 23)
                {
                    NowCondition = Demeanor.Elated;
                    changed = true;
                }
                break;

            case Demeanor.Doubtful:
                // 疑念 → 楽観的 (想定11)
                if (ConditionConsecutiveTurn >= 11)
                {
                    NowCondition = Demeanor.Optimistic;
                    changed = true;
                }
                // 疑念 → 混乱 (累積19)
                else if (TotalTurnsInSameCondition >= 19)
                {
                    NowCondition = Demeanor.Confused;
                    changed = true;
                }
                break;

            case Demeanor.Confused:
                // 混乱 → 普調 (想定11)
                if (ConditionConsecutiveTurn >= 11)
                {
                    NowCondition = Demeanor.Normal;
                    changed = true;
                }
                // 混乱 → 高揚 (累積22)
                else if (TotalTurnsInSameCondition >= 22)
                {
                    NowCondition = Demeanor.Elated;
                    changed = true;
                }
                break;

            case Demeanor.Elated:
                // 高揚 → 普調 (想定13)
                if (ConditionConsecutiveTurn >= 13)
                {
                    NowCondition = Demeanor.Normal;
                    changed = true;
                }
                break;

            case Demeanor.Painful:
                // 辛い → 普調 (想定14)
                if (ConditionConsecutiveTurn >= 14)
                {
                    NowCondition = Demeanor.Normal;
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
                case Demeanor.Painful://辛い
                    NowCondition = Demeanor.Confused;//辛いと誰でも混乱する
                    break;
                case Demeanor.Optimistic://楽観的
                    switch(MyImpression)
                    {
                        case SpiritualProperty.Kindergarten:
                            if(rollper(36))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                NowCondition = Demeanor.Painful;
                            }
                        break;
                        case SpiritualProperty.Psycho:
                            if(deathCount > 1)
                            {//二人なら危機感を感じて普調になる
                                NowCondition = Demeanor.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.BaleDrival:
                            if(deathCount > 1)
                            {//二人なら怒り
                                NowCondition = Demeanor.Angry;
                            }
                            else
                            {
                                //そうでないなら変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.Devil:
                        case SpiritualProperty.Sacrifaith:
                        case SpiritualProperty.Cquiest:
                        case SpiritualProperty.GodTier:
                        case SpiritualProperty.Pillar:
                        case SpiritualProperty.Doremis:
                            NowCondition = Demeanor.Angry;
                        break;
                        case SpiritualProperty.LiminalWhiteTile:
                            if(deathCount>1 && rollper(10))
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }else
                            {
                                NowCondition = Demeanor.Painful;
                            }
                        break;
                    }
                    break;
                case Demeanor.Elated:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.LiminalWhiteTile:
                            if(deathCount>1)
                            {//二人なら混乱
                                NowCondition = Demeanor.Confused;
                            }else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.Kindergarten:
                            NowCondition = Demeanor.Confused;
                        break;
                        case SpiritualProperty.Doremis:
                            if(deathCount == 1)
                            {//一人なら混乱する
                                NowCondition = Demeanor.Confused;
                            }else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.Sacrifaith:
                        case SpiritualProperty.GodTier:
                        case SpiritualProperty.Devil:
                            NowCondition = Demeanor.Normal;
                        break;
                        case SpiritualProperty.BaleDrival:
                            if(deathCount == 1)
                            {
                                NowCondition = Demeanor.Normal;
                            }
                            else
                            {
                                //変化なし　
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.Pillar:
                            if(deathCount > 1)
                            {
                                NowCondition = Demeanor.Normal;
                            }
                            else
                            {
                                //変化なし　
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        //シークイエストとサイコパスは変化なし
                        case SpiritualProperty.Cquiest:
                        case SpiritualProperty.Psycho:
                            ResetConditionConsecutiveTurn();//変化なし
                            break;
                    }
                    break;
                case Demeanor.Resolved:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.Devil:
                            if(deathCount>1)
                            {
                                NowCondition = Demeanor.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.Kindergarten:
                            if(rollper(44))
                            {
                                NowCondition =Demeanor.Optimistic;
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
                case Demeanor.Angry:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.Devil:
                            if(rollper(66.66f))
                            {
                                NowCondition = Demeanor.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.Kindergarten:
                            if(rollper(28))
                            {
                                NowCondition = Demeanor.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.Sacrifaith:
                            NowCondition = Demeanor.Resolved;
                        break;
                        //後は全て変化なし
                        default:
                            ResetConditionConsecutiveTurn();
                            break;
                    }
                    break;

                case Demeanor.Doubtful:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.Sacrifaith:
                            if(deathCount == 1)
                            {
                                NowCondition = Demeanor.Angry;
                            }
                            else if(deathCount > 1)
                            {
                                NowCondition = Demeanor.Resolved;
                            }
                        break;
                        case SpiritualProperty.LiminalWhiteTile:
                            if(deathCount > 1 && rollper(40))
                            {
                                NowCondition = Demeanor.Angry;
                            }
                            else
                            {
                                NowCondition = Demeanor.Painful;
                            }
                        break; 
                        case SpiritualProperty.Devil:  
                            if(deathCount > 1 && rollper(21.666f))
                            {
                                NowCondition = Demeanor.Angry;
                            }
                            else
                            {
                                NowCondition = Demeanor.Painful;
                            }
                        break;
                        case SpiritualProperty.Kindergarten:
                            if(rollper(20))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                NowCondition = Demeanor.Painful;
                            }
                        break;
                        case SpiritualProperty.BaleDrival:
                            if(deathCount > 1)
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                NowCondition = Demeanor.Painful;
                            }
                        break;
                        case SpiritualProperty.GodTier:
                            NowCondition = Demeanor.Resolved;
                        break;
                        case SpiritualProperty.Doremis:
                            ResetConditionConsecutiveTurn();//変化なし
                        break;
                        case SpiritualProperty.Psycho:
                            switch(RandomSource.NextInt(5))
                            {
                                case 0:
                                NowCondition = Demeanor.Optimistic;
                                break;
                                case 1:
                                NowCondition = Demeanor.Resolved;
                                break;
                                case 2:
                                //変化なし
                                ResetConditionConsecutiveTurn();
                                break;
                                case 3:
                                NowCondition = Demeanor.Doubtful;
                                break;
                                case 4:
                                NowCondition = Demeanor.Angry;
                                break;
                            }
                        break;
                        case SpiritualProperty.Pillar:
                        case SpiritualProperty.Cquiest:
                            NowCondition = Demeanor.Normal;
                        break;
                    }
                    break;
                case Demeanor.Confused:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.Cquiest:
                        case SpiritualProperty.Pillar:
                            NowCondition = Demeanor.Resolved;
                        break;   
                        case SpiritualProperty.Devil:
                            if(deathCount > 1)
                            {
                                NowCondition = Demeanor.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.GodTier:
                            if(deathCount == 1)
                            {
                                NowCondition = Demeanor.Resolved;
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
                case Demeanor.Normal:
                    switch(MyImpression)
                    {
                        case SpiritualProperty.Sacrifaith:
                            if(deathCount == 1)
                            {
                                NowCondition = Demeanor.Angry;
                            }
                            else if(deathCount > 1)
                            {
                                NowCondition = Demeanor.Resolved;
                            }
                        break;
                        case SpiritualProperty.LiminalWhiteTile:
                            if(deathCount > 1 && rollper(40))
                            {
                                NowCondition = Demeanor.Angry;
                            }
                            else
                            {
                                NowCondition = Demeanor.Painful;
                            }
                        break; 
                        case SpiritualProperty.Devil:  
                            if(deathCount > 1 && rollper(21.666f))
                            {
                                NowCondition = Demeanor.Angry;
                            }
                            else
                            {
                                NowCondition = Demeanor.Painful;
                            }
                        break;
                        case SpiritualProperty.Kindergarten:
                            if(rollper(20))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                NowCondition = Demeanor.Painful;
                            }
                        break;
                        case SpiritualProperty.BaleDrival:
                            if(deathCount > 1)
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                NowCondition = Demeanor.Painful;
                            }
                        break;
                        case SpiritualProperty.GodTier:
                            NowCondition = Demeanor.Resolved;
                        break;
                        case SpiritualProperty.Doremis:
                            if(deathCount > 1)
                            {
                                NowCondition = Demeanor.Painful;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                        break;
                        case SpiritualProperty.Psycho:
                            switch(RandomSource.NextInt(5))
                            {
                                case 0:
                                NowCondition = Demeanor.Optimistic;
                                break;
                                case 1:
                                NowCondition = Demeanor.Resolved;
                                break;
                                case 2:
                                //変化なし
                                ResetConditionConsecutiveTurn();
                                break;
                                case 3:
                                NowCondition = Demeanor.Doubtful;
                                break;
                                case 4:
                                NowCondition = Demeanor.Angry;
                                break;
                            }
                        break;
                        case SpiritualProperty.Cquiest:
                        case SpiritualProperty.Pillar:
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
                case Demeanor.Painful:
                    switch (imp)
                    {
                        case SpiritualProperty.LiminalWhiteTile:
                            if(rollper(66))
                            {
                                NowCondition = Demeanor.Normal;
                            }
                            else if(rollper(10))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else if(rollper(10))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Kindergarten:
                            if(rollper(33))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(57))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(33))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Sacrifaith:
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
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(OptimisticPer))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(30))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Psycho:
                            var NormalPer = 0;
                            var EneLeisure = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure);
                            var Leisure = TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure);
                            if(Leisure > EneLeisure)
                            {
                                NormalPer = (int)(Leisure - EneLeisure);
                            }
                            if(rollper(90 + NormalPer))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(15))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(9))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.BaleDrival:
                            if(rollper(40))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(30))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Devil:
                            if(rollper(35))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(50))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(7))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Cquiest:
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
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(20))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(22))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.GodTier:
                            if(rollper(78))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(75))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(5))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Pillar:
                            var VondPer = 0;
                            var Vond = TenDayValues(true).GetValueOrZero(TenDayAbility.Vond);
                            var EneVond = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Vond);
                            if(Vond > EneVond)
                            {
                                VondPer = (int)(Vond - EneVond);
                            }
                            if(rollper(97 + VondPer))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(4))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Doremis:
                            if(rollper(40))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(30))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(25))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case Demeanor.Optimistic:
                    switch (imp)
                    {
                        case SpiritualProperty.LiminalWhiteTile:
                            if(rollper(11))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Kindergarten:
                            var KinderOptimToElated_PersonaPer = TenDayValues(true).GetValueOrZero(TenDayAbility.PersonaDivergence);
                            if(KinderOptimToElated_PersonaPer> 776)KinderOptimToElated_PersonaPer = 776;//最低でも1%残るようにする
                            if(rollper(777 - KinderOptimToElated_PersonaPer))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Sacrifaith:
                            var SacrifaithOptimToElated_HumanKillerPer = TenDayValues(true).GetValueOrZero(TenDayAbility.HumanKiller);
                            if(rollper(-50 + SacrifaithOptimToElated_HumanKillerPer))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Psycho:
                            if(rollper(5))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.BaleDrival:
                            var baledrivalOptimToElated_HumanKillerPer = TenDayValues(true).GetValueOrZero(TenDayAbility.HumanKiller);
                            if(rollper(3 + baledrivalOptimToElated_HumanKillerPer*2))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Devil:
                            var DevilOptimToElatedPer = TenDayValues(true).GetValueOrZero(TenDayAbility.TentVoid) - TenDayValues(true).GetValueOrZero(TenDayAbility.Enokunagi);
                            if(rollper(60 - DevilOptimToElatedPer))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Cquiest:
                            if(rollper(1))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.GodTier:
                            if(rollper(6))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Pillar:
                            if(rollper(2))
                            {
                                NowCondition = Demeanor.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Doremis:
                            if(rollper(4))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(38)){
                                NowCondition = Demeanor.Elated;
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

                case Demeanor.Elated:
                    //変わらない
                    ResetConditionConsecutiveTurn();
                    break;

                case Demeanor.Resolved:
                    var ResolvedToOptimisticPer = TenDayValues(true).GetValueOrZero(TenDayAbility.FlameBreathingWife) - ene.TenDayValues(false).GetValueOrZero(TenDayAbility.FlameBreathingWife);
                    if(ResolvedToOptimisticPer < 0)
                    {
                        ResolvedToOptimisticPer = 0;
                    }
                    ResolvedToOptimisticPer = Mathf.Sqrt(ResolvedToOptimisticPer) * 2;
                    switch (imp)
                    {
                        case SpiritualProperty.LiminalWhiteTile:
                            if(rollper(11 + ResolvedToOptimisticPer))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Kindergarten:
                            var ResolvedToOptimisticKinder_luck = TenDayValues(true).GetValueOrZero(TenDayAbility.Lucky);
                            if(rollper(77 + ResolvedToOptimisticKinder_luck + ResolvedToOptimisticPer))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Sacrifaith:
                            var ResolvedToOptimisticSacrifaith_UnextinguishedPath = TenDayValues(true).GetValueOrZero(TenDayAbility.UnextinguishedPath);
                            if(rollper(15 -ResolvedToOptimisticSacrifaith_UnextinguishedPath + ResolvedToOptimisticPer))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Psycho:
                            if(rollper(10 + TenDayValues(true).GetValueOrZero(TenDayAbility.StarTersi) * 0.9f + ResolvedToOptimisticPer))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.BaleDrival:
                            if(rollper(40 + ResolvedToOptimisticPer + TenDayValues(true).GetValueOrZero(TenDayAbility.SpringWater)))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Devil:
                            var ResolvedToOptimisticDevil_BalePer= TenDayValues(true).GetValueOrZero(TenDayAbility.Vail) - ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Vail);
                            var ResolvedToOptimisticDevil_FaceToHandPer= TenDayValues(true).GetValueOrZero(TenDayAbility.FaceToHand) - ene.TenDayValues(false).GetValueOrZero(TenDayAbility.FaceToHand);
                            if(rollper(40 + ResolvedToOptimisticPer + (ResolvedToOptimisticDevil_BalePer - ResolvedToOptimisticDevil_FaceToHandPer)))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Cquiest:
                            if(rollper(12 + (TenDayValues(true).GetValueOrZero(TenDayAbility.SpringWater) - TenDayValues(true).GetValueOrZero(TenDayAbility.Taraiton)) + ResolvedToOptimisticPer))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.GodTier:
                            if(rollper(4 + ResolvedToOptimisticPer))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Pillar:
                            // 変化なし
                            ResetConditionConsecutiveTurn();
                            break;
                        case SpiritualProperty.Doremis:
                            if(rollper(7 + ResolvedToOptimisticPer))
                            {
                                NowCondition =Demeanor.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case Demeanor.Angry:
                    switch (imp)
                    {
                        case SpiritualProperty.LiminalWhiteTile:
                            if(rollper(10))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Kindergarten:
                            var AngryEneVail = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Vail);
                            var AngryVail = TenDayValues(true).GetValueOrZero(TenDayAbility.Vail);
                            var AngryEneWaterThunder = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.WaterThunderNerve);
                            var AngryWaterThunder = TenDayValues(true).GetValueOrZero(TenDayAbility.WaterThunderNerve);
                            var AngryToElated_KinderPer = AngryVail - AngryEneVail + (AngryWaterThunder - AngryEneWaterThunder);
                            if(rollper(50 + AngryToElated_KinderPer))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Sacrifaith:
                            if(rollper(30 - TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire)))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Psycho:
                            if(rollper(TenDayValues(true).GetValueOrZero(TenDayAbility.HumanKiller)))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.BaleDrival:
                            const float Threshold = 37.5f;
                            var AngryToElated_BaledrivalPer = Threshold;
                            var AngryToElated_Baledrival_VailValue = TenDayValues(true).GetValueOrZero(TenDayAbility.Vail)/2;
                            if(AngryToElated_Baledrival_VailValue >Threshold)AngryToElated_BaledrivalPer = AngryToElated_Baledrival_VailValue;
                            if(rollper(AngryToElated_BaledrivalPer))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Devil:
                            if(rollper(40 + (20 - TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire))))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Cquiest:
                            if(rollper(19))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.GodTier:
                            if(rollper(14))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Pillar:
                            if(rollper(2))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Doremis:
                            if(rollper(27))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case Demeanor.Doubtful:
                    switch (imp)
                    {
                        case SpiritualProperty.LiminalWhiteTile:
                            if(rollper(30 + TenDayValues(true).GetValueOrZero(TenDayAbility.SpringNap)))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(46))
                            {
                                NowCondition = Demeanor.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Kindergarten:
                            if(rollper(30))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(77))
                            {
                                NowCondition = Demeanor.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Sacrifaith:
                            if(rollper(10))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(1 + TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) + TenDayValues(true).GetValueOrZero(TenDayAbility.Smiler)))
                            {
                                NowCondition = Demeanor.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Psycho:
                            var eneRainCoat = ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Raincoat);
                            var EndWar = TenDayValues(true).GetValueOrZero(TenDayAbility.HeavenAndEndWar);
                            if(rollper(40 - (EndWar - eneRainCoat)))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(44))
                            {
                                NowCondition = Demeanor.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.BaleDrival:
                            if(rollper(80 + TenDayValues(true).GetValueOrZero(TenDayAbility.Rain)))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(90 + TenDayValues(true).GetValueOrZero(TenDayAbility.ColdHeartedCalm) / 4))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) * 1.2f))
                            {
                                NowCondition = Demeanor.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Devil:
                            if(rollper(32 + TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure)))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper((TenDayValues(true).GetValueOrZero(TenDayAbility.UnextinguishedPath)-2) / 5))
                            {
                                NowCondition = Demeanor.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Cquiest:
                            var DoubtfulToOptimistic_CPer = 0f;
                            if(ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure) < TenDayValues(true).GetValueOrZero(TenDayAbility.NightInkKnight) * 0.3f)
                            {
                                DoubtfulToOptimistic_CPer = TenDayValues(true).GetValueOrZero(TenDayAbility.ElementFaithPower);
                            }

                            if(rollper(38 + DoubtfulToOptimistic_CPer))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(33))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper((TenDayValues(true).GetValueOrZero(TenDayAbility.HeavenAndEndWar) - 6) / 2))
                            {
                                NowCondition = Demeanor.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.GodTier:
                            if(rollper(27 - TenDayValues(true).GetValueOrZero(TenDayAbility.NightInkKnight)))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(85))
                            {
                                NowCondition = Demeanor.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Pillar:
                            if(rollper(70))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(20))
                            {
                                NowCondition = Demeanor.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Doremis:
                            const float Threshold = 49f;
                            var DoubtfulToNorml_Doremis_nightDarknessAndVoidValue = TenDayValues(true).GetValueOrZero(TenDayAbility.NightDarkness) + TenDayValues(true).GetValueOrZero(TenDayAbility.TentVoid);
                            var DoubtfulToNorml_DoremisPer = Threshold;
                            if(DoubtfulToNorml_Doremis_nightDarknessAndVoidValue < Threshold) DoubtfulToNorml_DoremisPer = DoubtfulToNorml_Doremis_nightDarknessAndVoidValue;
                            if(rollper(TenDayValues(true).GetValueOrZero(TenDayAbility.NightDarkness) + 30))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(DoubtfulToNorml_DoremisPer))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(TenDayValues(true).GetValueOrZero(TenDayAbility.StarTersi) / 1.7f))
                            {
                                NowCondition = Demeanor.Resolved;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case Demeanor.Confused:
                    switch (imp)
                    {
                        case SpiritualProperty.LiminalWhiteTile:
                            if(rollper(70))
                            {
                                NowCondition = Demeanor.Painful;
                            }else if(rollper(44))
                            {
                                NowCondition = Demeanor.Normal;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Kindergarten:
                            var ConfusedToPainful_Kindergarden_DokumamusiAndRainCoatAverage = 
                            (TenDayValues(true).GetValueOrZero(TenDayAbility.Dokumamusi) + TenDayValues(true).GetValueOrZero(TenDayAbility.Raincoat)) / 2;
                            if(rollper(20))
                            {
                                NowCondition = Demeanor.Painful;
                            }else if(rollper(80 - (ConfusedToPainful_Kindergarden_DokumamusiAndRainCoatAverage - TenDayValues(true).GetValueOrZero(TenDayAbility.ColdHeartedCalm))))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(20 + TenDayValues(true).GetValueOrZero(TenDayAbility.Raincoat) * 0.4f + TenDayValues(true).GetValueOrZero(TenDayAbility.Dokumamusi) * 0.6f))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Sacrifaith:
                            if(rollper(40))
                            {
                                NowCondition = Demeanor.Painful;
                            }else if(rollper(70 + (TenDayValues(true).GetValueOrZero(TenDayAbility.Sort)-4)))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(60))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Psycho:
                            if(rollper(80))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(11))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.BaleDrival:
                            if(rollper(34))
                            {
                                NowCondition = Demeanor.Painful;
                            }else if(rollper(40))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(75))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Devil:
                            if(rollper(6))
                            {
                                NowCondition = Demeanor.Painful;
                            }else if(rollper(27))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(20))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Cquiest:
                            if(rollper(40))
                            {
                                NowCondition = Demeanor.Painful;
                            }else if(rollper(64))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(2))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.GodTier:
                            if(rollper(50))
                            {
                                NowCondition = Demeanor.Painful;
                            }else if(rollper(50))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(7))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Pillar:
                            if(rollper(60))
                            {
                                NowCondition = Demeanor.Painful;
                            }else if(rollper(60))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(3 - TenDayValues(true).GetValueOrZero(TenDayAbility.SpringWater)))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Doremis:
                            if(rollper(90))
                            {
                                NowCondition = Demeanor.Painful;
                            }else if(rollper(50))
                            {
                                NowCondition = Demeanor.Normal;
                            }else if(rollper(67))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                    }
                    break;

                case Demeanor.Normal:
                    var y = TenDayValues(true).GetValueOrZero(TenDayAbility.Leisure) - ene.TenDayValues(false).GetValueOrZero(TenDayAbility.Leisure);//余裕の差
                    switch (imp)
                    {
                        case SpiritualProperty.LiminalWhiteTile:
                            if(rollper(30 + y*0.8f))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(20 - y))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Kindergarten:
                            if(rollper(40 + y*2))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(70))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Sacrifaith:
                            if(rollper(50))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Psycho:
                            if(rollper(14))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(2))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.BaleDrival:
                            if(rollper(30 +y))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(80))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Devil:
                            if(rollper(35 + y*1.1f))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(50))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Cquiest:
                            if(rollper(30 + y*0.1f))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(20))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.GodTier:
                            if(rollper(20 + y/4))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(15 + y * 0.95f))
                            {
                                NowCondition = Demeanor.Elated;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Pillar:
                            if(rollper(12))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }
                            else
                            {
                                //変化なし
                                ResetConditionConsecutiveTurn();
                            }
                            break;
                        case SpiritualProperty.Doremis:
                            var NormalToElated_DoremisPer = 0f;
                            if(y > 0) NormalToElated_DoremisPer = TenDayValues(true).GetValueOrZero(TenDayAbility.BlazingFire) + TenDayValues(true).GetValueOrZero(TenDayAbility.Miza);
                            if(rollper(38 + y/2))
                            {
                                NowCondition = Demeanor.Optimistic;
                            }else if(rollper(20 + NormalToElated_DoremisPer))
                            {
                                NowCondition = Demeanor.Elated;
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
                case Demeanor.Painful:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.Kindergarten:
                        case SpiritualProperty.Sacrifaith:
                        case SpiritualProperty.BaleDrival:
                            NowCondition = Demeanor.Elated;
                            break;
                        case SpiritualProperty.Psycho:
                        case SpiritualProperty.LiminalWhiteTile:
                        case SpiritualProperty.Cquiest:
                        case SpiritualProperty.Pillar:
                        case SpiritualProperty.Doremis:
                            NowCondition = Demeanor.Normal;
                            break;
                        case SpiritualProperty.GodTier:
                        case SpiritualProperty.Devil:
                            NowCondition = Demeanor.Optimistic;
                            break;
                    }
                    break;

                case Demeanor.Optimistic:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.GodTier:
                            NowCondition = Demeanor.Elated;
                            break;
                        case SpiritualProperty.Pillar:
                        case SpiritualProperty.BaleDrival:
                            NowCondition = Demeanor.Normal;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;    
                    }
                    break;

                case Demeanor.Elated:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.Pillar:
                        case SpiritualProperty.Devil:
                        case SpiritualProperty.BaleDrival:
                        case SpiritualProperty.Kindergarten:
                            NowCondition = Demeanor.Optimistic;
                            break;
                        case SpiritualProperty.Doremis:
                        case SpiritualProperty.Cquiest:
                            NowCondition = Demeanor.Normal;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case Demeanor.Resolved:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.Kindergarten:
                        case SpiritualProperty.LiminalWhiteTile:
                            NowCondition = Demeanor.Normal;
                            break;
                        case SpiritualProperty.Doremis:
                        case SpiritualProperty.BaleDrival:
                            NowCondition = Demeanor.Optimistic;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case Demeanor.Angry:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.Devil:
                        case SpiritualProperty.BaleDrival:
                        case SpiritualProperty.LiminalWhiteTile:
                            NowCondition = Demeanor.Elated;
                            break;
                        case SpiritualProperty.Cquiest:
                        case SpiritualProperty.Psycho:
                        case SpiritualProperty.Kindergarten:
                        case SpiritualProperty.GodTier:
                        case SpiritualProperty.Pillar:
                        case SpiritualProperty.Doremis:
                            NowCondition = Demeanor.Normal;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case Demeanor.Doubtful:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.Kindergarten:
                        case SpiritualProperty.BaleDrival:
                        case SpiritualProperty.Cquiest:
                            NowCondition = Demeanor.Optimistic;
                            break;
                        default:
                            NowCondition = Demeanor.Normal;
                            break;
                    }
                    break;

                case Demeanor.Confused:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.LiminalWhiteTile:
                        case SpiritualProperty.Kindergarten:
                        case SpiritualProperty.Sacrifaith:
                        case SpiritualProperty.BaleDrival:
                        case SpiritualProperty.Devil:
                        case SpiritualProperty.Cquiest:
                        case SpiritualProperty.GodTier:
                        case SpiritualProperty.Pillar:
                            NowCondition = Demeanor.Normal;
                            break;
                        case SpiritualProperty.Doremis:
                            NowCondition = Demeanor.Optimistic;
                            break;
                        default:
                        ResetConditionConsecutiveTurn();
                        break;
                    }
                    break;

                case Demeanor.Normal:
                    switch (MyImpression)
                    {
                        case SpiritualProperty.Psycho:
                        case SpiritualProperty.LiminalWhiteTile:
                        case SpiritualProperty.GodTier:
                            NowCondition = Demeanor.Optimistic;
                            break;
                        case SpiritualProperty.Kindergarten:
                        case SpiritualProperty.Devil:
                            NowCondition = Demeanor.Elated;
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
            case Demeanor.Painful:
                // 普調 (一律50%)
                if (rollper(50))
                {
                    NowCondition = Demeanor.Normal;
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
            case Demeanor.Optimistic:
                switch (MyImpression)
                {
                    // 楽観的 → 辛い
                    case SpiritualProperty.Devil:
                    case SpiritualProperty.Sacrifaith:
                        NowCondition = Demeanor.Painful;
                        break;

                    // 楽観的 → 普調
                    case SpiritualProperty.Pillar:
                    case SpiritualProperty.GodTier:
                    case SpiritualProperty.LiminalWhiteTile:
                    case SpiritualProperty.Kindergarten:
                        NowCondition = Demeanor.Normal;
                        break;

                    case SpiritualProperty.Psycho:
                        // サイコパスは 50% 普調 / 50% 変化なし
                        if (rollper(50))
                        {
                            NowCondition = Demeanor.Normal;
                        }
                        else
                        {
                            // 変化なし
                            ResetConditionConsecutiveTurn();
                        }
                        break;

                    // 楽観的 → 変化なし
                    case SpiritualProperty.BaleDrival:
                    case SpiritualProperty.Cquiest:
                    case SpiritualProperty.Doremis:
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
            case Demeanor.Elated:
                switch (MyImpression)
                {
                    // 変化なし
                    case SpiritualProperty.Sacrifaith:
                    case SpiritualProperty.GodTier:
                    case SpiritualProperty.Devil:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 普調
                    case SpiritualProperty.Cquiest:
                    case SpiritualProperty.LiminalWhiteTile:
                    case SpiritualProperty.Pillar:
                    case SpiritualProperty.Kindergarten:
                    case SpiritualProperty.Doremis:
                    case SpiritualProperty.Psycho:
                        NowCondition = Demeanor.Normal;
                        break;

                    // 楽観的
                    case SpiritualProperty.BaleDrival:
                        NowCondition = Demeanor.Optimistic;
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
            case Demeanor.Resolved:
                switch (MyImpression)
                {
                    // 変化なし => ベール
                    case SpiritualProperty.BaleDrival:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 普調 => シークイエスト, ドレミス, デビル, ゴッドティア, キンダー
                    case SpiritualProperty.Cquiest:
                    case SpiritualProperty.Doremis:
                    case SpiritualProperty.Devil:
                    case SpiritualProperty.GodTier:
                    case SpiritualProperty.Kindergarten:
                        NowCondition = Demeanor.Normal;
                        break;

                    // 辛い => 支柱, リーミナル
                    case SpiritualProperty.Pillar:
                    case SpiritualProperty.LiminalWhiteTile:
                        NowCondition = Demeanor.Painful;
                        break;

                    // 疑念 => 自己犠牲, サイコ
                    case SpiritualProperty.Sacrifaith:
                    case SpiritualProperty.Psycho:
                        NowCondition = Demeanor.Doubtful;
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
            case Demeanor.Angry:
                switch (MyImpression)
                {
                    // 変化なし => リーミナル, 自己犠牲
                    case SpiritualProperty.LiminalWhiteTile:
                    case SpiritualProperty.Sacrifaith:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 楽観的 => サイコ
                    case SpiritualProperty.Psycho:
                        NowCondition = Demeanor.Optimistic;
                        break;

                    // 普調 => else
                    default:
                        NowCondition = Demeanor.Normal;
                        break;
                }
                break;

            //------------------------------
            // 疑念 (Doubtful)
            //------------------------------
            case Demeanor.Doubtful:
                switch (MyImpression)
                {
                    // 怒り => 自己犠牲, ベール, デビル
                    case SpiritualProperty.Sacrifaith:
                    case SpiritualProperty.BaleDrival:
                    case SpiritualProperty.Devil:
                        NowCondition = Demeanor.Angry;
                        break;

                    // 普調 => サイコ, リーミナル, 支柱
                    case SpiritualProperty.Psycho:
                    case SpiritualProperty.LiminalWhiteTile:
                    case SpiritualProperty.Pillar:
                        NowCondition = Demeanor.Normal;
                        break;

                    // 楽観的 => ドレミス, シークイエスト, キンダー, ゴッドティア
                    case SpiritualProperty.Doremis:
                    case SpiritualProperty.Cquiest:
                    case SpiritualProperty.Kindergarten:
                    case SpiritualProperty.GodTier:
                        NowCondition = Demeanor.Optimistic;
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
            case Demeanor.Confused:
                switch (MyImpression)
                {
                    // 変化なし => キンダー
                    case SpiritualProperty.Kindergarten:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 高揚 => ベール, リーミナル
                    case SpiritualProperty.BaleDrival:
                    case SpiritualProperty.LiminalWhiteTile:
                        NowCondition = Demeanor.Elated;
                        break;

                    // 普調 => else
                    default:
                        NowCondition = Demeanor.Normal;
                        break;
                }
                break;

            //------------------------------
            // 普調 (Normal)
            //------------------------------
            case Demeanor.Normal:
                switch (MyImpression)
                {
                    // 変化なし => 支柱, サイコ
                    case SpiritualProperty.Pillar:
                    case SpiritualProperty.Psycho:
                        ResetConditionConsecutiveTurn();
                        break;

                    // 楽観的 => ゴッドティア
                    case SpiritualProperty.GodTier:
                        NowCondition = Demeanor.Optimistic;
                        break;

                    // 疑念 => リーミナル, キンダー, デビル
                    case SpiritualProperty.LiminalWhiteTile:
                    case SpiritualProperty.Kindergarten:
                    case SpiritualProperty.Devil:
                        NowCondition = Demeanor.Doubtful;
                        break;

                    // 怒り => 自己犠牲, シークイエスト, ベール
                    case SpiritualProperty.Sacrifaith:
                    case SpiritualProperty.Cquiest:
                    case SpiritualProperty.BaleDrival:
                        NowCondition = Demeanor.Angry;
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
        return HP <= 0;
    }
    /* ---------------------------------
     * broken
     * --------------------------------- 
     */
    /// <summary>
    /// 完全死滅してるかどうか。
    /// </summary>
    [NonSerialized]
    public bool broken = false;

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
/// PowerLevelExtensionsで日本語に変更可能
/// </summary>
public enum PowerLevel
{
    /// <summary>たるい</summary>
    VeryLow,
    /// <summary>低い</summary>
    Low,
    /// <summary>普通</summary>
    Medium,
    /// <summary>高い</summary>
    High
}
/// <summary>
/// 人間状況　全員持つけど例えばLife以外なんかは固定されてたりしたりする。
/// </summary>
public enum Demeanor
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
    Doremis = 1 << 0,           // ドレミス
    Pillar = 1 << 1,            // ピラー
    Kindergarten = 1 << 2,      // キンダーガーデン
    LiminalWhiteTile = 1 << 3,  // リミナルホワイトタイル
    Sacrifaith = 1 << 4,        // サクリフェイス
    Cquiest = 1 << 5,           // シークイエスト
    Psycho = 1 << 6,            // サイコ
    GodTier = 1 << 7,           // ゴッドティア
    BaleDrival = 1 << 8,        // ベイルドライバル
    Devil = 1 << 9,             // デビル
    None = 1 << 10,             // なし
    Mvoid = 1 << 11,            // ムヴォイド
    Galvanize = 1 << 12,        // ガルヴァナイズ
    Air = 1 << 13,              // エア
    Memento = 1 << 14           // メメント
}


