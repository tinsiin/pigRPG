using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
[DefaultExecutionOrder(-1)]
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
    /// フリーハンド武器（リストとは別枠で管理）
    /// </summary>
    [SerializeReference, SelectableSerializeReference]
    private BaseWeapon _freehandWeapon;

    /// <summary>
    /// 武器をIDで入手（フリーハンドも含む）
    /// </summary>
    public BaseWeapon GetAtID(int id)
    {
        if (_freehandWeapon != null && _freehandWeapon.id == id)
            return _freehandWeapon;
        return _masterList.FirstOrDefault(weapon => weapon.id == id);//idが一致する最初アイテムを入手
    }
    [System.NonSerialized] private List<BaseWeapon> _allWeaponsCache;

    /// <summary>
    /// 全武器の読み取り専用リスト（フリーハンド含む）
    /// </summary>
    public IReadOnlyList<BaseWeapon> AllWeapons
    {
        get
        {
            if (_allWeaponsCache == null)
                RebuildAllWeaponsCache();
            return _allWeaponsCache;
        }
    }

    /// <summary>
    /// AllWeaponsキャッシュを再構築する。武器リストが変更された場合に呼ぶ。
    /// </summary>
    public void RebuildAllWeaponsCache()
    {
        _allWeaponsCache = new List<BaseWeapon>();
        if (_masterList != null)
            _allWeaponsCache.AddRange(_masterList);
        if (_freehandWeapon != null)
            _allWeaponsCache.Add(_freehandWeapon);
    }

    /// <summary>
    /// フリーハンド武器を取得
    /// </summary>
    public BaseWeapon GetFreehandWeapon() => _freehandWeapon;
}
