using UnityEngine;

/// <summary>
///     マップ画像上に二点を定義して、その上に現在進行度とエリアの終わりの割合をその二点上にベクトルポジションとして表し、
///     現在地を表示する。(このクラス自体は位置に点滅さして表示するだけ。)
/// </summary>
public class TenmetuNowImage : MonoBehaviour
{
    [SerializeField] private RectTransform nowimg;

    // Update is called once per frame
    private void Update()
    {
        //点滅させたり　魂の開放の時にその色に光らせようかな???
    }


    /// <summary>
    ///     位置情報を更新する命令
    /// </summary>
    public void LocationSet(Vector2 loc)
    {
        nowimg.anchoredPosition = loc;
    }
}