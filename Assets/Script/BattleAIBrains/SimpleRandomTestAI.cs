using System;
using UnityEngine;
using RandomExtensions;
using RandomExtensions.Linq;
using RandomExtensions.Collections;
using Unity.VisualScripting;
using R3;
using Cysharp.Threading.Tasks;
using UnityEditor.Experimental.GraphView;
using UnityEngine.Rendering.Universal;
using Mono.Cecil.Cil;
using TMPro;
using UnityEditor;
using static CommonCalc;
using UnityEditor.UIElements;
using System.Threading.Tasks;
using System.Linq;

/// <summary>
/// シンプルにランダムにスキルを選び、スキルが完全単体選択だった場合のみ、単体選択する
/// 複雑な範囲性質はキャラに合わせて考えるため、テスト用では利用しない。　　
/// 完全単体選択のみ実装しているが、基本は「戦闘全体フロー用」のテスト敵ControlByThisSituationで
/// </summary>
[CreateAssetMenu(fileName = "SimpleRandomTestAI", menuName = "BattleAIBrain/SimpleRandomTestAI")]
public class SimpleRandomTestAI : BattleAIBrain
{
    public override void Think()
    {
        base.Think();
        var user = manager.Acter;
        var rndSkill = RandomEx.Shared.GetItem(user.SkillList.ToArray());//一個適当に有効スキルから選ぶだけ
        user.NowUseSkill = rndSkill;//スキルを設定

        if(rndSkill.HasZoneTraitAny(SkillZoneTrait.CanPerfectSelectSingleTarget))
        {
            var target = RandomEx.Shared.GetItem(manager.AllyGroup.Ours.ToArray());
            manager.Acter.Target = DirectedWill.One;//ここで単体選択し、尚且つしたの行動者決定☆の処理を飛ばせる
            manager.unders.CharaAdd(target);//攻撃キャラ決定
        }

    }
}
