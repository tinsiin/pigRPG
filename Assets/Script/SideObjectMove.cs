using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LitMotion;
using LitMotion.Extensions;
using UnityEngine.Serialization;


public class SideObjectMove : MonoBehaviour
{
    [SerializeField]public Vector2 bornPos;//生成時の位置
    [SerializeField]public Vector2 bornScaleXY;//生成時のサイズ
    [SerializeField]public Vector2 pos;//標準の位置
    [SerializeField]public Vector2 scaleXY;//標準のサイズ
    
    [SerializeField]private float speed;//移動速度

    private RectTransform _thisRect;
    //widthとheightは基本的に線の描画するキャンバスとして固定にするので、スクリプトで変更することはない。

    private void Start()
    {
        _thisRect = GetComponent<RectTransform>();
        LMotion.Create(bornPos, pos, speed)
            .WithEase(Ease.Linear)
            .BindToAnchoredPosition(_thisRect)
            .AddTo(this);
        
        LMotion.Create(bornScaleXY, scaleXY, speed)
            .WithEase(Ease.Linear)
            .BindToLocalScaleXY(_thisRect)
            .AddTo(this);
    }
}
