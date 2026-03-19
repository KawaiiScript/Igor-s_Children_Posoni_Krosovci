using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// VRButtonManager - Простой менеджер кнопок (в каждой сцене свой)
/// </summary>
public class VRButtonManager : MonoBehaviour
{
    [Header("📊 Индексы сцен")]
    public int mainMenuIndex = 0;
    public int gameIndex = 1;

    [Header("🐛 Debug")]
    public bool showDebugLogs = true;

    // =================================================================
    // PUBLIC METHODS (для кнопок через Button.onClick)
    // =================================================================

    /// <summary>
    /// Закрыть меню (как кнопка B)
    /// </summary>
    public void CloseMenu()
    {
        DebugLog("[VRButtonManager] Закрытие меню...");

        // 🔹 Просто ищем VRMenuController в текущей сцене
        var mc = FindObjectOfType<VRMenuController>();

        if (mc != null)
        {
            mc.CloseMenu();
            DebugLog("[VRButtonManager] ✅ Меню закрыто");
        }
        else
        {
            DebugLog("[VRButtonManager] ⚠️ VRMenuController не найден в сцене");
        }
    }

    /// <summary>
    /// Загрузить сцену по индексу
    /// </summary>
    public void LoadScene(int index)
    {
        if (index < 0 || index >= SceneManager.sceneCountInBuildSettings)
        {
            DebugLog($"[VRButtonManager] ❌ Индекс {index} вне диапазона!");
            return;
        }

        DebugLog($"[VRButtonManager] 🔄 Загрузка сцены #{index}...");
        SceneManager.LoadScene(index);
    }

    /// <summary>
    /// Перезапустить текущую сцену
    /// </summary>
    public void RestartScene()
    {
        Scene current = SceneManager.GetActiveScene();

        if (current.buildIndex == -1)
        {
            DebugLog("[VRButtonManager] ❌ Сцена не в Build Settings!");
            return;
        }

        DebugLog($"[VRButtonManager] 🔄 Рестарт сцены #{current.buildIndex}");
        SceneManager.LoadScene(current.buildIndex);
    }

    /// <summary>
    /// Загрузить главное меню
    /// </summary>
    public void LoadMainMenu() => LoadScene(mainMenuIndex);

    /// <summary>
    /// Загрузить игру
    /// </summary>
    public void LoadGame() => LoadScene(gameIndex);

    /// <summary>
    /// Выход из приложения
    /// </summary>
    public void QuitApp()
    {
        DebugLog("[VRButtonManager] Выход из приложения");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }

    // =================================================================
    // DEBUG
    // =================================================================

    private void DebugLog(string message)
    {
        if (showDebugLogs)
            Debug.Log(message);
    }

    private void Start()
    {
        DebugLog($"[VRButtonManager] ✅ Запущен в сцене: {SceneManager.GetActiveScene().name}");
    }
}