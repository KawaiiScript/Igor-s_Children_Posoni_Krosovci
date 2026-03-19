using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Заставляет контроллер следовать за объектом при захвате.
/// Идеально для рычагов, кнопок и ручек с физикой (Joint).
/// Работает В ПАРЕ со стандартным XRGrabInteractable.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class SmartXRGrabInteractable : MonoBehaviour
{
    [Header("⚙️ Настройки прилипания")]
    [Tooltip("Если включено: виртуальная рука следует за рычагом. Если выключено: рычаг следует за рукой (стандарт).")]
    [SerializeField]
    private bool m_HandFollowsObject = true;

    public bool handFollowsObject
    {
        get => m_HandFollowsObject;
        set
        {
            m_HandFollowsObject = value;
            if (!value) ReleaseHand();
        }
    }

    [Tooltip("Скорость выравнивания руки (0 = мгновенно).")]
    [Range(0f, 20f)]
    [SerializeField]
    private float m_SnapSpeed = 15f;

    // Приватные переменные
    private XRGrabInteractable grabInteractable;
    private IXRSelectInteractor currentInteractor;
    private Transform controllerTransform;

    // Смещение руки относительно точки захвата на объекте
    private Vector3 positionOffset;
    private Quaternion rotationOffset;
    private bool isGrabbed = false;

    void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();

        if (grabInteractable == null)
        {
            Debug.LogError("[SmartLeverHandAttach] Не найден XRGrabInteractable!", this);
            enabled = false;
            return;
        }

        // Подписываемся на события захвата
        grabInteractable.selectEntered.AddListener(OnGrabStart);
        grabInteractable.selectExited.AddListener(OnGrabEnd);
    }

    /// <summary>
    /// Вызывается когда игрок берет объект
    /// </summary>
    private void OnGrabStart(SelectEnterEventArgs eventData)
    {
        currentInteractor = eventData.interactorObject;

        if (currentInteractor is XRBaseInteractor baseInteractor)
        {
            controllerTransform = baseInteractor.transform;
            isGrabbed = true;

            if (m_HandFollowsObject)
            {
                // Получаем точку прикрепления (статическую или динамическую)
                Transform attachPoint = grabInteractable.GetAttachTransform(currentInteractor);

                if (attachPoint != null)
                {
                    // Запоминаем смещение между контроллером и ТОЧКОЙ ЗАХВАТА на объекте
                    // Это ключевое отличие — мы привязываемся к точке на рычаге, а не к центру объекта
                    positionOffset = controllerTransform.position - attachPoint.position;
                    rotationOffset = Quaternion.Inverse(attachPoint.rotation) * controllerTransform.rotation;

                    // Мгновенно убираем разрыв в момент хвата
                    SnapHandToAttachPoint(attachPoint);
                }
            }
        }
    }

    /// <summary>
    /// Вызывается когда игрок отпускает объект
    /// </summary>
    private void OnGrabEnd(SelectExitEventArgs eventData)
    {
        ReleaseHand();
    }

    void LateUpdate()
    {
        // Работаем только если включена опция, идет захват и контроллер существует
        if (!m_HandFollowsObject || !isGrabbed || controllerTransform == null)
            return;

        // Получаем актуальную точку прикрепления (она может двигаться вместе с объектом)
        Transform attachPoint = grabInteractable.GetAttachTransform(currentInteractor);

        if (attachPoint != null)
        {
            SnapHandToAttachPoint(attachPoint);
        }
    }

    /// <summary>
    /// Перемещает контроллер вслед за точкой захвата на объекте
    /// </summary>
    private void SnapHandToAttachPoint(Transform attachPoint)
    {
        float smoothFactor = Time.deltaTime * m_SnapSpeed;

        // Цель = позиция точки захвата + запомненное смещение руки
        Vector3 targetPosition = attachPoint.position + positionOffset;
        Quaternion targetRotation = attachPoint.rotation * rotationOffset;

        // Плавное следование (или мгновенное при высокой скорости)
        controllerTransform.position = Vector3.Lerp(controllerTransform.position, targetPosition, smoothFactor);
        controllerTransform.rotation = Quaternion.Slerp(controllerTransform.rotation, targetRotation, smoothFactor);
    }

    /// <summary>
    /// Очищает данные при отпускании
    /// </summary>
    private void ReleaseHand()
    {
        isGrabbed = false;
        currentInteractor = null;
        controllerTransform = null;
    }

    // Валидация значений в инспекторе
    void OnValidate()
    {
        if (m_SnapSpeed < 0f) m_SnapSpeed = 0f;
        if (m_SnapSpeed > 20f) m_SnapSpeed = 20f;
    }
}