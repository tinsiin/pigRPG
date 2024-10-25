using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using RandomExtensions;

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

    /// <summary>
    /// 裏に出す種別も考慮した彼のことの名前
    /// </summary>
    public string ImpressionStringName;


    //次の攻撃ターンで使われる
    public BaseSkill NowUseSkill;

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
    private int _hp;
    public int HP
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
    public　virtual int DEF(float minusPer)
    {
        var def = b_DEF; //基礎防御力が基本。
        
        var minusAmount = def * minusPer;//防御低減率

        return (int)(def - minusAmount);
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
    public virtual void Damage(int atkPoint,float DEFAtkper)　
    {
        HP -= atkPoint - DEF(DEFAtkper); //HPから指定された攻撃力が引かれる。
    }
    public virtual void Heal(int HealPoint)
    {
        HP += HealPoint;//ヒールは防御できない、つまりヒールが逆効果のキャラクターならヒールは有効打ってこと
    }

    /// <summary>
    /// スキルに対するリアクション
    /// </summary>
    /// <param name="skill"></param>
    public virtual void ReactionSkill(BaseSkill skill)
    {
        //スキルの種別により、処理が分岐する
        switch (skill.WhatSkill)
        {
            case SkillType.Attack:
                {
                    Damage(skill.SkillPower, skill.DEFATK);
                    break;
                }

            case SkillType.Heal:
                {
                    Heal(skill.SkillPower);
                    break;
                }
        }
    }
    /// <summary>
    /// クラスを通じて相手を攻撃する
    /// </summary>
    /// <param name="UnderAttacker"></param>
    public virtual void AttackChara(BaseStates UnderAttacker)
    {
        //クラスのdamage関数にskillを渡して処理させる
        //ここに渡すやり方が敵と味方で変わる。
        //味方ならプレイヤーのボタン結果やそれによる遅延または加算行為など多岐にわたるが、
        //敵なら基本的に全てプログラム制御などになる。

        //基本的に味方キャラの処理
        UnderAttacker.ReactionSkill(NowUseSkill);
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

    //static 静的なメゾット(戦いに関する辞書データなど)

    /// <summary>
    /// 精神属性でのスキルの補正値　スキルの精神属性→キャラクター属性
    /// </summary>
    protected static Dictionary<(SpiritualProperty, SpiritualProperty), int> SkillSpiritualModifier;

    //CancellationToken cancellLoad;
    /// <summary>
    /// BaseStatus内で使われるデータ用のcsvファイルをロード
    /// </summary>
    public async static void CsvLoad()
    {
        SkillSpiritualModifier = new Dictionary<(SpiritualProperty, SpiritualProperty), int>();//初期化
        var csvFile = "Assets/csvData/SpiritualMatchData.csv";

        var textHandle = await Addressables.LoadAssetAsync<TextAsset>(csvFile);


        var rows = textHandle.text //そのままテキストを渡す
            .Split("\n")//改行ごとに分割
            .Select(line => line.Trim())//行の先頭と末尾の空白や改行を削除する
            .Select(line => line.Split(',').Select(int.Parse).ToArray()) //それをさらにカンマで分割してint型に変換して配列に格納する。
            .ToArray(); //配列になった行をさらに配列に格納する。
        /*
         * new List<List<int>> {  実際はarrayだけどこういうイメージ
            new List<int> { 50, 20, 44, 53, 42, 37, 90, 100, 90, 50 },
            new List<int> { 60, 77, 160, 50, 80, 23, 32, 50, 51, 56 }}
         */

        for (var i = 0; i < rows.Length; i++) //行ごとに回していく oneは行たちを格納した配列
            for (var j = 0; j < rows[i].Length; j++) //数字ごとに回す　one[j]は行の中の数字を格納した配列
            {
             //4行目と5行目はdevilの乱数min,max
             //8,9列目はbaleの乱数min,max

            }


            /*Debug.Log("読み込まれたキャラクター属性同士の相性\n" +
                  string.Join(", ",
                      ImpressionMatchupTable.Select(kvp => $"[{kvp.Key}: {kvp.Value}]" + "\n"))); //デバックで全内容羅列。*/
    }
}

/// <summary>
/// 固定値か最大値、最小値に応じた乱数のどっちかを返すクラス
/// </summary>
public class FixedOrRandomValue
{
    private bool IsRandom;//乱数かどうか

    private int rndMax;//乱数の最大値最小値
    private int rndMin;


    private int value;//単一の値

    /// <summary>
    /// 乱数か単一の値かを指定してクラス生成
    /// </summary>
    /// <param name="isRnd">乱数として保持するかどうか</param>
    /// <param name="minOrFixed">最小値または単一の値として</param>
    /// <param name="max">省略可能、乱数なら最大値</param>
    public FixedOrRandomValue(bool isRnd,int minOrFixed ,int max = -1)
    {
        if (isRnd)
        {
            rndMax = max;
            rndMin = minOrFixed;
        }
        else
        {
            value = minOrFixed;
        }

        IsRandom = isRnd;
    }
    public int GetValue()
    {
        if (IsRandom) return RandomEx.Shared.NextInt(rndMin,rndMax+1);//ランダムなら

        return value;//乱数じゃないなら単一の値が返る
    }
}