using System.Collections.Generic;
using UnityEngine;

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

/// <summary>
///     基礎ステータスのクラス　　クラスそのものは使用しないので抽象クラス
/// </summary>
public abstract class BaseStates
{
    [SerializeField] private readonly List<BasePassive> _passiveList;

    [SerializeField] private readonly List<BaseSkill> _skillList;
    public int b_AGI;
    public int b_ATK;

    //基礎攻撃防御　　(大事なのは、基本的にこの辺りは超スキル依存なので、少ない数でしか設定しないこと。)
    public int b_DEF;
    public int b_HIT;

    /// <summary>
    ///     このキャラクターの名前
    /// </summary>
    public string CharacterName;

    public int MAXP;

    //ポイント
    public int P;

    /// <summary>
    ///     リカバリターン
    ///     一回攻撃した後に、次のランダム敵選択リストに入り込むまでのターンカウンター。前のめり状態だと2倍の速度でカウントされる。
    /// </summary>
    public int recoveryTurn;

    protected BaseStates(int p, int maxp, string characterName, int recoveryTurn, List<BasePassive> passiveList,
        List<BaseSkill> skillList, int bDef, int bAgi, int bHit, int bAtk, int hp, int maxhp, CharacterType myType,
        SpiritualProperty myImpression, int maxRecoveryTurn)
    {
        P = p;
        MAXP = maxp;
        CharacterName = characterName;
        this.recoveryTurn = recoveryTurn;
        _passiveList = passiveList;
        _skillList = skillList;
        b_DEF = bDef;
        b_AGI = bAgi;
        b_HIT = bHit;
        b_ATK = bAtk;
        HP = hp;
        MAXHP = maxhp;
        MyType = myType;
        MyImpression = myImpression;
        this.maxRecoveryTurn = maxRecoveryTurn;
    }

    //HP
    public int HP { get; private set; }
    public int MAXHP { get; private set; }

    /// <summary>
    ///     このキャラクターの種別
    /// </summary>
    public CharacterType MyType { get; }

    /// <summary>
    ///     このキャラクターの属性 精神属性が入る
    /// </summary>
    public SpiritualProperty MyImpression { get; private set; }

    /// <summary>
    ///     リカバリターンの設定値。
    /// </summary>
    public int maxRecoveryTurn { get; private set; }

    //状態異常のリスト
    public IReadOnlyList<BasePassive> PassiveList => _passiveList;

    //スキルのリスト
    public IReadOnlyList<BaseSkill> SkillList => _skillList;

    /// <summary>
    ///     防御力計算
    /// </summary>
    /// <returns></returns>
    public　virtual int DEF()
    {
        var def = b_DEF; //基礎防御力が基本。


        return def;
    }

    /// <summary>
    ///     初期精神属性決定関数(基本は印象を持ってるスキルリストから適当に選び出す
    /// </summary>
    public virtual void InitializeMyImpression()
    {
        SpiritualProperty that;

        if (SkillList != null)
        {
            var rnd = Random.Range(0, SkillList.Count);
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
    public virtual void Damage(int atkPoint)
    {
        HP -= atkPoint - DEF(); //HPから指定された攻撃力が引かれる。
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
}