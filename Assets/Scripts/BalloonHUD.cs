using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BalloonHUD : MonoBehaviour
{
    [Header("🔗 Ссылки")]
    [Tooltip("Если не назначено — найдётся автоматически")]
    public BalloonController balloon;
    
    [Tooltip("Камера игрока (для следования). Если пусто — ищем Main Camera")]
    public Transform playerCamera;
    
    [Header("📊 Текстовые поля (TMP)")]
    public TextMeshProUGUI altitudeText;
    public TextMeshProUGUI temperatureText;
    
    [Header("🎯 Позиционирование в левом нижнем углу")]
    [Tooltip("Дистанция от камеры до HUD (метры)")]
    public float hudDistance = 1.5f;
    
    [Tooltip("Смещение влево от центра взгляда (метры)")]
    public float horizontalOffset = -0.8f;
    
    [Tooltip("Смещение вниз от центра взгляда (метры)")]
    public float verticalOffset = -0.5f;
    
    [Tooltip("Скорость плавного следования (чем больше — тем быстрее)")]
    public float followSpeed = 5f;
    
    [Tooltip("Угол поворота к камере (0 = всегда лицом, 90 = фиксированный)")]
    [Range(0f, 90f)]
    public float lookAtAngle = 15f;
    
    [Header("⚙️ Настройки обновления")]
    public float updateInterval = 0.1f;
    private float updateTimer = 0f;
    
    [Header("🎨 Цвета")]
    public Color normalColor = Color.white;
    public Color coldColor = Color.cyan;
    public Color hotColor = Color.yellow;
    
    private Transform canvasTransform;
    private bool isInitialized = false;
    
    void Start()
    {
        Initialize();
    }
    
    void Initialize()
    {
        // Автопоиск камеры
        if (playerCamera == null)
        {
            playerCamera = Camera.main?.transform;
            if (playerCamera == null)
            {
                Debug.LogError("[HUD] ❌ Камера не найдена! Назначьте playerCamera вручную.");
                enabled = false;
                return;
            }
        }
        
        // Автопоиск шара
        if (balloon == null)
        {
            balloon = FindObjectOfType<BalloonController>();
            if (balloon == null)
            {
                Debug.LogWarning("[HUD] ⚠️ BalloonController не найден. Данные не будут обновляться.");
            }
        }
        
        canvasTransform = transform;
        isInitialized = true;
        
        // Начальная позиция
        UpdateHUDPosition();
        
        Debug.Log("[HUD] ✅ Инициализирован. Позиция: левый нижний угол.");
    }
    
    void LateUpdate()
    {
        if (!isInitialized || playerCamera == null) return;
        
        // Плавное следование за камерой
        UpdateHUDPosition();
        UpdateHUDRotation();
    }
    
    void Update()
    {
        if (!isInitialized) return;
        
        updateTimer += Time.deltaTime;
        if (updateTimer < updateInterval) return;
        updateTimer = 0f;
        
        UpdateHUDData();
    }
    
    /// <summary>
    /// Вычисляет позицию: камера + смещение (влево/вниз/вперёд)
    /// </summary>
    void UpdateHUDPosition()
    {
        // Направление вперёд от камеры
        Vector3 forward = playerCamera.forward;
        Vector3 right = playerCamera.right;
        Vector3 down = -playerCamera.up;
        
        // Целевая позиция: камера + дистанция + смещения
        Vector3 targetPosition = playerCamera.position 
            + forward * hudDistance 
            + right * horizontalOffset 
            + down * verticalOffset;
        
        // Плавное движение к цели
        canvasTransform.position = Vector3.Lerp(
            canvasTransform.position, 
            targetPosition, 
            Time.deltaTime * followSpeed
        );
    }
    
    /// <summary>
    /// Поворачивает HUD к камере с настраиваемым углом
    /// </summary>
    void UpdateHUDRotation()
    {
        if (lookAtAngle <= 0f)
        {
            // Всегда лицом к камере (биллборд)
            canvasTransform.LookAt(
                canvasTransform.position + playerCamera.forward, 
                playerCamera.up
            );
        }
        else
        {
            // Частичный поворот (чтобы не крутился слишком резко)
            Vector3 directionToCamera = (playerCamera.position - canvasTransform.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(directionToCamera, playerCamera.up);
            canvasTransform.rotation = Quaternion.Slerp(
                canvasTransform.rotation, 
                targetRotation, 
                Time.deltaTime * (90f / lookAtAngle)
            );
        }
    }
    
    /// <summary>
    /// Обновляет данные на экране (температура, высота)
    /// </summary>
    void UpdateHUDData()
    {
        if (balloon == null) return;
        
        // === ОБЯЗАТЕЛЬНЫЕ ПАРАМЕТРЫ (Критерий 4.4) ===
        
        // Высота
        float altitude = balloon.GetAltitude();
        if (altitudeText != null)
        {
            altitudeText.text = $"{altitude:F0} м";
        }
        
        // Температура
        float temperature = balloon.GetTemperature();
        if (temperatureText != null)
        {
            temperatureText.text = $"{temperature:F0}°C";
            
            // Цветовая индикация
            if (temperature < 60f)
                temperatureText.color = coldColor;
            else if (temperature > 100f)
                temperatureText.color = hotColor;
            else
                temperatureText.color = normalColor;
        }
    }
    
    // === ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ НАСТРОЙКИ ===
    
    /// <summary>
    /// Установить дистанцию до HUD (вызов из другого скрипта)
    /// </summary>
    public void SetHudDistance(float newDistance)
    {
        hudDistance = Mathf.Max(0.5f, newDistance); // Минимум 0.5м
    }
    
    /// <summary>
    /// Сместить HUD (положительный X = вправо, положительный Y = вверх)
    /// </summary>
    public void SetHudOffset(float horizontal, float vertical)
    {
        horizontalOffset = horizontal;
        verticalOffset = vertical;
    }
    
    /// <summary>
    /// Показать/скрыть HUD
    /// </summary>
    public void SetHudVisible(bool visible)
    {
        canvasTransform.gameObject.SetActive(visible);
    }
    
    void OnDrawGizmosSelected()
    {
        if (playerCamera == null) return;
        
        // Показывает целевую позицию в редакторе
        Gizmos.color = Color.cyan;
        Vector3 targetPos = playerCamera.position 
            + playerCamera.forward * hudDistance 
            + playerCamera.right * horizontalOffset 
            + (-playerCamera.up) * verticalOffset;
        Gizmos.DrawWireSphere(targetPos, 0.1f);
        Gizmos.DrawLine(transform.position, targetPos);
    }
}