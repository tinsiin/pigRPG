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
        var skill = _context.Acter?.NowUseSkill;
        if (skill != null && skill.AggressiveOnStock.isAggressiveCommit)
        {
            _context.BeVanguard(_context.Acter);
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
