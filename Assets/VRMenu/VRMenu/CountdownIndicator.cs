using UnityEngine;
using UnityEngine.UI;

using System;
using System.Collections.Generic; // ← для List<T>

/// <summary>
/// CountdownIndicator_XR - Индикатор обратного отсчета для VR
/// Работает с XR Interaction Toolkit
/// Автор: NTO VR Final Helper
/// Версия: 1.1 (исправлены ошибки)
/// </summary>
public class CountdownIndicator : MonoBehaviour
{
    #region UI Элементы
    [Header("🖼️ UI Элементы")]
    [Tooltip("Image для отображения прогресса")]
    public Image progressImage;

    [Tooltip("Text для отображения времени")]
    public Text countdownText;

    [Tooltip("Корневой объект индикатора")]
    public GameObject indicatorRoot;
    #endregion

    #region Настройки
    [Header("⚙️ Настройки")]
    [Tooltip("Цвет в начале отсчёта")]
    public Color startColor = Color.yellow;

    [Tooltip("Цвет в конце отсчёта")]
    public Color endColor = Color.red;

    [Tooltip("Длительность отсчёта (сек)")]
    public float countdownDuration = 5f;

    [Tooltip("Задержка перед стартом (сек)")]
    public float startDelay = 1f;

    [Tooltip("Длина луча для проверки")]
    public float rayLength = 10f;

    [Tooltip("Слой для проверки (меню)")]
    public LayerMask menuLayerMask = -1; // -1 = все слои
    #endregion

    #region XR Компоненты
    [Header("🎮 XR Компоненты")]
    [Tooltip("Камера/голова — источник луча")]
    public Transform headTransform;

    [Tooltip("Меню, на которое смотрим")]
    public GameObject targetMenu;

    [Tooltip("RayInteractor для совместимости с UI (опционально)")]
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;

    [Tooltip("Автопоиск камеры при старте")]
    public bool autoFindHead = true;
    #endregion

    #region Приватные поля
    private float _currentTime = 0f;
    private bool _isCountingDown = false;
    private bool _wasLookingAtMenu = true;
    private float _elapsedTime = 0f;
    #endregion

