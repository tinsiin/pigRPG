using System.Collections.Generic;

/// <summary>
/// 攻撃対象者の一時処方に耐えうる保持リスト
/// </summary>
public class UnderActersEntryList
{
    private readonly IBattleQueryService _queryService;
    private BaseSkill _currentSkill;
    private int _frontIndex;
    private int _backIndex;

    /// <summary>
    /// 対象者キャラクターリスト
    /// </summary>
    public List<BaseStates> charas;
    /// <summary>
    /// 割り当てられる"当てられる"スキルの分散値
    /// </summary>
    List<float> spreadPer;

    public int Count => charas.Count;

    public UnderActersEntryList(IBattleQueryService queryService)
    {
        charas = new List<BaseStates>();
        spreadPer = new List<float>();
        _queryService = queryService;
    }

    public BaseStates GetAtCharacter(int index)
    {
        return charas[index];
    }
    public List<BaseStates> GetCharacterList()
    {
        return charas;
    }
    public float GetAtSpreadPer(int index)
    {
        return spreadPer[index];
    }

    /// <summary>
    /// 現在のスキルを設定（CharaAdd前に呼び出す）
    /// インデックスのみリセット、ターゲットはクリアしない
    /// </summary>
    public void SetCurrentSkill(BaseSkill skill)
    {
        _currentSkill = skill;
        _frontIndex = 0;
        _backIndex = 0;
    }

    /// <summary>
    /// ターゲットリストをクリアしてスキルを設定（UI再選択時用）
    /// </summary>
    public void ClearAndSetCurrentSkill(BaseSkill skill)
    {
        charas.Clear();
        spreadPer.Clear();
        SetCurrentSkill(skill);
    }

    /// <summary>
    /// 既にある対象者リストをそのまま処理。
    /// SetCurrentSkillを先に呼んでおくこと。
    /// </summary>
    public void SetList(List<BaseStates> charas)
    {
        foreach (var chara in charas)
        {
            CharaAdd(chara);
        }
    }

    /// <summary>
    /// スキル対象者を追加し整理する関数
    /// </summary>
    public void CharaAdd(BaseStates chara)
    {
        if (_currentSkill == null)
        {
            spreadPer.Add(1f);
            charas.Add(chara);
            return;
        }

        var spreadValues = _currentSkill.PowerSpread;
        float item = 1f;

        if (spreadValues != null && spreadValues.Length > 0)
        {
            var calculator = TargetDistributionService.GetCalculator(_currentSkill.DistributionType);
            var isVanguard = _queryService?.IsVanguard(chara) ?? false;
            var (ratio, nextFront, nextBack) = calculator.Calculate(
                spreadValues, _frontIndex, _backIndex, isVanguard, charas.Count + 1);
            item = ratio;
            _frontIndex = nextFront;
            _backIndex = nextBack;
        }

        spreadPer.Add(item);
        charas.Add(chara);
    }

    /// <summary>
    /// リストをクリア
    /// </summary>
    public void Clear()
    {
        charas.Clear();
        spreadPer.Clear();
        _currentSkill = null;
        _frontIndex = 0;
        _backIndex = 0;
    }
}
