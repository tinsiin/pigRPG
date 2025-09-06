using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class PassiveManager : MonoBehaviour
{
    public static PassiveManager Instance;

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

    [SerializeReference, SelectableSerializeReference]//パッシブを管理するリスト
    private List<BasePassive> _masterList;
    /// <summary>
    /// パッシブをIDで入手
    /// </summary>
    public BasePassive GetAtID(int id)
    {
        return _masterList.FirstOrDefault(pas => pas.ID == id);//idが一致する最初アイテムを入手
    }

}
