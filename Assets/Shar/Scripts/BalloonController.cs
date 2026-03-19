using UnityEngine;
using System;

/// <summary>
/// BalloonController — Шар падает когда ХОЛОДНЫЙ, летит когда ГОРЯЧИЙ
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BalloonController : MonoBehaviour
{
    #region Настройки

    [Header("🔧 DEBUG")]
    public bool debugMode = true;
    public bool autoEnableOnStart = false;  // ← Выключено по умолчанию!
    [Header("🌡️ Температура")]
    public float groundTemperature = 20f;
    public float maxTemperature = 120f;
    public float heatingRate = 30f;
    public float coolingRate = 8f;
    public float temperatureLapseRate = 6.5f;
    public float ascentCoolingFactor = 2f;

    [Header("⚖️ Физика")]
    public float balloonMass = 500f;
    public float envelopeVolume = 2800f;

    // 🔹 УМЕНЬШЕНО с 10000 до 6500!
    [Tooltip("Максимальная подъёмная сила при 100°C (Ньютоны)")]
    public float maxLiftForce = 6500f;  // ← БЫЛО 10000!

    [Tooltip("Температура при которой шар начинает подниматься")]
    public float liftOffTemperature = 60f;

    [Tooltip("Температура для максимальной подъёмной силы")]
    public float maxLiftTemperature = 100f;

    // 🔹 ДОБАВЛЕНО: Ограничение скорости
    [Tooltip("Максимальная скорость подъёма (м/с)")]
    public float maxAscentSpeed = 8f;

    [Tooltip("Сопротивление воздуха")]
    public float airDrag = 0.5f;  // ← Увеличено с 0.3!

    [Header("⛽ Топливо")]
    public float maxFuel = 100f;
    public float currentFuel = 100f;
    public float fuelConsumption = 0.5f;

    [Header("🎮 Состояние")]
    [Range(0f, 1f)] public float burnerIntensity = 0f;
    public bool isActive = false;
    public bool isPlayerInBalloon = false;

    [Header("🚀 Старт")]
    public float startHeight = 2f;
    #endregion

    #region Показатели

    [Header("📊 ПОКАЗАТЕЛИ")]
    [SerializeField] private float internalTemp;
    [SerializeField] private float externalTemp;
    [SerializeField] private float liftForce;
    [SerializeField] private float weightForce;
    [SerializeField] private float netForce;
    [SerializeField] private float altitude;
    [SerializeField] private float verticalSpeed;
    [SerializeField] private float fuelPercent;
    [SerializeField] private BalloonState state;

    public enum BalloonState { Falling, Neutral, Rising }
    #endregion

    #region Звуки

    [Header("🔊 Звуки")]
    public AudioClip burnerSound;
    public AudioClip crashSound;
    private AudioSource audioSource;
    private bool wasPlayingBurner;
    #endregion

    #region Внутренние

    public event Action OnCrash;
    private Rigidbody rb;
    private int frameCounter;
    #endregion

    #region Unity Lifecycle

    [Header("⚙️ Ограничение наклона")]
    [SerializeField] private float maxTiltAngle = 30f; // максимальный угол наклона
    private Quaternion targetRotation;

    [Header("👤 Привязка игрока")]
    public Transform playerRoot;          // Корень XR Origin (перетащите в инспекторе)
    private Transform originalParent;      // Запоминаем исходного родителя

    public void AttachPlayer(Transform player)
    {
        if (playerRoot == null) playerRoot = player;
        originalParent = playerRoot.parent;
        playerRoot.SetParent(transform, true);   // true — сохраняем мировую позицию
        Debug.Log("[Balloon] Игрок прикреплён к шару");
    }

    public void DetachPlayer()
    {
        if (playerRoot != null)
        {
            playerRoot.SetParent(originalParent, true);
            Debug.Log("[Balloon] Игрок откреплён от шара");
        }
    }



    // Добавьте в FixedUpdate после ApplyLiftForce()
    private void FixedUpdate()
    {
        if (!isActive) return;

        frameCounter++;

        UpdateTemperature(Time.fixedDeltaTime);
        UpdateFuel(Time.fixedDeltaTime);
        CalculateForces();
        ApplyLiftForce();

        // 🔽 НОВЫЙ БЛОК: ограничение вращения
        ClampRotation();

        altitude = transform.position.y;
        verticalSpeed = rb.velocity.y;

        if (debugMode && frameCounter % 60 == 0)
            LogStatus();
    }

    private void ClampRotation()
    {
        // Получаем текущие углы Эйлера (локальные)
        Vector3 currentEuler = transform.localEulerAngles;

        // Приводим углы к диапазону -180..180 для удобства
        float angleX = NormalizeAngle(currentEuler.x);
        float angleZ = NormalizeAngle(currentEuler.z);

        // Ограничиваем
        angleX = Mathf.Clamp(angleX, -maxTiltAngle, maxTiltAngle);
        angleZ = Mathf.Clamp(angleZ, -maxTiltAngle, maxTiltAngle);

        // Применяем обратно, сохраняя вращение по Y (рыскание) свободным
        transform.localEulerAngles = new Vector3(angleX, currentEuler.y, angleZ);
    }

    // Вспомогательная функция для приведения угла к [-180, 180]
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = balloonMass;
        rb.drag = 0.3f;
        rb.angularDrag = 0.5f;

        if (transform.position.y < startHeight)
            transform.position = new Vector3(transform.position.x, startHeight, transform.position.z);

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        // 🔹 ШАР ХОЛОДНЫЙ ПРИ СТАРТЕ!
        internalTemp = groundTemperature;
        externalTemp = groundTemperature;
        currentFuel = maxFuel;
        burnerIntensity = 0f;

        if (autoEnableOnStart)
        {
            ActivateBalloon();
        }

        CalculateForces();

        Debug.Log($"[Balloon] ✅ Готов! T: {internalTemp}°C (ХОЛОДНЫЙ)");
        Debug.Log($"[Balloon] ⚠️ Нужно нагреть до {liftOffTemperature}°C для взлёта!");
    }


    private void Update()
    {
        UpdateAudio();
    }
    #endregion

    #region Термодинамика

    private void UpdateTemperature(float dt)
    {
        externalTemp = GetAmbientTemperature(altitude);

        // 🔥 НАГРЕВ (только если горелка включена!)
        if (burnerIntensity > 0.01f && currentFuel > 0)
        {
            float heating = heatingRate * burnerIntensity * dt;
            internalTemp += heating;
        }

        // ❄️ ОХЛАЖДЕНИЕ (всегда!)
        float tempDiff = internalTemp - externalTemp;
        if (tempDiff > 0)
        {
            float naturalCooling = coolingRate * (tempDiff / 50f) * dt;
            internalTemp -= naturalCooling;
        }

        // Охлаждение при подъёме
        if (verticalSpeed > 0.5f)
        {
            float ascentCooling = ascentCoolingFactor * verticalSpeed * dt;
            internalTemp -= ascentCooling;
        }

        // Без горелки быстро остывает до окружающей
        if (burnerIntensity < 0.01f && internalTemp > externalTemp)
        {
            float idleCooling = coolingRate * 0.8f * dt;
            internalTemp -= idleCooling;
        }

        internalTemp = Mathf.Max(internalTemp, externalTemp - 5f);
        internalTemp = Mathf.Min(internalTemp, maxTemperature);
    }

    private float GetAmbientTemperature(float height)
    {
        return groundTemperature - (height / 1000f) * temperatureLapseRate;
    }

    private void UpdateFuel(float dt)
    {
        if (burnerIntensity > 0.01f)
        {
            currentFuel -= fuelConsumption * burnerIntensity * dt;
            currentFuel = Mathf.Max(0, currentFuel);
        }
        fuelPercent = (currentFuel / maxFuel) * 100f;
    }
    #endregion

    #region Физика

    private void CalculateForces()
    {
        weightForce = balloonMass * 9.81f;

        // 🔹 НОВАЯ ЛОГИКА: Lift зависит от температуры
        // При groundTemperature (20°C) → lift = 0 (падает!)
        // При liftOffTemperature (60°C) → lift = weight (нейтрально)
        // При maxLiftTemperature (100°C) → lift = max (летит вверх!)

        float tempRange = maxLiftTemperature - groundTemperature;
        float tempAboveGround = internalTemp - groundTemperature;

        // Коэффициент от 0 до 1 (0 = холодно, 1 = максимум)
        float heatFactor = Mathf.Clamp01(tempAboveGround / tempRange);

        // 🔹 Подъёмная сила = 0 когда холодно, растёт с нагревом
        liftForce = maxLiftForce * heatFactor;

        // Результирующая сила
        netForce = liftForce - weightForce;

        // Состояние
        if (netForce > 100f)
            state = BalloonState.Rising;
        else if (netForce < -100f)
            state = BalloonState.Falling;
        else
            state = BalloonState.Neutral;
    }

    private void ApplyLiftForce()
    {
        Vector3 lift = Vector3.up * liftForce;
        rb.AddForce(lift, ForceMode.Force);
    }
    #endregion

    #region Звук

    private void UpdateAudio()
    {
        if (audioSource == null || burnerSound == null) return;

        if (burnerIntensity > 0.1f && currentFuel > 0)
        {
            if (!wasPlayingBurner)
            {
                audioSource.clip = burnerSound;
                audioSource.loop = true;
                audioSource.Play();
                wasPlayingBurner = true;
            }
            audioSource.volume = burnerIntensity * 0.8f;
        }
        else if (wasPlayingBurner)
        {
            audioSource.Stop();
            wasPlayingBurner = false;
        }
    }
    #endregion

    #region Публичные методы

    public void SetBurnerIntensity(float intensity)
    {
        burnerIntensity = Mathf.Clamp01(intensity);
    }

    public void SetBurnerPower(float intensity) => SetBurnerIntensity(intensity);

    public void ActivateBalloon()
    {
        isActive = true;
        isPlayerInBalloon = true;
        internalTemp = groundTemperature;  // 🔹 ХОЛОДНЫЙ при входе!
        burnerIntensity = 0f;
        Debug.Log("[Balloon] ✅ Активирован — шар ХОЛОДНЫЙ! Грейте!");
    }

    public void DeactivateBalloon()
    {
        isActive = false;
        isPlayerInBalloon = false;
        burnerIntensity = 0f;
        if (wasPlayingBurner && audioSource != null)
            audioSource.Stop();
        wasPlayingBurner = false;
    }

    public void StartGame()
    {
        isActive = true;
        isPlayerInBalloon = true;
        currentFuel = maxFuel;
        internalTemp = groundTemperature;
        burnerIntensity = 0f;
        rb.velocity = Vector3.zero;
    }

    public float GetAltitude() => altitude;
    public float GetVerticalSpeed() => verticalSpeed;
    public float GetTemperature() => internalTemp;
    public float GetFuel() => currentFuel;
    public float GetFuelPercent() => fuelPercent;
    public BalloonState GetState() => state;
    #endregion

    #region Коллизии

    private void OnCollisionEnter(Collision collision)
    {
        float impact = collision.relativeVelocity.magnitude;
        if (impact > 10f)
        {
            Debug.LogError($"[Balloon] 💀 Крушение! {impact:F1} м/с");
            if (audioSource != null && crashSound != null)
                audioSource.PlayOneShot(crashSound);
            OnCrash?.Invoke();
        }
    }
    #endregion

    #region Логирование

    private void LogStatus()
    {
        string stateStr = state == BalloonState.Rising ? "🟢↑" :
                         state == BalloonState.Falling ? "🔴↓" : "🟡≈";

        Debug.Log($"[Balloon] {stateStr} H:{altitude:F1}м V:{verticalSpeed:F2}м/с " +
                 $"T:{internalTemp:F0}°C (нужно >{liftOffTemperature}°C) " +
                 $"↑{liftForce:F0}Н ↓{weightForce:F0}Н →{netForce:+0;-0}Н " +
                 $"🔥:{burnerIntensity * 100:F0}% ⛽:{fuelPercent:F0}%");
    }
    #endregion

    #region GUI

    private void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(10, 10, 400, 300));
        GUILayout.BeginVertical("box");

        GUILayout.Label("🎈 ВОЗДУШНЫЙ ШАР", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
        GUILayout.Space(5);

        GUILayout.Label($"📍 Высота: {altitude:F1} м");
        GUILayout.Label($"💨 Скорость: {verticalSpeed:F2} м/с");
        GUILayout.Space(5);

        string tempStatus = internalTemp < liftOffTemperature ? "❄️ ХОЛОДНЫЙ" : "🔥 ГОРЯЧИЙ";
        GUILayout.Label($"🌡️ Внутри: {internalTemp:F0}°C {tempStatus}");
        GUILayout.Label($"🎯 Нужно: > {liftOffTemperature}°C для взлёта");
        GUILayout.Space(5);

        GUILayout.Label($"⬆️ Подъём: {liftForce:F0} Н");
        GUILayout.Label($"⬇️ Вес: {weightForce:F0} Н");
        GUILayout.Label($"➡️ ИТОГО: {netForce:+0;-0} Н");
        GUILayout.Space(5);

        if (state == BalloonState.Falling)
        {
            GUIStyle redStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            redStyle.normal.textColor = Color.red;
            GUILayout.Label($"🔴 ПАДАЕМ! Грейте горелку!", redStyle);
        }
        else if (state == BalloonState.Rising)
        {
            GUIStyle greenStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            greenStyle.normal.textColor = Color.green;
            GUILayout.Label($"🟢 ВЗЛЕТАЕМ! +{netForce:F0}Н", greenStyle);
        }

        GUILayout.Space(5);
        GUILayout.Label($"⛽ Топливо: {fuelPercent:F0}% | 🔥 Горелка: {burnerIntensity * 100:F0}%");

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Подъёмная сила
        Gizmos.color = liftForce >= weightForce ? Color.green : Color.red;
        Gizmos.DrawRay(transform.position, Vector3.up * (liftForce / 500f));

        // Скорость
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, rb.velocity);
    }
    #endregion
}