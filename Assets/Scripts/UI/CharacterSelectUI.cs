using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 선택 패널 (슬롯 선택 시 상세 표시, 선택 후에만 시작 가능, 시작 시 선택 저장 후 GameScene 로드)
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    private const string GAME_SCENE_NAME = "GameScene";
    private const string START_WEAPON_FORMAT = "{0} ({1}칸)"; // "시작 무기" 라벨이 따로 있음, {0} 이름, {1} 점유 칸 수
    private const string UNIQUE_ITEM_FORMAT = "고유 아이템: {0} ({1}칸)";
    private const string LINE_SEPARATOR = "\n";

    [SerializeField] private CharacterSlot[] _slots;
    [SerializeField] private Text _nameText;
    [SerializeField] private Text _weaponText;
    [SerializeField] private Text _uniqueItemText;
    [SerializeField] private Text _descriptionText;
    [SerializeField] private Button _startButton;

    private CharacterDefinition _selected;
    private UnityAction[] _slotClickHandlers; // 람다 리스너를 OnDisable에서 해제하기 위해 보관

    private void Awake()
    {
        _slotClickHandlers = new UnityAction[_slots.Length];
        for (int i = 0; i < _slots.Length; i++)
        {
            int slotIndex = i; // 람다가 반복마다 다른 값을 캡처하도록 복사
            _slotClickHandlers[i] = () => Select(slotIndex);
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].Button.onClick.AddListener(_slotClickHandlers[i]);
        }

        _startButton.onClick.AddListener(StartGame);
    }

    private void OnDisable()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].Button.onClick.RemoveListener(_slotClickHandlers[i]);
        }

        _startButton.onClick.RemoveListener(StartGame);
    }

    // 선택 전 상태로 시작 : 선택 표시 끔, 상세 비움, 시작 버튼 비활성
    private void Start()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            CharacterSlot slot = _slots[i];
            slot.Portrait.sprite = slot.Character.Portrait;
            slot.SelectionMark.SetActive(false);
        }

        _nameText.text = string.Empty;
        _weaponText.text = string.Empty;
        _uniqueItemText.text = string.Empty;
        _descriptionText.text = string.Empty;
        _startButton.interactable = false;
    }

    private void Select(int slotIndex)
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            _slots[i].SelectionMark.SetActive(i == slotIndex);
        }

        _selected = _slots[slotIndex].Character;
        _nameText.text = _selected.DisplayName;
        _weaponText.text = GetItemLine(START_WEAPON_FORMAT, _selected.StartWeaponItem) ?? string.Empty;
        _uniqueItemText.text = GetUniqueItemText(_selected);
        _descriptionText.text = _selected.Description;
        _startButton.interactable = true;
    }

    // 고유 아이템 이름·장착 효과를 한 줄씩, 빈 줄은 생략
    private static string GetUniqueItemText(CharacterDefinition character)
    {
        List<string> lines = new List<string>();
        InventoryData item = character.UniqueItem;

        if (item != null)
        {
            lines.Add(GetItemLine(UNIQUE_ITEM_FORMAT, item));
            // 해제 효과는 Tooltip에서만 안내, 선택 화면은 장착 효과를 기본 패시브처럼 표시
            lines.Add(item.EquippedEffect);
        }

        return JoinNonEmpty(LINE_SEPARATOR, lines.ToArray());
    }

    private static string JoinNonEmpty(string separator, params string[] values)
    {
        return string.Join(separator, values.Where(value => !string.IsNullOrEmpty(value)));
    }

    // 칸수는 실제 점유 칸 수 (바운딩 박스 가로x세로는 빈 칸까지 세어 보여 오해 소지), 도형이 없으면 이름만
    private static string GetItemLine(string format, InventoryData item)
    {
        if (item == null)
            return null;

        if (item.ItemShape == null || item.ItemShape.Length == 0)
            return item.ItemName;

        return string.Format(format, item.ItemName, item.ItemShape.Length);
    }

    private void StartGame()
    {
        CharacterSelection.Selected = _selected;
        SceneManager.LoadScene(GAME_SCENE_NAME);
    }

    /// <summary>선택 슬롯 하나의 캐릭터·버튼·초상화·선택 표시</summary>
    [Serializable]
    private class CharacterSlot
    {
        [SerializeField] private CharacterDefinition _character;
        [SerializeField] private Button _button;
        [SerializeField] private Image _portrait;
        [SerializeField] private GameObject _selectionMark;

        public CharacterDefinition Character => _character;
        public Button Button => _button;
        public Image Portrait => _portrait;
        public GameObject SelectionMark => _selectionMark;
    }
}
