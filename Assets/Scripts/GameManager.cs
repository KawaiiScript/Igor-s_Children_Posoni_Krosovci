using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("UI")]
    public GameObject winText;      // Текст "Вы выиграли"
    public GameObject loseText;     // Текст "Вы проиграли"

    [Header("Настройки")]
    public float waitTime = 5f;

    private void Awake()
    {
        Debug.Log("=== GameManager.Awake() ===");

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("GameManager создан и сохранен");
        }
        else
        {
            Debug.Log("GameManager уже существует, удаляем дубликат");
            Destroy(gameObject);
        }
    }

    void Start()
    {
        Debug.Log("=== GameManager.Start() ===");

        // Скрываем оба текста при старте
        if (winText != null)
        {
            winText.SetActive(false);
            Debug.Log("WinText скрыт");
        }

        if (loseText != null)
        {
            loseText.SetActive(false);
            Debug.Log("LoseText скрыт");
        }
    }

    public void ShowWin()
    {
        Debug.Log("=== GameManager.ShowWin() ВЫЗВАН ===");
        StartCoroutine(RestartSequence(true)); // true = победа
    }

    public void ShowLose()
    {
        Debug.Log("=== GameManager.ShowLose() ВЫЗВАН ===");
        StartCoroutine(RestartSequence(false)); // false = поражение
    }

    private System.Collections.IEnumerator RestartSequence(bool isWin)
    {
        Debug.Log("=== RestartSequence НАЧАТ ===");
        Debug.Log("Результат: " + (isWin ? "ПОБЕДА" : "ПОРАЖЕНИЕ"));

        // Скрываем оба текста (на всякий случай)
        if (winText != null) winText.SetActive(false);
        if (loseText != null) loseText.SetActive(false);

        // Показываем нужный текст
        if (isWin)
        {
            if (winText != null)
            {
                winText.SetActive(true);
                Debug.Log("Показан WinText");
            }
            else
            {
                Debug.LogError("ОШИБКА: winText = NULL!");
            }
        }
        else
        {
            if (loseText != null)
            {
                loseText.SetActive(true);
                Debug.Log("Показан LoseText");
            }
            else
            {
                Debug.LogError("ОШИБКА: loseText = NULL!");
            }
        }

        Debug.Log("Ждем " + waitTime + " секунд...");
        yield return new WaitForSeconds(waitTime);

        // 🔁 ТЕПЕРЬ ВСЕГДА загружаем сцену с индексом 1
        int targetSceneIndex = 1;
        Debug.Log("Время вышло! Загружаем сцену с индексом: " + targetSceneIndex);

        SceneManager.LoadScene(targetSceneIndex);
    }
}