using UnityEngine;

using UnityEngine.InputSystem;

/// <summary>
/// GazePointer_XR - Луч из головы (HMD) для навигации по меню
/// Работает с XR Interaction Toolkit (OpenXR, Oculus, SteamVR)
///
/// Автор: NTO VR Final Helper
/// Версия: 2.0 (XR Toolkit)
/// </summary>
public class GazePointer : MonoBehaviour
{
    #region Singleton
    public static GazePointer Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    #endregion

    #region Настройки луча
    [Header("🎯 Настройки луча")]
    [Tooltip("Длина луча")]
    public float rayLength = 10f;

    [Tooltip("Слой объектов для обнаружения (меню)")]
    public LayerMask menuLayerMask = -1;

    [Tooltip("Точность определения границ")]
    [Range(0.01f, 1f)]
    public float boundaryThreshold = 0.1f;
    #endregion

    #region Визуализация
    [Header("🎨 Визуализация")]
    [Tooltip("Показывать линию луча")]
    public bool showRayLine = true;

    [Tooltip("LineRenderer для луча")]
    public LineRenderer rayLineRenderer;

    [Tooltip("Курсор на конце луча")]
    public GameObject cursorPrefab;

    [Tooltip("Цвет: наведение на меню")]
    public Color rayColorOnMenu = Color.green;

    [Tooltip("Цвет: вне меню")]
    public Color rayColorOffMenu = Color.red;

    [Tooltip("Ширина луча")]
    public float lineWidth = 0.005f;
    #endregion

    #region Камера / Голова
    [Header("👁️ Источник луча (камера)")]
    [Tooltip("Трансформ камеры (HMD)")]
    public Transform headTransform;

    [Tooltip("Автоматически найти камеру")]
    public bool autoFindHead = true;

    [Tooltip("XR Ray Interactor для совместимости с UI")]
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor xrRayInteractor;
    #endregion

    #region Таймаут
    [Header("⏱️ Таймаут")]
    [Tooltip("Время до перезапуска меню (сек)")]
    public float timeoutDuration = 2f;

    [Tooltip("Показывать индикатор отсчёта")]
    public bool showCountdown = true;
    #endregion

    #region Ссылки на меню
    [Header("📋 Меню")]
    [Tooltip("RectTransform меню")]
    public RectTransform menuRectTransform;

    [Tooltip("Контроллер позиции")]
    public MenuPositionController1 menuPositionController;
    #endregion

    #region Состояние (Read Only)
    [Header("📊 Состояние")]
    [SerializeField] private bool _isMenuOpen;
    [SerializeField] private bool _isLookingAtMenu;
    [SerializeField] private float _timeOutsideMenu;
    [SerializeField] private bool _isCountingDown;
    [SerializeField] private Vector3 _currentHitPoint;
    [SerializeField] private Vector3 _currentRayDirection;

    public bool IsLookingAtMenu => _isLookingAtMenu;
    public float CountdownProgress => _isCountingDown ? _timeOutsideMenu / timeoutDuration : 0f;
    #endregion

    #region События
    public event System.Action OnGazeEnterMenu;
    public event System.Action OnGazeExitMenu;
    public event System.Action<float> OnCountdownProgress;
    public event System.Action OnCountdownComplete;
    #endregion

    #region Приватные поля
    private GameObject _cursorInstance;
    private Coroutine _animCoroutine;
    #endregion

    #region Unity Lifecycle
    private void Start() => InitializeComponents();
    private void Update()
    {
        if (!_isMenuOpen) { UpdateRayVisualOnly(); return; }
        UpdateGazeRay();
        UpdateCountdown();
        UpdateRayVisualization();
    }
    private void OnDestroy() => CleanupCursor();
    #endregion

    #region Инициализация
    private void InitializeComponents()
    {
        // 🔍 Найти камеру
        if (headTransform == null && autoFindHead)
        {
            headTransform = Camera.main?.transform;
            if (headTransform == null)
                Debug.LogError("[GazePointer] Не найдена камера! Назначьте headTransform вручную.");
        }

        // 🎨 Настроить луч
        if (showRayLine && rayLineRenderer == null)
        {
            var lineObj = new GameObject("GazeRay");
            lineObj.transform.SetParent(transform, false);
            rayLineRenderer = lineObj.AddComponent<LineRenderer>();
            SetupLineRenderer();
        }

        // 👆 Создать курсор
        if (showRayLine && cursorPrefab == null)
            CreateDefaultCursor();

        // 🔗 XR Ray Interactor (для UI)
        if (xrRayInteractor == null)
            xrRayInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();

        ValidateReferences();
    }

    private void SetupLineRenderer()
    {
        if (rayLineRenderer == null) return;
        rayLineRenderer.positionCount = 2;
        rayLineRenderer.startWidth = rayLineRenderer.endWidth = lineWidth;
        rayLineRenderer.useWorldSpace = true;
        rayLineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        rayLineRenderer.material.color = rayColorOnMenu;
    }

    private void CreateDefaultCursor()
    {
        cursorPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cursorPrefab.name = "GazeCursor";
        cursorPrefab.transform.localScale = Vector3.one * 0.02f;
        Destroy(cursorPrefab.GetComponent<Collider>());
    }

    private void ValidateReferences()
    {
        if (headTransform == null)
            Debug.LogError("[GazePointer] headTransform не назначен!");
        if (menuRectTransform == null)
            Debug.LogWarning("[GazePointer] menuRectTransform не назначен!");
        if (menuPositionController == null)
            menuPositionController = FindObjectOfType<MenuPositionController1>();
    }

    private void CleanupCursor()
    {
        if (_cursorInstance != null) Destroy(_cursorInstance);
    }
    #endregion

