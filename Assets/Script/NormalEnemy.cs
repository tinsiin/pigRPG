using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Avx;

/// <summary>
/// 通常の敵
/// </summary>
[System.Serializable]
public class NormalEnemy : BaseStates
{
    /// <summary>
    /// この敵キャラクターの復活する歩数
    /// 手動だが基本的に生命キャラクターのみにこの歩数は設定する?
    /// </summary>
    public int RecovelySteps;

    /// <summary>
    /// このキャラクターが再生するかどうか
    /// Falseにすると一度倒すと二度と出てきません。例えば機械キャラなら、基本Falseにする。
    /// </summary>
    public bool Reborn;

    /// <summary>
    /// 敵が一人の際の属性をそのままパーティー属性に変換する辞書データ
    /// </summary>
    private static Dictionary<SpiritualProperty,PartyProperty> EnemyLonelyPartyImpression = new Dictionary<SpiritualProperty, PartyProperty>
    {
        {SpiritualProperty.doremis,PartyProperty.Flowerees},
        {SpiritualProperty.pillar,PartyProperty.Odradeks},
        {SpiritualProperty.kindergarden,PartyProperty.TrashGroup},
        {SpiritualProperty.liminalwhitetile,PartyProperty.MelaneGroup},
        {SpiritualProperty.sacrifaith,PartyProperty.HolyGroup},
        {SpiritualProperty.cquiest,PartyProperty.MelaneGroup},
        //{SpiritualProperty.pysco,PartyProperty.T},//chatgptの最新のチャットからランダムな値が出るようにする。
        {SpiritualProperty.godtier,PartyProperty.Flowerees},
        {SpiritualProperty.baledrival,PartyProperty.TrashGroup},
        {SpiritualProperty.devil,PartyProperty.HolyGroup}
    };

    /// <summary>
    /// 種別同士の敵集まりAIの相性の辞書データ
    /// 同じペアは逆順でも自動的に対応されるので、わざわざ追加しなくてOK
    /// </summary>
    private static Dictionary<(CharacterType, CharacterType), int> TypeMatchupTable = new Dictionary<(CharacterType, CharacterType), int>
    {
        {(CharacterType.TLOA, CharacterType.Life), 80},
        {(CharacterType.TLOA, CharacterType.Machine), 30},
        {(CharacterType.Machine, CharacterType.Life), 60},
        
    };
    /// <summary>
    /// キャラ属性同士の敵集まりAIの相性の辞書データ　方向がある為順序の情報も含む。
    /// </summary>
    private static Dictionary<(SpiritualProperty, SpiritualProperty), int> ImpressionMatchupTable = new Dictionary<(SpiritualProperty, SpiritualProperty), int>
    {
        {(SpiritualProperty.liminalwhitetile,SpiritualProperty.pillar),90 },//リーミナル→支柱
        {(SpiritualProperty.liminalwhitetile,SpiritualProperty.baledrival),90 },//リーミナル→ベール
        {(SpiritualProperty.kindergarden,SpiritualProperty.liminalwhitetile),20 },//キンダー→リーミナル
        {(SpiritualProperty.cquiest,SpiritualProperty.devil),30 },//シークイエスト→デビル
        {(SpiritualProperty.sacrifaith,SpiritualProperty.cquiest),80 },//自己犠牲→シークイエスト
        {(SpiritualProperty.sacrifaith,SpiritualProperty.devil), 70},//自己犠牲→デビル
        {(SpiritualProperty.devil,SpiritualProperty.cquiest),60 },//デビル→シークイエスト
        {(SpiritualProperty.devil,SpiritualProperty.kindergarden),80 },//デビル→キンダー
        {(SpiritualProperty.kindergarden,SpiritualProperty.devil),70 },//キンダ→デビル
        {(SpiritualProperty.baledrival,SpiritualProperty.devil),80 },//ベール→デビル
        {(SpiritualProperty.baledrival,SpiritualProperty.doremis),40},//ベール→ドレミス
        {(SpiritualProperty.baledrival,SpiritualProperty.pysco),70 },//ベール→サイコ
        {(SpiritualProperty.baledrival,SpiritualProperty.pillar),80},//ベール→支柱
        {(SpiritualProperty.baledrival,SpiritualProperty.godtier),100},//ベール→ゴッド
        {(SpiritualProperty.pysco,SpiritualProperty.pillar),0 },//サイコ→支柱
        {(SpiritualProperty.pillar,SpiritualProperty.sacrifaith),100 },//支柱→自己犠牲
    };

