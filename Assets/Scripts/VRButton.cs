using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
public class VRButton : MonoBehaviour
{
    // 🔽 Выбираем режим в Инспекторе
    public enum ActionMode { RestartScene, GoToMenu, CloseMenu }

    [Header("⚙️ Настройки")]
    [Tooltip("Что сделает эта кнопка")]
    public ActionMode mode = ActionMode.RestartScene;

    [Tooltip("Тег объекта-руки (контроллера)")]
    public string handTag = "Hand";

    [Tooltip("Защита от повторных нажатий (сек)")]
    public float clickCooldown = 0.3f;

    [Tooltip("Вибрация при нажатии (сила 0-1)")]
    public float hapticStrength = 0.4f;

    [Tooltip("Длительность вибрации")]
    public float hapticDuration = 0.1f;

    // 🔒 Приватные поля
    private bool _canClick = true;
    private XRController _handController; // Для вибрации

    // 🎯 Основной метод выполнения действия
    public void Execute()
    {
        if (!_canClick) return;
        _canClick = false;
        Invoke(nameof(ResetClick), clickCooldown); // Кулдаун

        // 📳 Вибрация (если нашли контроллер)
        if (_handController != null)
            _handController.SendHapticImpulse(hapticStrength, hapticDuration);

        // ▶️ Выполняем действие
        switch (mode)
        {
            case ActionMode.RestartScene:
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                break;

            case ActionMode.GoToMenu:
                SceneManager.LoadScene(0);
                break;

            case ActionMode.CloseMenu:
                if (VRMenuController.Instance != null)
                    VRMenuController.Instance.CloseMenu();
                break;
        }

        Debug.Log($"[Button] Выполнено: {mode}");
    }

    // 🔁 Сброс кулдауна
    private void ResetClick() => _canClick = true;

    // 👋 Когда рука заходит в триггер кнопки
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(handTag))
        {
            // Пытаемся найти контроллер для вибрации
            if (_handController == null)
                _handController = other.GetComponentInParent<XRController>();

            Execute();
        }
    }

    // 🔍 Опционально: отладка выхода руки
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(handTag))
        {
            Debug.Log($"[Button] Рука вышла из {gameObject.name}");
        }
    }
}