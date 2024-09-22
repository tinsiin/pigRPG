using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class Stages:ScriptableObject
{//�X�e�[�W�ɂ܂�镨������������Ăяo������(�X�e�[�W�f�[�^�x�[�X??
    public List<StageData> StageDates;//�X�e�[�W�̃f�[�^�x�[�X�̃��X�g     
    [SerializeField,TextArea(1,30)] string memo;

}

/// <summary>
/// �X�e�[�^�X�{�[�i�X�̃N���X �X�e�[�W���Ƃɓo�^����B
/// </summary>
[Serializable]
public class StatesBonus
{
    public int ATKBpunus;
    public int DEFBonus;
    public int AGIBonus;
    public int HITBonus;
    public int HPBonus;
    public int PBonus;
    public int RecovelyTurnMinusBonus;
}

[System.Serializable]
public class StageData//�X�e�[�W�f�[�^�̃N���X
{
    [SerializeField]string _stageName;
    [SerializeField] List<StageCut> _cutArea;


    /// <summary>
    /// �X�e�[�W�̖��O
    /// </summary>
    public string StageName => _stageName;//�����_���œǂݎ���p

    /// <summary>
    /// �X�e�[�W���������ɂ������X�g
    /// </summary>
    public IReadOnlyList<StageCut> CutArea => _cutArea;

    /// <summary>
    /// �X�e�[�W���Ƃɐݒ肳����l���w�c�����̃{�[�i�X�B
    /// </summary>
    public StatesBonus Satelite_StageBonus;
    /// <summary>
    /// �X�e�[�W���Ƃɐݒ肳����l���w�c�����̃{�[�i�X�B
    /// </summary>
    public StatesBonus Bass_StageBonus;
    /// <summary>
    /// �X�e�[�W���Ƃɐݒ肳����l���w�c�����̃{�[�i�X�B
    /// </summary>
    public StatesBonus Stair_StageBonus;
    
}
/// <summary>
/// ����ɑΉ����邽�߁A�X�e�[�W���������ɂ������́B
/// </summary>
/// 
[System.Serializable]
public class StageCut
{
    [SerializeField]string _areaName;
    [SerializeField]int _id;
    [SerializeField]List<AreaDate> _areaDates;//���ԃG���A
    [SerializeField] Vector2 _mapLineS;
    [SerializeField] Vector2 _mapLineE;
    [SerializeField] string _mapsrc;
    [SerializeField] List<NormalEnemy> _enemyList;//�G�̃��X�g


    /// <summary>
    /// �����������G���A�̖��O
    /// </summary>
    public string AreaName => _areaName;


    /// <summary>
    /// �}�b�v�摜�ɒ�`���钼���̎n�_�@nowimg��anchoredPosition�𒼐ړ���
    /// </summary>
    public Vector2 MapLineS => _mapLineS;
    /// <summary>
    /// �}�b�v�摜�ɒ�`���钼���̏I�_�@nowimg��anchoredPosition�𒼐ړ���
    /// </summary>
    public Vector2 MapLineE => _mapLineE;

    /// <summary>
    /// �������ɂ����G���A��ID
    /// </summary>
    public int Id => _id;
    /// <summary>
    /// �G���A���Ƃ̊ȈՃ}�b�v�̉摜�B
    /// </summary>
    public string MapSrc => _mapsrc;

    /// <summary>
    /// ���ׂ�G���A�̃f�[�^
    /// </summary>
    public IReadOnlyList<AreaDate> AreaDates => _areaDates;
    /// <summary>
    /// �G�̃��X�g
    /// </summary>
    public IReadOnlyList<BaseStates> EnemyList => _enemyList;

}
/// <summary>
/// �X�e�[�W�ɕ��ԃG���A�f�[�^
/// </summary>
[System.Serializable]
public class AreaDate
{
    
    [SerializeField] bool _rest;
    [SerializeField] string _backsrc;
    [SerializeField] string _nextID;
    [SerializeField] string _nextIDString;
    [SerializeField] string _nextStageID;

    /// <summary>
    /// ���̃X�e�[�W��id�A���͂���ĂȂ��Ȃ�X���[
    /// string.split��string[]�Ɋi�[���ĕ���ł���B
    /// �u,�v�ŋ�؂��ē��́B
    /// </summary>
    public string NextStageID => _nextStageID;
    /// <summary>
    /// �x�e�n�_���ǂ���
    /// </summary>
    public bool Rest => _rest;
    /// <summary>
    /// �w�i�摜�̃t�@�C����
    /// </summary>
    public string BackSrc => _backsrc;
    /// <summary>
    /// ���̃G���AID�A���͂���ĂȂ��Ȃ�X���[
    /// string.split��string[]�Ɋi�[���ĕ���ł���B
    /// �u,�v�ŋ�؂��ē��́B
    /// </summary>
    public string NextID => _nextID;/// <summary>
    /// ���̃G���A�I�����̃{�^�����́A���͂���ĂȂ��Ȃ�X���[
    /// string.split��string[]�Ɋi�[���ĕ���ł���B
    /// �u,�v�ŋ�؂��ē��́B
    /// </summary>
    public string NextIDString => _nextIDString;

}

