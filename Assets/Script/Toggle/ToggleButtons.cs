using System;
using R3;
using UnityEngine;


    public class ToggleButtons : MonoBehaviour//�J�X�^�}�C�Y���₷��TabContentsChanger���p���ƃW�F�l���b�N���g�������������Ă̎g�p����N���X�B
    {
        public enum TabContentsKind//tabContentsChanger�Ɏw�肷��񋓑�
        {
            Players,
            CharactorConfig,
            Config
        }

        [Serializable]
        public class SampleTabContentsChanger : TabContentsChanger<TabContents, TabContentsKind>
        {//�Ή������̃N���X���w�肵�Čp��
        }

        [SerializeField]
        private SampleTabContentsChanger _tabContentsChanger;//���̌p�������N���X�����B

        void Start()
        {
            _tabContentsChanger.Initialize();
            _tabContentsChanger.OnChangeStateAsObservable
                               .Subscribe(
                                   value =>
                                   {
                                       value.content.View.SetActive(value.active);//�؂�ւ����̃N���X�̓���������œo�^����B
                                   }).AddTo(this);
            //_tabContentsChanger.Select(0);//�I�񂾏�Ԃɗ\�߂���z����������Ƃ��B
        }
    }

