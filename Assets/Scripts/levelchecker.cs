using UnityEngine;

public class LevelChecker : MonoBehaviour
{
    // Флаги состояния
    private bool hasLeftStart = false;
    private bool hasHitWall = false;
    private bool isLevelFinished = false; // Чтобы не сработать дважды

    void Start()
    {
        Debug.Log("hfjghfjgfhdgjdfhjgd");
        // Сброс при старте
        hasLeftStart = false;
        hasHitWall = false;
        isLevelFinished = false;

        Debug.Log("=== [LevelChecker] Игра началась ===");
    }

    // 1. Выход из стартовой зоны
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("StartZone"))
        {
            hasLeftStart = true;
            Debug.Log(">>> Игрок покинул стартовую зону (StartZone)");
        }
    }

    // 2. Столкновение со стеной
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            hasHitWall = true;
            Debug.Log(">>> Игрок коснулся стены (Wall)!");
        }
    }

    // 3. Вход в финишную зону
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("FinishZone") && !isLevelFinished)
        {
            isLevelFinished = true; // Блокируем повторный вход
            CheckLevelResult();
        }
    }

    // 4. Проверка результата
    private void CheckLevelResult()
    {
        Debug.Log("=== [LevelChecker] Финиш! Проверка условий... ===");
        Debug.Log($"Покинул старт: {hasLeftStart} | Коснулся стены: {hasHitWall}");

        // Условие победы: Покинул старт И НЕ коснулся стены
        if (hasLeftStart && !hasHitWall)
        {
            Debug.Log("========================================");
            Debug.Log("✅ УСПЕХ! УРОВЕНЬ ПРОЙДЕН!");
            Debug.Log("========================================");

            // Попытка вызвать GameManager (безопасная)
            TryNotifyGameManager(true);
        }
        else
        {
            Debug.Log("========================================");
            Debug.Log("❌ ПРОВАЛ! УСЛОВИЯ НЕ ВЫПОЛНЕНЫ!");
            Debug.Log("========================================");

            // Попытка вызвать GameManager (безопасная)
            TryNotifyGameManager(false);
        }
    }

    // Безопасный вызов менеджера (не вызовет ошибку, если менеджера нет)
    private void TryNotifyGameManager(bool isWin)
    {
        GameManager[] managers = FindObjectsOfType<GameManager>();

        if (managers.Length > 0)
        {
            if (isWin) managers[0].ShowWin();
            else managers[0].ShowLose();
        }
        else
        {
            Debug.LogWarning("GameManager не найден на сцене. (Это нормально для теста в консоли)");
        }
    }

}