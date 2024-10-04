using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TextBoxControler : MonoBehaviour
{
    [SerializeField]private TextMeshProUGUI NameText;
    [SerializeField]private TextMeshProUGUI StatusText;
    [SerializeField]private Image NameBox;
    [SerializeField]private Image StatusBox;
     private String name;
     private String status;

     /// <summary>
     /// テキストを初期化する　instanceを生成した直後に呼び出す
     /// </summary>
     /// <param name="setName"></param>
     /// <param name="setStatus"></param>
     public void InitText(String setName, String setStatus)
     {
         this.name = setName;
         this.status = setStatus;
     }
    void Start()
    {
        NameText.text = name;
        StatusText.text = status;
        //文章のサイズ
        var nameTextWidth = NameText.preferredWidth;
        var statusTextWidth = StatusText.preferredWidth;
    
        var nameBoxborderWidth = 20;//余白の幅
        var statusBoxborderWidth = 31;

        NameBox.rectTransform.sizeDelta = new Vector2(nameTextWidth+nameBoxborderWidth, NameBox.rectTransform.sizeDelta.y);//テキストのサイズに合わせる
        StatusBox.rectTransform.sizeDelta = new Vector2(statusTextWidth+statusBoxborderWidth, StatusBox.rectTransform.sizeDelta.y);//テキストのサイズに合わせる
        
        NameText.rectTransform.sizeDelta = new Vector2(nameTextWidth, NameText.rectTransform.sizeDelta.y); //テキストのオブジェクト領域をテキストのサイズに合わせる
        StatusText.rectTransform.sizeDelta = new Vector2(statusTextWidth, StatusText.rectTransform.sizeDelta.y); //テキストのオブジェクト領域をテキストのサイズに合わせる
    }

    void Update()
    {
        //テキストボックスのエフェクトなど
    }
}
