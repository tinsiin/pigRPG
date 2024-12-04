using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RandomExtensions;
using System;
using UnityEditor.Experimental.GraphView;
using static BattleManager;
using Unity.Burst.CompilerServices;

/// <summary>
///     キャラクター達の種別
/// </summary>
public enum CharacterType
{
    TLOA,
    Machine,
    Life //TLOAそのもの、機械、生命
}

/// <summary>
///     物理属性、スキルに依存し、キャラクター達の種別や個人との相性で攻撃の通りが変わる
/// </summary>
public enum PhysicalProperty
{
    heavy,
    volten,
    dishSmack //床ずれ、ヴぉ流転、暴断
}
/// <summary>
/// 命中率、攻撃力、回避力、防御力への補正
/// </summary>
public class ModifierPart
{
    /// <summary>
    /// どういう補正かを保存する　攻撃時にunderに出てくる
    /// </summary>
    public string whatModifier;

    /// <summary>
    /// 補正率
    /// </summary>
    public float Modifier;

    public ModifierPart(string txt, float value)
    {
        whatModifier = txt;
        Modifier = value;
    }
}
/// <summary>
/// 慣れ補正で使用するスキルとその使用者
/// </summary>
public class FocusedSkillAndUser
{
    public FocusedSkillAndUser(BaseStates InitUser,BaseSkill askil,float InitDmg)
    {
        User = new List<BaseStates>();
        User.Add(InitUser);
        skill = askil;

        //Memory();//この記憶回数の処理の後に補正するので、作った瞬間はゼロから始めた方がいい
        DamageMemory(InitDmg);
    }

    /// <summary>
    /// そのスキルのユーザー
    /// </summary>
    public List<BaseStates> User;

    /// <summary>
    /// 保存スキル
    /// </summary>
    public BaseSkill skill;

    int _memoryCount;
    /// <summary>
    /// 慣れの記憶回数
    /// </summary>
    public int MemoryCount => _memoryCount;
    public void Memory()
    {
        _memoryCount++;
    }


    float _topDmg;
    /// <summary>
    /// このスキルが自らに施した最大限のダメージ
    /// </summary>
    public float TopDmg => _topDmg;
    public void DamageMemory(float dmg)
    {
        if(dmg > _topDmg) _topDmg = dmg;//越してたら記録
    }
}

/// <summary>
///     精神属性、スキル、キャラクターに依存し、キャラクターは直前に使った物が適用される
///     だから精神属性同士で攻撃の通りは設定される。
/// </summary>
public enum SpiritualProperty
{
    doremis,
    pillar,
    kindergarden,
    liminalwhitetile,
    sacrifaith,
    cquiest,
    pysco,
    godtier,
    baledrival,
    devil
}

public enum MemoryDensity
{
    /// <summary>
    /// 薄い
    /// </summary>
    Low,   
    /// <summary>
    /// 普通
    /// </summary>
    Medium,     
    /// <summary>
    /// しっかりと
    /// </summary>
    High,       
}

/// <summary>
///     基礎ステータスのクラス　　クラスそのものは使用しないので抽象クラス
/// </summary>
[Serializable]
public abstract class BaseStates
{
    /// <summary>
    /// 慣れ補正用　スキルの注目リスト
    /// </summary>
    public List<FocusedSkillAndUser> FocusSkillList;
    
