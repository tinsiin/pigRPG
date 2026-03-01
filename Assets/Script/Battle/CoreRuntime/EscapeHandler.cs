using System;
using System.Collections.Generic;

public sealed class EscapeHandler
{
    private readonly BattleActionContext _context;
    private readonly TurnExecutor _turnExecutor;
    private readonly float _stageEscapeRate;
    private readonly Action<List<NormalEnemy>> _onGroupEscape;

    public EscapeHandler(
        BattleActionContext context,
        TurnExecutor turnExecutor,
        float stageEscapeRate,
        Action<List<NormalEnemy>> onGroupEscape = null)
    {
        _context = context;
        _turnExecutor = turnExecutor;
        _stageEscapeRate = stageEscapeRate;
        _onGroupEscape = onGroupEscape;
    }

    public TabState EscapeACT()
    {
        if (_context.ActerFaction == Faction.Ally)
        {
            var rate = _stageEscapeRate;
            switch (_context.AllyGroup.Ours.Count)
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
                    _context.Logger.LogWarning("味方のグループが三人以上_EscapeACTにて検出された");
                    break;
            }

            if (RollPercent(rate))
            {
                _context.StateManager.MarkAlliesRunOut();
                _context.Logger.Log("逃げた");
            }
            else
            {
                _context.Logger.Log("逃げ失敗");
            }
        }
        else
        {
            if (RollPercent(50))
            {
                var voluntaryRunOutEnemy = _context.Acter as NormalEnemy;
                _context.Logger.Log("敵は逃げた");

                // 最初の逃走者を除去（相性値テーブルは除去後も参照可能）
                _context.EnemyGroup.EscapeAndRemove(voluntaryRunOutEnemy);

                // 連鎖判定（即時解決。ローカルリストに収集）
                var chainEscapers = CollectChainEscapers(voluntaryRunOutEnemy);

                // グループ逃走判定: 連鎖者1人以上（= 合計2人以上逃走）→ コンビ登録（×2.0）
                if (chainEscapers.Count >= 1)
                {
                    var allEscapers = new List<NormalEnemy> { voluntaryRunOutEnemy };
                    allEscapers.AddRange(chainEscapers);
                    _onGroupEscape?.Invoke(allEscapers);
                }

                // 連鎖者を即時除去
                foreach (var enemy in chainEscapers)
                {
                    _context.EnemyGroup.EscapeAndRemove(enemy);
                    _context.Logger.Log("敵は連鎖逃走");
                }
            }
            else
            {
                _context.Logger.Log("敵は逃げ失敗");
            }
        }

        _turnExecutor.NextTurn(true);
        return _turnExecutor.ACTPop();
    }

    /// <summary>
    /// 連鎖逃走する敵を判定し、ローカルリストとして返す。
    /// 相性値≥77で無条件連鎖、それ以外は精神属性ごとの確率判定。
    /// </summary>
    private List<NormalEnemy> CollectChainEscapers(NormalEnemy voluntaryRunOutEnemy)
    {
        var result = new List<NormalEnemy>();
        foreach (var remainingEnemy in _context.EnemyGroup.Ours)
        {
            // 逃げた敵に対する相性値が高ければ連鎖逃走
            if (_context.EnemyGroup.CharaCompatibility[(remainingEnemy, voluntaryRunOutEnemy)] >= 77)
            {
                result.Add(remainingEnemy as NormalEnemy);
                continue;
            }
            // キャラクター属性による逃走判定
            if (RollPercent(GetRunOutRateByCharacterImpression(remainingEnemy.MyImpression)))
            {
                result.Add(remainingEnemy as NormalEnemy);
            }
        }
        return result;
    }

    private static float GetRunOutRateByCharacterImpression(SpiritualProperty property)
    {
        return property switch
        {
            SpiritualProperty.LiminalWhiteTile => 55,
            SpiritualProperty.Kindergarten => 80,
            SpiritualProperty.Sacrifaith => 5,
            SpiritualProperty.Cquiest => 25,
            SpiritualProperty.Devil => 40,
            SpiritualProperty.Doremis => 40,
            SpiritualProperty.Pillar => 10,
            SpiritualProperty.GodTier => 50,
            SpiritualProperty.BaleDrival => 60,
            SpiritualProperty.Psycho => 100,
            SpiritualProperty.None => 0,
            _ => 0
        };
    }

    private bool RollPercent(float percentage)
    {
        if (percentage < 0) percentage = 0;
        return _context.Random.NextFloat(100) < percentage;
    }
}
