using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using System.Collections;

/// <summary>
/// VRMenuController_XR - Контроллер VR меню для XR Interaction Toolkit
/// Автор: NTO VR Final Helper
/// Версия: 2.0
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
    [Tooltip("XR Controller для этого меню")]
    public XRController xrController;

    [Tooltip("Действие для открытия меню")]
    public InputActionReference menuAction;

    [Tooltip("Действие для выбора/триггера")]
    public InputActionReference selectAction;

    [Header("🔦 Луч")]
    [Tooltip("Длина луча")]
    public float rayLength = 10f;

    [Tooltip("Объект-указатель на конце луча")]
    public GameObject pointerTip;

    [Tooltip("Слои для игнорирования")]
    public LayerMask ignoreLayers;

    [Header("📳 Вибрация")]
    [Tooltip("Сила вибрации при наведении (0-1)")]
    public float hoverHapticStrength = 0.3f;

    [Tooltip("Длительность вибрации")]
    public float hapticDuration = 0.1f;

    [Header("🎭 Меню")]
    [Tooltip("Объект меню")]
    public GameObject menuObject;

    [Tooltip("Контроллер позиции меню")]
    public MenuPositionController1 menuPositionController;

    [Tooltip("Анимация открытия")]
    public bool useAnimation = true;

    [Tooltip("Длительность анимации")]
    public float animationDuration = 0.3f;

    [Tooltip("Расстояние от головы до меню")]
    public float menuDistance = 1.5f;
    #endregion

    #region Приватные поля
    private bool _isMenuOpen = false;
    private Vector3 _originalScale;
    private LineRenderer _rayLine;
    private GameObject _hoveredObject;
    private UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor _rayInteractor;
    private Coroutine _animCoroutine;
    private Transform _headTransform;
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
        if (selectAction?.action != null)
            selectAction.action.performed -= OnSelectActionPerformed;
    }

    private void Update()
    {
        UpdateRayPointer();
        CheckFallbackInput();
    }
    #endregion

    #region Инициализация
    private void InitializeComponents()
    {
        // 🔍 Найти камеру (голову)
        _headTransform = Camera.main?.transform;
        if (_headTransform == null)
        {
            Debug.LogError("[VRMenu] Камера не найдена!");
        }

        // 💾 Сохраняем масштаб меню
        if (menuObject != null)
            _originalScale = menuObject.transform.localScale;

        // 🎮 Получаем или создаём XRController
        if (xrController == null)
            xrController = GetComponent<XRController>();

        if (xrController == null)
        {
            Debug.LogWarning("[VRMenu] XRController не найден на этом объекте!");
        }

        // 🔦 Получаем интерактор (луч)
        _rayInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
        if (_rayInteractor == null)
        {
            _rayInteractor = gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
            Debug.Log("[VRMenu] XRRayInteractor добавлен");
        }

        // 🎨 Настраиваем луч
        SetupRayPointer();

        // 🔗 Проверяем MenuPositionController
        if (menuPositionController == null)
        {
            menuPositionController = FindObjectOfType<MenuPositionController1>();
        }
    }

    private void SetupInput()
    {
        if (menuAction?.action != null)
        {
            menuAction.action.performed += OnMenuActionPerformed;
            menuAction.action.Enable();
            Debug.Log("[VRMenu] Menu Action подключён");
        }
        else
        {
            Debug.LogWarning("[VRMenu] Menu Action не назначен! Используйте клавиатуру (M/Escape)");
        }

        if (selectAction?.action != null)
        {
            selectAction.action.performed += OnSelectActionPerformed;
            selectAction.action.Enable();
        }
    }

    private void SetupRayPointer()
    {
        _rayLine = gameObject.AddComponent<LineRenderer>();
        _rayLine.positionCount = 2;
        _rayLine.startWidth = _rayLine.endWidth = 0.002f;
        _rayLine.material = new Material(Shader.Find("Unlit/Color"));
        _rayLine.startColor = _rayLine.endColor = Color.white;
        _rayLine.enabled = true;
    }
    #endregion

    #region Обработка ввода
    private void OnMenuActionPerformed(InputAction.CallbackContext context)
    {
        ToggleMenu();
    }

    private void OnSelectActionPerformed(InputAction.CallbackContext context)
    {
        // Обработка клика лучом
        if (_hoveredObject != null)
        {
            if (_hoveredObject.CompareTag("MenuToggleButton"))
                ToggleMenu();
        }
    }

    private void CheckFallbackInput()
    {
        // Резервная проверка для редактора
        if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleMenu();
        }
    }
    #endregion

    #region Луч + Вибрация
    private void UpdateRayPointer()
    {
        if (_rayLine == null) return;

        // 🎯 Пускаем луч
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;
        bool didHit = Physics.Raycast(ray, out hit, rayLength, ~ignoreLayers);

        // 📏 Визуал
        Vector3 endPos = didHit ? hit.point : transform.position + transform.forward * rayLength;
        _rayLine.SetPosition(0, transform.position);
        _rayLine.SetPosition(1, endPos);

        if (pointerTip != null)
        {
            pointerTip.transform.position = endPos;
            pointerTip.SetActive(didHit);
        }

        // 🔄 Наведение
        GameObject newTarget = didHit ? hit.collider.gameObject : null;
        if (newTarget != _hoveredObject)
        {
            if (_hoveredObject != null) TriggerHaptic(0.1f);
            if (newTarget != null) TriggerHaptic(hoverHapticStrength);
            _hoveredObject = newTarget;
        }

        // 👆 Клик по объекту
        if (_hoveredObject != null && IsSelectPressed())
        {
            if (_hoveredObject.CompareTag("MenuToggleButton"))
                ToggleMenu();
        }

        // 🎨 Цвет луча
        _rayLine.startColor = _rayLine.endColor =
            _hoveredObject != null ? Color.yellow : Color.white;
    }

    private bool IsSelectPressed()
    {
        if (selectAction?.action != null && selectAction.action.enabled)
        {
            if (selectAction.action.ReadValue<float>() > 0.5f)
                return true;
        }
        return Input.GetMouseButtonDown(0);
    }

    private void TriggerHaptic(float strength)
    {
        if (xrController != null)
        {
            xrController.SendHapticImpulse(strength, hapticDuration);
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

        // 🔹 Позиционируем меню перед головой
        PositionMenuInFrontOfHead();

        // 🔹 Анимация
        if (useAnimation)
        {
            if (_animCoroutine != null) StopCoroutine(_animCoroutine);
            _animCoroutine = StartCoroutine(AnimateMenuOpen());
        }

        // 🔹 Уведомляем MenuPositionController
        if (menuPositionController != null)
        {
            menuPositionController.OnMenuOpened();
        }

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

        if (menuPositionController != null)
        {
            menuPositionController.OnMenuClosed();
        }

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

    /// <summary>
    /// Позиционирует меню перед головой
    /// </summary>
    private void PositionMenuInFrontOfHead()
    {
        if (_headTransform == null || menuObject == null) return;

        // Позиция перед головой
        Vector3 menuPosition = _headTransform.position + _headTransform.forward * menuDistance;

        // Небольшое смещение вниз (чтобы не закрывало обзор)
        menuPosition += _headTransform.up * -0.1f;

        menuObject.transform.position = menuPosition;

        // Поворот к голове
        menuObject.transform.rotation = Quaternion.LookRotation(-_headTransform.forward, Vector3.up);

        // Сброс масштаба
        menuObject.transform.localScale = _originalScale;

        Debug.Log($"[VRMenu] Меню позиционировано: {menuPosition}");
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
            float t = elapsed / animationDuration;
            menuObject.transform.localScale = Vector3.Lerp(Vector3.zero, _originalScale, t);
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
            float t = elapsed / (animationDuration * 0.5f);
            menuObject.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
            yield return null;
        }

        menuObject.SetActive(false);
        menuObject.transform.localScale = _originalScale;
        _animCoroutine = null;
    }
    #endregion
}