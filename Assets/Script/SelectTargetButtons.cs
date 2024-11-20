using RandomExtensions;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UI;

public class SelectTargetButtons : MonoBehaviour
{
    public static SelectTargetButtons Instance { get; private set; }

    [SerializeField]
    Button buttonPrefab;
    [SerializeField]
    Button SelectEndBtn;
    [SerializeField]
    RectTransform parentRect;

    [Header("Layout Settings")]
    [SerializeField] float horizontalPadding = 10f; // �{�^���Ԃ̉��]��
    [SerializeField] float verticalPadding = 10f;   // �{�^���Ԃ̏c�]��

    public void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
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
    int NeedSelectCountAlly;//����needcount�͊�{�I�ɂ͑ΏۑI���̂�
    int NeedSelectCountEnemy;
    List<Button> AllybuttonList;
    List<Button> EnemybuttonList;
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


        //buttonPrefab���X�L�������ɉ�����bm�̃O���[�v�����̐l������������Đ�������
        bool EnemyTargeting = false;//�G�̑ΏۑI��
        bool AllyTargeting = false;//�����̑ΏۑI��
        bool EnemyVanguardOrBackLine = false;//�G�̑O�̂߂�or��q

        if (skill.HasZoneTrait(SkillZoneTrait.CanPerfectSelectSingleTarget))//�I���\�ȒP�̑Ώ�
        {
            EnemyTargeting = true;
            NeedSelectCountEnemy = 1;

            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))
            {
                AllyTargeting = true;//�������I�ׂ��疡�����ǉ�
                NeedSelectCountAlly = 1;
            }

        }
        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget))//�O�̂߂肩��q(�����_���P��)��_����
        {
            EnemyVanguardOrBackLine = true;
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))//�������I�ׂ�Ȃ�
            {
                AllyTargeting = true;//�����͒P�̂ł����I�ׂȂ�
                //��l�܂��͓�l�P�� 
                NeedSelectCountAlly = Random.Range(1, 3);
            }
        }

        if (skill.HasZoneTrait(SkillZoneTrait.CanSelectSingleTarget))//�O�̂߂肩��q(�͈�)��_����
        {
            EnemyVanguardOrBackLine = true;
            if (skill.HasZoneTrait(SkillZoneTrait.CanSelectAlly))//�������I�ׂ�Ȃ�
            {
                AllyTargeting = true;//�����͒P�̂ł����I�ׂȂ�
                //��l�͈� 
                NeedSelectCountAlly = 2;
            }
        }

        //�{�^���쐬�t�F�[�Y��������������������������������������������������������������������


        if (EnemyVanguardOrBackLine) //�O�̂߂肩��q����G����I�Ԃɂ�
        {
            //�O�̂߂肪���݂��邩���Ȃ����A����������l���ǂ������Ƌ����I��BackOrAny��DirectedWill�ɂȂ��đΏۑI����ʂ��΂��B
            //�܂�{�^������炸���̂܂�NextWait��

            var enemyLives = bm.RemoveDeathCharacters(bm.EnemyGroup.Ours);//�����Ă�G����
            if((bm.EnemyGroup.InstantVanguard == null || enemyLives.Count < 2)) //�O�̂߂肪���Ȃ����@�G�̐����Ă�l������l����
            {
                if (!AllyTargeting)//�����I�����Ȃ��Ȃ�
                {
                    ReturnNextWaitView();//���̂܂܎��̉�ʂ�
                    bm.Acter.Target = DirectedWill.BacklineOrAny;//��q�܂��͒N���̈ӎv�����Ƃ��B
                }
                else//�������I���ł���Ȃ�
                {
                    //�G��l��I���\�ȃ{�^���Ƃ��Ĕz�u����
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

                    button.onClick.AddListener(() => OnClickSelectVanguardOrBacklines(button, DirectedWill.BacklineOrAny));
                    button.GetComponentInChildren<TextMeshProUGUI>().text = "�G";//�{�^���̃e�L�X�g
                    EnemybuttonList.Add(button);//�G�̃{�^�����X�g�ɓ����
                }
            }
            else//�O�̂߂肪���ē�l�ȏア��Ȃ�
            {
                DirectedWill[] WillSet = new DirectedWill[] { DirectedWill.InstantVanguard, DirectedWill.BacklineOrAny };//for���ŏ������邽�ߔz��
                string[] BtnStringSet = new string[] { "�O�̂߂�", "����ȊO" };

                for (var i = 0; i < 2; i++)
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

                    button.onClick.AddListener(() => OnClickSelectVanguardOrBacklines(button, WillSet[i]));
                    button.GetComponentInChildren<TextMeshProUGUI>().text = BtnStringSet[i];//�{�^���̃e�L�X�g�ɑO�̂߂蓙�̋L�q
                    EnemybuttonList.Add(button);//�G�̃{�^�����X�g�ɓ����

                }


            }
        }



        if (EnemyTargeting)//�G�S��������
        {
            var selects = bm.EnemyGroup.Ours;

            if (!skill.HasZoneTrait(SkillZoneTrait.CanSelectDeath))//���S�ґI��s�\�Ȃ�
            {
                selects = bm.RemoveDeathCharacters(selects);//�Ȃ�
            }

            if (selects.Count < 2 && AllyTargeting)//�G�̐����Ă�l������l�����ŁA�����̑I�����Ȃ����
            {
                ReturnNextWaitView();//���̂܂܎��̉�ʂ�
                bm.Acter.Target = DirectedWill.BacklineOrAny;//��q�܂��͒N���̈ӎv�����Ƃ��B
            }
            else
            {
                for (var i = 0; i < selects.Count; i++)
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

                    button.onClick.AddListener(() => OnClickSelectTarget(selects[i], button, WhichGroup.Enemyiy, DirectedWill.One));//�֐���o�^
                    button.GetComponentInChildren<TextMeshProUGUI>().text = selects[i].CharacterName;//�{�^���̃e�L�X�g�ɃL������
                    EnemybuttonList.Add(button);//�G�̃{�^�����X�g������

                }

            }
        }

        if (AllyTargeting)//�����S��������
        {
            var selects = bm.AllyGroup.Ours;


            if(!skill.HasZoneTrait(SkillZoneTrait.CanSelectDeath))//���S�ґI��s�\�Ȃ�
            {
                selects = bm.RemoveDeathCharacters(selects);//�Ȃ�
            }


            for (var i = 0; i < selects.Count; i++)
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

                button.onClick.AddListener(() => OnClickSelectTarget(selects[i], button, WhichGroup.alliy, DirectedWill.One));//�֐���o�^
                button.GetComponentInChildren<TextMeshProUGUI>().text = selects[i].CharacterName;//�{�^���̃e�L�X�g�ɃL������
                EnemybuttonList.Add(button);//�G�̃{�^�����X�g������

            }

        }

        //�I����r���ŏI����{�^��
        SelectEndBtn.gameObject.SetActive(false);//�����Ȃ�����B
    }
    /// <summary>
    /// �r���ŕ����I�����~�߂�{�^��
    /// </summary>
    public void OnClickSelectEndBtn()
    {
        ReturnNextWaitView();
    }
    /// <summary>
    /// �O�̂߂肩��q����I������{�^���B
    /// </summary>
    void OnClickSelectVanguardOrBacklines(Button thisBtn,DirectedWill will)
    {
        bm.Acter.Target = will;//�n���ꂽ�O�̂߂肩��q���̈ӎv������B

        ReturnNextWaitView();
    }

    /// <summary>
    /// "�l����ΏۂƂ��đI�ԃN���b�N�֐�"
    /// </summary>
    void OnClickSelectTarget(BaseStates target, Button thisBtn, WhichGroup faction,DirectedWill will)
    {
        bm.UnderActer.Add(target);


        if (AllybuttonList.Count > 0 && faction == WhichGroup.Enemyiy)///�G�̃{�^���Ŗ����̃{�^������ȏ゠������
        {
            foreach (var button in AllybuttonList)
            {
                Destroy(button);//�����̃{�^����S������
            }
        }

        if (EnemybuttonList.Count > 0 && faction == WhichGroup.alliy)///�����̃{�^���œG�̃{�^������ȏ゠������
        {
            foreach (var button in EnemybuttonList)
            {
                Destroy(button);//�����̃{�^����S������
            }
        }

        //�l���Z���N�g�J�E���g���f�N�������g
        if (faction == WhichGroup.alliy)
            NeedSelectCountAlly--;
        if (faction == WhichGroup.Enemyiy)
            NeedSelectCountEnemy--;

        //�e�w�c���Ƃɑ������{�^������ȏ�Ȃ��Ă��I��� (����������Ă�����͔p���\���"���̃I�u�W�F�N�g"������)
        //�܂�����I�ԃ{�^�����Ȃ��Ȃ�
        if (faction == WhichGroup.alliy)
        {//�����{�^���Ȃ�
            if (AllybuttonList.Count > 1 || NeedSelectCountAlly <= 0)//�����{�^������ȏ�Ȃ����A�����I��K�v�J�E���g�_�E�����[���ȉ��Ȃ玟�s������
            {
                ReturnNextWaitView();
            }
            else
            {
                //�܂��I�ׂ�̂Ȃ�A�r���őI�����~�߂���{�^����\������B
                SelectEndBtn.gameObject.SetActive(true);
            }
        }
        else if (faction == WhichGroup.Enemyiy)
        {//�G�{�^���Ȃ�
            if (EnemybuttonList.Count > 1 || NeedSelectCountEnemy <= 0) //�G�{�^������ȏ�Ȃ��Ȃ�A�G�I��K�v�J�E���g�_�E�����[���ȉ��Ȃ玟�s������
            {
                ReturnNextWaitView();
            }
            else
            {
                //�܂��I�ׂ�̂Ȃ�A�r���őI�����~�߂���{�^����\������B
                SelectEndBtn.gameObject.SetActive(true);
            }


            bm.Acter.Target = will;//�I���ӎv������
            Destroy(thisBtn);//���̃{�^���͔j��
        }
    }
    /// <summary>
    /// �e�Ώێ҃{�^���ɓn��NextWait��tabState��߂�����
    /// </summary>
    private void ReturnNextWaitView()
    {
        Walking.USERUI_state.Value = TabState.NextWait;

        foreach (var button in AllybuttonList)
        {
            Destroy(button);//�{�^���S���폜
        }
        foreach (var button in EnemybuttonList)
        {
            Destroy(button);//�{�^���S���폜
        }
    }
}
