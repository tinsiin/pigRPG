using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TabCharaStateContent : MonoBehaviour
{
    [SerializeField]
    GameObject geino;
    [SerializeField]
    GameObject sites;
    [SerializeField]
    GameObject normalia;

    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }


    public void SwitchContent(SkillUICharaState state)
    {
        switch (state)
        {
            case SkillUICharaState.geino:
                geino.SetActive(true);
                sites.SetActive(false);
                normalia.SetActive(false);
                break;
            case SkillUICharaState.sites:
                geino.SetActive(false);
                sites.SetActive(true);
                normalia.SetActive(false);
                break;
            case SkillUICharaState.normalia:
                geino.SetActive(false);
                sites.SetActive(false);
                normalia.SetActive(true);
                break;
        }
    }
}
