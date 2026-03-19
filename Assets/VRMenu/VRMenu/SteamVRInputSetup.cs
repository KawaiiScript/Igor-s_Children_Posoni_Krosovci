using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

/// <summary>
/// XRInputHelper - Помощник для поиска Input Actions по имени
/// Работает через строковые ссылки (не создаёт InputActionReference)
///
/// Автор: NTO VR Final Helper
/// Версия: 2.1 (исправлена ошибка read-only)
/// </summary>
public class SteamVRInputSetup : MonoBehaviour
{
    #region Настройки
    [Header("⚙️ Настройки")]
    [Tooltip("Имя Input Action Asset")]
    public string inputAssetName = "VRControls";

    [Tooltip("Показывать отладку")]
    public bool showDebugInfo = true;
    #endregion

    #region Имена действий (настраиваются в Inspector)
    [Header("🎮 Имена действий для поиска")]
    [Tooltip("Имя действия для открытия меню")]
    public string menuActionName = "ToggleMenu";

    [Tooltip("Имя действия для выбора/триггера")]
    public string selectActionName = "Select";

    [Tooltip("Имя действия для грип")]
    public string gripActionName = "Grip";
    #endregion

    #region Найденные действия (Read Only)
    [Header("🔍 Найденные действия")]
    [SerializeField] private InputAction _menuAction;
    [SerializeField] private InputAction _selectAction;
    [SerializeField] private InputAction _gripAction;
    #endregion

    #region Ссылки
    [Header("🔗 Контроллеры")]
    public VRMenuController menuController;
    #endregion

    #region Unity Lifecycle
    private void Start()
    {
        SetupInputActions();
    }
    #endregion

    #region Настройка
    [ContextMenu("Find Input Actions")]
    public void SetupInputActions()
    {
        if (showDebugInfo) Debug.Log("[XRInputHelper] Поиск действий...");

        // 🔍 Находим ассет
        var asset = FindInputAsset(inputAssetName);
        if (asset == null) return;

        // 🔍 Ищем действия по именам
        _menuAction = FindAction(asset, menuActionName);
        _selectAction = FindAction(asset, selectActionName);
        _gripAction = FindAction(asset, gripActionName);

        // 🔗 Применяем к контроллеру (через публичные поля, не через action property)
        ApplyToController();

        if (showDebugInfo) PrintResults();
    }

    /// <summary>
    /// Поиск ассета по имени
    /// </summary>
    private InputActionAsset FindInputAsset(string name)
    {
        var assets = Resources.FindObjectsOfTypeAll<InputActionAsset>();
        var found = assets.FirstOrDefault(a => a.name.Equals(name, System.StringComparison.OrdinalIgnoreCase));

        if (found == null)
            Debug.LogWarning($"[XRInputHelper] Ассет '{name}' не найден!");

        return found;
    }

    /// <summary>
    /// Поиск действия внутри ассета по имени
    /// </summary>
    private InputAction FindAction(InputActionAsset asset, string actionName)
    {
        if (asset == null || string.IsNullOrEmpty(actionName)) return null;

        var action = asset.FindAction(actionName, false);

        if (action == null && showDebugInfo)
            Debug.LogWarning($"[XRInputHelper] Действие '{actionName}' не найдено в '{asset.name}'");

        return action;
    }

    /// <summary>
    /// Применение к контроллеру — БЕЗ присваивания action property
    /// </summary>
    private void ApplyToController()
    {
        if (menuController == null)
            menuController = FindObjectOfType<VRMenuController>();

        if (menuController == null) return;

        // 🔹 Вместо присваивания InputActionReference — просто логируем или используем напрямую
        // Поля menuController.menuAction и selectAction должны быть назначены вручную в Inspector!

        if (showDebugInfo)
        {
            Debug.Log($"[XRInputHelper] Menu Action: {_menuAction?.name ?? "не найдено"}");
            Debug.Log($"[XRInputHelper] Select Action: {_selectAction?.name ?? "не найдено"}");
        }
    }

    private void PrintResults()
    {
        Debug.Log("[XRInputHelper] === Готово ===");
        Debug.Log($"  Menu:   {_menuAction?.name ?? "❌"}");
        Debug.Log($"  Select: {_selectAction?.name ?? "❌"}");
        Debug.Log($"  Grip:   {_gripAction?.name ?? "❌"}");
    }
    #endregion

    #region Публичные методы для использования в других скриптах
    /// <summary>
    /// Получить найденное действие меню
    /// </summary>
    public InputAction GetMenuAction() => _menuAction;

    /// <summary>
    /// Получить найденное действие выбора
    /// </summary>
    public InputAction GetSelectAction() => _selectAction;

    /// <summary>
    /// Проверить нажатие меню (удобно для внешних скриптов)
    /// </summary>
    public bool IsMenuPressed() => _menuAction?.ReadValue<float>() > 0.5f;
    #endregion
}