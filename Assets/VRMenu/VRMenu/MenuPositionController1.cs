using UnityEngine;
// ❌ УДАЛЕНО: using Valve.VR; — больше не нужен
using System.Collections;

/// <summary>
/// MenuPositionController_XR - Контроллер позиции VR меню для XR Toolkit
/// Отвечает за позиционирование меню по центру луча взгляда
/// с плавной анимацией перехода
///
/// Автор: NTO VR Final Helper
/// Версия: 2.0 (XR Toolkit)
/// </summary>
public class MenuPositionController1 : MonoBehaviour
{
    #region Singleton
    public static MenuPositionController1 Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    #endregion

    #region Helpers
    /// <summary>
    /// Убеждается, что локальный масштаб по всем осям положительный, чтобы UI не зеркалилось.
    /// </summary>
    private void EnsurePositiveScale(Transform t)
    {
        if (t == null) return;
        Vector3 ls = t.localScale;
        Vector3 newLs = new Vector3(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
        if (newLs != ls) t.localScale = newLs;

        for (int i = 0; i < t.childCount; i++)
            EnsurePositiveScale(t.GetChild(i));
    }

    /// <summary>
    /// Проверяет, находится ли меню внутри конуса взгляда и на нужной дистанции
    /// </summary>
    private bool IsMenuInView()
    {
        if (headTransform == null || menuTransform == null) return false;

        Vector3 dirToMenu = (menuTransform.position - headTransform.position);
        float dist = dirToMenu.magnitude;

        if (dist < Mathf.Max(menuDistance - distanceRepositionThreshold, 0f))
            return true;

        dirToMenu.Normalize();
        float angle = Vector3.Angle(headTransform.forward, dirToMenu);

        return angle <= viewAngleThreshold && Mathf.Abs(dist - menuDistance) <= distanceRepositionThreshold;
    }

    private void StartRepositionTimer()
    {
        _outOfViewTimer += Time.deltaTime;
        if (_outOfViewTimer >= autoRepositionDelay)
        {
            RepositionMenuToGaze();
            _outOfViewTimer = 0f;
        }
    }

    private void CancelRepositionTimer() => _outOfViewTimer = 0f;
    #endregion

    #region Ссылки
    [Header("🔗 Основные ссылки")]
    [Tooltip("Трансформ меню для перемещения")]
    public Transform menuTransform;

    [Tooltip("GazePointer_XR для получения направления взгляда")]
    public GazePointer gazePointer;

    [Tooltip("Камера (HMD) — источник луча")]
    public Transform headTransform;

    [Tooltip("Автопоиск камеры при старте")]
    public bool autoFindHead = true;
    #endregion

    #region Настройки позиционирования
    [Header("📐 Позиционирование")]
    [Tooltip("Расстояние от камеры до меню")]
    public float menuDistance = 1.5f;

    [Tooltip("Минимальное расстояние")]
    public float minDistance = 0.5f;

    [Tooltip("Максимальное расстояние")]
    public float maxDistance = 5f;

    [Tooltip("Высота относительно горизонта")]
    [Range(-1f, 1f)]
    public float heightOffset = -0.1f;

    [Tooltip("Угол наклона меню к пользователю")]
    [Range(-45f, 45f)]
    public float tiltAngle = 0f;
    #endregion

    #region Настройки анимации
    [Header("🎬 Анимация")]
    [Tooltip("Использовать плавное перемещение")]
    public bool useSmoothTransition = true;

    [Tooltip("Скорость перемещения")]
    public float moveSpeed = 5f;

    [Tooltip("Скорость поворота")]
    public float rotateSpeed = 8f;

    [Tooltip("Использовать интерполяцию (Lerp/Slerp)")]
    public bool useInterpolation = true;
    #endregion

    #region Настройки прилипания
    [Header("👁️ Прилипание к взгляду")]
    [Tooltip("Следовать за взглядом постоянно")]
    public bool followGaze = false;

    [Tooltip("Автоперемещение при событии GazePointer")]
    public bool autoRepositionOnGaze = true;

    [Tooltip("Закрепить меню в мировых координатах при открытии")]
    public bool lockToWorldOnOpen = true;

    [Tooltip("Применять оси камеры к меню")]
    public bool matchCameraAxes = true;

    [Header("⏱️ Автоперемещение")]
    [Tooltip("Задержка перед перемещением (сек)")]
    public float autoRepositionDelay = 2f;

    [Tooltip("Угол зоны взгляда (градусы)")]
    public float viewAngleThreshold = 20f;

    [Tooltip("Допустимое смещение по дистанции (метры)")]
    public float distanceRepositionThreshold = 0.3f;

    [Tooltip("Скорость следования за взглядом")]
    public float followSpeed = 3f;

    [Tooltip("Максимальный угол поворота при следовании")]
    [Range(0f, 90f)]
    public float maxFollowAngle = 30f;
    #endregion

    #region Состояние
    [Header("📊 Состояние (Read Only)")]
    [SerializeField] private bool _isMenuOpen;
    [SerializeField] private bool _isRepositioning;
    [SerializeField] private Vector3 _targetPosition;
    [SerializeField] private Quaternion _targetRotation;
    [SerializeField] private Vector3 _lastValidPosition;

    private Coroutine _repositionCoroutine;
    private bool _lockedInWorld;
    private float _outOfViewTimer;
    #endregion

    #region События
    public event System.Action OnRepositionStart;
    public event System.Action OnRepositionComplete;
    #endregion

    #region Unity Lifecycle
    private void Start() => InitializeComponents();

    private void Update()
    {
        if (!_isMenuOpen) return;

        if (_isRepositioning) UpdateRepositioning();

        if (matchCameraAxes && !_isRepositioning && headTransform != null && menuTransform != null)
        {
            if (IsMenuInView())
            {
                EnsurePositiveScale(menuTransform);
                CancelRepositionTimer();
            }
            else
            {
                StartRepositionTimer();
            }
        }

        if (!matchCameraAxes && followGaze && !_isRepositioning && !lockToWorldOnOpen)
        {
            UpdateFollowGaze();
        }
    }

    private void OnDestroy()
    {
        // Отписка от событий
        if (gazePointer != null && autoRepositionOnGaze)
        {
            gazePointer.OnCountdownComplete -= RepositionMenuToGaze;
        }
    }
    #endregion

    #region Инициализация
    private void InitializeComponents()
    {
        // 🔍 Найти GazePointer_XR
        if (gazePointer == null)
        {
            gazePointer = FindObjectOfType<GazePointer>();
            if (gazePointer == null)
                Debug.LogWarning("[MenuPosition] GazePointer_XR не найден!");
        }

        // 🔍 Найти камеру (HMD)
        if (headTransform == null && autoFindHead)
        {
            headTransform = Camera.main?.transform;
            if (headTransform == null)
                Debug.LogError("[MenuPosition] Камера не найдена! Назначьте headTransform вручную.");
        }

        // 🔍 Меню
        if (menuTransform == null) menuTransform = transform;

        // 🔗 Подписка на события
        if (gazePointer != null && autoRepositionOnGaze)
        {
            gazePointer.OnCountdownComplete += RepositionMenuToGaze;
        }

        ValidateReferences();

        // Исправляем масштаб сразу
        if (menuTransform != null) EnsurePositiveScale(menuTransform);
    }

    private void ValidateReferences()
    {
        if (headTransform == null) Debug.LogError("[MenuPosition] headTransform не назначен!");
        if (menuTransform == null) Debug.LogError("[MenuPosition] menuTransform не назначен!");
    }
    #endregion

    #region Управление состоянием
    public void OnMenuOpened()
    {
        _isMenuOpen = true;
        SetInitialMenuPosition();
    }

    public void OnMenuClosed()
    {
        _isMenuOpen = false;
        _isRepositioning = false;
        if (_repositionCoroutine != null) StopCoroutine(_repositionCoroutine);
    }

    private void SetInitialMenuPosition()
    {
        if (headTransform == null || menuTransform == null) return;

        Vector3 rayOrigin = headTransform.position;
        Vector3 rayDirection = headTransform.forward;

        Vector3 targetPos = CalculateMenuPositionFromRay(rayOrigin, rayDirection);
        Quaternion targetRot = (matchCameraAxes && headTransform != null)
            ? headTransform.rotation
            : CalculateMenuRotation(targetPos);

        if (lockToWorldOnOpen && menuTransform.parent != null)
            menuTransform.SetParent(null, true);

        menuTransform.position = targetPos;
        menuTransform.rotation = targetRot;
        EnsurePositiveScale(menuTransform);

        _lastValidPosition = targetPos;
        _targetPosition = targetPos;
        _targetRotation = targetRot;
    }
    #endregion

    #region Расчет позиции
    public Vector3 CalculateMenuPosition()
    {
        if (headTransform == null) return Vector3.zero;
        return CalculateMenuPositionFromRay(headTransform.position, headTransform.forward);
    }

    public Vector3 CalculateMenuPositionFromRay(Vector3 rayOrigin, Vector3 rayDirection)
    {
        Vector3 menuPos = rayOrigin + rayDirection.normalized * menuDistance;
        if (headTransform != null) menuPos += headTransform.up * heightOffset;
        return menuPos;
    }

    public Quaternion CalculateMenuRotation(Vector3 menuPosition)
    {
        if (headTransform == null) return Quaternion.identity;

        Vector3 forward = -headTransform.forward;
        forward.Normalize();
        Vector3 up = Vector3.up;
        Quaternion look = Quaternion.LookRotation(forward, up);

        if (Mathf.Abs(tiltAngle) > 0.01f)
            look *= Quaternion.Euler(tiltAngle, 0f, 0f);

        Vector3 e = look.eulerAngles;
        e.z = 0f;
        return Quaternion.Euler(e);
    }
    #endregion

    #region Позиционирование
    public void RepositionMenuToGaze()
    {
        if (headTransform == null || menuTransform == null)
        {
            Debug.LogWarning("[MenuPosition] Невозможно переместить меню");
            return;
        }

        Debug.Log("[MenuPosition] Перезапуск меню в центр луча");

        Vector3 rayOrigin = headTransform.position;
        Vector3 rayDirection = headTransform.forward;

        _targetPosition = CalculateMenuPositionFromRay(rayOrigin, rayDirection);
        _targetRotation = (matchCameraAxes && headTransform != null)
            ? headTransform.rotation
            : CalculateMenuRotation(_targetPosition);

        if (useSmoothTransition) StartSmoothReposition();
        else ApplyPositionImmediately();
    }

    private void StartSmoothReposition()
    {
        _isRepositioning = true;
        OnRepositionStart?.Invoke();
        if (_repositionCoroutine != null) StopCoroutine(_repositionCoroutine);
        _repositionCoroutine = StartCoroutine(SmoothRepositionCoroutine());
    }

    private IEnumerator SmoothRepositionCoroutine()
    {
        Vector3 startPos = menuTransform.position;
        Quaternion startRot = menuTransform.rotation;
        float elapsed = 0f;
        float duration = 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easedT = 1f - (1f - t) * (1f - t); // Ease Out Quad

            menuTransform.position = Vector3.Lerp(startPos, _targetPosition, easedT);
            menuTransform.rotation = Quaternion.Slerp(startRot, _targetRotation, easedT);
            yield return null;
        }

        if (menuTransform.parent != null) menuTransform.SetParent(null, true);
        menuTransform.position = _targetPosition;
        menuTransform.rotation = _targetRotation;
        EnsurePositiveScale(menuTransform);

        _lastValidPosition = _targetPosition;
        _isRepositioning = false;
        _repositionCoroutine = null;
        OnRepositionComplete?.Invoke();
    }

