using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using UnityEngine.XR.Interaction.Toolkit;
/// <summary>
/// VRMenuController - Версия только с триггерами (без лучей)
/// Автор: NTO VR Final Helper
/// Версия: 3.0 (Trigger Only)
/// </summary>
public class VRMenuController : MonoBehaviour
{
    #region Singleton
    public static VRMenuController Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Опционально: чтобы меню не сбрасывалось при смене сцен
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    #endregion

    #region Настройки
    [Header("🎮 XR Input")]
    [Tooltip("XR Controller (нужен для вибрации и позиции)")]
    public XRController xrController;

    [Tooltip("Действие для открытия/закрытия самого меню")]
    public InputActionReference menuAction;

    [Header("🎭 Меню")]
    [Tooltip("Объект меню (панель с кнопками)")]
    public GameObject menuObject;

    [Tooltip("Контроллер позиции меню")]
    public MenuPositionController1 menuPositionController;

    [Tooltip("Анимация открытия/закрытия")]
    public bool useAnimation = true;
    public float animationDuration = 0.3f;

    [Tooltip("Расстояние от головы до меню при открытии")]
    public float menuDistance = 1.5f;

    [Header("📳 Вибрация")]
    public float hoverHapticStrength = 0.3f;
    public float hapticDuration = 0.1f;
    #endregion

    #region Приватные поля
    private bool _isMenuOpen = false;
    private Vector3 _originalScale;
    private Coroutine _animCoroutine;
    private Transform _headTransform;
    private string _hoveredTag; // Храним тег объекта, на котором висит курсор
    #endregion

    #region События
    public event System.Action OnMenuOpened;
    public event System.Action OnMenuClosed;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        InitializeComponents();
        SetupInput();
        CloseMenuImmediate();
    }

    private void OnDestroy()
    {
        if (menuAction?.action != null)
            menuAction.action.performed -= OnMenuActionPerformed;
    }

    private void Update()
    {
        CheckFallbackInput();
    }
    #endregion

    #region Инициализация
    private void InitializeComponents()
    {
        _headTransform = Camera.main?.transform;
        if (_headTransform == null)
            Debug.LogError("[VRMenu] Камера не найдена!");

        if (menuObject != null)
            _originalScale = menuObject.transform.localScale;

        if (xrController == null)
            xrController = GetComponent<XRController>();
    }

    private void SetupInput()
    {
        if (menuAction?.action != null)
        {
            menuAction.action.performed += OnMenuActionPerformed;
            menuAction.action.Enable();
        }
        else
        {
            Debug.LogWarning("[VRMenu] Menu Action не назначен! Работают только клавиши M/Escape");
        }
    }
    #endregion

    #region Обработка ввода
    private void OnMenuActionPerformed(InputAction.CallbackContext context)
    {
        ToggleMenu();
    }

    private void CheckFallbackInput()
    {
        if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape))
            ToggleMenu();
    }
    #endregion

    #region Физика триггеров (Взаимодействие с кнопками)
    // Вызывается, когда контроллер входит в триггер кнопки
    private void OnTriggerEnter(Collider other)
    {
        if (!_isMenuOpen) return; // Игнорируем, если меню закрыто

        _hoveredTag = other.CompareTag("MenuToggleButton") ? "MenuToggleButton" : null;

        if (_hoveredTag != null)
        {
            TriggerHaptic(hoverHapticStrength); // Вибрация при наведении
            Debug.Log($"[VRMenu] Наведение на: {other.name}");
        }
    }

    // Вызывается, когда контроллер выходит из триггера кнопки
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("MenuToggleButton"))
        {
            _hoveredTag = null;
        }
    }

    // Вызывается, когда нажимается триггер контроллера (кнопка выбора)
    // Назначьте это событие в инспекторе на XR Controller -> Select Action
    public void OnSelectPressed()
    {
        if (!_isMenuOpen || string.IsNullOrEmpty(_hoveredTag)) return;

        if (_hoveredTag == "MenuToggleButton")
        {
            ToggleMenu(); // Закрываем меню при выборе
            Debug.Log("[VRMenu] Клик по кнопке меню!");
        }
    }
    #endregion

    #region Управление меню
    public void ToggleMenu()
    {
        if (_isMenuOpen) CloseMenu();
        else OpenMenu();
    }

    public void OpenMenu()
    {
        if (_isMenuOpen || menuObject == null) return;

        _isMenuOpen = true;
        menuObject.SetActive(true);

        PositionMenuInFrontOfHead();

        if (useAnimation)
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateMenuOpen());
        }

        menuPositionController?.OnMenuOpened();
        OnMenuOpened?.Invoke();
        Debug.Log("[VRMenu] Меню открыто");
    }

    public void CloseMenu()
    {
        if (!_isMenuOpen || menuObject == null) return;

        _isMenuOpen = false;

        if (useAnimation)
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateMenuClose());
        }
        else
        {
            menuObject.SetActive(false);
        }

        menuPositionController?.OnMenuClosed();
        OnMenuClosed?.Invoke();
        Debug.Log("[VRMenu] Меню закрыто");
    }

    private void CloseMenuImmediate()
    {
        _isMenuOpen = false;
        if (menuObject != null)
        {
            menuObject.SetActive(false);
            menuObject.transform.localScale = _originalScale;
        }
    }

    private void PositionMenuInFrontOfHead()
    {
        if (_headTransform == null || menuObject == null) return;

        Vector3 menuPosition = _headTransform.position + _headTransform.forward * menuDistance;
        menuPosition += _headTransform.up * -0.1f;

        menuObject.transform.position = menuPosition;
        menuObject.transform.rotation = Quaternion.LookRotation(-_headTransform.forward, Vector3.up);
        menuObject.transform.localScale = _originalScale;
    }
    #endregion

    #region Анимация
    private IEnumerator AnimateMenuOpen()
    {
        if (menuObject == null) yield break;
        menuObject.transform.localScale = Vector3.zero;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            menuObject.transform.localScale = Vector3.Lerp(Vector3.zero, _originalScale, elapsed / animationDuration);
            yield return null;
        }
        menuObject.transform.localScale = _originalScale;
        _animCoroutine = null;
    }

    private IEnumerator AnimateMenuClose()
    {
        if (menuObject == null) yield break;
        Vector3 startScale = menuObject.transform.localScale;
        float elapsed = 0f;

        while (elapsed < animationDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            menuObject.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, elapsed / (animationDuration * 0.5f));
            yield return null;
        }
        menuObject.SetActive(false);
        menuObject.transform.localScale = _originalScale;
        _animCoroutine = null;
    }
    #endregion

    #region Утилиты
    private void TriggerHaptic(float strength)
    {
        if (xrController != null)
            xrController.SendHapticImpulse(strength, hapticDuration);
    }
    #endregion
}