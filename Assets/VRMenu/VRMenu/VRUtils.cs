using UnityEngine;

/// <summary>
/// VRUtils - Утилиты для VR разработки
/// Содержит вспомогательные методы и расширения
///
/// Автор: NTO VR Final Helper
/// Версия: 1.0
/// </summary>
public static class VRUtils
{
    #region Математические утилиты
    /// <summary>
    /// Ограничение угла в пределах [-180, 180]
    /// </summary>
    public static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

    /// <summary>
    /// Плавная интерполяция с эффектом пружины
    /// </summary>
    public static float SpringInterpolate(float current, float target, ref float velocity, float springStrength, float damping)
    {
        float difference = target - current;
        velocity += difference * springStrength * Time.deltaTime;
        velocity *= 1f - damping * Time.deltaTime;
        return current + velocity;
    }

    /// <summary>
    /// Ease In Out Quad интерполяция
    /// </summary>
    public static float EaseInOutQuad(float t)
    {
        return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
    }

    /// <summary>
    /// Ease Out Back интерполяция (с небольшим перелётом)
    /// </summary>
    public static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    /// <summary>
    /// Ease Out Elastic интерполяция (упругий эффект)
    /// </summary>
    public static float EaseOutElastic(float t)
    {
        const float c4 = (2f * Mathf.PI) / 3f;

        if (t == 0f) return 0f;
        if (t == 1f) return 1f;

        return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
    }
    #endregion

    #region Работа с трансформациями
    /// <summary>
    /// Установить позицию и поворот с плавной интерполяцией
    /// </summary>
    public static void SmoothSetTransform(Transform transform, Vector3 targetPosition, Quaternion targetRotation, float speed)
    {
        transform.position = Vector3.Lerp(transform.position, targetPosition, speed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, speed * Time.deltaTime);
    }

    /// <summary>
    /// Получить позицию перед камерой
    /// </summary>
    public static Vector3 GetPositionInFrontOfCamera(Camera camera, float distance)
    {
        return camera.transform.position + camera.transform.forward * distance;
    }

    /// <summary>
    /// Проверить, находится ли точка в поле зрения камеры
    /// </summary>
    public static bool IsPointInViewFrustum(Camera camera, Vector3 point)
    {
        Vector3 viewportPoint = camera.WorldToViewportPoint(point);
        return viewportPoint.z > 0f &&
               viewportPoint.x >= 0f && viewportPoint.x <= 1f &&
               viewportPoint.y >= 0f && viewportPoint.y <= 1f;
    }

    /// <summary>
    /// Получить угол между направлением взгляда и точкой
    /// </summary>
    public static float GetAngleToPosition(Transform viewer, Vector3 position)
    {
        Vector3 directionToTarget = position - viewer.position;
        directionToTarget.y = 0f;
        directionToTarget.Normalize();

        return Vector3.Angle(viewer.forward, directionToTarget);
    }
    #endregion

    #region Работа с слоями
    /// <summary>
    /// Создать LayerMask для указанных слоёв
    /// </summary>
    public static LayerMask CreateLayerMask(params string[] layerNames)
    {
        LayerMask mask = 0;
        foreach (string layerName in layerNames)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0)
            {
                mask |= (1 << layer);
            }
        }
        return mask;
    }

    /// <summary>
    /// Создать LayerMask для указанных индексов слоёв
    /// </summary>
    public static LayerMask CreateLayerMask(params int[] layerIndices)
    {
        LayerMask mask = 0;
        foreach (int layer in layerIndices)
        {
            if (layer >= 0 && layer < 32)
            {
                mask |= (1 << layer);
            }
        }
        return mask;
    }

    /// <summary>
    /// Проверить, находится ли объект на указанном слое
    /// </summary>
    public static bool IsOnLayer(GameObject obj, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        return obj.layer == layer;
    }

    /// <summary>
    /// Проверить, находится ли объект на одном из указанных слоёв
    /// </summary>
    public static bool IsOnAnyLayer(GameObject obj, LayerMask mask)
    {
        return (mask.value & (1 << obj.layer)) != 0;
    }
    #endregion

    #region Работа с RectTransform
    /// <summary>
    /// Проверить, находится ли мировая точка внутри RectTransform
    /// </summary>
    public static bool IsWorldPointInRectTransform(Vector3 worldPoint, RectTransform rectTransform, Camera camera = null)
    {
        if (camera == null)
        {
            camera = Camera.main;
        }

        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            camera.WorldToScreenPoint(worldPoint),
            camera,
            out localPoint
        );

        return rectTransform.rect.Contains(localPoint);
    }

    /// <summary>
    /// Получить границы RectTransform в мировых координатах
    /// </summary>
    public static Bounds GetWorldBounds(RectTransform rectTransform)
    {
        Bounds bounds = new Bounds(rectTransform.position, Vector3.zero);

        Vector3[] corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        foreach (Vector3 corner in corners)
        {
            bounds.Encapsulate(corner);
        }

        return bounds;
    }
    #endregion

    #region Отладка
    /// <summary>
    /// Нарисовать линию в редакторе (для Gizmos)
    /// </summary>
    public static void DrawLine(Vector3 start, Vector3 end, Color color)
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.DrawLine(start, end);
#endif
    }

    /// <summary>
    /// Нарисовать сферу в редакторе (для Gizmos)
    /// </summary>
    public static void DrawSphere(Vector3 center, float radius, Color color)
    {
#if UNITY_EDITOR
        UnityEditor.Handles.color = color;
        UnityEditor.Handles.SphereHandleCap(0, center, Quaternion.identity, radius * 2f, EventType.Repaint);
#endif
    }

    /// <summary>
    /// Вывести сообщение только в редакторе
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public static void LogEditor(string message)
    {
        Debug.Log(message);
    }
    #endregion
}
