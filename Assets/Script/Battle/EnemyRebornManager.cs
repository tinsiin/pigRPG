using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyRebornState
{
    Idle,
    Counting,
    Ready,
    Reborned
}

public sealed class EnemyRebornManager
{
    private sealed class RebornInfo
    {
        public int RemainingSteps;
        public int LastProgress;
        public EnemyRebornState State;
    }

    private readonly Dictionary<NormalEnemy, RebornInfo> _infos = new();

    public static EnemyRebornManager Instance { get; } = new EnemyRebornManager();

    private EnemyRebornManager()
    {
    }

    public void OnBattleEnd(IReadOnlyList<NormalEnemy> enemies, int globalSteps)
    {
        if (enemies == null || enemies.Count == 0) return;

        for (var i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy == null) continue;
            if (!enemy.Death()) continue;
            if (!enemy.Reborn || enemy.broken) continue;
            ReadyRecovelyStep(enemy, globalSteps);
        }
    }

    public void ReadyRecovelyStep(NormalEnemy enemy, int globalSteps)
    {
        if (enemy == null) return;
        var info = GetOrCreate(enemy);
        info.LastProgress = globalSteps;
        info.RemainingSteps = enemy.RecovelySteps;
        info.State = EnemyRebornState.Counting;
    }

    public bool CanReborn(NormalEnemy enemy, int globalSteps)
    {
        if (enemy == null) return false;

        if (!_infos.TryGetValue(enemy, out var info))
        {
            Debug.Log($"{enemy.CharacterName} is eligible to reborn (recovelyStepCount <= 0).");
            return true;
        }

        if (info.RemainingSteps <= 0)
        {
            info.State = EnemyRebornState.Ready;
            Debug.Log($"{enemy.CharacterName} is eligible to reborn (recovelyStepCount <= 0).");
            return true;
        }

        var distanceTraveled = Math.Abs(globalSteps - info.LastProgress);
        if ((info.RemainingSteps -= distanceTraveled) <= 0)
        {
            info.RemainingSteps = 0;
            info.State = EnemyRebornState.Reborned;
            enemy.OnReborn();
            return true;
        }

        info.LastProgress = globalSteps;
        info.State = EnemyRebornState.Counting;
        return false;
    }

    public void Clear(NormalEnemy enemy)
    {
        if (enemy == null) return;
        _infos.Remove(enemy);
    }

    private RebornInfo GetOrCreate(NormalEnemy enemy)
    {
        if (_infos.TryGetValue(enemy, out var info)) return info;

        info = new RebornInfo
        {
            RemainingSteps = -1,
            LastProgress = -1,
            State = EnemyRebornState.Idle
        };
        _infos.Add(enemy, info);
        return info;
    }
}