    #region Логика луча
    private void UpdateRayVisualOnly()
    {
        if (rayLineRenderer != null) rayLineRenderer.enabled = false;
        if (_cursorInstance != null) _cursorInstance.SetActive(false);
    }

    private void UpdateGazeRay()
    {
        if (headTransform == null) return;

        Vector3 origin = headTransform.position;
        _currentRayDirection = headTransform.forward;
        Ray ray = new Ray(origin, _currentRayDirection);

        if (Physics.Raycast(ray, out RaycastHit hit, rayLength, menuLayerMask))
        {
            _currentHitPoint = hit.point;
            CheckIfHitIsOnMenu(hit);
        }
        else
        {
            _isLookingAtMenu = false;
            _currentHitPoint = origin + _currentRayDirection * rayLength;
        }
    }

    private void CheckIfHitIsOnMenu(RaycastHit hit)
    {
        if (menuRectTransform == null) { _isLookingAtMenu = true; return; }

        Vector3 localPoint = menuRectTransform.InverseTransformPoint(hit.point);
        Rect rect = menuRectTransform.rect;

        bool insideX = localPoint.x >= rect.xMin + boundaryThreshold &&
                       localPoint.x <= rect.xMax - boundaryThreshold;
        bool insideY = localPoint.y >= rect.yMin + boundaryThreshold &&
                       localPoint.y <= rect.yMax - boundaryThreshold;

        bool wasLooking = _isLookingAtMenu;
        _isLookingAtMenu = insideX && insideY;

        if (_isLookingAtMenu && !wasLooking)
        {
            OnGazeEnterMenu?.Invoke();
            Debug.Log("[GazePointer] Взгляд в меню");
        }
        else if (!_isLookingAtMenu && wasLooking)
        {
            OnGazeExitMenu?.Invoke();
            Debug.Log("[GazePointer] Взгляд вне меню");
        }
    }
    #endregion

    #region Таймаут
    private void UpdateCountdown()
    {
        if (!_isLookingAtMenu)
        {
            if (!_isCountingDown) { _isCountingDown = true; _timeOutsideMenu = 0f; }
            _timeOutsideMenu += Time.deltaTime;
            OnCountdownProgress?.Invoke(_timeOutsideMenu / timeoutDuration);

            if (_timeOutsideMenu >= timeoutDuration)
                OnCountdownCompleted();
        }
        else if (_isCountingDown)
        {
            _isCountingDown = false;
            _timeOutsideMenu = 0f;
            OnCountdownProgress?.Invoke(0f);
        }
    }

    private void OnCountdownCompleted()
    {
        Debug.Log("[GazePointer] Таймаут — перезапуск позиции меню");
        menuPositionController?.RepositionMenuToGaze();
        _isCountingDown = false;
        _timeOutsideMenu = 0f;
        OnCountdownComplete?.Invoke();
    }
    #endregion

    #region Визуализация
    private void UpdateRayVisualization()
    {
        if (!showRayLine || headTransform == null) return;

        Vector3 origin = headTransform.position;
        Vector3 end = _currentHitPoint;

        // 🔴 Линия
        if (rayLineRenderer != null)
        {
            rayLineRenderer.enabled = true;
            rayLineRenderer.SetPosition(0, origin);
            rayLineRenderer.SetPosition(1, end);

            Color color = _isLookingAtMenu ? rayColorOnMenu : rayColorOffMenu;
            if (_isCountingDown)
                color = Color.Lerp(rayColorOffMenu, Color.yellow, _timeOutsideMenu / timeoutDuration);
            rayLineRenderer.material.color = color;
        }

        // 🔵 Курсор
        if (_cursorInstance == null && cursorPrefab != null)
            _cursorInstance = Instantiate(cursorPrefab);

        if (_cursorInstance != null)
        {
            _cursorInstance.transform.position = end;
            _cursorInstance.transform.forward = _currentRayDirection;

            if (_isCountingDown)
            {
                float pulse = 1f + Mathf.Sin(Time.time * 10f) * 0.3f;
                _cursorInstance.transform.localScale = Vector3.one * 0.02f * pulse;
            }
            else
            {
                _cursorInstance.transform.localScale = Vector3.one * 0.02f;
            }
        }
    }
    #endregion

    #region Публичные методы
    public void OnMenuOpened()
    {
        _isMenuOpen = true;
        _timeOutsideMenu = 0f;
        _isCountingDown = false;
        Debug.Log("[GazePointer] Меню открыто");
    }

    public void OnMenuClosed()
    {
        _isMenuOpen = false;
        _timeOutsideMenu = 0f;
        _isCountingDown = false;
        if (rayLineRenderer != null) rayLineRenderer.enabled = false;
        if (_cursorInstance != null) _cursorInstance.SetActive(false);
        Debug.Log("[GazePointer] Меню закрыто");
    }

    public Ray GetCurrentRay() =>
        headTransform != null ? new Ray(headTransform.position, headTransform.forward) : new Ray();

    public Vector3 GetPointAlongRay(float distance) =>
        headTransform != null ? headTransform.position + headTransform.forward * distance : Vector3.zero;

    public bool IsLookingAtObject(GameObject obj, float maxDistance = 10f)
    {
        if (headTransform == null || obj == null) return false;
        Ray ray = new Ray(headTransform.position, headTransform.forward);
        return Physics.Raycast(ray, out RaycastHit hit, maxDistance) &&
               (hit.collider.gameObject == obj || hit.collider.transform.IsChildOf(obj.transform));
    }
    #endregion

    #region Editor Gizmos
    private void OnDrawGizmos()
    {
        if (headTransform == null) return;
        Gizmos.color = _isLookingAtMenu ? Color.green : Color.red;
        Gizmos.DrawRay(headTransform.position, headTransform.forward * rayLength);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_currentHitPoint, 0.02f);
    }
    #endregion
}