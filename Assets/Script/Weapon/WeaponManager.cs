using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public static WeaponManager Instance;

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

    [SerializeReference, SelectableSerializeReference]//武器を管理するリスト
    private List<BaseWeapon> _masterList;
    /// <summary>
    /// 武器をIDで入手
    /// </summary>
    public BaseWeapon GetAtID(int id)
    {
        return _masterList.FirstOrDefault(weapon => weapon.id == id);//idが一致する最初アイテムを入手
    }
}
