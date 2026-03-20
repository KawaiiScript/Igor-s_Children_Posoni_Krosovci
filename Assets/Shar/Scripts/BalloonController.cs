using UnityEngine;
using System;

/// <summary>
/// BalloonController — Шар падает когда ХОЛОДНЫЙ, летит когда ГОРЯЧИЙ
/// Версия: 2.0 (с ограничением наклона и привязкой игрока)
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class BalloonController : MonoBehaviour
{
    #region Настройки

    [Header("🔧 DEBUG")]
    public bool debugMode = true;
    public bool autoEnableOnStart = false;

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
    [Tooltip("Максимальная подъёмная сила при 100°C (Ньютоны)")]
    public float maxLiftForce = 6500f;
    [Tooltip("Температура при которой шар начинает подниматься")]
    public float liftOffTemperature = 60f;
    [Tooltip("Температура для максимальной подъёмной силы")]
    public float maxLiftTemperature = 100f;
    [Tooltip("Максимальная скорость подъёма (м/с)")]
    public float maxAscentSpeed = 8f;
    [Tooltip("Сопротивление воздуха")]
    public float airDrag = 0.5f;

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

    [Header("⚙️ Ограничение наклона")]
    [SerializeField] private float maxTiltAngle = 30f;
    #endregion

    #region Показатели

    [Header("📊 ПОКАЗАТЕЛИ (Read Only)")]
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

    #region Привязка игрока

    [Header("👤 Привязка игрока")]
    public Transform playerRoot;          // XR Origin игрока
    private Transform originalParent;
    private Vector3 playerLocalOffset;
    #endregion

    #region Внутренние

    public event Action OnCrash;
    private Rigidbody rb;
    private int frameCounter;
    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = balloonMass;
        rb.drag = airDrag;
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

        if (autoEnableOnStart) ActivateBalloon();

        CalculateForces();
        Debug.Log($"[Balloon] ✅ Готов! T: {internalTemp}°C (ХОЛОДНЫЙ)");
    }

    private void Start()
    {
        if (playerRoot == null)
            Debug.LogWarning("[Balloon] ⚠️ playerRoot не назначен!");
    }

    private void FixedUpdate()
    {
        if (!isActive) return;

        frameCounter++;

        UpdateTemperature(Time.fixedDeltaTime);
        UpdateFuel(Time.fixedDeltaTime);
        CalculateForces();
        ApplyLiftForce();
        ClampRotation();

        altitude = transform.position.y;
        verticalSpeed = rb.velocity.y;

        // Ограничение скорости подъёма
        if (verticalSpeed > maxAscentSpeed)
        {
            rb.velocity = new Vector3(rb.velocity.x, maxAscentSpeed, rb.velocity.z);
        }

        if (debugMode && frameCounter % 60 == 0)
            LogStatus();
    }

    private void Update()
    {
        UpdateAudio();
    }

    private void OnDestroy()
    {
        // Отвязываем игрока при удалении шара
        if (isPlayerInBalloon && playerRoot != null)
            DetachPlayer();
    }

    #endregion

    #region Привязка/отвязка игрока

    public void AttachPlayer(Transform player)
    {
        if (playerRoot == null) playerRoot = player;
        if (playerRoot == null) return;

        originalParent = playerRoot.parent;
        playerLocalOffset = playerRoot.localPosition;

        // Сохраняем мировую позицию и привязываем
        Vector3 worldPos = playerRoot.position;
        Quaternion worldRot = playerRoot.rotation;

        playerRoot.SetParent(transform, true);
        playerRoot.position = worldPos;
        playerRoot.rotation = worldRot;

        Debug.Log("[Balloon] 👤 Игрок прикреплён к шару");
    }

    public void DetachPlayer()
    {
        if (playerRoot == null) return;

        // Сохраняем мировую позицию перед отвязкой
        Vector3 worldPos = playerRoot.position;
        Quaternion worldRot = playerRoot.rotation;

        playerRoot.SetParent(originalParent, true);
        playerRoot.position = worldPos;
        playerRoot.rotation = worldRot;

        Debug.Log("[Balloon] 👤 Игрок откреплён от шара");
    }

    #endregion

    #region Ограничение вращения

    private void ClampRotation()
    {
        Vector3 currentEuler = transform.localEulerAngles;
        float angleX = NormalizeAngle(currentEuler.x);
        float angleZ = NormalizeAngle(currentEuler.z);

        angleX = Mathf.Clamp(angleX, -maxTiltAngle, maxTiltAngle);
        angleZ = Mathf.Clamp(angleZ, -maxTiltAngle, maxTiltAngle);

        transform.localEulerAngles = new Vector3(angleX, currentEuler.y, angleZ);
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    #endregion

    #region Термодинамика

    private void UpdateTemperature(float dt)
    {
        externalTemp = GetAmbientTemperature(altitude);

        // 🔥 НАГРЕВ
        if (burnerIntensity > 0.01f && currentFuel > 0)
        {
            float heating = heatingRate * burnerIntensity * dt;
            internalTemp += heating;
        }

        // ❄️ ОХЛАЖДЕНИЕ
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

        // Быстрое остывание без горелки
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

        float tempRange = maxLiftTemperature - groundTemperature;
        float tempAboveGround = internalTemp - groundTemperature;
        float heatFactor = Mathf.Clamp01(tempAboveGround / tempRange);

        liftForce = maxLiftForce * heatFactor;
        netForce = liftForce - weightForce;

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
        internalTemp = groundTemperature;
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

    // Геттеры для UI
    public float GetAltitude() => altitude;
    public float GetVerticalSpeed() => verticalSpeed;
    public float GetTemperature() => internalTemp;
    public float GetFuel() => currentFuel;
    public float GetFuelPercent() => fuelPercent;
    public BalloonState GetState() => state;
    public bool IsPlayerInside() => isPlayerInBalloon;

    #endregion

    #region Коллизии

    private void OnCollisionEnter(Collision collision)
    {
        float impact = collision.relativeVelocity.magnitude;
        if (impact > 4f)
        {
            Debug.LogError($"[Balloon] 💀 Крушение! {impact:F1} м/с");
            if (audioSource != null && crashSound != null)
                audioSource.PlayOneShot(crashSound);
            OnCrash?.Invoke();
        }
    }

    #endregion

    #region Логирование и GUI

    private void LogStatus()
    {
        string stateStr = state == BalloonState.Rising ? "🟢↑" :
                         state == BalloonState.Falling ? "🔴↓" : "🟡≈";

        Debug.Log($"[Balloon] {stateStr} H:{altitude:F1}м V:{verticalSpeed:F2}м/с " +
                 $"T:{internalTemp:F0}°C (нужно >{liftOffTemperature}°C) " +
                 $"↑{liftForce:F0}Н ↓{weightForce:F0}Н →{netForce:+0;-0}Н " +
                 $"🔥:{burnerIntensity * 100:F0}% ⛽:{fuelPercent:F0}%");
    }

    private void OnGUI()
    {
        if (!debugMode) return;

        GUILayout.BeginArea(new Rect(10, 10, 420, 340));
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
            GUIStyle redStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.red } };
            GUILayout.Label($"🔴 ПАДАЕМ! Грейте горелку!", redStyle);
        }
        else if (state == BalloonState.Rising)
        {
            GUIStyle greenStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, normal = { textColor = Color.green } };
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