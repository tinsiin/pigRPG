using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class VitalLayerManager : MonoBehaviour
{
    public static VitalLayerManager Instance;

    void Awake()//シングルトンオブジェクト
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

    [SerializeReference, SelectableSerializeReference]//追加HPを管理するリスト
    private List<BaseVitalLayer> _masterList;
    /// <summary>
    /// 追加HPをIDで入手
    /// </summary>
    public BaseVitalLayer GetAtID(int id)
    {
        return _masterList.FirstOrDefault(lay => lay.id == id);//idが一致する最初アイテムを入手
    }
}
