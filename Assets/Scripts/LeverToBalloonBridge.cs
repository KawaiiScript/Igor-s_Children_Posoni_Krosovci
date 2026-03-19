using UnityEngine;
using UnityEngine.XR.Content.Interaction;

public class LeverToBalloonBridge : MonoBehaviour
{
    [SerializeField] private XRLever m_Lever;
    [SerializeField] private BalloonController m_Balloon;

    private void OnEnable()
    {
        if (m_Lever != null)
            m_Lever.onNormalizedValueChanged.AddListener(OnLeverValueChanged);
    }

    private void OnDisable()
    {
        if (m_Lever != null)
            m_Lever.onNormalizedValueChanged.RemoveListener(OnLeverValueChanged);
    }

    private void OnLeverValueChanged(float normalizedValue)
    {
        if (m_Balloon != null)
            m_Balloon.SetBurnerIntensity(normalizedValue);
    }

    // Для удобства можно также обновлять ссылки в редакторе
    private void Reset()
    {
        if (m_Lever == null)
            m_Lever = GetComponent<XRLever>();
        if (m_Balloon == null)
            m_Balloon = FindObjectOfType<BalloonController>();
    }
}