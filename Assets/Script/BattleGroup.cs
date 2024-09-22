using System.Collections;
using System.Collections.Generic;
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
        List<BaseStates> ResultList = new List<BaseStates>();//返す用のリスト
        PartyProperty ourImpression = PartyProperty.TrashGroup;//初期値は馬鹿共

        //最初の一人はランダムで選ぶ
        var rndIndex = Random.Range(0, targetList.Count - 1);//ランダムインデックス指定
        var ReferenceOne= targetList[rndIndex];//抽出
        targetList.RemoveAt(rndIndex);//削除
        ResultList.Add(ReferenceOne);//追加

        //数判定(一人判定)　ここのif処理でwhileを含む
        if(NormalEnemy.LonelyMatchUp(ReferenceOne.MyImpression)>0){

            //パーティー属性を決める　一人なのでその一人の属性をそのままパーティー属性にする

            return new BattleGroup(ResultList,ourImpression) ;//一人だけの場合はそのまま返す      
        }

        while (true)
        {
            //まず吟味する加入対象をランダムに選ぶ
            var targetIndex = Random.Range(0, targetList.Count - 1);//ランダムでインデックス指定
            int TypePer;//種別の相性値
            int ImpPer;//印象の相性値
            int okCount = 0;//適合数

            foreach(var one in ResultList)//既に選ばれた敵全員との相性を見る
            {
                //種別同士の判定 if文内で変数に代入できる
                if ((TypePer = NormalEnemy.TypeMatchUp(one.MyType, targetList[targetIndex].MyType)) > 0)
                {
                    //属性同士の判定　上クリアしたら
                    if ((ImpPer = NormalEnemy.ImpressionMatchUp(one.MyImpression, targetList[targetIndex].MyImpression)) > 0)
                    {
                        okCount++;//適合数を増やす
                    }
                }
            }
            //foreachで全員との相性を見たら、加入させる。
            if (okCount == ResultList.Count)//全員との相性が合致したら
            {
                ResultList.Add(targetList[targetIndex]);//追加
                targetList.RemoveAt(targetIndex);//削除
            }

            //数判定

            if(ResultList.Count>=3) break;//三人になったら強制終了

        }

        return new BattleGroup(ResultList, ourImpression);
    }    
}
