using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// �퓬�̊Ǘ��N���X
/// </summary>
public class BatlleManager
{
    /// <summary>
    /// �����O���[�v
    /// </summary>
    BattleGroup Alliy;
    /// <summary>
    /// �G�O���[�v
    /// </summary>
    BattleGroup Enemy;
    public BatlleManager(BattleGroup ali,BattleGroup ene)
    {
         Alliy = ali;
        Enemy = ene;
    }
}
