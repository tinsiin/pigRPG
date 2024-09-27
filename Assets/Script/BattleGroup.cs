using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RandomExtensions;
using UnityEngine;


/// <summary>
/// パーティー属性
/// </summary>
public enum PartyProperty
{
    TrashGroup,HolyGroup,MelaneGroup,Odradeks,Flowerees
        //馬鹿共、聖戦(必死、目的使命)、メレーンズ(王道的)、オドラデクス(秩序から離れてる、目を開いて我々から離れるようにコロコロと)、花樹(オサレ)
}
/// <summary>
/// 戦いをするパーティーのクラス
/// </summary>
public  class BattleGroup
{
    /// <summary>
    /// パーティー属性
    /// </summary>
    public PartyProperty OurImpression;

    private List<BaseStates> _ours;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public BattleGroup(List<BaseStates> ours, PartyProperty ourImpression)
    {
        _ours = ours;
        OurImpression = ourImpression;
    }

    /// <summary>
    /// 集団の人員リスト
    /// </summary>
    public IReadOnlyList<BaseStates> Ours => _ours;

    

    /// <summary>
    /// 与えられた敵のリストを基に今回の敵を決める、
    /// 汎用的な相性で敵を集めてリストで返す静的関数
    /// </summary>
    public static BattleGroup EnemyCollectAI(List<NormalEnemy> targetList)
    {
        List<NormalEnemy> ResultList = new List<NormalEnemy>();//返す用のリスト
        PartyProperty ourImpression = PartyProperty.TrashGroup;//初期値は馬鹿共
        
        //最初の一人はランダムで選ぶ
        var rndIndex = Random.Range(0, targetList.Count - 1);//ランダムインデックス指定
        var ReferenceOne= targetList[rndIndex];//抽出
        targetList.RemoveAt(rndIndex);//削除
        ResultList.Add(ReferenceOne);//追加

        //数判定(一人判定)　
        if(NormalEnemy.LonelyMatchUp(ReferenceOne.MyImpression)){

            //パーティー属性を決める　一人なのでその一人の属性をそのままパーティー属性にする
            ourImpression = NormalEnemy.EnemyLonelyPartyImpression[ReferenceOne.MyImpression];//()ではなく[]でアクセスすることに注意

            return new BattleGroup(ResultList.Cast<BaseStates>().ToList(),ourImpression) ;//while文に入らずに返す  
        }

        while (true)
        {
            //まず吟味する加入対象をランダムに選ぶ
            var targetIndex = Random.Range(0, targetList.Count - 1);//ランダムでインデックス指定
            int okCount = 0;//適合数 これがResultList.Countと同じになったら加入させる

            for (int i = 0; i < ResultList.Count; i++)//既に選ばれた敵全員との相性を見る
            {//for文で判断しないと現在の配列のインデックスを相性値用の配列のインデックス指定に使えない
                //種別同士の判定 if文内で変数に代入できる
                if ((NormalEnemy.TypeMatchUp(ResultList[i].MyType, targetList[targetIndex].MyType)) )
                {
                    //属性同士の判定　上クリアしたら
                    if ((NormalEnemy.ImpressionMatchUp(ResultList[i].MyImpression, targetList[targetIndex].MyImpression)) )
                    {
                        okCount++;//適合数を増やす
                    }
                }
            }
            //foreachで全員との相性を見たら、加入させる。
            if (okCount == ResultList.Count)//全員との相性が合致したら
            {
                ResultList.Add(targetList[targetIndex]);//結果のリストに追加
                targetList.RemoveAt(targetIndex);//候補リストから削除
            }

            //数判定
            if (ResultList.Count == 1)//一人だったら(まだ一人も見つけれてない場合)
            {
                if (RandomEx.Shared.NextInt(100) < 88)//88%の確率で一人で終わる計算に入る。
                {
                    //数判定(一人判定)　
                    if(NormalEnemy.LonelyMatchUp(ReferenceOne.MyImpression)){

                        //パーティー属性を決める　一人なのでその一人の属性をそのままパーティー属性にする
                        ourImpression = NormalEnemy.EnemyLonelyPartyImpression[ReferenceOne.MyImpression];//()ではなく[]でアクセスすることに注意

                        break;
                    }
                }
            }

            if (ResultList.Count == 2)//二人だったら三人目の加入を決める
            {
                if (RandomEx.Shared.NextInt(100) < 65)//この確率で終わる。
                {
                    //パーティー属性を決める
                    ourImpression = NormalEnemy.calculatePartyProperty(ResultList);
                    break;
                }
            }
            
            if(ResultList.Count>=3)
            {
                //パーティー属性を決める
                ourImpression = NormalEnemy.calculatePartyProperty(ResultList);
                break;//三人になったら強制終了
            } 
    }

        

        return new BattleGroup(ResultList.Cast<BaseStates>().ToList(), ourImpression);//バトルグループを制作 
        }    
}
