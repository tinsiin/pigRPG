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
    //�]���͒��ځ@�q��tmp��margin����ݒ肷��B
    public void OnCreated(float speed,string text,RectTransform parent)
    {
        _upSpeed = speed;
        tmpText.text = text;
        _parentRect = parent;
        MessageRect = GetComponent<RectTransform>();
        Debug.Log("���b�Z�[�W�u���b�N�̐������R�[���o�b�N");
    }

    /// <summary>
    /// �I�u�W�F�N�g��Y���w�肵���X�y�[�X����΂�
    /// </summary>
    public void JumpUp(float SpaceY)
    {
        Vector3 newPosition = MessageRect.localPosition;
        newPosition.y += SpaceY;
        MessageRect.localPosition = newPosition;
    }

    /// <summary>
    /// �I�u�W�F�N�g�̉����̎w�肳�ꂽ�Ԋu�ɓn���ꂽtransform���܂܂�Ă��邩�ǂ���
    /// </summary>
    public bool  ContainBelow(RectTransform otherRect,float spaceY)
    {
        // ���̃I�u�W�F�N�g�̉��[�ʒu�iY���W�j���v�Z
        var thisSpaceBottom = MessageRect.localPosition.y - (MessageRect.rect.height * 0.5f);

        // �w�肳�ꂽ���̃I�u�W�F�N�g�̏�[�ʒu�iY���W�j���v�Z
        float otherTopY = otherRect.localPosition.y + (otherRect.rect.height * 0.5f);



        // otherRect�̏�[���AthisBottomY - spaceY ����ɂ��邩�ǂ����𔻒�@�@�I�u�W�F�N�g+space��艺�ɂ���ꍇ��false���Ă��ƁB
        return otherTopY >= (thisSpaceBottom - spaceY);
    }

    void Start()
    {
        //tmpText.text = "����������������������";
        _upSpeed = 0.1f;
        MessageRect = GetComponent<RectTransform>();
        AdjustBackgroundSize();
    }

    private void Update()
    {
        Vector3 newPosition = MessageRect.localPosition;
        newPosition.y += _upSpeed ;
        MessageRect .localPosition = newPosition;

        // �e�I�u�W�F�N�g�̋��E�l�i���[�J�����W�j
        float parentTop = _parentRect.rect.height * 0.5f;
        float parentBottom = -_parentRect.rect.height * 0.5f;

        // ���̃I�u�W�F�N�g�̋��E�l
        float childTop = MessageRect.localPosition.y + MessageRect.rect.height * 0.5f;
        float childBottom = MessageRect.localPosition.y - MessageRect.rect.height * 0.5f;

        if (childBottom > parentTop)
        {
            // ������Ɉړ����Ɏq�I�u�W�F�N�g������𒴂����ꍇ
            Destroy(gameObject);//����
        }
    }

    /// <summary>
    /// �w�iImage�̃T�C�Y��TextMeshPro�̃T�C�Y�Ɋ�Â��Ē������A�]����ǉ����܂��B
    /// </summary>
    private void AdjustBackgroundSize()
    {
        // TextMeshPro�̐����T�C�Y���擾
        Vector2 tmpPreferredSize = tmpText.GetPreferredValues();

        // �w�iImage�̃T�C�Y��ݒ�i�e�����̗]����ǉ��j
        float backgroundWidth = tmpPreferredSize.x; // �� + Right
        float backgroundHeight = tmpPreferredSize.y; // Top + Bottom
        MessageRect.sizeDelta = new Vector2(backgroundWidth, backgroundHeight);

        // TextMeshPro�̈ʒu�������ɃI�t�Z�b�g
        // �A���J�[�������̏ꍇ�͈ȉ��̂悤�ɒ���
        /*tmpRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        tmpRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        tmpRectTransform.pivot = new Vector2(0.5f, 0.5f);
        tmpRectTransform.anchoredPosition = Vector2.zero;*/
    }
}
