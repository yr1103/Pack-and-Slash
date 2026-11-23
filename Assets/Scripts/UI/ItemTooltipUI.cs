using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 보유 아이템·보상 후보 공통 툴팁 하나를 커서 옆에 표시 (마지막으로 Show한 source만 Hide 가능)
/// </summary>
public class ItemTooltipUI : MonoBehaviour
{
    private const int REWARD_LEVEL = 1; // 보상은 항상 1레벨로 지급
    private const string LEVEL_FORMAT = "Lv.{0}";
    private const float PERCENT = 100f;
    private const string LINE_SEPARATOR = "\n";
    private const string BASE_ATTACK_FORMAT = "기본 공격력 {0}";
    private const string LEVEL_BONUS_FORMAT = "레벨 보너스 +{0:0}%";
    private const string ADJACENT_DAMAGE_FORMAT = "인접 효과 공격력 +{0:0}%";
    private const string ADJACENT_FIRE_INTERVAL_FORMAT = "인접 효과 발사 간격 -{0:0}%";
    private const string ADJACENT_SPREAD_FORMAT = "인접 효과 탄퍼짐 -{0:0}%";
    private const string FINAL_ATTACK_FORMAT = "최종 공격력 {0:0.#}";
    private const string LEVEL3_ACTIVE_TEXT = "Lv.3 효과 활성";
    private const string LEVEL3_INACTIVE_TEXT = "Lv.3 효과 비활성";
    private const string EQUIPPED_FORMAT = "장착: {0}";
    private const string UNEQUIPPED_FORMAT = "해제 시: {0}";
    private const string SELL_PRICE_FORMAT = "판매가 {0}";
    private const string UNSELLABLE_TEXT = "판매 불가";
    private const string WEAPON_LABEL = "무기";
    private const string PASSIVE_LABEL = "패시브";
    private const string EXPENDABLE_LABEL = "소비";
    private const string ESSENTIAL_LABEL = "고유";

    private static readonly Vector2 _defaultPivot = new Vector2(0f, 1f); // 커서 오른쪽 아래로 펼침

    [SerializeField] private Inventory _inventory;
    [SerializeField] private RectTransform _tooltipRect; // 툴팁 패널 루트 (활성/비활성 대상, 자신과 같아도 됨)
    [SerializeField] private Image _icon;
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _typeText;
    [SerializeField] private Text _levelText;
    [SerializeField] private Text _descriptionText;
    [SerializeField] private Text _attackText;
    [SerializeField] private GameObject _effectContainer;
    [SerializeField] private Text _equippedEffectText;
    [SerializeField] private Text _unequippedBuffText;
    [SerializeField] private Text _unequippedDebuffText;
    [SerializeField] private Text _sellPriceText;
    [SerializeField] private Vector2 _cursorOffset = new Vector2(16f, -16f); // 커서를 가리지 않도록 띄우는 화면 픽셀 거리

    private object _source; // 현재 표시 요청자, null이면 숨김 상태

    private void Awake()
    {
        _tooltipRect.gameObject.SetActive(false);
    }

    // 패널이 닫혔다 열릴 때 이전 툴팁이 남지 않도록 초기화
    private void OnDisable()
    {
        _source = null;

        // 루트가 자신이면 이미 비활성화 중이라 다시 끄지 않음
        if (_tooltipRect.gameObject != gameObject)
            _tooltipRect.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (_source != null)
            UpdatePosition();
    }

    /// <summary>보유 아이템 표시 (현재 레벨·판매가)</summary>
    public void Show(Inventory.ItemInstance item, object source)
    {
        Display(item, _inventory.GetSellPrice(item), source);
    }

    /// <summary>보상 후보 표시 (1레벨·인접 없음 기준)</summary>
    public void Show(InventoryData data, object source)
    {
        // 미배치(Location None) 인스턴스라 Inventory 계산이 인접 없음·Lv.1 기준으로 나옴
        Inventory.ItemInstance candidate = new Inventory.ItemInstance(data) { Level = REWARD_LEVEL };
        Display(candidate, _inventory.GetSellPrice(data, REWARD_LEVEL), source);
    }

    /// <summary>source가 현재 표시 요청자일 때만 숨김 (다른 아이템 Enter 직후 이전 아이템 Exit 무시)</summary>
    public void Hide(object source)
    {
        if (_source != source)
            return;

        _source = null;
        _tooltipRect.gameObject.SetActive(false);
    }

