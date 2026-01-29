using System;
using System.Collections.Generic;
using System.Text;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class CharaconfigController : MonoBehaviour, IPlayersContextConsumer
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

    [Header("TenDays Viewer (Characonfig)")]
    //[SerializeField] private Button m_OpenTenDayAbilityButton;
    [SerializeField] private TenDaysMordaleAreaController m_TenDaysArea;

    [Header("Shared Buttons (Characonfig)")]
    [SerializeField] private Button m_OpenEmotionalAttachmentButton;
    [SerializeField] private Button m_StopFreezeConsecutiveButton;
    [Header("Toggle Groups (Characonfig)")]
    [SerializeField] private ToggleSingleController m_InterruptCounterToggle;

    [Header("Passive List (Characonfig)")]
    [SerializeField] private TextMeshProUGUI[] m_PassivesTexts; // 複数フィールドに分割表示
    [SerializeField] private PassivesMordaleAreaController m_PassivesModalArea; // パッシブ専用モーダル
    [Header("Passive List Debug")]    
    [SerializeField] private bool m_PassivesDebugMode = false;
    [SerializeField] private int m_PassivesDebugCount = 100;
    [SerializeField] private string m_PassivesDebugPrefix = "pas";
    [Header("Passive List Fit Settings")]    
    [SerializeField] private int m_PassivesEllipsisDotCount = 4;    // 末尾ドット数
    [SerializeField] private float m_PassivesFitSafety = 1.0f;      // 高さ方向の余白
    [SerializeField] private bool m_PassivesAlwaysAppendEllipsis = true; // 常に末尾ドットを付加

    /// <summary>
    /// 現在選択中のキャラクターのインデックス
    /// </summary>
    private int m_CurrentIndex = 0;
    private IPlayersSkillUI skillUi;
    private IPlayersParty playersParty;
    private IPlayersRoster playersRoster;
    private IPartyComposition composition;

    /// <summary>
    /// パーティーメンバーのIDリスト（固定順序: Geino, Noramlia, Sites, その他）
    /// </summary>
    private readonly List<CharacterId> _partyMemberIds = new();

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
                var actor = GetActor(i);
                if (actor == null) return;

                if (skillUi != null)
                {
                    // CharacterId版を使用（新キャラ対応）
                    skillUi.OpenEmotionalAttachmentSkillSelectUIArea(actor.CharacterId);
                }
                else
                {
                    Debug.LogError("CharaconfigController: SkillUI が null です");
                }
            });
        }
        if (m_StopFreezeConsecutiveButton != null)
        {
            BindSharedButtonWithIndex(m_StopFreezeConsecutiveButton, i =>
            {
                var actor = GetActor(i);
                if (actor == null) return;

                if (playersParty != null)
                {
                    // CharacterId版を使用（新キャラ対応）
                    playersParty.RequestStopFreezeConsecutive(actor.CharacterId);
                }
                else
                {
                    Debug.LogError("CharaconfigController: Party が null です");
                }
                // 状態変更後にUIを再同期
                RefreshUI();
            });
        }

        // パッシブモーダル（最後のフィールド）オープン用バインド
        BindPassivesModalOpenButton();
    }

    private void OnEnable()
    {
        PlayersContextRegistry.Register(this);
    }

    private void OnDisable()
    {
        PlayersContextRegistry.Unregister(this);
    }


    private void Start()
    {
        // Awake/OnEnable 時点で PlayersStates.Instance が未初期化のケースに備え、
        // Start タイミングでもう一度同期して初期表示を正す
        RefreshUI();
    }

    public void InjectPlayersContext(PlayersContext context)
    {
        skillUi = context?.SkillUI;
        playersParty = context?.Party;
        playersRoster = context?.Roster;
        composition = context?.Composition;
        RefreshPartyMemberIds();
    }

    /// <summary>
    /// パーティーメンバーIDリストを更新（固定順序でソート）
    /// </summary>
    private void RefreshPartyMemberIds()
    {
        _partyMemberIds.Clear();
        if (composition == null) return;

        // 固定順序: Geino, Noramlia, Sites, その他（将来の新キャラ）
        var fixedOrder = new[] { CharacterId.Geino, CharacterId.Noramlia, CharacterId.Sites };
        var activeIds = composition.ActiveMemberIds;

        // 固定順序のキャラを先に追加
        foreach (var id in fixedOrder)
        {
            if (activeIds.Contains(id))
            {
                _partyMemberIds.Add(id);
            }
        }

        // 固定順序以外のキャラ（新キャラ）を追加
        foreach (var id in activeIds)
        {
            if (!fixedOrder.Contains(id))
            {
                _partyMemberIds.Add(id);
            }
        }
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

    // 外部API: 十日能力モーダルを開く（現在選択中キャラ）
    public void OnClickOpenTenDayAbility()
    {
        var actor = GetActor(m_CurrentIndex);
        if (m_TenDaysArea == null) return;

        // Instance が未確立でも拾えるようフォールバック検索
        var mc = ModalAreaController.Instance ?? FindObjectOfType<ModalAreaController>(true);
        if (mc == null)
        {
            Debug.LogWarning("[Characonfig] ModalAreaController not found.");
            return;
        }

        mc.ShowSingle(m_TenDaysArea.gameObject);
        m_TenDaysArea.SetPageIndex(0);
        m_TenDaysArea.Bind(actor);
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

    // 外部API: CharacterId指定
    public void SetSelectedByCharacterId(CharacterId id)
    {
        if (!id.IsValid) return;

        // パーティーメンバーリストから該当するインデックスを検索
        for (int i = 0; i < _partyMemberIds.Count; i++)
        {
            if (_partyMemberIds[i] == id)
            {
                SetSelectedIndex(i);
                return;
            }
        }

        Debug.LogWarning($"CharaconfigController: CharacterId '{id}' はパーティーに含まれていません");
    }

    // 外部API: アクターからインデックスへ変換して選択同期
    public void SetSelectedByActor(BaseStates actor)
    {
        if (actor == null || playersRoster == null) return;

        if (playersRoster.TryGetCharacterId(actor, out var characterId))
        {
            SetSelectedByCharacterId(characterId);
        }
    }

    private void RefreshUI()
    {
        // パーティー編成が変わっている可能性があるため、毎回リフレッシュ
        RefreshPartyMemberIds();

        // インデックスが範囲外になった場合は補正
        if (m_CurrentIndex >= _partyMemberIds.Count)
        {
            m_CurrentIndex = Mathf.Max(0, _partyMemberIds.Count - 1);
        }

        var actor = GetActor(m_CurrentIndex);

        //キャラ名表示
        if (m_CharacterNameText != null)
        {
            // PlayersStates.Instance が未初期化で actor が null の可能性があるためガード
            m_CharacterNameText.text = actor != null ? actor.CharacterName : string.Empty;
        }        

        // パッシブ一覧の表示更新（複数フィールドに分割。各フィールド自身のRectに収める）
        UpdatePassivesTexts(actor);

        // 割り込みカウンターActive（単独UI）のラジオを、現在インデックスのキャラにバインド
        if (m_InterruptCounterToggle != null)
        {
            if (actor != null)
            {
                m_InterruptCounterToggle.AddListener(actor.OnSelectInterruptCounterActiveBtnCallBack);
                // 現在の状態をUIに反映（0: 有効, 1: 無効）
                m_InterruptCounterToggle.SetOnWithoutNotify(actor.IsInterruptCounterActive ? 0 : 1);
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

    // ====== Passive List: 表示更新（Characonfig・複数フィールド） ======
    private void UpdatePassivesTexts(BaseStates actor)
    {
        if (m_PassivesTexts == null || m_PassivesTexts.Length == 0)
            return;

        // まず全フィールドの描画条件を固定（測定と描画の一致）
        for (int i = 0; i < m_PassivesTexts.Length; i++)
        {
            var tmp = m_PassivesTexts[i];
            if (tmp == null) continue;
            PassiveTextUtils.SetupTmpBasics(tmp, truncate: true);
        }

        // トークン列を構築
        string allTokens;
        if (actor == null)
        {
            allTokens = string.Empty;
        }
        else
        {
            allTokens = m_PassivesDebugMode
                ? PassiveTextUtils.BuildDummyPassivesTokens(m_PassivesDebugCount, m_PassivesDebugPrefix)
                : PassiveTextUtils.BuildPassivesTokens(actor);
        }

        if (string.IsNullOrEmpty(allTokens))
        {
            // 何もない時は全フィールド空
            for (int i = 0; i < m_PassivesTexts.Length; i++)
            {
                if (m_PassivesTexts[i] != null) m_PassivesTexts[i].text = string.Empty;
            }
            // パッシブボタンの有効性を更新
            UpdatePassiveButtonInteractable();
            return;
        }

        // 空白で分割（半角スペース1つ区切り前提）
        var tokens = allTokens.Split(' ');
        int cursor = 0;
        int lastIndex = m_PassivesTexts.Length - 1;
        for (int i = 0; i < m_PassivesTexts.Length; i++)
        {
            var tmp = m_PassivesTexts[i];
            if (tmp == null)
                continue;

            if (cursor >= tokens.Length)
            {
                tmp.text = string.Empty;
                continue;
            }

            if (i < lastIndex)
            {
                // 先頭〜N-2: はみ出しそうなトークンは次のフィールドへ回す（ドットなし）
                string acc = string.Empty;
                int start = cursor;
                while (cursor < tokens.Length)
                {
                    string next = tokens[cursor];
                    string trial = string.IsNullOrEmpty(acc) ? next : acc + " " + next;
                    if (PassiveTextUtils.FitsHeight(tmp, trial, m_PassivesFitSafety))
                    {
                        acc = trial;
                        cursor++;
                    }
                    else
                    {
                        break;
                    }
                }
                tmp.text = acc; // 空でもOK（次フィールドで続き表示）
            }
            else
            {
                // 最後のフィールド: 余りをまとめて省略付きで収める
                string rest = string.Join(" ", tokens, cursor, tokens.Length - cursor);
                if (string.IsNullOrEmpty(rest))
                {
                    tmp.text = string.Empty;
                }
                else
                {
                    string fitted = PassiveTextUtils.FitTextIntoRectWithEllipsis(
                        rest,
                        tmp,
                        Mathf.Max(1, m_PassivesEllipsisDotCount),
                        Mathf.Max(0f, m_PassivesFitSafety),
                        m_PassivesAlwaysAppendEllipsis
                    );
                    tmp.text = fitted;
                }
                // 最終フィールドで終了
                break;
            }
        }
        
        // パッシブテキスト更新後にボタンの有効性を更新
        UpdatePassiveButtonInteractable();
    }

    // パッシブ列挙（<> リテラル表示。トークン間は半角スペース1つ）
    private string BuildPassivesTokens(BaseStates actor)
    {
        return PassiveTextUtils.BuildPassivesTokens(actor);
    }

    // デバッグ用：ダミーパッシブ列挙
    private string BuildDummyPassivesTokens(int count, string prefix)
    {
        return PassiveTextUtils.BuildDummyPassivesTokens(count, prefix);
    }

    // フィット＋省略（••••）: テキスト自身のRectTransformに収める
    // 以降の高度な省略フィット処理はユーティリティへ寄せています。

    private int GetAllyCount()
    {
        return _partyMemberIds.Count;
    }

    private AllyClass GetActor(int index)
    {
        if (index < 0 || index >= _partyMemberIds.Count)
        {
            return null;
        }
        var id = _partyMemberIds[index];
        return playersRoster?.GetAlly(id);
    }

    // 共有ボタンに「現在のindexで」コールバックを流したい場合のユーティリティ
    // 例: 設定系の共有ボタンから「現在選択中キャラのみに適用」するようなとき
    public void BindSharedButtonWithIndex(Button button, Action<int> onClickWithIndex)
    {
        if (button == null || onClickWithIndex == null) return;
        button.onClick.AddListener(() => onClickWithIndex(m_CurrentIndex));
    }

    // 最後のパッシブ表示フィールドをタップしたらモーダルを開く
    private void BindPassivesModalOpenButton()
    {
        if (m_PassivesTexts == null || m_PassivesTexts.Length == 0) return;
        var last = m_PassivesTexts.LastOrDefault(t => t != null);
        if (last == null) return;

        var go = last.gameObject;
        var btn = go.GetComponent<Button>();
        if (btn == null)
        {
            btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = last; // TMP自体をターゲットに
        }
        
        // ボタンの初期設定のみ行い、有効性チェックはUpdatePassiveButtonInteractableで実行
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnPassiveButtonClicked);
    }
    
    // パッシブボタンがクリックされた時の処理（実際のパッシブデータがある場合のみモーダルを開く）
    private void OnPassiveButtonClicked()
    {
        // 現在のキャラクターの実際のパッシブデータをチェック
        var actor = GetActor(m_CurrentIndex);
        if (actor == null) return;
        
        // 実際のパッシブトークンを取得（デバッグモードでない場合のみ）
        string actualPassiveTokens = m_PassivesDebugMode 
            ? PassiveTextUtils.BuildDummyPassivesTokens(m_PassivesDebugCount, m_PassivesDebugPrefix)
            : PassiveTextUtils.BuildPassivesTokens(actor);
        
        // 実際のパッシブデータが存在する場合のみモーダルを開く
        bool hasActualPassiveData = !string.IsNullOrEmpty(actualPassiveTokens) && !string.IsNullOrWhiteSpace(actualPassiveTokens);
        
        UnityEngine.Debug.Log($"[OnPassiveButtonClicked] actualPassiveTokens: '{actualPassiveTokens}' hasActualPassiveData: {hasActualPassiveData}");
        
        if (hasActualPassiveData)
        {
            OpenPassivesModalForCurrent();
        }
        else
        {
            UnityEngine.Debug.Log("[OnPassiveButtonClicked] パッシブデータが存在しないためモーダルを開きませんでした");
        }
    }
    
    // パッシブボタンの有効性を更新（UI更新時に呼び出す）
    private void UpdatePassiveButtonInteractable()
    {
        if (m_PassivesTexts == null || m_PassivesTexts.Length == 0) return;
        var last = m_PassivesTexts.LastOrDefault(t => t != null);
        if (last == null) return;

        var btn = last.gameObject.GetComponent<Button>();
        if (btn == null) return;

        // 現在のキャラクターの実際のパッシブデータをチェック
        var actor = GetActor(m_CurrentIndex);
        if (actor == null)
        {
            btn.interactable = false;
            return;
        }
        
        // 実際のパッシブトークンを取得（デバッグモードでない場合のみ）
        string actualPassiveTokens = m_PassivesDebugMode 
            ? PassiveTextUtils.BuildDummyPassivesTokens(m_PassivesDebugCount, m_PassivesDebugPrefix)
            : PassiveTextUtils.BuildPassivesTokens(actor);
        
        // 実際のパッシブデータが存在する場合のみボタンを有効化
        bool hasActualPassiveData = !string.IsNullOrEmpty(actualPassiveTokens) && !string.IsNullOrWhiteSpace(actualPassiveTokens);
        btn.interactable = hasActualPassiveData;
        
        UnityEngine.Debug.Log($"[UpdatePassiveButtonInteractable] hasActualPassiveData: {hasActualPassiveData} button.interactable: {btn.interactable}");
    }

    private void OpenPassivesModalForCurrent()
    {
        if (m_PassivesModalArea == null)
        {
            Debug.LogWarning("[Characonfig] m_PassivesModalArea is null. Assign in inspector.");
            return;
        }
        var actor = GetActor(m_CurrentIndex);
        if (actor == null)
        {
            Debug.LogWarning("[Characonfig] actor is null.");
            return;
        }
        // デバッグ設定をモーダルへ伝搬
        m_PassivesModalArea.SetDebug(m_PassivesDebugMode, m_PassivesDebugCount, m_PassivesDebugPrefix);
        m_PassivesModalArea.ShowFor(actor);
    }
}
