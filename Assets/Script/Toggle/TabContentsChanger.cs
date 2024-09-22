using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using R3;
using System;

public class TabContentsChanger<TView, TKind>
    where TView : MonoBehaviour
    where TKind : Enum
//https://qiita.com/mikuri8/items/cc807a6c8de2ca7cb95e
//���̃W�F�l���b�N�N���X�͈�����񋓑̂�View(UI)�����R�Ɏw��o����悤�ɂ���ׂ̂��́H
{
    [Serializable]
    public class ContentHolder//���X�g�����Ďg�p����ׂɃN���X�Ƃ��č쐬
    {
        public TView View;//MonoBehavior�Ȃ牽�ł��󂯓����̂ŁAMonoBehavior��������GameObject�����ł�SerializeField����o�^�ł���
        public TKind Kind;

        public bool IsSelect { get; private set; }//���̏������͌����������v���p�e�B�̏��������Ǝv���B

        public void SetSelect(bool select)
        {
            IsSelect = select;
        }
    }

    public List<ContentHolder> Contents;

    [SerializeField]
    private ToggleButtonGroup _toggleButtonGroup;

    private Subject<(ContentHolder, bool)> _onChangeStateSubject = new();//�o�b�L���O�t�B�[���h??
    public Observable<(ContentHolder content, bool active)> OnChangeStateAsObservable => _onChangeStateSubject;//���J�t�B�[���h?�@�����_��get��p�v���p�e�B
  
    public void Initialize()
    {
        for(int i = 0; i<Contents.Count; i++)
        {
            //�{�^���ƃR���e���c�̃C���f�b�N�X�͓��ꂵ�Ă���Ƃ����O��@���ŃR���e���c�ƃ{�^���ɓ����C���f�b�N�X��n���Ă���B
            var content = Contents[i];//��ꃋ�[�v�̊Y���̃R���e���c�z���_�[
            _toggleButtonGroup.OnClickAsObservable(i)//��ꃋ�[�v�ŉ��{�^���ɓo�^����N���b�N���̃R�[���o�b�N��o�^����B
                .Subscribe(
                _ =>
                {
                    //����foreach���[�v�́A����{�^�����������Ƃ��̑S�ẴR���e���c(�{�^���������ƕς��z)
                    //�̋�����\�߂ǂ������������œo�^���Ă�A���ăC���[�W

                    foreach (var one in Contents)//�R���e���c�S�ĂɎ��s
                    {
                        bool active = one.Kind.Equals(content.Kind);//�I�񂾃{�^���ɑΉ�����R���e���c�ƃ��[�v���Ă�R���e���c����v���Ă邩
                        _onChangeStateSubject.OnNext((one, active));//�ύX�R�[���o�b�N���񃋁[�v�̃R���e���c�z���_�[�Ə�̔�r���ʂ�n���B
                        //��̃{�^�����������ۂɑS�ẴR���e���c�Ɏ��s����

                        one.SetSelect(active);//��񃋁[�v��isSelect�ɔ�r����bool��n��
                        //�I������Ă镨������ContentHolder��isselect��true�ɂȂ�悤�ɂȂ��Ă�
                    }
                }).AddTo(content.View);//��ꃋ�[�v�̊Y���R���e���c�z���_�[��View�Ɍ��т���
        }
    }
    /// <summary>
    /// �w��C���f�b�N�X�̑I��
    /// </summary>
    public void Select(int index)
    {
        _toggleButtonGroup.SelectToggleIndex(index);//�O���[�v�Ǘ��N���X����I������
        for(int i=0; i<Contents.Count; i++)
        {
            Contents[i].SetSelect(i == index);//�R���e���c�̊Y�����Z���N�g����
            _onChangeStateSubject.OnNext((Contents[i], i == index));//�R���e���c�ƃA�N�e�B�u��Ԃ��Ď��s����B
        }
    }

    /// <summary>
    /// ���݃A�N�e�B�u��ContentHolder�̎擾
    /// </summary>
    /// <returns></returns>
    public ContentHolder GetActiveContent()
    {
        return Contents.Find(content => content.IsSelect);//list��find��var�ȗ��̃����_�ł��ꂪ���v����Ȃ�A�����Ԃ��B
    }
}
