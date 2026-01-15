using Cysharp.Threading.Tasks;

public interface IBattleRunner
{
    UniTask<BattleResult> RunBattleAsync(EncounterContext context);
}
