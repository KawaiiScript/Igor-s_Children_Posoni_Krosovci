using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(LineRenderer))]
public class TeleportManager : MonoBehaviour
{
    [Header("🎮 Input")]
    [SerializeField] private InputActionReference teleportAction;
    [SerializeField] private XRController xrController;

    [Header("🎨 Визуал")]
    [SerializeField] private Material mat;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float rayLength = 10f;

    [Header("🎯 Настройки")]
    [SerializeField] private Transform nushSlon;
    [SerializeField] private float forwardThreshold = 0.7f;

    [Header("🐛 Debug")]
    [SerializeField] private bool showDebugLogs = true;

    public BalloonEnterTrigger inShar;
    private RaycastHit hit;
    private LineRenderer lineRenderer;
    private GameObject sphere;
    private Coroutine movement = null;
    private bool _isTeleportActive = false;
    private bool _isValidTeleport = false;  // 🔹 Флаг: валидная ли точка

    private void Awake()
    {
        DebugLog("🔧 TeleportManager: Awake");

        lineRenderer = GetComponent<LineRenderer>();
        if (mat != null) lineRenderer.material = mat;
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = lineRenderer.endWidth = 0.02f;
        lineRenderer.enabled = false;

        sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        if (mat != null) sphere.GetComponent<Renderer>().material = mat;
        Destroy(sphere.GetComponent<Collider>());
        sphere.transform.localScale = Vector3.one * 0.1f;
        sphere.SetActive(false);

        if (xrController == null)
            xrController = GetComponent<XRController>();

        if (nushSlon == null)
        {
            var origin = GetComponentInParent<XROrigin>();
            if (origin != null) nushSlon = origin.transform;
        }

        if (teleportAction == null)
            DebugLog("❌ teleportAction: НЕ НАЗНАЧЕН!");
        else if (teleportAction.action == null)
            DebugLog("❌ teleportAction.action: NULL!");
        else
            DebugLog($"✅ teleportAction: {teleportAction.action.name}");
    }

    private void OnEnable()
    {
        if (teleportAction?.action != null)
            teleportAction.action.Enable();
    }

    private void OnDisable()
    {
        if (teleportAction?.action != null)
            teleportAction.action.Disable();
        if (movement != null) StopCoroutine(movement);
        lineRenderer.enabled = false;
        sphere.SetActive(false);
    }

    private void Update()
    {
        if (teleportAction?.action == null) return;
        if (inShar.isPlayerInBalloon == false)
        {
            // 🔹 Читаем Vector2 с джойстика
            Vector2 joystick = teleportAction.action.ReadValue<Vector2>();
            bool isForward = joystick.y >= forwardThreshold;

            // 🔹 1. Джойстик нажат ВПЕРЁД — начинаем прицеливание
            if (isForward && !_isTeleportActive)
            {
                DebugLog("🟡 Start Aiming");
                _isTeleportActive = true;
                _isValidTeleport = false;  // Сбрасываем валидность
            }
            // 🔹 2. Джойстик отпущен — заканчиваем прицеливание
            else if (!isForward && _isTeleportActive)
            {
                DebugLog("🔴 Stop Aiming");
                _isTeleportActive = false;

                // 🔹 ТЕЛЕПОРТИРУЕМСЯ только если точка валидна!
                if (_isValidTeleport && hit.collider != null)
                {
                    DebugLog($"🚀 TELEPORT to: {hit.point}");
                    movement = StartCoroutine(SmoothMove(nushSlon.transform.position, hit.point, 0.1f));
                }
                else
                {
                    DebugLog("⚠️ Телепортация отменена — нет валидной точки");
                }

                // Скрываем всё
                lineRenderer.enabled = false;
                sphere.SetActive(false);
                _isValidTeleport = false;
            }

            // 🔹 3. Если прицеливание активно — ОБНОВЛЯЕМ луч КАЖДЫЙ КАДР
            if (_isTeleportActive)
            {
                UpdateRaycast();
            }
        }
    }

    /// <summary>
    /// Обновление луча — следует за рукой + проверка границ
    /// </summary>
    private void UpdateRaycast()
    {
        // 🔹 Пускаем луч ИЗ ТЕКУЩЕЙ ПОЗИЦИИ РУКИ
        Ray ray = new Ray(transform.position, transform.forward);

        if (Physics.Raycast(ray, out hit, rayLength, groundMask))
        {
            // ✅ Попадание в землю — показываем луч
            _isValidTeleport = true;

            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, transform.position);  // 🔹 Начало луча = позиция РУКИ
            lineRenderer.SetPosition(1, hit.point);            // 🔹 Конец луча = точка попадания

            sphere.SetActive(true);
            sphere.transform.position = hit.point;

            if (showDebugLogs && Time.frameCount % 30 == 0)
                DebugLog($"✅ Raycast OK: {hit.point}");
        }
        else
        {
            // ❌ Нет попадания (за границей) — СКРЫВАЕМ луч
            _isValidTeleport = false;

            lineRenderer.enabled = false;
            sphere.SetActive(false);

            if (showDebugLogs && Time.frameCount % 30 == 0)
                DebugLog("❌ Raycast MISS — за границей");
        }
    }

    private IEnumerator SmoothMove(Vector3 startPos, Vector3 endPos, float step)
    {
        float s = 0;
        Vector3 currentPos = startPos;

        while ((currentPos - endPos).magnitude > 0.01f)
        {
            s += step;
            nushSlon.transform.position = Vector3.Lerp(startPos, endPos, Mathf.Clamp01(s));
            currentPos = nushSlon.transform.position;
            yield return null;
        }

        movement = null;
        DebugLog("✅ Телепортация завершена!");
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[TeleportManager] {message}");
    }

    // 🔹 Визуализация в Scene View
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        // Показываем направление контроллера
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);

        // Точка попадания
        if (_isValidTeleport && hit.collider != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(hit.point, 0.1f);
        }
    }
}