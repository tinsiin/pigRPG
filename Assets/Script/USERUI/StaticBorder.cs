using UnityEngine;
using UnityEngine.UI;

public class StaticBorder : MonoBehaviour
{
    [SerializeField] private float borderWidth = 2f;
    [SerializeField] private Color borderColor = Color.black;
    
    void Start()
    {
        RectTransform rect = GetComponent<RectTransform>();
        float w = rect.rect.width;
        float h = rect.rect.height;
        
        // 配列で一括処理（メモリ効率化）
        var borders = new (Vector2 pos, Vector2 size)[] {
            (new Vector2(0, h/2 + borderWidth/2), new Vector2(w + borderWidth*2, borderWidth)),
            (new Vector2(0, -h/2 - borderWidth/2), new Vector2(w + borderWidth*2, borderWidth)),
            (new Vector2(-w/2 - borderWidth/2, 0), new Vector2(borderWidth, h)),
            (new Vector2(w/2 + borderWidth/2, 0), new Vector2(borderWidth, h))
        };
        
        foreach (var (pos, size) in borders)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);
            var img = go.AddComponent<Image>();
            img.color = borderColor;
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
        
        Destroy(this);
    }
}