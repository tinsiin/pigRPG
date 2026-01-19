using System;
using UnityEngine;

[Serializable]
public struct FixedSideObjectPair
{
    [SerializeField] private SideObjectSO left;
    [SerializeField] private SideObjectSO right;

    public SideObjectSO Left => left;
    public SideObjectSO Right => right;

    public bool HasLeft => left != null;
    public bool HasRight => right != null;
    public bool HasAny => HasLeft || HasRight;
}
