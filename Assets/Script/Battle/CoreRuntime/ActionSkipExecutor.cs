public sealed class ActionSkipExecutor
{
    private readonly BattleActionContext _context;
    private readonly TurnExecutor _turnExecutor;

    public ActionSkipExecutor(
        BattleActionContext context,
        TurnExecutor turnExecutor)
    {
        _context = context;
        _turnExecutor = turnExecutor;
    }

    public TabState SkillStockACT()
    {
        var acter = _context.Acter;
        var skill = acter?.NowUseSkill;
        if (acter == null || skill == null)
        {
            _context.SkillStock = false;
            _turnExecutor.NextTurn(true);
            return _turnExecutor.ACTPop();
        }

        // 満杯チェック（防御的。UI側でも弾いているが念のため）
        if (skill.IsFullStock())
        {
            _context.Logger.Log(skill.SkillName + "のストックが満杯のためスキップ。");
            _context.SkillStock = false;
            _turnExecutor.NextTurn(true);
            return _turnExecutor.ACTPop();
        }

        // ストックロジック（ここに集約）
        skill.ATKCountStock();
        _context.Logger.Log(skill.SkillName + "をストックしました。");

        // 他のStockpileスキルを忘れさせる
        foreach (var other in acter.SkillList)
        {
            if (!ReferenceEquals(other, skill) && other.HasConsecutiveType(SkillConsecutiveType.Stockpile))
            {
                other.ForgetStock();
            }
        }

        // 前のめり判定
        if (skill.AggressiveOnStock.isAggressiveCommit)
        {
            _context.BeVanguard(acter);
        }

        _context.SkillStock = false;
        _turnExecutor.NextTurn(true);
        return _turnExecutor.ACTPop();
    }

    public TabState PassiveCancelACT()
    {
        _context.PassiveCancel = false;
        _turnExecutor.NextTurn(true);
        return _turnExecutor.ACTPop();
    }

    public TabState DoNothingACT()
    {
        _context.DoNothing = false;
        _turnExecutor.NextTurn(true);
        return _turnExecutor.ACTPop();
    }
}
