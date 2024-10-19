using LitMotion;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DarkWaveManager : MonoBehaviour
{
    Material _wave;
    private static readonly int adma = Shader.PropertyToID("_adma");
    Material Wave
    {
        get
        {
            if(_wave == null)
            {
                _wave = GetComponent<Image>().material;
            }
            return _wave;
        }
    }

    [SerializeField] private float _normalToRightX;
    [SerializeField] private float _waveToRightX;


    [SerializeField] private AnimationCurve _waveCurve;
    [SerializeField] private float _waveSpeed;
    public void InWave()
    {
        LMotion.Create(_normalToRightX, _waveToRightX, _waveSpeed)
            .WithEase(_waveCurve)
            .Bind(x => Wave.SetFloat(adma, x))
            .AddTo(this);
    }
    public void OutWave()
    {

    }
}
