using UnityEngine;

/// <summary>
/// EyeArea側のメインContent切替実装。
/// レイヤー方式: Walkは常時表示、Novel/Battleは上に重ねる（排他）。
/// </summary>
public class EyeAreaMainContent : EyeAreaContents
{
    public override void SwitchContent(EyeAreaState state)
    {
        // Walkは常にactive（基盤層）
        if (walkContent != null) walkContent.SetActive(true);

        // Novel/Battleは排他的
        switch (state)
        {
            case EyeAreaState.Walk:
                if (novelContent != null) novelContent.SetActive(false);
                if (battleContent != null) battleContent.SetActive(false);
                break;

            case EyeAreaState.Novel:
                if (novelContent != null) novelContent.SetActive(true);
                if (battleContent != null) battleContent.SetActive(false);
                break;

            case EyeAreaState.Battle:
                if (novelContent != null) novelContent.SetActive(false);
                if (battleContent != null) battleContent.SetActive(true);
                break;
        }
    }
}
