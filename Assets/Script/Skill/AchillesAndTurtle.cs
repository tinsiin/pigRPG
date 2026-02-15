using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static CommonCalc;

public class AchillesAndTurtle : BaseSkill//アキレスと亀- 混沌時間
{


    public override void ManualSkillEffect(BaseStates actor, BaseStates target,HitResult hitResult)//命中成功して、実行時の効果の本体
    {
        //相手がTLOAの種別なら発生しない
        if(target.MyType == CharacterType.TLOA) return;
        //相手が精神属性デビルなら、熱血的理由(ベールとキンダーはそれぞれ幼稚すぎ、はっちゃけ過ぎの理由で省かれる)で発生しない
        if(target.MyImpression == SpiritualProperty.devil) return;
        //完全回避ならなし
        if(hitResult == HitResult.CompleteEvade) return;
        //かすりならば、4.8割の確率で発生 = 5.2割で発生しない
        if(hitResult == HitResult.Graze && rollper(52)) return;
        //クリティカルとHItに何の違いもない。

        const int TurtleID =22;
        const int AchillesID = 23;
        //自分に亀を付与
        actor.ApplyPassiveBufferInBattleByID(TurtleID);
        //相手にアキレスを付与
        target.ApplyPassiveBufferInBattleByID(AchillesID);

        //パッシブリンクさせる。
        var turtlePas = actor.GetBufferPassiveByID(TurtleID);
        var achillesPas = target.GetBufferPassiveByID(AchillesID);
        turtlePas.SetPassiveLink(new LinkedPassive(achillesPas, true));
        achillesPas.SetPassiveLink(new LinkedPassive(turtlePas, true));

        //亀のパッシブにアキレス限定の回避率を登録
        turtlePas.AddEvasionPercentageModifierByAttacker(target,1.31f);//31%上昇
    }

}
