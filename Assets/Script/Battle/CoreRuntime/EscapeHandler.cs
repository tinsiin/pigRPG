public sealed class EscapeHandler
{
    private readonly BattleActionContext _context;
    private readonly TurnExecutor _turnExecutor;
    private readonly float _stageEscapeRate;

    public EscapeHandler(
        BattleActionContext context,
        TurnExecutor turnExecutor,
        float stageEscapeRate)
    {
        _context = context;
        _turnExecutor = turnExecutor;
        _stageEscapeRate = stageEscapeRate;
    }

    public TabState EscapeACT()
    {
        if (_context.ActerFaction == allyOrEnemy.alliy)
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
                _context.StateManager.SetVoluntaryRunOutEnemy(voluntaryRunOutEnemy);
                _context.EnemyGroup.EscapeAndRemove(voluntaryRunOutEnemy);
                _context.Logger.Log("敵は逃げた");
                GetRunOutEnemies(voluntaryRunOutEnemy);
            }
            else
            {
                _context.Logger.Log("敵は逃げ失敗");
            }
        }

        _turnExecutor.NextTurn(true);
        return _turnExecutor.ACTPop();
    }

    public TabState DominoEscapeACT()
    {
        foreach (var enemy in _context.DominoRunOutEnemies)
        {
            _context.EnemyGroup.EscapeAndRemove(enemy);
            _context.Logger.Log("敵は連鎖逃走");
        }

        _context.StateManager.ClearDominoRunOutEnemies();
        _turnExecutor.NextTurn(true);
        return _turnExecutor.ACTPop();
    }

    /// <summary>
    /// 連鎖逃走する敵を取得し、連鎖逃走リストに追加
    /// </summary>
    private void GetRunOutEnemies(NormalEnemy voluntaryRunOutEnemy)
    {
        foreach (var remainingEnemy in _context.EnemyGroup.Ours)
        {
            // 逃げた敵に対する相性値が高ければ連鎖逃走
            if (_context.EnemyGroup.CharaCompatibility[(remainingEnemy, voluntaryRunOutEnemy)] >= 77)
            {
                _context.StateManager.AddDominoRunOutEnemy(remainingEnemy as NormalEnemy);
                continue;
            }
            // キャラクター属性による逃走判定
            if (RollPercent(GetRunOutRateByCharacterImpression(remainingEnemy.MyImpression)))
            {
                _context.StateManager.AddDominoRunOutEnemy(remainingEnemy as NormalEnemy);
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

    private bool RollPercent(float percentage)
    {
        if (percentage < 0) percentage = 0;
        return _context.Random.NextFloat(100) < percentage;
    }
}
