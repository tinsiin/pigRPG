using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VitalLayerManager : MonoBehaviour
{
    public static VitalLayerManager Instance;

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

    [SerializeReference, SelectableSerializeReference]//�ǉ�HP���Ǘ����郊�X�g
    private List<BaseVitalLayer> _masterList;
    /// <summary>
    /// �p�b�V�u��ID�œ���
    /// </summary>
    public BaseVitalLayer GetAtID(int id)
    {
        return _masterList.FirstOrDefault(lay => lay.id == id);//id����v����ŏ��A�C�e�������
    }
}