    /// <summary>
    /// スキルに慣れる処理 慣れ補正を返す
    /// </summary>
    float AdaptToSkill(BaseStates enemy,BaseSkill skill,float dmg)
    {
        var donthaveskill = true;
        float AdaptModify = -1;//デフォルト値

        foreach(var fo in FocusSkillList)
        {
            if(fo.skill == skill)//スキル既にあるなら
            {
                fo.DamageMemory(dmg);// ダメージ記録
                donthaveskill = false;//既にあるフラグ！
            }
            else
            {
                //それ以外全ての記憶回数をターン数経過によって減らす

            }
        }
        //もし初めて食らうのなら
        if (donthaveskill)
        {
            var fo = new FocusedSkillAndUser(enemy, skill,dmg);
            FocusSkillList.Add(fo);//最初のキャラクターとスキルを記録
        }




        //スキルの記憶回数での並べ替え
        //記憶回数が多い方から数えて、　　"今回のスキル"がそれに入ってるなら慣れ補正を返す
        //数える範囲は　記憶範囲
        FocusSkillList = FocusSkillList.OrderByDescending(skill => skill.MemoryCount).ToList();

        //記憶範囲の取得　　精神属性による場合分け
        List<MemoryDensity> rl;
        switch (MyImpression)//左から降順に入ってくる　一番左が最初の、一番上の値ってこと
        {
            case SpiritualProperty.doremis:
                rl = new List<MemoryDensity> {MemoryDensity.High,MemoryDensity.Medium,MemoryDensity.Medium};
                break;//しっかりと　普通　普通

            case SpiritualProperty.pillar:
                rl = new List<MemoryDensity> { MemoryDensity.Medium, MemoryDensity.Medium, MemoryDensity.Medium,
                MemoryDensity.Medium,MemoryDensity.Medium,MemoryDensity.Medium,};
                break;//普通　×6

            case SpiritualProperty.kindergarden:
                rl = new List<MemoryDensity> { MemoryDensity.Low};
                break;//薄い

            case SpiritualProperty.liminalwhitetile:
                rl = new List<MemoryDensity> { MemoryDensity.Medium,MemoryDensity.Medium,
                    MemoryDensity.Low,MemoryDensity.Low, MemoryDensity.Low};
                break;//普通×2 薄い×3

            case SpiritualProperty.sacrifaith:
                rl = new List<MemoryDensity> { MemoryDensity.High,MemoryDensity.Low};
                break;//ハイアンドロー

            case SpiritualProperty.cquiest:
                rl = new List<MemoryDensity> { MemoryDensity.High, MemoryDensity.High,MemoryDensity.High,MemoryDensity.High, MemoryDensity.High,
                MemoryDensity.Low};//しっかりと×5 //薄い1
                break;

            case SpiritualProperty.pysco:
                rl = new List<MemoryDensity> { MemoryDensity.High, MemoryDensity.Low };
                break;//ハイアンドロー

            case SpiritualProperty.godtier:
                rl = new List<MemoryDensity> { MemoryDensity.High,MemoryDensity.High,MemoryDensity.Medium,
                MemoryDensity.Medium,MemoryDensity.Medium,MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low};
                break;//しっかりと×2 普通×3 薄く×3

            case SpiritualProperty.baledrival:
                rl = new List<MemoryDensity> { MemoryDensity.High,MemoryDensity.High,MemoryDensity.High,MemoryDensity.High,
                MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low,
                MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low};
                break;//しっかりと×4 薄く　×8

            case SpiritualProperty.devil:
                rl = new List<MemoryDensity> { MemoryDensity.Medium,MemoryDensity.Medium,
                MemoryDensity.Low,MemoryDensity.Low,MemoryDensity.Low};
                break;//普通×2 薄く×3
            default:
                rl = new List<MemoryDensity> { MemoryDensity.Low };
                break;//適当

        }




        //二回目以降で記憶範囲にあるのなら、補正計算して返す
        if (!donthaveskill)
        {
            for(var i = 0; i < rl.Count; i++)//記憶範囲のサイズ分ループ
            {
                var fo = FocusSkillList[i];
                if (fo.skill == skill)//もし記憶範囲に今回のスキルがあるならば
                {
                    //fo.MemoryCount  //記憶回数の数
                    //rl[i]  //精神属性による段階
                    //HITによる固定値の範囲
                }
            }
        }

        //ダメージの大きさで並べ替える
        FocusSkillList = FocusSkillList.OrderByDescending(skill => skill.TopDmg).ToList();

        //最大ダメージの序列で記憶回数の増加をする
        //カウントアップして回して、該当のスキルになったら記憶回数の増加
        if (!donthaveskill)
        {

        }
        return AdaptModify;
    }





    private BattleManager manager;

    public void Managed(BattleManager ma)
    {
        manager = ma;
    }

    [SerializeField] private List<BasePassive> _passiveList;

    [SerializeField] List<BaseSkill> _skillList;
    public float b_AGI;
    public float b_ATK;

    //基礎攻撃防御　　(大事なのは、基本的にこの辺りは超スキル依存なので、少ない数でしか設定しないこと。)
    public float b_DEF;
    public float b_HIT;

    /// <summary>
    ///     このキャラクターの名前
    /// </summary>
    public string CharacterName;

    /// <summary>
    /// 裏に出す種別も考慮した彼のことの名前
    /// </summary>
    public string ImpressionStringName;


    /// <summary>
    ///現在のの攻撃ターンで使われる
    /// </summary>
    public BaseSkill NowUseSkill;

