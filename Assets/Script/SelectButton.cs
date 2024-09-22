using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class SelectButton : MonoBehaviour
{
    //public class ClickEvent : UnityEvent<int> { };//�p���H
    //int�������Ɏ��֐�����𐶐��B
    UnityEvent<int> OnClicked = new UnityEvent<int>();

    /// <summary>
    /// �{�^���̋��ID
    /// </summary>
    int buttonID;

    [SerializeField] TextMeshProUGUI buttonText = null;


    // ���N�g�g�����X�t�H�[���ۊǗp.
    RectTransform rect = null;

    private void Awake()
    {
        this.gameObject.SetActive(false);//�R�[�����Ă΂��܂Ō����Ȃ����Ƃ�
        Debug.Log("�I�����̃{�^�����C���X�^���X�������ꂽ�B");
    }

    /// <summary>
    /// �쐬���R�[��
    /// </summary>
    /// <param name="buttonIndex">�{�^���̐�������鏇�ԁA��������ƂɎl���ɔz�u</param>
    /// <param name="txt">�{�^���̂ɋL������</param>
    /// <param name="onclick">�{�^���ɓn��������id��Ԃ����߂̊֐�����</param>
    /// <param name="id">�{�^���ɕt�^�����id</param>
    public void OnCreateButton(int buttonIndex,string txt, UnityAction<int> onclick,int id,int size)
    {
        rect = GetComponent<RectTransform>();//�ʒu�����擾
        
        buttonID = id;
        buttonText.text = txt;//�{�^������
        OnClicked.AddListener(onclick);//�֐���n���B

        switch (buttonIndex)//�l���̕���
        {
            case 0:
                rect.pivot = new Vector2(0, 1);//����
                rect.anchorMax = new Vector2(0, 1);
                rect.anchorMin = new Vector2(0, 1);
                break;
            case 1:
                rect.pivot = new Vector2(1, 1);//�E��
                rect.anchorMax = new Vector2(1, 1);
                rect.anchorMin = new Vector2(1, 1);
                break;
            case 2:
                rect.pivot = new Vector2(0, 0);//����
                rect.anchorMax = new Vector2(0, 0);
                rect.anchorMin = new Vector2(0, 0);
                break;
            case 3:
                rect.pivot = new Vector2(1, 0);//�E��
                rect.anchorMax = new Vector2(1, 0);
                rect.anchorMin = new Vector2(1, 0);
                break;
        }

        rect.anchoredPosition= Vector3.zero;//�A���J�[�ɉ����Ĉʒu�����ׂĂ����ɂ���A�S�ă[���ɂ��邱�Ƃ�
        rect.sizeDelta = new Vector2(size, size);//width��height��ύX
        this.gameObject.SetActive(true);//���������̂ŉf���B
        Debug.Log("�I�����̃{�^���̃R�[���֐�����������");
    }

    /// <summary>
    /// �{�^���N���b�N�R�[���o�b�N
    /// </summary>
    public void OnButtonClicked()
    {
        OnClicked.Invoke(buttonID);//�n���ꂽ�֐������s
    }

    // ------------------------------------------------------------
    // ����.
    // ------------------------------------------------------------
    public void Close()
    {
        Destroy(gameObject);
    }
}
