using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PassiveManager : MonoBehaviour
{
    public static PassiveManager Instance;

    void Awake()//�V���O���g���I�u�W�F�N�g
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

    }

    [SerializeReference, SelectableSerializeReference]//�p�b�V�u���Ǘ����郊�X�g
    private List<BasePassive> _masterList;
    /// <summary>
    /// �p�b�V�u��ID�œ���
    /// </summary>
    public BasePassive GetAtID(int id)
    {
        return _masterList.FirstOrDefault(pas => pas.ID == id);//id����v����ŏ��A�C�e�������
    }

}
