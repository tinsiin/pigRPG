using UnityEngine;
using static CommonCalc;

public sealed class EscapeHandler
{
    private readonly BattleManager _manager;

    public EscapeHandler(BattleManager manager)
    {
        _manager = manager;
    }

    public TabState EscapeACT()
    {
        if (_manager.CurrentActerFaction == allyOrEnemy.alliy)
        {
            var rate = _manager.StageEscapeRate;
            switch (_manager.AllyGroup.Ours.Count)
            {
                case 1:
                    rate *= 0.5f;
                    break;
                case 2:
                    rate *= 0.96f;
                    break;
                case 3:
                    break;
                default:
                    Debug.LogWarning("味方のグループが三人以上_EscapeACTにて検出された");
                    break;
            }

            if (rollper(rate))
            {
                _manager.StateManager.MarkAlliesRunOut();
                Debug.Log("逃げた");
            }
            else
            {
                Debug.Log("逃げ失敗");
            }
        }
        else
        {
            if (rollper(50))
            {
                var voluntaryRunOutEnemy = _manager.Acter as NormalEnemy;
                _manager.StateManager.SetVoluntaryRunOutEnemy(voluntaryRunOutEnemy);
                _manager.EnemyGroup.EscapeAndRemove(voluntaryRunOutEnemy);
                Debug.Log("敵は逃げた");
                GetRunOutEnemies(voluntaryRunOutEnemy);
            }
            else
            {
                Debug.Log("敵は逃げ失敗");
            }
        }

        _manager.NextTurn(true);
        return _manager.ACTPop();
    }

    public TabState DominoEscapeACT()
    {
        foreach (var enemy in _manager.DominoRunOutEnemies)
        {
            _manager.EnemyGroup.EscapeAndRemove(enemy);
            Debug.Log("敵は連鎖逃走");
        }

        _manager.StateManager.ClearDominoRunOutEnemies();
        _manager.NextTurn(true);
        return _manager.ACTPop();
    }

    /// <summary>
    /// 連鎖逃走する敵を取得し、連鎖逃走リストに追加
    /// </summary>
    private void GetRunOutEnemies(NormalEnemy voluntaryRunOutEnemy)
    {
        foreach (var remainingEnemy in _manager.EnemyGroup.Ours)
        {
            // 逃げた敵に対する相性値が高ければ連鎖逃走
            if (_manager.EnemyGroup.CharaCompatibility[(remainingEnemy, voluntaryRunOutEnemy)] >= 77)
            {
                _manager.StateManager.AddDominoRunOutEnemy(remainingEnemy as NormalEnemy);
                continue;
            }
            // キャラクター属性による逃走判定
            if (rollper(GetRunOutRateByCharacterImpression(remainingEnemy.MyImpression)))
            {
                _manager.StateManager.AddDominoRunOutEnemy(remainingEnemy as NormalEnemy);
            }
        }
    }

    private static float GetRunOutRateByCharacterImpression(SpiritualProperty property)
    {
        return property switch
        {
            SpiritualProperty.liminalwhitetile => 55,
            SpiritualProperty.kindergarden => 80,
            SpiritualProperty.sacrifaith => 5,
            SpiritualProperty.cquiest => 25,
            SpiritualProperty.devil => 40,
            SpiritualProperty.doremis => 40,
            SpiritualProperty.pillar => 10,
            SpiritualProperty.godtier => 50,
            SpiritualProperty.baledrival => 60,
            SpiritualProperty.pysco => 100,
            SpiritualProperty.none => 0,
            _ => 0
        };
    }
}
