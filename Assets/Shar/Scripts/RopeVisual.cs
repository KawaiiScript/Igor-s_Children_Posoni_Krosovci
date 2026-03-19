using UnityEngine;

/// <summary>
/// Компонент для визуализации верёвки (если используется LineRenderer)
/// </summary>
public class RopeVisual : MonoBehaviour
{
    [Header("🎨 Настройки")]
    public LineRenderer lineRenderer;
    public int segments = 10;
    public float ropeSag = 0.1f;

    private void Awake()
    {
        if (lineRenderer == null)
            lineRenderer = GetComponent<LineRenderer>();

        if (lineRenderer != null)
        {
            lineRenderer.positionCount = segments + 1;
        }
    }

    public void UpdateRope(Vector3 anchorPos, Vector3 leverPos)
    {
        if (lineRenderer == null) return;

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 pos = Vector3.Lerp(anchorPos, leverPos, t);

            // Добавляем провисание
            float sag = Mathf.Sin(t * Mathf.PI) * ropeSag;
            pos.y -= sag;

            lineRenderer.SetPosition(i, pos);
        }
    }
}