    private void ApplyPositionImmediately()
    {
        if (menuTransform.parent != null) menuTransform.SetParent(null, true);
        menuTransform.position = _targetPosition;
        menuTransform.rotation = _targetRotation;
        EnsurePositiveScale(menuTransform);
        _lastValidPosition = _targetPosition;
        OnRepositionComplete?.Invoke();
    }

    private void UpdateRepositioning() { }

    private void UpdateFollowGaze()
    {
        if (headTransform == null || menuTransform == null) return;

        Vector3 dirToMenu = menuTransform.position - headTransform.position;
        dirToMenu.y = 0f; dirToMenu.Normalize();

        Vector3 gazeDir = headTransform.forward;
        gazeDir.y = 0f; gazeDir.Normalize();

        float angleDiff = Vector3.Angle(dirToMenu, gazeDir);

        if (angleDiff > maxFollowAngle)
        {
            float direction = Mathf.Sign(Vector3.SignedAngle(dirToMenu, gazeDir, Vector3.up));
            float rotateAmount = direction * followSpeed * Time.deltaTime;
            menuTransform.RotateAround(headTransform.position, Vector3.up, rotateAmount);
        }
    }
    #endregion

    #region Публичные методы
    public void SetDistance(float distance) =>
        menuDistance = Mathf.Clamp(distance, minDistance, maxDistance);

    public void MoveToPosition(Vector3 position, bool smooth = true)
    {
        _targetPosition = position;
        _targetRotation = CalculateMenuRotation(position);
        if (smooth && useSmoothTransition) StartSmoothReposition();
        else ApplyPositionImmediately();
    }

    public void ResetPosition() => SetInitialMenuPosition();
    public Vector3 GetTargetPosition() => _targetPosition;
    #endregion

    #region Editor Gizmos
    private void OnDrawGizmosSelected()
    {
        if (headTransform == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.1f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(headTransform.position, transform.position);
        if (_isRepositioning)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_targetPosition, 0.15f);
        }
    }
    #endregion
}