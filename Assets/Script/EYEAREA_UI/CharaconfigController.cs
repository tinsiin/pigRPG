using System;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharaconfigController : MonoBehaviour
{
    public static CharaconfigController Instance { get; private set; }
    [Header("Navigation")]
    [SerializeField] private Button m_LeftButton;
    [SerializeField] private Button m_RightButton;

    [Header("Display")]
    /// <summary>
    /// ステータスバナー
    /// </summary>
    [SerializeField] private StatesBannerController m_StatesBanner;
    [Header("キャラ名表示")]
    [SerializeField] private TextMeshProUGUI m_CharacterNameText;

    [Header("Shared Buttons (Characonfig)")]
    [SerializeField] private Button m_OpenEmotionalAttachmentButton;
    [SerializeField] private Button m_StopFreezeConsecutiveButton;
    [Header("Toggle Groups (Characonfig)")]
    [SerializeField] private ToggleGroupController m_InterruptCounterRadio;

    /// <summary>
    /// 現在選択中のキャラクターのインデックス
    /// </summary>
    private int m_CurrentIndex = 0;

    // 選択変更イベント（indexを流す）
    private readonly Subject<int> _onSelectionChanged = new();
    public Observable<int> OnSelectionChangedAsObservable => _onSelectionChanged;

    private void Awake()
    {
        Instance = this;
        // 矢印コールバック（1回だけ登録）
        if (m_LeftButton != null)  m_LeftButton.onClick.AddListener(Prev);
        if (m_RightButton != null) m_RightButton.onClick.AddListener(Next);

        // CharaconfigタブがアクティブになったらUIを更新
        ToggleButtons.OnCharaConfigSelectAsObservable
            .Subscribe(_ => RefreshUI())
            .AddTo(this);

        // 共有ボタンの一度だけのバインド（現在の index をキャプチャ）
        if (m_OpenEmotionalAttachmentButton != null)
        {
            BindSharedButtonWithIndex(m_OpenEmotionalAttachmentButton, i =>
            {
                PlayersStates.Instance?.OpenEmotionalAttachmentSkillSelectUIArea(i);
            });
        }
        if (m_StopFreezeConsecutiveButton != null)
        {
            BindSharedButtonWithIndex(m_StopFreezeConsecutiveButton, i =>
            {
                PlayersStates.Instance?.RequestStopFreezeConsecutive(i);
                // 状態変更後にUIを再同期
                RefreshUI();
            });
        }
    }

    private void OnEnable()
    {
        RefreshUI();
    }

    private void Start()
    {
        // Awake/OnEnable 時点で PlayersStates.Instance が未初期化のケースに備え、
        // Start タイミングでもう一度同期して初期表示を正す
        RefreshUI();
    }

    // 外部API: 次へ
    public void Next()
    {
        SetSelectedIndex(m_CurrentIndex + 1);
        Debug.Log($"[Characonfig] Next pressed -> index: {m_CurrentIndex}");
    }

    // 外部API: 前へ
    public void Prev()
    {
        SetSelectedIndex(m_CurrentIndex - 1);
        Debug.Log($"[Characonfig] Prev pressed -> index: {m_CurrentIndex}");
    }

    // 外部API: インデックス指定
    public void SetSelectedIndex(int index)
    {
        int count = GetAllyCount();
        if (count <= 0) return;

        int clamped = Mathf.Clamp(index, 0, count - 1);
        if (clamped == m_CurrentIndex)
        {
            UpdateNavInteractable(count);
            return;
        }

        m_CurrentIndex = clamped;
        RefreshUI();
        _onSelectionChanged.OnNext(m_CurrentIndex);
    }

    // 外部API: enum指定（PlayersStates.AllyId）
    public void SetSelectedAllyByEnum(PlayersStates.AllyId id)
    {
        SetSelectedIndex((int)id);
    }

    // 外部API: アクターからインデックスへ変換して選択同期
    public void SetSelectedByActor(BaseStates actor)
    {
        var ps = PlayersStates.Instance;
        if (actor == null || ps == null) return;
        if (ps.TryGetAllyIndex(actor, out var idx))
        {
            SetSelectedIndex(idx);
        }
    }

    private void RefreshUI()
    {
        var actor = GetActor(m_CurrentIndex);

        //キャラ名表示
        if (m_CharacterNameText != null)
        {
            m_CharacterNameText.text = actor.CharacterName;
        }        

        // 割り込みカウンターActive（単独UI）のラジオを、現在インデックスのキャラにバインド
        if (m_InterruptCounterRadio != null)
        {
            if (actor != null)
            {
                m_InterruptCounterRadio.AddListener(actor.OnSelectInterruptCounterActiveBtnCallBack);
                // 現在の状態をUIに反映（0: 有効, 1: 無効）
                m_InterruptCounterRadio.SetOnWithoutNotify(actor.IsInterruptCounterActive ? 0 : 1);
            }
            else
            {
                // アクターが取得できない場合は未選択状態にしておく
                m_InterruptCounterRadio.SetOnWithoutNotify(-1);
            }
        }

        // FreezeConsecutive 停止予約ボタン（単独UI）の表示更新
        if (m_StopFreezeConsecutiveButton != null)
        {
            bool visible = actor != null && actor.IsNeedDeleteMyFreezeConsecutive() && !actor.IsDeleteMyFreezeConsecutive;
            m_StopFreezeConsecutiveButton.gameObject.SetActive(visible);
            Debug.Log($"[Characonfig] FreezeStop visible={visible}, idx={m_CurrentIndex}, actor={(actor!=null)}, need={actor?.IsNeedDeleteMyFreezeConsecutive()}, deleted={actor?.IsDeleteMyFreezeConsecutive}");
        }

        if (actor == null || m_StatesBanner == null) {
            UpdateNavInteractable(GetAllyCount());
            return;
        }

        // StatesBanner への反映（将来の装飾差替えもここに集約）
        // 一括バインドで AttrP の購読 + ゲージ/テキストの即値反映を行う
        m_StatesBanner.Bind(actor);
        // キャラインデックスに応じて背景スプライトを切替
        m_StatesBanner.SetCharacterIndex(m_CurrentIndex);

        UpdateNavInteractable(GetAllyCount());
    }

    private void UpdateNavInteractable(int count)
    {
        bool hasMany = count > 1;
        if (m_LeftButton != null)
            m_LeftButton.interactable = hasMany && (m_CurrentIndex > 0);
        if (m_RightButton != null)
            m_RightButton.interactable = hasMany && (m_CurrentIndex < count - 1);
    }

    private int GetAllyCount()
    {
        var ps = PlayersStates.Instance;
        return ps?.AllyCount ?? 0;
    }

    private AllyClass GetActor(int index)
    {
        var ps = PlayersStates.Instance;
        if (ps == null) return null;
        return index switch
        {
            0 => ps.geino,
            1 => ps.noramlia,
            2 => ps.sites,
            _ => null
        };
    }

    // 共有ボタンに「現在のindexで」コールバックを流したい場合のユーティリティ
    // 例: 設定系の共有ボタンから「現在選択中キャラのみに適用」するようなとき
    public void BindSharedButtonWithIndex(Button button, Action<int> onClickWithIndex)
    {
        if (button == null || onClickWithIndex == null) return;
        button.onClick.AddListener(() => onClickWithIndex(m_CurrentIndex));
    }
}