using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

/// <summary>
/// 武器選択サブパネル（CharaConfigContent内）。
/// TMP_Dropdown で武器リストを表示し、選択時にコールバックを発火する。
/// 能力値不足の武器はグレーアウトし選択不可。
/// 「武器を外す」ボタンでフリーハンドを装備。
/// </summary>
public class WeaponSelectArea : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown weaponDropdown;
    [SerializeField] private Button goBackButton;
    [SerializeField] private Button removeWeaponButton;
    [SerializeField] private TextMeshProUGUI currentWeaponText;
    [SerializeField] private TextMeshProUGUI memoText;
    [SerializeField] private string defaultMemo = "装備する武器を選んでください";
    [SerializeField] private string changedMemo = "{0} に変更しました";

    private readonly List<int> weaponIdMap = new();
    private readonly List<bool> equippableFlags = new();
    private int previousDropdownIndex;
    private UnityAction<int> onSelected;

    private void Awake()
    {
        if (goBackButton != null)
            goBackButton.onClick.AddListener(Close);

        if (removeWeaponButton != null)
            removeWeaponButton.onClick.AddListener(OnRemoveWeaponClicked);

        if (weaponDropdown != null)
            weaponDropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    /// <summary>
    /// 武器リストを表示する
    /// </summary>
    public void ShowWeapons(
        IReadOnlyList<BaseWeapon> weapons,
        int equippedWeaponId,
        TenDayAbilityDictionary characterAbilities,
        UnityAction<int> onWeaponSelected)
    {
        onSelected = onWeaponSelected;

        if (weaponDropdown == null) return;

        weaponDropdown.ClearOptions();
        weaponIdMap.Clear();
        equippableFlags.Clear();

        var options = new List<string>();
        int selectedIndex = 0;

        for (int i = 0; i < weapons.Count; i++)
        {
            var weapon = weapons[i];

            bool canEquip = CanEquip(weapon, characterAbilities);
            string label = FormatWeaponLabel(weapon, canEquip);
            options.Add(label);
            weaponIdMap.Add(weapon.id);
            equippableFlags.Add(canEquip);

            if (weapon.id == equippedWeaponId)
                selectedIndex = options.Count - 1;
        }

        weaponDropdown.AddOptions(options);

        weaponDropdown.SetValueWithoutNotify(selectedIndex);
        previousDropdownIndex = selectedIndex;

        UpdateCurrentWeaponText(weapons, equippedWeaponId);
        UpdateRemoveWeaponButton(equippedWeaponId);

        if (memoText != null)
            memoText.text = defaultMemo;
    }

    private void OnDropdownValueChanged(int index)
    {
        if (index < 0 || index >= weaponIdMap.Count) return;

        // 装備不可の武器を選択した場合は巻き戻す
        if (!equippableFlags[index])
        {
            weaponDropdown.SetValueWithoutNotify(previousDropdownIndex);
            if (memoText != null)
                memoText.text = "能力値が不足しています";
            return;
        }

        previousDropdownIndex = index;

        int weaponId = weaponIdMap[index];
        var weapon = WeaponManager.Instance?.GetAtID(weaponId);
        if (weapon == null) return;

        onSelected?.Invoke(weaponId);

        if (currentWeaponText != null)
            currentWeaponText.text = FormatWeaponInfo(weapon);

        UpdateRemoveWeaponButton(weaponId);

        if (memoText != null)
        {
            memoText.text = changedMemo.Contains("{0}")
                ? string.Format(changedMemo, weapon.name)
                : changedMemo;
        }
    }

    private void OnRemoveWeaponClicked()
    {
        var freehand = WeaponManager.Instance?.GetFreehandWeapon();
        if (freehand == null)
        {
            Debug.LogWarning("WeaponSelectArea: フリーハンド武器が見つかりません");
            return;
        }

        onSelected?.Invoke(freehand.id);

        if (currentWeaponText != null)
            currentWeaponText.text = FormatWeaponInfo(freehand);

        UpdateRemoveWeaponButton(freehand.id);

        if (memoText != null)
            memoText.text = "武器を外しました";
    }

    private void UpdateCurrentWeaponText(IReadOnlyList<BaseWeapon> weapons, int equippedId)
    {
        if (currentWeaponText == null) return;

        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].id == equippedId)
            {
                currentWeaponText.text = "現在: " + FormatWeaponInfo(weapons[i]);
                return;
            }
        }
        currentWeaponText.text = "現在: なし";
    }

    private void UpdateRemoveWeaponButton(int equippedWeaponId)
    {
        if (removeWeaponButton == null) return;
        var freehand = WeaponManager.Instance?.GetFreehandWeapon();
        bool isFreehand = freehand != null && freehand.id == equippedWeaponId;
        removeWeaponButton.interactable = !isFreehand;
    }

    private static bool CanEquip(BaseWeapon weapon, TenDayAbilityDictionary charAbilities)
    {
        if (weapon.TenDayValues == null || weapon.TenDayValues.Count == 0)
            return true;
        if (charAbilities == null)
            return false;

        foreach (var req in weapon.TenDayValues)
        {
            charAbilities.TryGetValue(req.Key, out float charVal);
            if (charVal < req.Value)
                return false;
        }
        return true;
    }

    private static string FormatWeaponLabel(BaseWeapon weapon, bool canEquip)
    {
        string protocolText = weapon.HasMultipleProtocols
            ? string.Join("/", weapon.protocols.Select(p => p.ToDisplayShortText()))
            : weapon.protocol.ToDisplayShortText();
        string label = $"{weapon.name} ({protocolText})";
        if (weapon.IsBlade) label += " [刃]";
        if (!canEquip) label = $"<color=#888888>{label} ※能力不足</color>";
        return label;
    }

    private static string FormatWeaponInfo(BaseWeapon weapon)
    {
        string protocolText = weapon.HasMultipleProtocols
            ? string.Join("/", weapon.protocols.Select(p => p.ToDisplayText()))
            : weapon.protocol.ToDisplayText();
        string info = $"{weapon.name} ({protocolText})";
        if (weapon.IsBlade) info += " [刃物]";
        return info;
    }

    public void Open()
    {
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }
}
