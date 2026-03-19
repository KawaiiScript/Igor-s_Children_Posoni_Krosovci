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
    [SerializeField] private LayerMask groundMask;   // слой для телепортации по земле
    [SerializeField] private LayerMask balloonMask;  // слой для телепортации в шаре
    [SerializeField] private float rayLength = 10f;

    [Header("🎯 Настройки")]
    [SerializeField] private Transform nushSlon;      // корневой трансформ игрока (XR Origin)
    [SerializeField] private float forwardThreshold = 0.7f;

    [Header("🎈 Шар (автоматически найдётся, если не указан)")]
    [SerializeField] private BalloonEnterTrigger inShar; // триггер входа в шар

    [Header("🐛 Debug")]
    [SerializeField] private bool showDebugLogs = true;

    private RaycastHit hit;
    private LineRenderer lineRenderer;
    private GameObject sphere;
    private Coroutine movement = null;
    private bool _isTeleportActive = false;
    private bool _isValidTeleport = false;

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

        // 🔍 Автоматический поиск триггера шара, если не назначен
        if (inShar == null)
        {
            inShar = FindObjectOfType<BalloonEnterTrigger>();
            if (inShar != null)
                DebugLog("✅ BalloonEnterTrigger найден автоматически");
            else
                DebugLog("⚠️ BalloonEnterTrigger не найден в сцене");
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

        // Читаем Vector2 с джойстика
        Vector2 joystick = teleportAction.action.ReadValue<Vector2>();
        bool isForward = joystick.y >= forwardThreshold;

        // Начало прицеливания
        if (isForward && !_isTeleportActive)
        {
            DebugLog("🟡 Start Aiming");
            _isTeleportActive = true;
            _isValidTeleport = false;
        }
        // Завершение прицеливания
        else if (!isForward && _isTeleportActive)
        {
            DebugLog("🔴 Stop Aiming");
            _isTeleportActive = false;

            if (_isValidTeleport && hit.collider != null)
            {
                DebugLog($"🚀 TELEPORT to: {hit.point}");
                movement = StartCoroutine(SmoothMove(nushSlon.position, hit.point, 0.1f));
            }
            else
            {
                DebugLog("⚠️ Телепортация отменена — нет валидной точки");
            }

            lineRenderer.enabled = false;
            sphere.SetActive(false);
            _isValidTeleport = false;
        }

        // Обновление луча при активном прицеливании
        if (_isTeleportActive)
        {
            UpdateRaycast();
        }
    }

    private void UpdateRaycast()
    {
        // Определяем, какую маску использовать
        bool inBalloon = (inShar != null && inShar.isPlayerInBalloon);
        LayerMask currentMask = inBalloon ? balloonMask : groundMask;

        Ray ray = new Ray(transform.position, transform.forward);
        if (Physics.Raycast(ray, out hit, rayLength, currentMask))
        {
            _isValidTeleport = true;
            lineRenderer.enabled = true;
            lineRenderer.SetPosition(0, transform.position);
            lineRenderer.SetPosition(1, hit.point);
            sphere.SetActive(true);
            sphere.transform.position = hit.point;

            if (showDebugLogs && Time.frameCount % 30 == 0)
                DebugLog($"✅ Raycast OK: {hit.point} (Mask: {currentMask.value})");
        }
        else
        {
            _isValidTeleport = false;
            lineRenderer.enabled = false;
            sphere.SetActive(false);

            if (showDebugLogs && Time.frameCount % 30 == 0)
                DebugLog($"❌ Raycast MISS — маска {currentMask.value}");
        }
    }

    private IEnumerator SmoothMove(Vector3 startPosWorld, Vector3 endPosWorld, float step)
    {
        // Проверяем, находимся ли в шаре и есть ли ссылка на BalloonController
        bool inBalloon = (inShar != null && inShar.isPlayerInBalloon && inShar.balloonController != null);
        Transform balloon = inShar?.balloonController?.transform;

        if (inBalloon && balloon != null)
        {
            // Локальная телепортация внутри шара
            Vector3 startLocal = nushSlon.localPosition; // текущая позиция внутри шара
            Vector3 endLocal = balloon.InverseTransformPoint(endPosWorld); // мировая → локальная

            float s = 0;
            while (s < 1f)
            {
                s += step;
                nushSlon.localPosition = Vector3.Lerp(startLocal, endLocal, Mathf.Clamp01(s));
                yield return null;
            }
            nushSlon.localPosition = endLocal; // финальная точность
            DebugLog("✅ Локальная телепортация внутри шара завершена");
        }
        else
        {
            // Обычная мировая телепортация (на земле или вне шара)
            float s = 0;
            while (s < 1f)
            {
                s += step;
                nushSlon.position = Vector3.Lerp(startPosWorld, endPosWorld, Mathf.Clamp01(s));
                yield return null;
            }
            nushSlon.position = endPosWorld;
            DebugLog("✅ Мировая телепортация завершена");
        }

        movement = null;
    }

    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log($"[TeleportManager] {message}");
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, transform.forward * 2f);

        if (_isValidTeleport && hit.collider != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(hit.point, 0.1f);
        }
    }
}