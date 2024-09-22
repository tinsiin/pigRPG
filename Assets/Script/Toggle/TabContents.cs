using UnityEngine;

    public class TabContents : MonoBehaviour//tabContentsChangerのクラスに登録するMonoBehavior
    {//複雑な操作するならこのクラスで作る。
        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }
    }
