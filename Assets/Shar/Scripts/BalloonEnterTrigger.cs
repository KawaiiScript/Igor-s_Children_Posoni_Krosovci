using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// BalloonEnterTrigger - Вход через зону, ВЫХОД ВЕЗДЕ!
/// </summary>
public class BalloonEnterTrigger : MonoBehaviour
{
    [Header("🎯 Точки")]
    public Transform teleportPointInside;
    public Transform teleportPointOutside;

    [Header("🎮 XR Origin")]
    public Transform xrOrigin;

    [Header("🎮 Input")]
    public InputActionReference interactAction;

    [Header("⚙️ Настройки")]
    public float triggerRadius = 3f;
    public bool isPlayerInBalloon = false;

    [Header("🔗 Ссылки")]
    public BalloonController balloonController;
    public Transform balloonTransform;

    [Header("🔊 Звуки")]
    public AudioClip enterSound;
    public AudioClip exitSound;
    private AudioSource audioSource;

    [Header("🐛 Debug")]
    public bool showDebugLogs = true;

    // =================================================================

    private GameObject currentPlayer;
    private Vector3 localOffset;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        if (balloonController == null)
            balloonController = FindObjectOfType<BalloonController>();

        if (balloonTransform == null)
        {
            balloonTransform = transform.parent;
            if (balloonTransform != null)
            {
                DebugLog($"[BalloonEnter] ✅ Шар найден: {balloonTransform.name}");
            }
        }
    }

    private void OnEnable()
    {
        if (interactAction?.action != null)
        {
            interactAction.action.Enable();
            interactAction.action.performed += OnInteractPressed;
        }
    }

    private void OnDisable()
    {
        if (interactAction?.action != null)
        {
            interactAction.action.performed -= OnInteractPressed;
            interactAction.action.Disable();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // 🔹 Вход в зону — показываем подсказку
        if (other.CompareTag("Player") || other.CompareTag("Hand"))
        {
            currentPlayer = other.gameObject;
            DebugLog($"[BalloonEnter] ✅ Игрок в зоне: {other.name}");
            ShowPrompt("Нажмите A чтобы войти в шар");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // 🔹 Выход из зоны — скрываем подсказку (но не выходим из шара!)
        if (other.CompareTag("Player") || other.CompareTag("Hand"))
        {
            DebugLog($"[BalloonEnter] ❌ Игрок вышел из зоны: {other.name}");
            HidePrompt();
            currentPlayer = null;
        }
    }

    private void OnInteractPressed(InputAction.CallbackContext context)
    {
        // 🔹 ГЛАВНОЕ ИЗМЕНЕНИЕ:
        // Если игрок В ШАРЕ (isPlayerInBalloon = true) → может выйти ВЕЗДЕ!

        if (isPlayerInBalloon)
        {
            ExitBalloon();  // ← Работает ВЕЗДЕ!
        }
        else
        {
            // Если НЕ в шаре — проверяем что в зоне для входа
            if (currentPlayer != null)
            {
                EnterBalloon();  // ← Только в зоне!
            }
            else
            {
                DebugLog("[BalloonEnter] ⚠️ Не в зоне! Отойдите ближе к шару.");
                ShowPrompt("Отойдите ближе к шару чтобы войти");
            }
        }
    }

    // =================================================================
    // 🔹 ВХОД В ШАР (только в зоне!)
    // =================================================================

    private void EnterBalloon()
    {
        if (teleportPointInside == null || xrOrigin == null)
        {
            DebugLog("[BalloonEnter] ❌ teleportPointInside или xrOrigin не назначены!");
            return;
        }

        DebugLog("[BalloonEnter] 🎈 Вход в шар...");

        // 🔹 Телепортация в точку внутри шара
        xrOrigin.position = teleportPointInside.position;
        xrOrigin.rotation = teleportPointInside.rotation;

        // 🔹 ПРИВЯЗКА К ШАРУ
        if (balloonTransform != null)
        {
            Vector3 worldPos = xrOrigin.position;
            Quaternion worldRot = xrOrigin.rotation;

            xrOrigin.SetParent(balloonTransform);

            xrOrigin.position = worldPos;
            xrOrigin.rotation = worldRot;

            localOffset = xrOrigin.localPosition;

            DebugLog($"[BalloonEnter] ✅ Прикреплён к {balloonTransform.name}");
        }

        isPlayerInBalloon = true;

        if (balloonController != null)
        {
            balloonController.ActivateBalloon();
        }

        if (audioSource != null && enterSound != null)
            audioSource.PlayOneShot(enterSound);

        DebugLog("[BalloonEnter] ✅ Игрок внутри шара — ЛЕТИМ! 🎈");
        ShowPrompt("Нажмите A чтобы выйти из шара (в любом месте!)");
    }

    // =================================================================
    // 🔹 ВЫХОД ИЗ ШАРА (ВЕЗДЕ!)
    // =================================================================

    private void ExitBalloon()
    {
        if (teleportPointOutside == null || xrOrigin == null)
        {
            DebugLog("[BalloonEnter] ❌ teleportPointOutside или xrOrigin не назначены!");
            return;
        }

        DebugLog("[BalloonEnter] 🚪 Выход из шара...");

        // 🔹 Сохраняем мировую позицию ПЕРЕД отвязкой
        Vector3 worldPos = xrOrigin.position;
        Quaternion worldRot = xrOrigin.rotation;

        // 🔹 ОТВЯЗКА от шара
        if (xrOrigin.parent != null)
        {
            xrOrigin.SetParent(null);
            DebugLog("[BalloonEnter] ✅ Откреплён от шара");
        }

        // 🔹 Телепортация в точку снаружи
        xrOrigin.position = teleportPointOutside.position;
        xrOrigin.rotation = Quaternion.Euler(0, teleportPointOutside.eulerAngles.y, 0);

        isPlayerInBalloon = false;

        if (balloonController != null)
        {
            balloonController.DeactivateBalloon();
        }

        if (audioSource != null && exitSound != null)
            audioSource.PlayOneShot(exitSound);

        DebugLog("[BalloonEnter] ✅ Игрок снаружи шара");
        HidePrompt();
        currentPlayer = null;
    }

    // =================================================================

    private void ShowPrompt(string message) => DebugLog($"[PROMPT] {message}");
    private void HidePrompt() => DebugLog("[PROMPT] Скрыть подсказку");
    private void DebugLog(string message) { if (showDebugLogs) Debug.Log(message); }

    // =================================================================
    // 🎨 GIZMOS
    // =================================================================

    private void OnDrawGizmos()
    {
        // Зона триггера (для входа)
        Gizmos.color = new Color(0, 1, 0, 0.3f);
        Gizmos.DrawSphere(transform.position, triggerRadius);

        // Точка входа
        if (teleportPointInside != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(teleportPointInside.position, 0.2f);
        }

        // Точка выхода
        if (teleportPointOutside != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(teleportPointOutside.position, 0.2f);
        }

        // 🔹 Индикатор когда игрок в шаре
        if (isPlayerInBalloon)
        {
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.DrawWireSphere(transform.position, triggerRadius * 1.2f);
        }
    }
}