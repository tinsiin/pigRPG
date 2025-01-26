using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class SelectButton : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI buttonText;

    //public class ClickEvent : UnityEvent<int> { };//継承？
    //intを引数に持つ関数入れを生成。
    private readonly UnityEvent<int> OnClicked = new();

    /// <summary>
    ///     ボタンの区別ID
    /// </summary>
    private int buttonID;


    // レクトトランスフォーム保管用.
    private RectTransform rect;

    private void Awake()
    {
        gameObject.SetActive(false); //コールが呼ばれるまで見えなくしとく
        Debug.Log("選択肢のボタンがインスタンス生成された。");
    }

    /// <summary>
    ///     作成時コール
    /// </summary>
    /// <param name="buttonIndex">ボタンの生成される順番、これをもとに四隅に配置</param>
    /// <param name="txt">ボタンのに記す文章</param>
    /// <param name="onclick">ボタンに渡す押したidを返すための関数入れ</param>
    /// <param name="id">ボタンに付与されるid</param>
    public void OnCreateButton(int buttonIndex, string txt, UnityAction<int> onclick, int id, int size)
    {
        rect = GetComponent<RectTransform>(); //位置情報を取得

        buttonID = id;
        buttonText.text = txt; //ボタン文章
        OnClicked.AddListener(onclick); //関数を渡す。

        switch (buttonIndex) //四隅の分岐
        {
            case 0:
                rect.pivot = new Vector2(0, 1); //左上
                rect.anchorMax = new Vector2(0, 1);
                rect.anchorMin = new Vector2(0, 1);
                break;
            case 1:
                rect.pivot = new Vector2(1, 1); //右上
                rect.anchorMax = new Vector2(1, 1);
                rect.anchorMin = new Vector2(1, 1);
                break;
            case 2:
                rect.pivot = new Vector2(0, 0); //左下
                rect.anchorMax = new Vector2(0, 0);
                rect.anchorMin = new Vector2(0, 0);
                break;
            case 3:
                rect.pivot = new Vector2(1, 0); //右下
                rect.anchorMax = new Vector2(1, 0);
                rect.anchorMin = new Vector2(1, 0);
                break;
        }

        rect.anchoredPosition = Vector3.zero; //アンカーに応じて位置をすべてそこにする、全てゼロにすることで
        rect.sizeDelta = new Vector2(size, size); //widthとheightを変更
        gameObject.SetActive(true); //完了したので映す。
        Debug.Log("選択肢のボタンのコール関数が完了した");
    }

    /// <summary>
    ///     ボタンクリックコールバック
    /// </summary>
    public void OnButtonClicked()
    {
        OnClicked.Invoke(buttonID); //渡された関数を実行
    }

    // ------------------------------------------------------------
    // 閉じる.
    // ------------------------------------------------------------
    public void Close()
    {
        Destroy(gameObject);
    }
}