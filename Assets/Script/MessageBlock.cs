using System.Collections;
using System.Collections.Generic;
using System.Xml;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class MessageBlock : MonoBehaviour
{
    private float _upSpeed;

    [SerializeField] TextMeshProUGUI tmpText;
    RectTransform _parentRect;


    public RectTransform MessageRect;
    //余白は直接　子のtmpのmarginから設定する。
    public void OnCreated(float speed,string text,RectTransform parent)
    {
        _upSpeed = speed;
        tmpText.text = text;
        _parentRect = parent;
        MessageRect = GetComponent<RectTransform>();
        Debug.Log("メッセージブロックの生成時コールバック");
    }

    /// <summary>
    /// オブジェクトのYを指定したスペース分飛ばす
    /// </summary>
    public void JumpUp(float SpaceY)
    {
        Vector3 newPosition = MessageRect.localPosition;
        newPosition.y += SpaceY;
        MessageRect.localPosition = newPosition;
    }

    /// <summary>
    /// オブジェクトの下方の指定された間隔に渡されたtransformが含まれているかどうか
    /// </summary>
    public bool  ContainBelow(RectTransform otherRect,float spaceY)
    {
        // このオブジェクトの下端位置（Y座標）を計算
        var thisSpaceBottom = MessageRect.localPosition.y - (MessageRect.rect.height * 0.5f);

        // 指定された他のオブジェクトの上端位置（Y座標）を計算
        float otherTopY = otherRect.localPosition.y + (otherRect.rect.height * 0.5f);



        // otherRectの上端が、thisBottomY - spaceY より上にあるかどうかを判定　　オブジェクト+spaceより下にいる場合がfalseってこと。
        return otherTopY >= (thisSpaceBottom - spaceY);
    }

    void Start()
    {
        //tmpText.text = "ｄさｋｄさｄさｄかそだ";
        _upSpeed = 0.1f;
        MessageRect = GetComponent<RectTransform>();
        AdjustBackgroundSize();
    }

    private void Update()
    {
        Vector3 newPosition = MessageRect.localPosition;
        newPosition.y += _upSpeed ;
        MessageRect .localPosition = newPosition;

        // 親オブジェクトの境界値（ローカル座標）
        float parentTop = _parentRect.rect.height * 0.5f;
        float parentBottom = -_parentRect.rect.height * 0.5f;

        // このオブジェクトの境界値
        float childTop = MessageRect.localPosition.y + MessageRect.rect.height * 0.5f;
        float childBottom = MessageRect.localPosition.y - MessageRect.rect.height * 0.5f;

        if (childBottom > parentTop)
        {
            // 上方向に移動中に子オブジェクトが上限を超えた場合
            Destroy(gameObject);//消す
        }
    }

    /// <summary>
    /// 背景ImageのサイズをTextMeshProのサイズに基づいて調整し、余白を追加します。
    /// </summary>
    private void AdjustBackgroundSize()
    {
        // TextMeshProの推奨サイズを取得
        Vector2 tmpPreferredSize = tmpText.GetPreferredValues();

        // 背景Imageのサイズを設定（各方向の余白を追加）
        float backgroundWidth = tmpPreferredSize.x; // 左 + Right
        float backgroundHeight = tmpPreferredSize.y; // Top + Bottom
        MessageRect.sizeDelta = new Vector2(backgroundWidth, backgroundHeight);

        // TextMeshProの位置を左下にオフセット
        // アンカーが中央の場合は以下のように調整
        /*tmpRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        tmpRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        tmpRectTransform.pivot = new Vector2(0.5f, 0.5f);
        tmpRectTransform.anchoredPosition = Vector2.zero;*/
    }
}
