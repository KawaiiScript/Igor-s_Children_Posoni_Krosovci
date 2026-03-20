using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SmenaScene : MonoBehaviour
{
    [Header("Номер сцены (Build Index)")]
    [SerializeField] private int sceneIndex = 0;

    [Header("Настройки затемнения")]
    [SerializeField] private float fadeDuration = 1f;

    // Статическая ссылка на общий CanvasGroup (живёт между сценами)
    private static CanvasGroup fadeCanvasGroup;
    private static GameObject fadeObject;

    private void Awake()
    {
        // Если ещё нет общего CanvasGroup — создаём
        if (fadeCanvasGroup == null)
            CreateFadeCanvas();
    }

    private void CreateFadeCanvas()
    {
        // Создаём объект с Canvas и CanvasGroup
        fadeObject = new GameObject("SceneFader");
        DontDestroyOnLoad(fadeObject);

        Canvas canvas = fadeObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // поверх всего

        // Добавляем чёрный Image
        Image image = fadeObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;

        // Растягиваем на весь экран
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;

        // Добавляем CanvasGroup
        fadeCanvasGroup = fadeObject.AddComponent<CanvasGroup>();
        fadeCanvasGroup.alpha = 0f; // прозрачный
        fadeCanvasGroup.blocksRaycasts = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Можно проверить тег, если нужно, например "Player"
        // if (!other.CompareTag("Player")) return;

        // Запускаем корутину на общем объекте, чтобы она не прервалась при смене сцены
        if (fadeObject != null)
            fadeObject.GetComponent<MonoBehaviour>().StartCoroutine(FadeAndLoad());
    }

    private IEnumerator FadeAndLoad()
    {


        // Если индекс отрицательный — выход из игры
        if (sceneIndex < 0)
        {
            Debug.Log("Выход из игры");
            Application.Quit();
        }
        else
        {
            // Асинхронная загрузка сцены
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneIndex);
            while (!asyncLoad.isDone)
                yield return null;
        }


    }
}