    /// <summary>
    /// この属性の敵キャラクターが最初の一人に選ばれたとき、そのまま一人で終わる確率。
    /// </summary>
    private static Dictionary<SpiritualProperty, int> LonelyMatchImpression = new Dictionary<SpiritualProperty, int>
{
    {SpiritualProperty.doremis, 30},
    {SpiritualProperty.pillar, 30},
    {SpiritualProperty.kindergarden, 20},
    {SpiritualProperty.liminalwhitetile, 30},
    {SpiritualProperty.sacrifaith, 70},
    {SpiritualProperty.cquiest, 30},
    {SpiritualProperty.pysco, 30},
    {SpiritualProperty.godtier, 30},
    {SpiritualProperty.baledrival, 30},
    {SpiritualProperty.devil, 30}
};
    /// <summary>
    /// 一人で終わる確率を返す、失敗したら-1を返す。
    /// </summary>
    /// <param name="I"></param>
    /// <returns></returns>
    public static int LonelyMatchUp(SpiritualProperty I)
    {
        var matchPer = LonelyMatchImpression[I];
        if (Random.Range(1,101)<=matchPer)//乱数が確率以下なら、確率を返して相性成功を伝える。
        {
            return matchPer;
        }

        return -1;//一人で終わらなかった場合失敗を返す。
    }



    /// <summary>
    /// 種別同士の敵集まりの相性 成功すると成功した際のパーセントかくりつ
    /// </summary>
    /// <param name="I">既にパーティにいて、ジャッジする方</param>
    /// <param name="You">ジャッジされる側、相性が悪ければ</param>
    public static int TypeMatchUp(CharacterType I, CharacterType You)
    {
        // 順序に関係なく常に同じ結果を得るため、IとYouを並び替える
        var sortedPair = I < You ? (I, You) : (You, I);
        //1<2 ならtrueなので(1,2)が返り、　2<1ならfalseなので逆にして(1,2)が返る。常に同じキーを生成するロジック。
        //このロジックのお陰で順序を気にしなくていい。　characteTypeのenumは暗黙的に数値で扱われること前提。

        var matchPer = TypeMatchupTable[sortedPair];//ジャッジ確率

        if(Random.Range(1,101)<=matchPer)return matchPer;//乱数が確率以下なら、確率を返して相性成功を伝える。


         return -1;//相性が合わなかった場合失敗を返す。
    }

    /// <summary>
    /// 属性同士の敵集まりの相性 成功すると成功した際のパーセントかくりつ
    /// </summary>
    /// <param name="I">既にパーティにいて、ジャッジする方</param>
    /// <param name="You">ジャッジされる側、相性が悪ければ</param>
    public static int ImpressionMatchUp(SpiritualProperty I, SpiritualProperty You)
    {

        // 順序が重要なので、順序をそのままにしてマッチを確認
        var key = (I, You);

        // ジャッジ確率のデフォルト
        var matchPer = 50; 

        if (ImpressionMatchupTable.ContainsKey(key))//デフォルトの確率ではないものだけが辞書に存在するので、ここで判定。
        {
           matchPer=ImpressionMatchupTable[key];//ジャッジ確率
        }
            

        if (Random.Range(1, 101) <= matchPer)return matchPer; // 乱数が確率以下なら、確率を返して相性成功を伝える
        

        return -1;//相性が合わなかった場合失敗を返す。
    }
}
