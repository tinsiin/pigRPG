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

    public ModifierPart(string txt,float value)
    {
        whatModifier = txt;
        Modifier = value;
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

/// <summary>
///     基礎ステータスのクラス　　クラスそのものは使用しないので抽象クラス
/// </summary>
[Serializable]
public abstract class BaseStates
{
    [SerializeField] private  List<BasePassive> _passiveList;

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
    public void SkillUseConsecutiveCountUp(BaseSkill useSkill )
    {
        useSkill.SkillHitCount();//スキルのヒット回数の計算

        if (useSkill == _tempUseSkill)//前回使ったスキルと同じなら
        {
            useSkill.DoConsecutiveCount++;//連続実行回数を増やす
            useSkill.HitConsecutiveCount++;//連続ヒット回数を増やす
        }
        else//違ったら
        {
            if(_tempUseSkill != null)//nullじゃなかったら
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
    ///     リカバリターン
    ///     一回攻撃した後に、次のランダム敵選択リストに入り込むまでのターンカウンター。前のめり状態だと2倍の速度でカウントされる。
    /// </summary>
    public int recoveryTurn;

    /// <summary>
    /// このキャラクターを操作できるかどうか。　味方なら基本的に操作するね
    /// </summary>
    public bool CanOprate;

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
    /// 早い行動を心がけるかどうか。　敵はAIで、味方ならCharaConfyまたは逐次実行する際に決定
    /// </summary>
    private bool _commitToSwiftAction;
    /// <summary>
    /// 前のめりするキャラクターを狙うかどうか　オーバーライド可能
    /// </summary>
    public virtual bool ActWithoutHesitation()
    {
        return _commitToSwiftAction;
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
    ///     リカバリターンの設定値。
    /// </summary>
    public int maxRecoveryTurn { get; private set; }

    /// <summary>
    /// 次に使用する命中率へのパーセント補正用保持リスト
    /// </summary>
    private List<ModifierPart> _useHITPercentageModifiers;

    /// <summary>
    /// 命中率補正をセットする。
    /// </summary>
    public void SetHITPercentageModifier(float value,string memo)
    {
        if(_useHITPercentageModifiers == null)_useHITPercentageModifiers=new List<ModifierPart>();//nullチェック、処理
        _useHITPercentageModifiers.Add(new ModifierPart(memo, value));
    }

    /// <summary>
    /// 特別な命中率補正
    /// </summary>
    /// <param name="per"></param>
    public float UseHITPercentageModifier
    {
        get =>_useHITPercentageModifiers.Aggregate(1.0f, (total, m) => total * m.Modifier);//リスト内全ての値を乗算
    }
    /// <summary>
    /// 特別な命中率補正の保持リストを返す。　主にフレーバー要素用。
    /// </summary>
    public List<ModifierPart> UseHitPercentageModifiers
    {
        get => _useHITPercentageModifiers;
    }

    /// <summary>
    /// 一時的な補正などをすべて消す
    /// </summary>
    public void RemoveUseThings()
    {
        _useHITPercentageModifiers =new List<ModifierPart>();
    }



    //状態異常のリスト
    public IReadOnlyList<BasePassive> PassiveList => _passiveList;

    //スキルのリスト
    public IReadOnlyList<BaseSkill> SkillList => _skillList;

    /// <summary>
    /// 命中率計算
    /// </summary>
    /// <returns></returns>
    public virtual float HIT()
    {
        float hit = b_HIT;//基礎命中率

        hit *= UseHITPercentageModifier;//命中率補正。リスト内がゼロならちゃんと1.0fが返る。

        return hit;
    }

    /// <summary>
    /// 回避率計算
    /// </summary>
    public virtual float AGI()
    {
        float AGI = b_AGI;//基礎回避率

        //状態異常やら武器枠なんやらでなんか補正ある多分

        return AGI;
    }

    /// <summary>
    ///     防御力計算
    /// </summary>
    /// <returns></returns>
    public　virtual float DEF(float minusPer)
    {
        var def = b_DEF; //基礎防御力が基本。
        
        var minusAmount = def * minusPer;//防御低減率

        return def - minusAmount;
    }


    /// <summary>
    ///     初期精神属性決定関数(基本は印象を持ってるスキルリストから適当に選び出す
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
    public virtual string Damage(float atkPoint,float DEFAtkper)　
    {
        HP -= atkPoint - DEF(DEFAtkper); //HPから指定された攻撃力が引かれる。
        Debug.Log("攻撃が実行された");
        return "-+~*8";
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
    private bool IsReactHIT(BaseSkill skill)
    {
        var hit = skill.SkillHitCalc();

        if(RandomEx.Shared.NextFloat(0,hit+AGI()) < hit)//術者の命中+僕の回避率　をMAXに　ランダム値が術者の命中に収まったら　命中。
        {
            return true;
        }

        return false;
    }


    /// <summary>
    /// スキルに対するリアクション ここでスキルの解釈をする。
    /// </summary>
    /// <param name="skill"></param>
    public virtual string ReactionSkill(BaseSkill skill)
    {
        //スキルパワーの精神属性による計算
        var modifier = SkillSpiritualModifier[(skill.SkillSpiritual, MyImpression)];//スキルの精神属性と自分の精神属性による補正
        var skillPower = skill.SkillPowerCalc() * modifier.GetValue() / 100.0f;
        var txt = "";//メッセージテキスト用
        skill.DoCount++;//スキルを実行した回数をカウントアップ

        //スキルの持ってる性質を全て処理として実行

        if (skill.HasType(SkillType.Attack))
        {
            if (IsReactHIT(skill))
            {
                //成功されるとダメージを受ける
                txt += Damage(skillPower, skill.DEFATK);
            }
            else
            {//外したら
                skill.HitConsecutiveCount = 0;//連続ヒット回数がゼロ　
            }
        }

         if(skill.HasType(SkillType.Heal))txt += Heal(skillPower);

        Debug.Log("ReactionSkill");
        return txt;
    }


    /// <summary>
    /// クラスを通じて相手を攻撃する
    /// </summary>
    /// <param name="UnderAttacker"></param>
    public virtual string AttackChara(BaseStates UnderAttacker)
    {
        //本来この関数は今のところ無駄　BMでの処理では直接UnderActerのreactionSkill呼びだしゃいい話だし
        //ただもしかしたらここでのunderAttackerによっての何らかの分岐処理するかもだから念のためね。


        SkillUseConsecutiveCountUp(NowUseSkill);//連続カウントアップ

        var txt = UnderAttacker.ReactionSkill(NowUseSkill);//敵がスキルにリアクション

        NowUseSkill.ConsecutiveFixedATKCountUP();//使用したスキルの攻撃回数をカウントアップ
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
    /// RecordedDeath用のバッキングフィールド
    /// </summary>
    private bool _recordedDeath;
    /// <summary>
    /// キャラクターによって認識される死　これがtrueなら誰も彼の事を攻撃したりはしない
    /// </summary>
    public bool RecordedDeath => _recordedDeath;
    /// <summary>
    /// 死を記録
    /// </summary>
    /// <returns></returns>
    public virtual void RecordDeath()
    {
        if (Death())
        {
            _recordedDeath = true;
        }
    }

    /// <summary>
    /// 持ってるスキルリストを初期化する
    /// </summary>
    public void SkillsInitialize()
    {
        foreach(var skill in SkillList)
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
    public FixedOrRandomValue(int minOrFixed )
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

        return RandomEx.Shared.NextInt(rndMinOrFixed,rndMax+1);//ランダムなら

    }
}