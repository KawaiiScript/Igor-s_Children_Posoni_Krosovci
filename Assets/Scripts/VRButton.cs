using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VRButton - Кнопка для VR через Unity Button компонент
/// Все действия настраиваются через Button.onClick в Inspector
/// </summary>
public class VRButton : MonoBehaviour
{
    [Header("⚙️ Настройки")]
    [Tooltip("Тег объекта который может нажать кнопку")]
    public string requiredTag = "XRController";

    [Header("🎨 Визуал")]
    [Tooltip("Button компонент (автоматически находится)")]
    public Button button;

    [Tooltip("Цвет при наведении")]
    public Color hoverColor = Color.yellow;

    [Tooltip("Цвет в обычном состоянии")]
    public Color normalColor = Color.white;

    [Header("🐛 Debug")]
    public bool showDebug = true;

    private bool _isHovered = false;
    private Collider _buttonCollider;
    private Image _buttonImage;
    private Color _originalColor;

    private void Awake()
    {
        // 🔹 Автопоиск Button компонента
        if (button == null)
            button = GetComponent<Button>();

        // 🔹 Автопоиск Image для цвета
        if (button != null)
        {
            _buttonImage = button.targetGraphic as Image;
        }
        else
        {
            _buttonImage = GetComponent<Image>();
        }

        // 🔹 Сохраняем оригинальный цвет
        if (_buttonImage != null)
            _originalColor = _buttonImage.color;

        // 🔹 Автодобавление коллайдера
        _buttonCollider = GetComponent<Collider>();
        if (_buttonCollider == null)
        {
            _buttonCollider = gameObject.AddComponent<BoxCollider>();
            _buttonCollider.isTrigger = true;
        }

        // 🔹 Отключаем стандартный клик мышью (для VR)
        if (button != null)
            button.interactable = false;
    }

    private void Start()
    {
        UpdateVisuals();

        if (showDebug)
            Debug.Log($"[VRButton] ✅ Инициализировано: {name}");
    }

    // =================================================================
    // TRIGGER EVENTS
    // =================================================================

    private void OnTriggerEnter(Collider other)
    {
        if (other?.gameObject == null) return;
        if (!other.gameObject.CompareTag(requiredTag)) return;

        _isHovered = true;

        if (showDebug)
            Debug.Log($"[VRButton] 🟡 Hover: {other.name}");
        button?.onClick.Invoke();
        UpdateVisuals();
    }

    private void OnTriggerExit(Collider other)
    {
        if (other?.gameObject == null) return;
        if (!other.gameObject.CompareTag(requiredTag)) return;

        _isHovered = false;

        if (showDebug)
            Debug.Log($"[VRButton] ⚪ Exit: {other.name}");

        UpdateVisuals();
    }

    // =================================================================
    // PUBLIC METHODS
    // =================================================================

    /// <summary>
    /// Вызывается из VRButtonTrigger при нажатии триггера
    /// </summary>
    public void Press()
    {
        if (!_isHovered)
        {
            if (showDebug)
                Debug.LogWarning("[VRButton] ❌ Нет наведения!");
            return;
        }

        if (showDebug)
            Debug.Log($"[VRButton] ✅ НАЖАТИЕ: {name}");

        // 🔥 ВЫЗЫВАЕМ Button.onClick — здесь ваши функции!
        button?.onClick.Invoke();

        // Сброс наведения
        _isHovered = false;
        UpdateVisuals();
    }

    // =================================================================
    // PRIVATE METHODS
    // =================================================================

    private void UpdateVisuals()
    {
        if (_buttonImage == null) return;

        if (_isHovered)
            _buttonImage.color = hoverColor;
        else
            _buttonImage.color = _originalColor;
    }

    // =================================================================
    // EDITOR GIZMOS
    // =================================================================

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isHovered ? Color.yellow : Color.blue;
        Gizmos.DrawWireCube(transform.position, transform.lossyScale);
    }
}