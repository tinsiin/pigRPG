using System;

[Serializable]
public struct StageBonus
{
    public int atkBonus;
    public int defBonus;
    public int agiBonus;
    public int hitBonus;
    public int hpBonus;
    public int pBonus;
    public int recovelyTurnMinusBonus;

    public bool IsZero =>
        atkBonus == 0 &&
        defBonus == 0 &&
        agiBonus == 0 &&
        hitBonus == 0 &&
        hpBonus == 0 &&
        pBonus == 0 &&
        recovelyTurnMinusBonus == 0;

    public static StageBonus operator +(StageBonus a, StageBonus b)
    {
        return new StageBonus
        {
            atkBonus = a.atkBonus + b.atkBonus,
            defBonus = a.defBonus + b.defBonus,
            agiBonus = a.agiBonus + b.agiBonus,
            hitBonus = a.hitBonus + b.hitBonus,
            hpBonus = a.hpBonus + b.hpBonus,
            pBonus = a.pBonus + b.pBonus,
            recovelyTurnMinusBonus = a.recovelyTurnMinusBonus + b.recovelyTurnMinusBonus
        };
    }
}