    #region События
    /// <summary>
    /// Вызывается при завершении отсчёта
    /// </summary>
    public event Action OnCountdownComplete;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        InitializeComponents();
        SetVisible(false);
    }

    private void Update()
    {
        UpdateCountdown();
        UpdateIndicator();
    }
    #endregion

    #region Инициализация
    /// <summary>
    /// Настройка компонентов при старте
    /// </summary>
    private void InitializeComponents()
    {
        // 🔍 Автопоиск камеры
        if (headTransform == null && autoFindHead)
        {
            headTransform = Camera.main?.transform;
            if (headTransform == null)
                Debug.LogWarning("[Countdown] Камера не найдена! Назначьте headTransform вручную.");
        }

        // 🔍 Автопоиск RayInteractor
        if (rayInteractor == null)
        {
            rayInteractor = FindObjectOfType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
        }

        // ✅ Проверка обязательных полей
        if (targetMenu == null)
            Debug.LogWarning("[Countdown] targetMenu не назначен!");
    }
    #endregion

    #region Логика отсчёта
    /// <summary>
    /// Обновление логики обратного отсчёта
    /// </summary>
    private void UpdateCountdown()
    {
        bool isLookingAtMenu = CheckIfLookingAtMenu();

        // 👁️ Если смотрим на меню — сбрасываем таймер
        if (isLookingAtMenu)
        {
            if (_isCountingDown)
            {
                Debug.Log("[Countdown] Взгляд вернулся — сброс таймера");
            }
            _isCountingDown = false;
            _elapsedTime = 0f;
            _wasLookingAtMenu = true;
            return;
        }

        // ⏱️ Если не смотрим — запускаем/продолжаем отсчёт
        if (!_wasLookingAtMenu)
        {
            _elapsedTime += Time.deltaTime;
            if (_elapsedTime >= startDelay && !_isCountingDown)
            {
                _isCountingDown = true;
                Debug.Log("[Countdown] Отсчёт начат");
            }
        }
        else
        {
            _wasLookingAtMenu = false;
            _elapsedTime = 0f;
        }

        // 🔥 Сам отсчёт
        if (_isCountingDown)
        {
            _currentTime += Time.deltaTime;

            if (_currentTime >= countdownDuration)
            {
                Debug.Log("[Countdown] Отсчёт завершён!");
                OnCountdownComplete?.Invoke();
                ResetCountdown();
            }
        }
    }

    /// <summary>
    /// Проверка: смотрит ли луч на меню или его дочерние объекты
    /// </summary>
    private bool CheckIfLookingAtMenu()
    {
        // 🔹 Базовые проверки
        if (targetMenu == null || headTransform == null)
            return false;

        // 🔹 Пускаем луч из головы вперёд
        Ray ray = new Ray(headTransform.position, headTransform.forward);

        // 🔹 Физический рейкаст
        if (Physics.Raycast(ray, out RaycastHit hit, rayLength, menuLayerMask))
        {
            GameObject hitObject = hit.collider.gameObject;

            // 🔹 Проверяем: это меню или его ребёнок?
            return hitObject == targetMenu ||
                   hitObject.transform.IsChildOf(targetMenu.transform);
        }

        // 🔹 Луч ни во что не попал
        return false;
    }
    #endregion

    #region Визуализация
    /// <summary>
    /// Обновление UI индикатора
    /// </summary>
    private void UpdateIndicator()
    {
        if (_isCountingDown)
        {
            SetVisible(true);

            float progress = 1f - (_currentTime / countdownDuration);
            progress = Mathf.Clamp01(progress);

            // 🎨 Обновляем прогресс-бар
            if (progressImage != null)
            {
                progressImage.fillAmount = progress;
                progressImage.color = Color.Lerp(startColor, endColor, 1f - progress);
            }

            // 🔢 Обновляем текст
            if (countdownText != null)
            {
                float remainingTime = countdownDuration - _currentTime;
                countdownText.text = remainingTime.ToString("F1");
            }
        }
        else
        {
            SetVisible(false);
        }
    }

    /// <summary>
    /// Показать/скрыть индикатор
    /// </summary>
    private void SetVisible(bool visible)
    {
        if (indicatorRoot != null)
            indicatorRoot.SetActive(visible);
    }
    #endregion

    #region Публичные методы
    /// <summary>
    /// Сбросить отсчёт
    /// </summary>
    public void ResetCountdown()
    {
        _currentTime = 0f;
        _isCountingDown = false;
        _elapsedTime = 0f;
        _wasLookingAtMenu = true;
        SetVisible(false);
    }

    /// <summary>
    /// Получить текущий прогресс (0..1)
    /// </summary>
    public float GetProgress() => _currentTime / countdownDuration;

    /// <summary>
    /// Получить оставшееся время
    /// </summary>
    public float GetRemainingTime() => countdownDuration - _currentTime;

    /// <summary>
    /// Принудительно запустить отсчёт (для тестов)
    /// </summary>
    public void StartCountdown()
    {
        if (!_isCountingDown)
        {
            _isCountingDown = true;
            _elapsedTime = startDelay; // сразу после задержки
            Debug.Log("[Countdown] Отсчёт запущен вручную");
        }
    }
    #endregion

    #region Editor Gizmos
    private void OnDrawGizmos()
    {
        if (headTransform == null || targetMenu == null) return;

        // 🎯 Рисуем луч в редакторе
        Gizmos.color = CheckIfLookingAtMenu() ? Color.green : Color.red;
        Gizmos.DrawRay(headTransform.position, headTransform.forward * rayLength);

        // 📦 Рисуем границы меню
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(targetMenu.transform.position, targetMenu.transform.lossyScale);
    }
    #endregion
}