    // 값이 없는 줄은 비활성화해 레이아웃 공간 제거
    private void Display(Inventory.ItemInstance item, int sellPrice, object source)
    {
        _source = source;
        InventoryData data = item.Data;

        _icon.sprite = data.Icon;
        _nameText.text = data.ItemName;
        _typeText.text = GetTypeLabel(data.ItemType);
        _descriptionText.text = data.Description;

        _levelText.gameObject.SetActive(data.MaxLevel > 1);
        _levelText.text = string.Format(LEVEL_FORMAT, item.Level);

        string statText = GetStatText(item);
        _attackText.gameObject.SetActive(statText != null);
        if (statText != null)
            _attackText.text = statText;

        bool hasEquipped = SetEffectLine(_equippedEffectText, EQUIPPED_FORMAT, data.EquippedEffect);
        bool hasBuff = SetEffectLine(_unequippedBuffText, UNEQUIPPED_FORMAT, data.UnequippedBuff);
        bool hasDebuff = SetEffectLine(_unequippedDebuffText, UNEQUIPPED_FORMAT, data.UnequippedDebuff);
        _effectContainer.SetActive(hasEquipped || hasBuff || hasDebuff);

        _sellPriceText.text = data.IsSellable ? string.Format(SELL_PRICE_FORMAT, sellPrice) : UNSELLABLE_TEXT;

        _tooltipRect.gameObject.SetActive(true);

        // 줄 활성 상태가 바뀐 직후 크기를 반영해야 위치 보정이 맞음
        LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRect);
        UpdatePosition();
    }

    // 무기는 공격력·인접 효과·Lv.3, 패시브는 인접 효과·Lv.3 여러 줄, 그 외 null (줄 숨김)
    private string GetStatText(Inventory.ItemInstance item)
    {
        InventoryData data = item.Data;
        List<string> lines = new List<string>();

        if (data.ItemType == ItemType.Weapon && data.WeaponDefinition != null)
        {
            float baseDamage = data.WeaponDefinition.ProjectileDamage;
            float levelMultiplier = Inventory.GetWeaponLevelMultiplier(item.Level);
            float damageBonus = _inventory.GetEffectiveAdjacencyBonus(item, AdjacencyEffectType.Damage);

            lines.Add(string.Format(BASE_ATTACK_FORMAT, baseDamage));
            lines.Add(string.Format(LEVEL_BONUS_FORMAT, (levelMultiplier - 1f) * PERCENT));
            AddAdjacencyLine(lines, AdjacencyEffectType.Damage, damageBonus);
            AddAdjacencyLine(lines, AdjacencyEffectType.FireInterval,
                _inventory.GetEffectiveAdjacencyBonus(item, AdjacencyEffectType.FireInterval));
            AddAdjacencyLine(lines, AdjacencyEffectType.Spread,
                _inventory.GetEffectiveAdjacencyBonus(item, AdjacencyEffectType.Spread));
            // TODO : 전투 연결 시 최종 공격력 공식을 Inventory로 옮겨 툴팁·전투가 함께 사용
            lines.Add(string.Format(FINAL_ATTACK_FORMAT, baseDamage * levelMultiplier * (1f + damageBonus)));
        }
        else if (data.ItemType == ItemType.Passive)
        {
            if (data.AdjacencyEffectType != AdjacencyEffectType.None)
                AddAdjacencyLine(lines, data.AdjacencyEffectType, Inventory.GetPassiveAdjacencyBonus(item.Level));
        }
        else
        {
            return null;
        }

        lines.Add(_inventory.IsLevel3EffectActive(item) ? LEVEL3_ACTIVE_TEXT : LEVEL3_INACTIVE_TEXT);
        return string.Join(LINE_SEPARATOR, lines);
    }

    // 값이 0인 인접 효과 줄은 생략
    private static void AddAdjacencyLine(List<string> lines, AdjacencyEffectType effectType, float bonus)
    {
        if (bonus <= 0f)
            return;

        lines.Add(string.Format(GetAdjacencyFormat(effectType), bonus * PERCENT));
    }

    // None은 호출 전에 걸러지므로 default는 Spread
    private static string GetAdjacencyFormat(AdjacencyEffectType effectType)
    {
        switch (effectType)
        {
            case AdjacencyEffectType.Damage:
                return ADJACENT_DAMAGE_FORMAT;
            case AdjacencyEffectType.FireInterval:
                return ADJACENT_FIRE_INTERVAL_FORMAT;
            default:
                return ADJACENT_SPREAD_FORMAT;
        }
    }

    // 값이 비어 있으면 줄을 숨기고 false 반환
    private static bool SetEffectLine(Text text, string format, string value)
    {
        bool hasValue = !string.IsNullOrEmpty(value);
        text.gameObject.SetActive(hasValue);

        if (hasValue)
            text.text = string.Format(format, value);

        return hasValue;
    }

    private static string GetTypeLabel(ItemType itemType)
    {
        switch (itemType)
        {
            case ItemType.Weapon:
                return WEAPON_LABEL;
            case ItemType.Passive:
                return PASSIVE_LABEL;
            case ItemType.Expendables:
                return EXPENDABLE_LABEL;
            default:
                return ESSENTIAL_LABEL;
        }
    }

    // 기본은 커서 오른쪽 아래, 화면을 넘는 축만 커서 반대편으로 뒤집은 뒤 화면 안으로 보정
    // shortcut: Screen Space Overlay 전제(InventoryItemUI와 동일)로 월드 좌표를 화면 좌표로 사용, Camera 캔버스 전환 시 카메라 기준 변환 필요
    private void UpdatePosition()
    {
        if (Mouse.current == null)
            return;

        Vector2 cursor = Mouse.current.position.ReadValue();
        Vector2 size = Vector2.Scale(_tooltipRect.rect.size, _tooltipRect.lossyScale);
        Vector2 pivot = _defaultPivot;
        Vector2 offset = _cursorOffset;

        if (cursor.x + offset.x + size.x > Screen.width)
        {
            pivot.x = 1f; // 오른쪽 끝을 커서에 맞춰 왼쪽으로 펼침
            offset.x = -offset.x;
        }

        if (cursor.y + offset.y - size.y < 0f)
        {
            pivot.y = 0f; // 아래쪽 끝을 커서에 맞춰 위로 펼침
            offset.y = -offset.y;
        }

        Vector2 position = cursor + offset;
        position.x = Mathf.Clamp(position.x, pivot.x * size.x, Screen.width - (1f - pivot.x) * size.x);
        position.y = Mathf.Clamp(position.y, pivot.y * size.y, Screen.height - (1f - pivot.y) * size.y);

        _tooltipRect.pivot = pivot;
        _tooltipRect.position = position;
    }
}
