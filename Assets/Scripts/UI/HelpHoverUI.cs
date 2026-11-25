using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 마우스를 올리면 도움말 패널 표시, 벗어나면 숨김
/// </summary>
public class HelpHoverUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private GameObject _helpPanel;

    private void Awake()
    {
        _helpPanel.SetActive(false);
    }

    private void OnDisable()
    {
        // 상위 패널이 닫혀 비활성화되면 Exit가 오지 않으므로 직접 숨김
        _helpPanel.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _helpPanel.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _helpPanel.SetActive(false);
    }
}
