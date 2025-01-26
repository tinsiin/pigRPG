using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MessageDropper : MonoBehaviour
{
    [SerializeField]
    MessageBlock MessageBlockPrefab;
    [SerializeField]
    float MessageUpspeed;
    [SerializeField]
    float MessageSpaceY;


    List<MessageBlock> messages;//一括管理用
    RectTransform rect;

    public static MessageDropper Instance { get; private set; }//staticなインスタンス

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); //シーン遷移しても破棄されないようにする。

            rect = GetComponent<RectTransform>();
            messages = new List<MessageBlock>();
        }
        else Destroy(gameObject);//nullじゃないってことは何処かで生成されてるから消す
    }

    public void CreateMessage(string txt)
    {
        var message = Instantiate(MessageBlockPrefab, transform);
        message.OnCreated(MessageUpspeed, txt,rect);

        messages.RemoveAll(m => m==null);//破棄されたオブジェクトをリストから消去する

        //もし生成したメッセージが直後に入れたメッセージと近ければ、リスト全てのメッセージを上に突き飛ばす
        if (messages.Count > 0)
            if (messages[messages.Count-1].ContainBelow(message.MessageRect,MessageSpaceY))
            foreach (var m in messages)
                    if (m != null) // null チェックを追加
                        m.JumpUp(MessageSpaceY);


        messages.Add(message);//一括管理のリストに入れる。

    }
}
