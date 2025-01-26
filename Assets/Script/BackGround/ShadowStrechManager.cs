using LitMotion;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ShadowStrechManager : MonoBehaviour
{
    private static readonly int PhaseY = Shader.PropertyToID("_PhaseY");
    private static readonly int FreqY = Shader.PropertyToID("_FreqY");
    private static readonly int Speed = Shader.PropertyToID("_Speed");
    [SerializeField]private Image　shadow;//子オブジェクト
    private Material _material;
    private float _normalizedXScale;


    private void Start()
    {
        _material = GetComponent<Image>().material;//materialを取得
        AnimateShadow();
    }
    private void AnimateShadow()
    {
        // シェーダーのプロパティから必要な値を取得
        var speed = _material.GetFloat(Speed);
        var freqY = _material.GetFloat(FreqY);
        var phaseY = _material.GetFloat(PhaseY);


        LMotion.Create(0f,1f,1/speed)
            .WithLoops(-1)
            .Bind(t => {
                _normalizedXScale = 0.5f + 0.5f * Mathf.Sin(t * freqY + phaseY); // WalkWebのyスケールの正規化された値を影の横幅として取得

                shadow.rectTransform.localScale = new Vector3(Mathf.Lerp(1f, 1.07f, _normalizedXScale), 1, 1); // 影の横幅を決める
            })
            .AddTo(this);


        // 時間を使ってnormalizedYScaleをスクリプト側で計算
        //var t = Time.time * speed;
        //var normalizedXScale = 0.5f + 0.5f * Mathf.Sin(t * freqY + phaseY);//WalkWebのyスケールの正規化された値を影の横幅として入手。


        //shadow.rectTransform.localScale = new Vector3(Mathf.Lerp(1f, 1.07f, normalizedXScale), 1, 1);//影の横幅を決める。
        //Debug.Log(normalizedXScale +"←これがオブジェクトのyスケールの正規化された値　これが現在の影のxスケール→" + shadow.rectTransform.localScale.x);
    }

}
