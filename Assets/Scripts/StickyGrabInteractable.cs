using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// При захвате объекта виртуальная рука «прилипает» к нему.
/// Вешается рядом с XRGrabInteractable на рычаги, вентили, ручки и т.д.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class StickyGrabInteractable : MonoBehaviour
{
    [Header("✋ Точка хвата (Hand Pose)")]
    [Tooltip("Пустой дочерний объект, задающий позу руки.\n" +
             "Если не задан — берётся Attach Transform интерактабла.")]
    [SerializeField] private Transform handPosePoint;

    [Header("⚙️ Режим")]
    [Tooltip("true  — рука встаёт точно в Hand Pose Point (фикс. хват)\n" +
             "false — рука сохраняет смещение (хват «где схватил»)")]
    [SerializeField] private bool snapToExactPose = true;

    // ──────────────────
    private XRGrabInteractable grab;
    private VRHandVisual attachedHand;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    private void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }
    }

    // ──────────────────

    private void OnGrab(SelectEnterEventArgs args)
    {
        var hand = VRHandVisual.FindByInteractor(args.interactorObject);
        if (hand == null) return;

        attachedHand = hand;

        // Определяем, к какому Transform прилипнуть
        Transform target = handPosePoint != null
            ? handPosePoint
            : grab.GetAttachTransform(args.interactorObject);

        hand.StickTo(target, preserveOffset: !snapToExactPose);
    }

    private void OnRelease(SelectExitEventArgs args)
    {
        if (attachedHand != null)
        {
            attachedHand.Unstick();
            attachedHand = null;
        }
    }
}