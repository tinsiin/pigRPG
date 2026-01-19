using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Walk/SFXDatabase")]
public sealed class SFXDatabaseSO : ScriptableObject
{
    [SerializeField] private SFXEntry[] entries;

    private Dictionary<string, AudioClip> lookup;

    public AudioClip GetClip(string sfxId)
    {
        if (string.IsNullOrEmpty(sfxId)) return null;

        if (lookup == null)
        {
            BuildLookup();
        }

        return lookup.TryGetValue(sfxId, out var clip) ? clip : null;
    }

    private void BuildLookup()
    {
        lookup = new Dictionary<string, AudioClip>();
        if (entries == null) return;

        foreach (var entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.Id) || entry.Clip == null) continue;
            lookup[entry.Id] = entry.Clip;
        }
    }

    private void OnValidate()
    {
        lookup = null;
    }
}

[Serializable]
public sealed class SFXEntry
{
    [SerializeField] private string id;
    [SerializeField] private AudioClip clip;

    public string Id => id;
    public AudioClip Clip => clip;
}
