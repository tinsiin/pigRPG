using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UI;


public class SelectRangeButtons : MonoBehaviour
{
    public static SelectRangeButtons Instance { get; private set; }

    [SerializeField]
    Button buttonPrefab;
    [SerializeField]
    RectTransform parentRect;
    [Header("Layout Settings")]
    [SerializeField] float horizontalPadding = 10f; // �{�^���Ԃ̉��]��
    [SerializeField] float verticalPadding = 10f;   // �{�^���Ԃ̏c�]��

    private void Awake()
    {
        if (Instance == null)
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
        startX = -parentSize.x / 2 + buttonSize.x / 2 + horizontalPadding;
        startY = parentSize.y / 2 - buttonSize.y + horizontalPadding;
        //�e�I�u�W�F�N�g�̍����ɌŒ肷��ׂ̃I�v�V�����p�I�t�Z�b�g
        optionStartX = -parentSize.x / 2 + buttonSize.x / 2 + horizontalPadding;
        optionStartY = -parentSize.y / 2 + buttonSize.y / 2 + horizontalPadding;
    }
    // �{�^���̃T�C�Y���擾
    Vector2 buttonSize;
    // �e�I�u�W�F�N�g�̃T�C�Y���擾
    Vector2 parentSize;
    // �e�I�u�W�F�N�g�̍������Ƃ��邽�߂̃I�t�Z�b�g
    float startX;
    float startY;
    float optionStartX;
    float optionStartY;

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
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                "�O�̂߂�܂��͂���ȊO�̂ǂ��炩��_��" + AddPercentageTextOnButton(SkillZoneTrait.CanSelectSingleTarget);//�{�^���̃e�L�X�g
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
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                "�X��_��" + AddPercentageTextOnButton(SkillZoneTrait.CanPerfectSelectSingleTarget);//�{�^���̃e�L�X�g
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
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                "�O�̂߂肩����ȊO��l��_��" + AddPercentageTextOnButton(SkillZoneTrait.CanSelectMultiTarget);//�{�^���̃e�L�X�g
            buttonList.Add(button);//�{�^�����X�g�ɓ����

        }


        if (skill.HasZoneTrait(SkillZoneTrait.AllTarget))//�S�͈͂Ȃ�
        {
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

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.AllTarget));
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                "�G�̑S�͈͂�_��" + AddPercentageTextOnButton(SkillZoneTrait.AllTarget);//�{�^���̃e�L�X�g
            buttonList.Add(button);//�{�^�����X�g�ɓ����

        }


        //��������I�v�V�����̃{�^��

        currentX = optionStartX;
        currentY = optionStartY;

        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))//������I�ԃI�v�V������ǉ�����Ȃ�
        {
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

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.CanSelectAlly));
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                "����" + AddPercentageTextOnButton(SkillZoneTrait.CanSelectAlly);//�{�^���̃e�L�X�g
            buttonList.Add(button);//�{�^�����X�g�ɓ����

        }

        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectDeath))//���҂�I�ԃI�v�V������ǉ�����Ȃ�
        {
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

            button.onClick.AddListener(() => OnClickRangeBtn(button, SkillZoneTrait.CanSelectDeath));
            button.GetComponentInChildren<TextMeshProUGUI>().text =
                "����" + AddPercentageTextOnButton(SkillZoneTrait.CanSelectDeath);//�{�^���̃e�L�X�g
            buttonList.Add(button);//�{�^�����X�g�ɓ����

        }






    }
    /// <summary>
    /// �e�X�g�p�{�^��
    /// </summary>
    public void OnClickTestButton()
    {
        // ���݂̈ʒu��������
        float currentX = startX;
        float currentY = startY;

        const int Optioncount = 2;
        const int count = 3;

        currentX = startX;//�ʏ�{�^��
        currentY = startY;

        for (int i = 0; i < count; i++)
        {
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
        }



        currentX = optionStartX;//�I�v�V�����{�^��
        currentY = optionStartY;

        for (int i = 0; i < Optioncount; i++)
        {
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
        }



    }

    /// <summary>
    /// �I�v�V�����͈̔͑I���{�^���ɓn���R�[���o�b�N
    /// </summary>
    public void OnClickOptionRangeBtn(Button thisbtn, SkillZoneTrait option)
    {
        bm.Acter.RangeWill |= option;
        Destroy(thisbtn);//�{�^���͏�����

        //�I�v�V�����Ȃ̂ł���I�񂾂����ł͎��֐i�܂Ȃ��B
    }

    public void OnClickRangeBtn(Button thisbtn, SkillZoneTrait range)
    {
        bm.Acter.RangeWill |= range;
        foreach (var button in buttonList)
        {
            Destroy(button);//�{�^���S���폜
        }
        NextTab();//���֍s��

    }
    /// <summary>
    /// �{�^���ɈЗ͈͂̔͂ɂ�銄�������̃e�L�X�g��ǉ�����
    /// �����ɔ͈͐�����n���ƁA����ɉ�������������������Ȃ�΁A�����̃e�L�X�g��Ԃ��B
    /// </summary>
    private string AddPercentageTextOnButton(SkillZoneTrait zone)
    {
        var skill = bm.Acter.NowUseSkill;
        var txt = "";//�����Ȃ���΋󕶎����Ԃ�̂�
        if (skill.PowerRangePercentageDictionary.ContainsKey(zone))//���͈̔͐����̊�������������Ȃ��
        {
            txt = "\n��������:" + (skill.PowerRangePercentageDictionary[zone] * 100).ToString() + "%";//�e�L�X�g�ɓ����B
        }

        return txt;
    }

    /// <summary>
    /// ���̃^�u�֍s��
    /// </summary>
    private void NextTab()
    {
        //�S�͈͂Ȃ炻�̂܂�nextWait�@�@�Ώۂ�I�ԕK�v���Ȃ������
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