    /// <summary>
    /// 中断できない発動カウント中のスキル　nullならその状態でないということ
    /// </summary>
    public BaseSkill FreezeUseSkill;
    /// <summary>
    /// 前回使ったスキルの保持
    /// </summary>
    private BaseSkill _tempUseSkill;
    /// <summary>
    /// スキルを連続実行した回数などをスキルのクラスに記録する関数
    /// </summary>
    /// <param name="useSkill"></param>
    public void SkillUseConsecutiveCountUp(BaseSkill useSkill)
    {
        useSkill.SkillHitCount();//スキルのヒット回数の計算

        if (useSkill == _tempUseSkill)//前回使ったスキルと同じなら
        {
            useSkill.DoConsecutiveCount++;//連続実行回数を増やす
            useSkill.HitConsecutiveCount++;//連続ヒット回数を増やす
        }
        else//違ったら
        {
            if (_tempUseSkill != null)//nullじゃなかったら
            {
                _tempUseSkill.DoConsecutiveCount = 0;//リセット
                _tempUseSkill.HitConsecutiveCount++;//連続ヒット回数をリセット　
            }
            useSkill.DoConsecutiveCount++;//最初の一回目として
            useSkill.HitConsecutiveCount++;//連続ヒット回数を増やす
        }
    }
    public int MAXP;

    //ポイント
    public int P;

    /// <summary>
    ///     リカバリターン/再行動クールタイムの設定値。
    /// </summary>
    public int maxRecoveryTurn;

    /// <summary>
    ///     recovelyTurnの基礎バッキングフィールド
    /// </summary>
    private int recoveryTurn;

    /// <summary>
    /// skillDidWaitCountなどで一時的に通常recovelyTurnに追加される一時的に再行動クールタイム/追加硬直値
    /// </summary>
    private int tmpTurnsToAdd;
    /// <summary>
    /// 一時保存用のリカバリターン判別用の前ターン変数
    /// </summary>
    private int tmp_EncountTurn;
    /// <summary>
    /// recovelyCountという行動クールタイムに一時的に値を加える
    /// </summary>
    public void RecovelyCountTmpAdd(int addTurn)
    {
        tmpTurnsToAdd += addTurn;
    }
    /// <summary>
    /// このキャラが戦場にて再行動を取れるかどうかと時間を唱える関数
    /// </summary>
    public bool RecovelyBattleField(int nowTurn)
    {
        var difference = Math.Abs(nowTurn - tmp_EncountTurn);//前ターンと今回のターンの差異から経過ターン
        tmp_EncountTurn = nowTurn;//一時保存
        if ((recoveryTurn += difference) >= maxRecoveryTurn + tmpTurnsToAdd)//累計ターン経過が最大値を超えたら
        {
            //ここでrecovelyTurnを初期化すると　リストで一括処理した時にカウントアップだけじゃなくて、
            //選ばれたことになっちゃうから、0に初期化する部分はBattleManagerで選ばれた時に処理する。
            return true;
        }
        return false;
    }
    /// <summary>
    /// 戦場へ参戦回復出来るまでのカウントスタート
    /// </summary>
    public void RecovelyWaitStart()
    {
        recoveryTurn = 0;
        RemoveRecovelyTmpAddTurn();
    }
    /// <summary>
    /// キャラに設定された追加硬直値をリセットする
    /// </summary>
    public void RemoveRecovelyTmpAddTurn()
    {
        tmpTurnsToAdd = 0;
    }
    /// <summary>
    /// 戦場へ参戦回復が出来るようにする
    /// </summary>
    public void RecovelyOK()
    {
        recoveryTurn = maxRecoveryTurn;
    }

    //HP
    [SerializeField]
    private float _hp;
    public float HP
    {
        get { return _hp; }
        set
        {
            if (value > MAXHP)//最大値を超えないようにする
            {
                _hp = MAXHP;
            }
            else _hp = value;
        }
    }
    [SerializeField]
    private float _maxHp;
    public float MAXHP => _maxHp;

    /// <summary>
    /// このキャラがどの辺りを狙っているか
    /// </summary>
    public DirectedWill Target;

    /// <summary>
    /// このキャラの現在の範囲の意思　　複数持てる
    /// スキルの範囲性質にcanSelectRangeがある場合のみ、ない場合はskillのzoneTraitをそのまま代入される。
    /// </summary>
    public SkillZoneTrait RangeWill;

