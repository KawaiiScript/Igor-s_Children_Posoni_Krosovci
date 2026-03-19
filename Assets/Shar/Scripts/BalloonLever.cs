using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// BalloonLeverSpring — рычаг на физической пружине (SpringJoint).
/// Ручка соединена с точкой крепления на шаре пружиной.
/// Чем сильнее растянута пружина (дальше от точки крепления), тем выше мощность горелки.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(XRGrabInteractable))]
public class BalloonLever : MonoBehaviour
{
    [Header("Ссылки")]
    [Tooltip("Контроллер шара (должен иметь метод SetBurnerIntensity)")]
    public BalloonController balloonController;

    [Tooltip("Точка крепления верёвки на шаре (дочерняя шара). Должна иметь Rigidbody (kinematic).")]
    public Transform attachPoint;

    [Tooltip("Rigidbody точки крепления (если не задан, будет создан автоматически)")]
    public Rigidbody attachRigidbody;

    [Tooltip("Линия верёвки (LineRenderer) — для визуализации")]
    public LineRenderer ropeLine;

    [Header("Настройки пружины")]
    [Tooltip("Длина верёвки в покое (м)")]
    public float ropeLength = 0.5f;

    [Tooltip("Жёсткость пружины (чем выше, тем труднее тянуть)")]
    public float springStiffness = 100f;

    [Tooltip("Демпфирование (гасит колебания)")]
    public float damper = 10f;

    [Header("Настройки мощности")]
    [Tooltip("Максимальное растяжение для 100% мощности (м)")]
    public float maxExtension = 0.3f;

    [Tooltip("Множитель мощности (1 = 100% при maxExtension)")]
    public float powerMultiplier = 1f;

    [Tooltip("Мёртвая зона — минимальное растяжение для начала реакции (м)")]
    public float deadZone = 0.02f;

    [Tooltip("Сглаживание изменения мощности (0-1)")]
    public float powerSmoothing = 10f;

    [Header("Звуки")]
    public AudioClip grabSound;
    public AudioClip releaseSound;
    [Range(0f, 1f)] public float soundVolume = 0.5f;

    [Header("Debug")]
    [SerializeField] private float currentPower;
    [SerializeField] private float currentExtension;
    [SerializeField] private bool isGrabbed;

    private Rigidbody rb;
    private AudioSource audioSource;
    private XRGrabInteractable grabInteractable;
    private SpringJoint springJoint;
    private float targetPower;
    private float smoothPower;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grabInteractable = GetComponent<XRGrabInteractable>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (balloonController == null)
            balloonController = FindObjectOfType<BalloonController>();
    }

    private void Start()
    {
        // Создаём точку крепления, если не задана
        if (attachPoint == null)
        {
            CreateAttachPoint();
        }

        // Убеждаемся, что attachRigidbody есть и kinematic
        if (attachRigidbody == null)
        {
            attachRigidbody = attachPoint.GetComponent<Rigidbody>();
            if (attachRigidbody == null)
            {
                attachRigidbody = attachPoint.gameObject.AddComponent<Rigidbody>();
            }
        }
        attachRigidbody.isKinematic = true;
        attachRigidbody.useGravity = false;

        // Настраиваем SpringJoint
        springJoint = gameObject.AddComponent<SpringJoint>();
        springJoint.connectedBody = attachRigidbody;
        springJoint.autoConfigureConnectedAnchor = false;
        springJoint.anchor = Vector3.zero; // можно подстроить под модель ручки
        springJoint.connectedAnchor = Vector3.zero; // точка крепления в локальных координатах attachPoint
        springJoint.spring = springStiffness;
        springJoint.damper = damper;
        springJoint.minDistance = 0; // не используем лимиты расстояний
        springJoint.maxDistance = 0;
        springJoint.tolerance = 0.001f;

        // Настраиваем XR Grab
        grabInteractable.trackPosition = true;  // пусть XR управляет движением
        grabInteractable.trackRotation = true;
        grabInteractable.throwOnDetach = false; // не бросаем, а отпускаем

        // Подписываемся на события захвата
        grabInteractable.selectEntered.AddListener(OnGrabbed);
        grabInteractable.selectExited.AddListener(OnReleased);

        // Настраиваем LineRenderer
        if (ropeLine != null)
        {
            ropeLine.positionCount = 2;
            ropeLine.useWorldSpace = true;
        }

        Debug.Log("[LeverSpring] Инициализирован. Длина пружины: " + ropeLength);
    }

    private void CreateAttachPoint()
    {
        GameObject attachObj = new GameObject("LeverAttachPoint");
        // Делаем дочерним шара или объекта, который двигается с шаром
        if (balloonController != null)
            attachObj.transform.parent = balloonController.transform;
        else
            attachObj.transform.parent = transform.parent; // предполагаем, что ручка тоже внутри шара

        attachObj.transform.position = transform.position + Vector3.up * ropeLength;
        attachPoint = attachObj.transform;
        attachRigidbody = attachObj.AddComponent<Rigidbody>();
        Debug.Log("[LeverSpring] AttachPoint создан автоматически.");
    }

    private void OnDestroy()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        PlaySound(grabSound);
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        isGrabbed = false;
        PlaySound(releaseSound);
    }

    private void Update()
    {
        if (attachPoint == null) return;

        // Вычисляем текущее расстояние между ручкой и точкой крепления
        float distance = Vector3.Distance(transform.position, attachPoint.position);

        // Растяжение = расстояние - длина покоя
        float extension = distance - ropeLength;
        extension = Mathf.Max(0, extension - deadZone); // мёртвая зона

        // Целевая мощность пропорциональна растяжению
        targetPower = (extension / maxExtension) * powerMultiplier;
        targetPower = Mathf.Clamp01(targetPower);

        // Сглаживание
        smoothPower = Mathf.Lerp(smoothPower, targetPower, Time.deltaTime * powerSmoothing);
        currentPower = smoothPower;

        // Применяем к контроллеру шара
        if (balloonController != null)
            balloonController.SetBurnerIntensity(currentPower);

        // Обновляем отладочные поля
        currentExtension = extension;

        // Рисуем верёвку
        if (ropeLine != null)
        {
            ropeLine.SetPosition(0, attachPoint.position);
            ropeLine.SetPosition(1, transform.position);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip, soundVolume);
    }

    /// <summary> Публичный метод для получения текущей мощности (0-1) </summary>
    public float GetCurrentPower() => currentPower;

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || attachPoint == null) return;

        // Точка крепления
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(attachPoint.position, 0.03f);

        // Положение ручки
        Gizmos.color = isGrabbed ? Color.red : Color.cyan;
        Gizmos.DrawSphere(transform.position, 0.03f);

        // Верёвка
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(attachPoint.position, transform.position);

        // Ограничивающая сфера (для визуализации длины покоя)
        Gizmos.color = new Color(1, 1, 0, 0.2f);
        Gizmos.DrawWireSphere(attachPoint.position, ropeLength);
    }
}