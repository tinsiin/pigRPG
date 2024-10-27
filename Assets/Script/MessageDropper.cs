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


    List<MessageBlock> messages;//�ꊇ�Ǘ��p
    RectTransform rect;

    public static MessageDropper Instance { get; private set; }//static�ȃC���X�^���X

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); //�V�[���J�ڂ��Ă��j������Ȃ��悤�ɂ���B

            rect = GetComponent<RectTransform>();
            messages = new List<MessageBlock>();
        }
        else Destroy(gameObject);//null����Ȃ����Ă��Ƃ͉������Ő�������Ă邩�����
    }

    public void CreateMessage(string txt)
    {
        var message = Instantiate(MessageBlockPrefab, transform);
        message.OnCreated(MessageUpspeed, txt,rect);

        messages.RemoveAll(m => m==null);//�j�����ꂽ�I�u�W�F�N�g�����X�g�����������

        //���������������b�Z�[�W������ɓ��ꂽ���b�Z�[�W�Ƌ߂���΁A���X�g�S�Ẵ��b�Z�[�W����ɓ˂���΂�
        if (messages.Count > 0)
            if (messages[messages.Count-1].ContainBelow(message.MessageRect,MessageSpaceY))
            foreach (var m in messages)
                    if (m != null) // null �`�F�b�N��ǉ�
                        m.JumpUp(MessageSpaceY);


        messages.Add(message);//�ꊇ�Ǘ��̃��X�g�ɓ����B

    }
}