    /// <summary>
    /// スキル範囲性質を持ってるかどうか
    /// 複数指定した場合は全て当てはまってるかどうかで判断
    /// </summary>
    public bool HasRangeWill(params SkillZoneTrait[] skills)
    {
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }
        return (RangeWill & combinedSkills) == combinedSkills;
    }

    /// <summary>
    /// 指定されたスキルフラグのうち、一つでもRangeWillに含まれている場合はfalseを返し、
    /// 全く含まれていない場合はtrueを返します。
    /// </summary>
    public bool DontHasRangeWill(params SkillZoneTrait[] skills)
    {
        // 受け取ったスキルフラグをビット単位で結合
        SkillZoneTrait combinedSkills = 0;
        foreach (SkillZoneTrait skill in skills)
        {
            combinedSkills |= skill;
        }

        // RangeWillに含まれるフラグとcombinedSkillsのビットAND演算
        // 結果が0でなければ、一つ以上のフラグが含まれている
        bool containsAny = (RangeWill & combinedSkills) != 0;

        // 一つでも含まれていればfalse、含まれていなければtrueを返す
        return !containsAny;
    }



    /// <summary>
    /// 使用中のスキルを強制続行中のスキルとする。　
    /// 例えばスキルの連続実行中の処理や発動カウント中のキャンセル不可能なスキルなどで使う
    /// </summary>
    public void FreezeSkill()
    {
        FreezeUseSkill = NowUseSkill;
    }
    /// <summary>
    /// 強制続行中のスキルをなくす
    /// </summary>
    public void Defrost()
    {
        FreezeUseSkill = null;
    }

    /// <summary>
    ///     このキャラクターの種別
    /// </summary>
    public CharacterType MyType { get; }


    /// <summary>
    ///     このキャラクターの属性 精神属性が入る
    /// </summary>
    public SpiritualProperty MyImpression { get; private set; }



    /// <summary>
    /// 次に使用する命中率へのパーセント補正用保持リスト
    /// </summary>
    private List<ModifierPart> _useHITPercentageModifiers;
    /// <summary>
    /// 次に使用する攻撃力へのパーセント補正用保持リスト
    /// </summary>
    private List<ModifierPart> _useATKPercentageModifiers;
    /// <summary>
    /// 次に使用する回避率へのパーセント補正用保持リスト
    /// </summary>
    private List<ModifierPart> _useAGIPercentageModifiers;
    /// <summary>
    /// 次に使用する防御力へのパーセント補正用保持リスト
    /// </summary>
    private List<ModifierPart> _useDEFPercentageModifiers;

    /// <summary>
    /// 命中率補正をセットする。
    /// </summary>
    public void SetHITPercentageModifier(float value, string memo)
    {
        if (_useHITPercentageModifiers == null) _useHITPercentageModifiers = new List<ModifierPart>();//nullチェック、処理
        _useHITPercentageModifiers.Add(new ModifierPart(memo, value));
    }
    /// <summary>
    /// 攻撃力補正をセットする。
    /// </summary>
    public void SetATKPercentageModifier(float value, string memo)
    {
        if (_useATKPercentageModifiers == null) _useATKPercentageModifiers = new List<ModifierPart>();//nullチェック、処理
        _useATKPercentageModifiers.Add(new ModifierPart(memo, value));
    }
    /// <summary>
    /// 回避率補正をセットする。
    /// </summary>
    public void SetAGIPercentageModifier(float value, string memo)
    {
        if (_useAGIPercentageModifiers == null) _useAGIPercentageModifiers = new List<ModifierPart>();//nullチェック、処理
        _useAGIPercentageModifiers.Add(new ModifierPart(memo, value));
    }
    /// <summary>
    /// 防御力補正をセットする。
    /// </summary>
    public void SetDEFPercentageModifier(float value, string memo)
    {
        if (_useDEFPercentageModifiers == null) _useDEFPercentageModifiers = new List<ModifierPart>();//nullチェック、処理
        _useDEFPercentageModifiers.Add(new ModifierPart(memo, value));
    }

    /// <summary>
    /// 特別な命中率補正
    /// </summary>
    /// <param name="per"></param>
    public float UseHITPercentageModifier
    {
        get => _useHITPercentageModifiers.Aggregate(1.0f, (total, m) => total * m.Modifier);//リスト内全ての値を乗算
    }

    /// <summary>
    /// 特別な攻撃力補正
    /// </summary>
    public float UseATKPercentageModifier
    {
        get => _useATKPercentageModifiers.Aggregate(1.0f, (total, m) => total * m.Modifier);//リスト内全ての値を乗算
    }

    /// <summary>
    /// 特別な回避率補正
    /// </summary>
    public float UseAGIPercentageModifier
    {
        get => _useAGIPercentageModifiers.Aggregate(1.0f, (total, m) => total * m.Modifier);//リスト内全ての値を乗算
    }

    /// <summary>
    /// 特別な防御力補正
    /// </summary>
    public float UseDEFPercentageModifier
    {
        get => _useDEFPercentageModifiers.Aggregate(1.0f, (total, m) => total * m.Modifier);//リスト内全ての値を乗算
    }
    /// <summary>
    /// 特別な命中率補正の保持リストを返す。　主にフレーバー要素用。
    /// </summary>
    public List<ModifierPart> UseHitPercentageModifiers
    {
        get => _useHITPercentageModifiers;
    }

    /// <summary>
    /// 特別な攻撃力補正の保持リストを返す。　主にフレーバー要素用。
    /// </summary>
    public List<ModifierPart> UseATKPercentageModifiers
    {
        get => _useATKPercentageModifiers;
    }

    /// <summary>
    /// 特別な回避率補正の保持リストを返す。　主にフレーバー要素用。
    /// </summary>
    public List<ModifierPart> UseAGIPercentageModifiers
    {
        get => _useAGIPercentageModifiers;
    }

    /// <summary>
    /// 特別な防御力補正の保持リストを返す。　主にフレーバー要素用。
    /// </summary>
    public List<ModifierPart> UseDEFPercentageModifiers
    {
        get => _useDEFPercentageModifiers;
    }

    /// <summary>
    /// 一時的な補正などをすべて消す
    /// </summary>
    public void RemoveUseThings()
    {
        _useHITPercentageModifiers = new List<ModifierPart>();
        _useATKPercentageModifiers = new List<ModifierPart>();
        _useAGIPercentageModifiers = new List<ModifierPart>();
        _useDEFPercentageModifiers = new List<ModifierPart>();
    }



    //状態異常のリスト
    public IReadOnlyList<BasePassive> PassiveList => _passiveList;

    //スキルのリスト
    public IReadOnlyList<BaseSkill> SkillList => _skillList;
    /// <summary>
    /// 完全な単体攻撃かどうか
    /// (例えばControlByThisSituationの場合はrangeWillにそのままskillのzoneTraitが入るので、
    /// そこに範囲系の性質(事故で範囲攻撃に変化)がある場合はfalseが返る
    /// </summary>
    /// <returns></returns>
    private bool IsSingleATK()
    {
        return DontHasRangeWill(SkillZoneTrait.CanSelectMultiTarget,
            SkillZoneTrait.RandomSelectMultiTarget, SkillZoneTrait.RandomMultiTarget,
            SkillZoneTrait.AllTarget);
    }

    /// <summary>
    /// 命中率計算
    /// </summary>
    /// <returns></returns>
    public virtual float HIT()
    {
        float hit = b_HIT;//基礎命中率

        hit *= UseHITPercentageModifier;//命中率補正。リスト内がゼロならちゃんと1.0fが返る。

        //範囲意志によるボーナス
        foreach (KeyValuePair<SkillZoneTrait, float> entry
            in NowUseSkill.HitRangePercentageDictionary)//辞書に存在する物全てをループ
        {
            if (HasRangeWill(entry.Key))//キーの内容が範囲意志と合致した場合
            {
                hit += entry.Value;//範囲意志による補正が掛かる

                //基本的に範囲は一つだけのはずなので無用なループは避けてここで終了
                break;
            }
        }

        //単体攻撃による命中補正
        //複数性質を持っていない、完全なる単体の攻撃なら
        if (IsSingleATK())
        //ControlBySituationでの事故性質でも複数性質で複数事故が起こるかもしれないので、それも加味してる。
        {
            var agiPer = 6;//攻撃者のAgiの命中補正用 割る数
            if (NowUseSkill.SkillPhysical == PhysicalProperty.heavy)//暴断攻撃なら
            {
                agiPer *= 2;//割る数が二倍に
            }
            hit += AGI() / agiPer;
        }



        return hit;
    }

    /// <summary>
    /// 回避率計算
    /// </summary>
    public virtual float AGI()
    {
        float agi = b_AGI;//基礎回避率

        agi *= UseAGIPercentageModifier;//回避率補正。リスト内がゼロならちゃんと1.0fが返る。

        if (manager.IsVanguard(this))//自分が前のめりなら
        {
            agi /= 2;//回避率半減
        }

        return agi;
    }

    public virtual float ATK()
    {
        float atk = b_ATK;//基礎攻撃力

        atk *= UseATKPercentageModifier;//攻撃力補正

        //範囲意志によるボーナス
        foreach (KeyValuePair<SkillZoneTrait, float> entry
            in NowUseSkill.PowerRangePercentageDictionary)//辞書に存在する物全てをループ
        {
            if (HasRangeWill(entry.Key))//キーの内容が範囲意志と合致した場合
            {
                atk += entry.Value;//範囲意志による補正が掛かる

                //基本的に範囲は一つだけのはずなので無用なループは避けてここで終了
                break;
            }
        }

        //単体攻撃で暴断物理攻撃の場合のAgi攻撃補正
        if (IsSingleATK())
        {
            if(NowUseSkill.SkillPhysical == PhysicalProperty.heavy)
            {
                atk += AGI() / 6;
            }
        }


            return atk;
    }

    /// <summary>
    ///     防御力計算
    /// </summary>
    /// <returns></returns>
    public virtual float DEF(float minusPer)
    {
        var def = b_DEF; //基礎防御力が基本。

        def *= UseDEFPercentageModifier;//防御力補正

        var minusAmount = def * minusPer;//防御低減率


        return def - minusAmount;
    }


    /// <summary>
    ///初期精神属性決定関数(基本は印象を持ってるスキルリストから適当に選び出す
    /// </summary>
    public virtual void InitializeMyImpression()
    {
        SpiritualProperty that;

        if (SkillList != null)
        {
            var rnd = RandomEx.Shared.NextInt(0, SkillList.Count);
            that = SkillList[rnd].SkillSpiritual; //スキルの精神属性を抽出
            MyImpression = that; //印象にセット
        }
        else
        {
            Debug.Log(CharacterName + " のスキルが空です。");
        }
    }

    /// <summary>
    ///     オーバライド可能なダメージ関数
    /// </summary>
    /// <param name="atkPoint"></param>
    public virtual string Damage(BaseStates Atker, float SkillPower)
    {
        var skill = Atker.NowUseSkill;
        var dmg = (Atker.ATK() - DEF(skill.DEFATK)) * SkillPower;//(攻撃-対象者の防御) ×スキルパワー？

        //慣れ補正
        AdaptToSkill(Atker,skill,dmg);

        HP -= dmg;
        Debug.Log("攻撃が実行された");
        return "-+~*⋮¦";
    }

    /// <summary>
    /// ヒールは防御できない、つまりヒールが逆効果のキャラクターならヒールは有効打ってこと
    /// </summary>
    /// <param name="HealPoint"></param>
    public virtual string Heal(float HealPoint)
    {
        HP += HealPoint;
        Debug.Log("ヒールが実行された");
        return "癒された";
    }

    /// <summary>
    /// 攻撃者と防御者とスキルを利用してヒットするかの計算
    /// </summary>
    private bool IsReactHIT(BaseStates Attacker)
    {
        var skill = Attacker.NowUseSkill;

        if (RandomEx.Shared.NextFloat(0, Attacker.HIT() + AGI()) < Attacker.HIT())//術者の命中+僕の回避率　をMAXに　ランダム値が術者の命中に収まったら　命中。
        {
            //スキルそのものの命中率
            return skill.SkillHitCalc();
        }

        return false;
    }


    /// <summary>
    /// スキルに対するリアクション ここでスキルの解釈をする。
    /// </summary>
    /// <param name="skill"></param>
    /// <param name="UnderIndex">攻撃される人の順番　スキルのPowerSpreadの順番に同期している</param>
    public virtual string ReactionSkill(BaseStates attacker, float spread)
    {
        var skill = attacker.NowUseSkill;

        //スキルパワーの精神属性による計算
        var modifier = SkillSpiritualModifier[(skill.SkillSpiritual, MyImpression)];//スキルの精神属性と自分の精神属性による補正
        var skillPower = skill.SkillPowerCalc(spread) * modifier.GetValue() / 100.0f;
        var txt = "";//メッセージテキスト用
        skill.DoCount++;//スキルを実行した回数をカウントアップ


        //スキルの持ってる性質を全て処理として実行

        if (skill.HasType(SkillType.Attack))
        {
            if (IsReactHIT(attacker))
            {
                //成功されるとダメージを受ける
                txt += Damage(attacker, skillPower);
            }
            else
            {//外したら
                skill.HitConsecutiveCount = 0;//連続ヒット回数がゼロ　
            }
        }

        if (skill.HasType(SkillType.Heal)) 
        {
            if (skill.SkillHitCalc())//スキル命中率の計算だけ行う
            {
                txt += Heal(skillPower);
            }
        }

        Debug.Log("ReactionSkill");
        return txt;
    }


    /// <summary>
    /// クラスを通じて相手を攻撃する
    /// </summary>
    /// <param name="UnderAttacker"></param>
    public virtual string AttackChara(UnderActersEntryList Unders)
    {
        //本来この関数は今のところ無駄　BMでの処理では直接UnderActerのreactionSkill呼びだしゃいい話だし
        //ただもしかしたらここでのunderAttackerによっての何らかの分岐処理するかもだから念のためね。


        SkillUseConsecutiveCountUp(NowUseSkill);//連続カウントアップ
        string txt = "";

        for (var i = 0; i < Unders.Count; i++)
        {
            txt += Unders.GetAtCharacter(i).ReactionSkill(this, Unders.GetAtSpreadPer(i));//敵がスキルにリアクション
        }

        NowUseSkill.ConsecutiveFixedATKCountUP();//使用したスキルの攻撃回数をカウントアップ
        RemoveUseThings();//特別な補正を消去
        Debug.Log("AttackChara");

        _tempUseSkill = NowUseSkill;//使ったスキルを一時保存
        return txt;
    }

    /// <summary>
    ///     死を判定するオーバライド可能な関数
    /// </summary>
    /// <returns></returns>
    public virtual bool Death()
    {
        if (HP <= 0) return true;
        return false;
    }
    /// <summary>
    /// 持ってるスキルリストを初期化する
    /// </summary>
    public void SkillsInitialize()
    {
        foreach (var skill in SkillList)
        {
            skill.OnInitialize(this);
        }
    }
    /// <summary>
    /// 全スキルの一時保存系プロパティをリセットする
    /// </summary>
    public void SkillsTmpReset()
    {
        foreach (var skill in SkillList)
        {
            skill.ResetTmpProperty();//プロパティをリセットする
        }
    }

    /// <summary>
    ///     パッシブを適用
    /// </summary>
    public virtual void ApplyPassive(BasePassive status)
    {
        var typeMatch = false;
        var propertyMatch = false;
        //キャラクター種別の相性判定
        foreach (var type in status.TypeOkList)
            if (MyType == type)
            {
                typeMatch = true;
                break;
            }

        //キャラクター属性と
        foreach (var property in status.CharaPropertyOKList)
            if (MyImpression == property)
            {
                propertyMatch = true;
                break;
            }

        //相性条件クリアしたら
        if (typeMatch && propertyMatch)
        {
            var isactive = false;
            foreach (var passive in _passiveList)
                if (passive == status)
                {
                    isactive = true; //既にリストに含まれているパッシブなら。
                    passive.AddPassivePower(1); //既に含まれてるパッシブを強くする                   
                    break;
                }

            if (!isactive) _passiveList.Add(status); //状態異常リストに直接追加
        }
    } //remove処理はR3で処理する。　

    //static 静的なメゾット(戦いに関する辞書データなど)

    /// <summary>
    /// 精神属性でのスキルの補正値　スキルの精神属性→キャラクター属性
    /// </summary>
    protected static Dictionary<(SpiritualProperty, SpiritualProperty), FixedOrRandomValue> SkillSpiritualModifier;

    /// <summary>
    /// セルの文字列を整数にパースする。空または無効な場合はデフォルト値を返す。
    /// </summary>
    /// <param name="cell">セルの文字列</param>
    /// <returns>パースされた整数値またはデフォルト値</returns>
    private static int ParseCell(string cell)
    {
        if (int.TryParse(cell, out int result))
        {
            return result;
        }//空セルの場合は整数変換に失敗してelseが入る　splitで,,みたいに区切り文字が二連続すると""空文字列が入る
        else return -1;  //空セルには-1が入る　空セルが入るのはrndMaxが入る所のみになってるはずなので、最大値が無効になる-1が入る
    }
    /// <summary>
    /// BaseStatus内で使われるデータ用のcsvファイルをロード
    /// </summary>
    public async static void CsvLoad()
    {
        SkillSpiritualModifier = new Dictionary<(SpiritualProperty, SpiritualProperty), FixedOrRandomValue>();//初期化
        var csvFile = "Assets/csvData/SpiritualMatchData.csv";

        var textHandle = await Addressables.LoadAssetAsync<TextAsset>(csvFile);


        var rows = textHandle.text //そのままテキストを渡す
            .Split("\n")//改行ごとに分割
                        //.Select(line => line.Trim())//行の先頭と末尾の空白や改行を削除する
            .Select(line => line.Split(',').Select(ParseCell).ToArray()) //それをさらにカンマで分割してint型に変換して配列に格納する。
            .ToArray(); //配列になった行をさらに配列に格納する。
        /*
         * new List<List<int>> {  実際はarrayだけどこういうイメージ
            new List<int> { 50, 20, 44, 53, 42, 37, 90, 100, 90, 50 },
            new List<int> { 60, 77, 160, 50, 80, 23, 32, 50, 51, 56 }}
         */

        var SpiritualCsvArrayRows = new[]
        {
            //精神攻撃の相性の　行の属性並び順
            SpiritualProperty.liminalwhitetile,
            SpiritualProperty.kindergarden,
            SpiritualProperty.sacrifaith,
            SpiritualProperty.cquiest,
            SpiritualProperty.devil,
            SpiritualProperty.devil,//乱数のmax
            SpiritualProperty.doremis,
            SpiritualProperty.pillar,
            SpiritualProperty.godtier,
            SpiritualProperty.baledrival,
            SpiritualProperty.pysco
        };
        var SpiritualCsvArrayColumn = new[]
        {
            //精神攻撃の相性の　列の属性並び順
            SpiritualProperty.liminalwhitetile,
            SpiritualProperty.kindergarden,
            SpiritualProperty.sacrifaith,
            SpiritualProperty.cquiest,
            SpiritualProperty.devil,
            SpiritualProperty.doremis,
            SpiritualProperty.pillar,
            SpiritualProperty.godtier,
            SpiritualProperty.baledrival,
            SpiritualProperty.baledrival,//乱数のmax
            SpiritualProperty.pysco
        };


        for (var i = 0; i < rows.Length; i++) //行ごとに回していく oneは行たちを格納した配列
        {
            //4行目と5行目はdevilへの乱数min,max

            //min 部分でクラスを生成　max部分で既にあるクラスにmaxをセット　空なら-1
            //つまり乱数のmaxにあたる行でのみSetmaxが既にある辞書に実行されるという仕組みにすればいいのだ楽だラクダ
            for (var j = 0; j < rows[i].Length; j++) //数字ごとに回す　one[j]は行の中の数字を格納した配列
            {
                //8,9列目はbaleが相手に対する乱数min,max

                var key = (SpiritualCsvArrayColumn[j], SpiritualCsvArrayRows[i]);
                var value = rows[i][j];
                if (i == 5 || j == 9)//もし五行目、または九列目の場合
                {
                    if (SkillSpiritualModifier.ContainsKey(key))//キーが既にあれば
                    {
                        SkillSpiritualModifier[key].SetMax(value);//乱数最大値を設定
                        //Debug.Log($"乱数セット{value}");
                    }
                    else
                    {
                        Debug.LogError($"キー {key} が存在しません。SetMax を実行できません。");
                    }
                    //既にある辞書データの乱数単一の値のクラスに最大値をセット
                }
                else
                {
                    //固定値としてクラスを生成 (生成時にrndMaxに初期値-1が入るよ)
                    if (!SkillSpiritualModifier.ContainsKey(key))//キーが存在していなければ
                    {
                        SkillSpiritualModifier.Add(key, new FixedOrRandomValue(value));//キーを追加
                    }
                    else
                    {
                        Debug.LogWarning($"キー {key} は既に存在しています。追加をスキップします。");
                    }

                }


            }
        }


        /*Debug.Log("読み込まれたキャラクター精神スキル補正値\n" +
              string.Join(", ",
                  SkillSpiritualModifier.Select(kvp => $"[{kvp.Key}: {kvp.Value.GetValue()} rndMax({kvp.Value.rndMax})]" + "\n"))); //デバックで全内容羅列。*/
    }
}

/// <summary>
/// 固定値か最大値、最小値に応じた乱数のどっちかを返すクラス
/// </summary>
public class FixedOrRandomValue
{

    private int rndMax;//乱数の最大値 乱数かどうかはrndMaxに-1を入れればいい
    private int rndMinOrFixed;//単一の値または乱数としての最小値

    /// <summary>
    /// クラス生成
    /// </summary>
    /// <param name="isRnd">乱数として保持するかどうか</param>
    /// <param name="minOrFixed">最小値または単一の値として</param>
    /// <param name="max">省略可能、乱数なら最大値</param>
    public FixedOrRandomValue(int minOrFixed)
    {
        rndMinOrFixed = minOrFixed;//まず最小値またはデフォルトありきでクラスを作成
        rndMax = -1;//予め無を表す-1で初期化
    }

    public void SetMax(int value)
    {
        rndMax = value;//-1を指定するとないってこと
    }
    public int GetValue()
    {
        if (rndMax == -1) return rndMinOrFixed;//乱数じゃないなら単一の値が返る

        return RandomEx.Shared.NextInt(rndMinOrFixed, rndMax + 1);//ランダムなら

    }
}