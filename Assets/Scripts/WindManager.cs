using UnityEngine;
using System;

public class WindZonesManager : MonoBehaviour
{
    [Serializable]
    public class WindZone
    {
        public string zoneName = "Zone";
        public Vector3 center = new Vector3(0, 0, 0);   // Центр зоны
        public Vector3 size = new Vector3(100, 500, 100); // Размер зоны (X, Y, Z)
        public Vector2 direction = new Vector2(1, 0);   // Направление (X, Z)
        public float force = 10f;                       // Сила ветра
    }

    [Header("Ветровые зоны (расставить на карте)")]
    public WindZone[] windZones;

    [Header("Ссылки")]
    public Rigidbody balloonRigidbody;

    [Header("Настройки")]
    public float updateInterval = 0.1f;
    public float defaultWindForce = 0f; // Ветер вне зон (обычно 0)

    private float updateTimer = 0f;
    private WindZone currentZone = null;

    void FixedUpdate()
    {
        if (balloonRigidbody == null || windZones == null) return;

        updateTimer += Time.fixedDeltaTime;
        if (updateTimer < updateInterval) return;
        updateTimer = 0f;

        ApplyWind();
    }

    void ApplyWind()
    {
        Vector3 balloonPos = balloonRigidbody.position;
        currentZone = GetZoneByPosition(balloonPos);

        if (currentZone != null)
        {
            // Ветер внутри зоны
            Vector3 windVector = new Vector3(currentZone.direction.x, 0, currentZone.direction.y).normalized * currentZone.force;
            balloonRigidbody.AddForce(windVector, ForceMode.Force);
            Debug.Log($"В зоне: {currentZone.zoneName}");
        }
        else
        {
            // Ветер вне зон (тишина или слабый фон)
            if (defaultWindForce > 0)
            {
                balloonRigidbody.AddForce(Vector3.forward * defaultWindForce, ForceMode.Force);
            }
        }
    }

    WindZone GetZoneByPosition(Vector3 pos)
    {
        foreach (var zone in windZones)
        {
            // Проверяем, находится ли шар внутри бокса
            float halfX = zone.size.x / 2f;
            float halfY = zone.size.y / 2f;
            float halfZ = zone.size.z / 2f;

            if (pos.x >= zone.center.x - halfX && pos.x <= zone.center.x + halfX &&
                pos.y >= zone.center.y - halfY && pos.y <= zone.center.y + halfY &&
                pos.z >= zone.center.z - halfZ && pos.z <= zone.center.z + halfZ)
            {
                return zone;
            }
        }
        return null;
    }

    // Рисует зелёные коробки в редакторе, чтобы вы видели зоны
    void OnDrawGizmosSelected()
    {
        if (windZones == null) return;

        Gizmos.color = new Color(0, 1, 0, 0.3f);
        foreach (var zone in windZones)
        {
            Gizmos.DrawCube(zone.center, zone.size);

            // Стрелка направления
            Gizmos.color = Color.yellow;
            Vector3 arrowStart = zone.center;
            Vector3 arrowEnd = arrowStart + new Vector3(zone.direction.x, 0, zone.direction.y).normalized * 30f;
            Gizmos.DrawLine(arrowStart, arrowEnd);
        }
    }
}
