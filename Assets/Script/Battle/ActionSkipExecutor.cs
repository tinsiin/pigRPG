public sealed class ActionSkipExecutor
{
    private readonly BattleManager _manager;

    public ActionSkipExecutor(BattleManager manager)
    {
        _manager = manager;
    }

    public TabState SkillStockACT()
    {
        var skill = _manager.Acter?.NowUseSkill;
        if (skill != null && skill.IsStockAgressiveCommit)
        {
            _manager.BeVanguard(_manager.Acter);
        }

        _manager.SkillStock = false;
        _manager.NextTurn(true);
        return _manager.ACTPop();
    }

    public TabState PassiveCancelACT()
    {
        _manager.PassiveCancel = false;
        _manager.NextTurn(true);
        return _manager.ACTPop();
    }

    public TabState DoNothingACT()
    {
        _manager.DoNothing = false;
        _manager.NextTurn(true);
        return _manager.ACTPop();
    }
}
