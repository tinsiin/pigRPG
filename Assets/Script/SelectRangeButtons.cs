using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


public class SelectRangeButtons : MonoBehaviour
{
    public�@static SelectRangeButtons Instance {  get; private set; }

    [SerializeField]
    Button buttonPrefab;
    [SerializeField]
    RectTransform parentRect;
    [Header("Layout Settings")]
    [SerializeField] float horizontalPadding = 10f; // �{�^���Ԃ̉��]��
    [SerializeField] float verticalPadding = 10f;   // �{�^���Ԃ̏c�]��

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(this);
        }
        buttonSize = buttonPrefab.GetComponent<RectTransform>().sizeDelta;
        parentSize = parentRect.rect.size;

        // �e�I�u�W�F�N�g�̍������Ƃ��邽�߂̃I�t�Z�b�g
        startX = -parentSize.x / 2 + buttonSize.x / 2;
        startY = parentSize.y / 2 - buttonSize.y;

    }
    // �{�^���̃T�C�Y���擾
    Vector2 buttonSize;
    // �e�I�u�W�F�N�g�̃T�C�Y���擾
    Vector2 parentSize;
    // �e�I�u�W�F�N�g�̍������Ƃ��邽�߂̃I�t�Z�b�g
    float startX;
    float startY;

    BattleManager bm;
    List<Button> buttonList;
    /// <summary>
    /// �����p�R�[���o�b�N
    /// </summary>
    public void OnCreated(BattleManager _bm)
    {
        bm = _bm;
        var acter = bm.Acter;
        var underActer = bm.UnderActer;
        var skill = acter.NowUseSkill;

        // ���݂̈ʒu��������
        float currentX = startX;
        float currentY = startY;


        //random�ȊO�͈̔͂̐������X�L�������Ɋ܂܂�Ă镪�����A���͈̔͂̐�����I��


        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget))//�O�̂߂肩��q�������_�����őI�ׂ�Ȃ�
        {
            //.CanSelectSingleTarget������͈̔͂Ƃ���{�^�����쐬����B
            var button = Instantiate(buttonPrefab, transform);
            var rect = button.GetComponent<RectTransform>();

            // �e�I�u�W�F�N�g�̉E�[�𒴂���ꍇ�͎��̍s�Ɉړ�
            if (currentX + buttonSize.x / 2 > parentSize.x / 2)
            {
                // ���[�Ƀ��Z�b�g
                currentX = startX;

                // ���̍s�Ɉړ�
                currentY -= (buttonSize.y + verticalPadding);
            }

            // �{�^���̈ʒu��ݒ�
            rect.anchoredPosition = new Vector2(currentX, currentY);

            // ���̃{�^����X�ʒu���X�V
            currentX += (buttonSize.x + horizontalPadding);

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.CanSelectSingleTarget));
            button.GetComponentInChildren<TextMeshProUGUI>().text = "�O�̂߂�܂��͂���ȊO�̂ǂ��炩��_��";//�{�^���̃e�L�X�g
            buttonList.Add(button);//�{�^�����X�g�ɓ����

        }

        if (skill.HasZoneTrait(SkillZoneTrait.CanPerfectSelectSingleTarget))//�X�őI�ׂ�Ȃ�
        {
            //.CanSelectSingleTarget������͈̔͂Ƃ���{�^�����쐬����B
            var button = Instantiate(buttonPrefab, transform);
            var rect = button.GetComponent<RectTransform>();

            // �e�I�u�W�F�N�g�̉E�[�𒴂���ꍇ�͎��̍s�Ɉړ�
            if (currentX + buttonSize.x / 2 > parentSize.x / 2)
            {
                // ���[�Ƀ��Z�b�g
                currentX = startX;

                // ���̍s�Ɉړ�
                currentY -= (buttonSize.y + verticalPadding);
            }

            // �{�^���̈ʒu��ݒ�
            rect.anchoredPosition = new Vector2(currentX, currentY);

            // ���̃{�^����X�ʒu���X�V
            currentX += (buttonSize.x + horizontalPadding);

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.CanPerfectSelectSingleTarget));
            button.GetComponentInChildren<TextMeshProUGUI>().text = "�X��_��";//�{�^���̃e�L�X�g
            buttonList.Add(button);//�{�^�����X�g�ɓ����

        }
        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectMultiTarget))//�O�̂߂�܂��͌�q�̒c�̂��őI�ׂ�Ȃ�
        {
            //.CanSelectSingleTarget������͈̔͂Ƃ���{�^�����쐬����B
            var button = Instantiate(buttonPrefab, transform);
            var rect = button.GetComponent<RectTransform>();

            // �e�I�u�W�F�N�g�̉E�[�𒴂���ꍇ�͎��̍s�Ɉړ�
            if (currentX + buttonSize.x / 2 > parentSize.x / 2)
            {
                // ���[�Ƀ��Z�b�g
                currentX = startX;

                // ���̍s�Ɉړ�
                currentY -= (buttonSize.y + verticalPadding);
            }

            // �{�^���̈ʒu��ݒ�
            rect.anchoredPosition = new Vector2(currentX, currentY);

            // ���̃{�^����X�ʒu���X�V
            currentX += (buttonSize.x + horizontalPadding);

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.CanSelectMultiTarget));
            button.GetComponentInChildren<TextMeshProUGUI>().text = "�O�̂߂肩����ȊO��l��_��";//�{�^���̃e�L�X�g
            buttonList.Add(button);//�{�^�����X�g�ɓ����

        }


        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectMultiTarget))//�O�̂߂�܂��͌�q�̒c�̂��őI�ׂ�Ȃ�
        {
            //.CanSelectSingleTarget������͈̔͂Ƃ���{�^�����쐬����B
            var button = Instantiate(buttonPrefab, transform);
            var rect = button.GetComponent<RectTransform>();

            // �e�I�u�W�F�N�g�̉E�[�𒴂���ꍇ�͎��̍s�Ɉړ�
            if (currentX + buttonSize.x / 2 > parentSize.x / 2)
            {
                // ���[�Ƀ��Z�b�g
                currentX = startX;

                // ���̍s�Ɉړ�
                currentY -= (buttonSize.y + verticalPadding);
            }

            // �{�^���̈ʒu��ݒ�
            rect.anchoredPosition = new Vector2(currentX, currentY);

            // ���̃{�^����X�ʒu���X�V
            currentX += (buttonSize.x + horizontalPadding);

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.CanSelectMultiTarget));
            button.GetComponentInChildren<TextMeshProUGUI>().text = "�O�̂߂肩����ȊO��l��_��";//�{�^���̃e�L�X�g
            buttonList.Add(button);//�{�^�����X�g�ɓ����

        }






    }

    /// <summary>
    /// �I�v�V�����͈̔͑I���{�^���ɓn���R�[���o�b�N
    /// </summary>
    public void OnClickOptionRangeBtn(Button thisbtn,SkillZoneTrait option)
    {
        bm.Acter.RangeWill |= option;
        Destroy(thisbtn);//�{�^���͏�����

        //�I�v�V�����Ȃ̂ł���I�񂾂����ł͎��֐i�܂Ȃ��B
    }

    public void OnClickRangeBtn(Button thisbtn,SkillZoneTrait range)
    {
        bm.Acter.RangeWill |= range;
        foreach (var button in buttonList)
        {
            Destroy(button);//�{�^���S���폜
        }
        NextTab();//���֍s��

    }

    /// <summary>
    /// ���̃^�u�֍s��
    /// </summary>
    public void NextTab()
    {
        //�S�͈͂Ȃ炻�̂܂�nextWait
        if (bm.Acter.HasRangeWill(SkillZoneTrait.AllTarget))
        {
            Walking.USERUI_state.Value = TabState.NextWait;
        }
        else 
        {
            Walking.USERUI_state.Value = TabState.SelectTarget;//�����łȂ��Ȃ�I����ʂցB

        }

    }





}