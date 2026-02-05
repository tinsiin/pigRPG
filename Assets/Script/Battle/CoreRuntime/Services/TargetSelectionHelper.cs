using System.Collections.Generic;
using System.Linq;

public static class TargetSelectionHelper
{
    public static List<BaseStates> SelectByPassiveAndRandom(
        IEnumerable<BaseStates> candidates,
        int want,
        IBattleRandom random,
        IBattleLogger logger,
        int nextChancePercent = 100,
        bool debugSelectLog = false)
    {
        random ??= new SystemBattleRandom();
        logger ??= new NoOpBattleLogger();
        var pool = candidates.ToList();
        var winners = new List<BaseStates>();
        if (want <= 0 || pool.Count == 0)
        {
            if (debugSelectLog)
                logger.Log($"[SelectByPassiveAndRandom] 早期終了: 要求数={want}, 候補数={pool.Count}, 当選数={winners.Count}（選出不要または候補なし）");
            return winners;
        }

        var positives = pool
            .Where(u => u.PassivesTargetProbability() > 0)
            .OrderByDescending(u => u.PassivesTargetProbability())
            .ToList();
        foreach (var u in positives)
        {
            if (winners.Count >= want) break;
            if (RollPercent(random, u.PassivesTargetProbability()))
                winners.Add(u);
        }
        if (winners.Count >= want)
        {
            if (debugSelectLog)
                logger.Log($"[SelectByPassiveAndRandom] Positive選抜で終了: 要求数={want}, 当選数={winners.Count}/{pool.Count}");
            return winners;
        }

        var negatives = pool.Where(u => u.PassivesTargetProbability() < 0).ToList();
        var excludedByNegative = new HashSet<BaseStates>();
        foreach (var u in negatives)
        {
            if (RollPercent(random, -u.PassivesTargetProbability()))
                excludedByNegative.Add(u);
        }
        if (debugSelectLog)
            logger.Log($"[SelectByPassiveAndRandom] Negative除外結果: ネガ対象数={negatives.Count}, 除外確定数={excludedByNegative.Count}");

        var others = pool
            .Except(winners)
            .Where(u => !excludedByNegative.Contains(u))
            .ToList();
        if (others.Count == 0 && winners.Count < want)
        {
            if (debugSelectLog)
                logger.Log($"[SelectByPassiveAndRandom] ランダム抽選スキップ: 候補0, 当選数={winners.Count}, 要求数={want}");
        }
        if (others.Count > 0 && winners.Count < want)
        {
            var weightedItems = new List<BaseStates>();
            var weights = new List<float>();
            foreach (var u in others)
            {
                var w = u.PassivesTargetProbability();
                if (w <= 0) w = 1;
                weightedItems.Add(u);
                weights.Add(w);
            }
            if (debugSelectLog)
                logger.Log($"[SelectByPassiveAndRandom] ランダム抽選開始: 候補={others.Count}, 重み数={weightedItems.Count}, 当選数={winners.Count}, 要求数={want}, 継続率={nextChancePercent}%");
            do
            {
                if (weightedItems.Count == 0) break;
                var item = PickWeighted(weightedItems, weights, random);
                RemoveWeightedItem(weightedItems, weights, item);
                winners.Add(item);
            } while (weightedItems.Count > 0 && winners.Count < want && random.NextInt(100) <= nextChancePercent);
            if (debugSelectLog)
                logger.Log($"[SelectByPassiveAndRandom] ランダム抽選終了: 当選数={winners.Count}, 残り重み={weightedItems.Count}");
        }

        if (winners.Count < want && nextChancePercent == 100)
        {
            var negPicked = negatives.Where(excludedByNegative.Contains).ToList();
            random.Shuffle(negPicked);
            var before = winners.Count;
            foreach (var u in negPicked)
            {
                if (winners.Count >= want) break;
                winners.Add(u);
            }
            if (debugSelectLog)
                logger.Log($"[SelectByPassiveAndRandom] ネガ補充: 追加数={winners.Count - before}, 当選数={winners.Count}, 要求数={want}");
        }
        if (debugSelectLog)
            logger.Log($"[SelectByPassiveAndRandom] 最終返却: 要求数={want}, 当選数={winners.Count}/{pool.Count}, 継続率={nextChancePercent}%");
        return winners;
    }

    private static bool RollPercent(IBattleRandom random, float percentage)
    {
        if (percentage < 0) percentage = 0;
        return random.NextFloat(100) < percentage;
    }

    private static BaseStates PickWeighted(IReadOnlyList<BaseStates> items, IReadOnlyList<float> weights, IBattleRandom random)
    {
        if (items == null || items.Count == 0) return null;
        var total = 0f;
        for (var i = 0; i < weights.Count; i++)
        {
            var weight = weights[i];
            if (weight <= 0) continue;
            total += weight;
        }
        if (total <= 0f)
        {
            return random.GetItem(items);
        }
        var roll = random.NextFloat(total);
        var cumulative = 0f;
        for (var i = 0; i < items.Count; i++)
        {
            var weight = i < weights.Count ? weights[i] : 1f;
            if (weight <= 0) continue;
            cumulative += weight;
            if (roll <= cumulative)
            {
                return items[i];
            }
        }
        return items[items.Count - 1];
    }

    private static void RemoveWeightedItem(List<BaseStates> items, List<float> weights, BaseStates item)
    {
        if (items == null || weights == null || item == null) return;
        var index = items.IndexOf(item);
        if (index < 0) return;
        items.RemoveAt(index);
        if (index < weights.Count)
        {
            weights.RemoveAt(index);
        }
    }
}
