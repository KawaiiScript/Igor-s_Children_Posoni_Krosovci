using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Управляет визуальной моделью руки.
/// Обычно следует за контроллером; при захвате — прилипает к объекту.
///
/// SETUP: вынесите модель руки из-под контроллера,
///        повесьте этот скрипт и укажите интерактор.
/// </summary>
public class VRHandVisual : MonoBehaviour
{
    // ────────────────────────────────────────────
    //  Инспектор
    // ────────────────────────────────────────────

    [Header("🎯 Привязка к контроллеру")]
    [Tooltip("XR Interactor этой руки (Direct / NearFar / и т.д.)")]
    [SerializeField] private XRBaseInteractor interactor;

    [Header("⚙️ Скорости")]
    [Tooltip("Скорость прилипания к объекту (0 = мгновенно)")]
    [SerializeField, Range(0f, 50f)] private float stickSpeed = 20f;

    [Tooltip("Скорость возврата руки к контроллеру")]
    [SerializeField, Range(0f, 50f)] private float returnSpeed = 15f;

    [Tooltip("Расстояние, при котором рука считается вернувшейся")]
    [SerializeField] private float snapThreshold = 0.002f;

    // ────────────────────────────────────────────
    //  Состояния
    // ────────────────────────────────────────────

    private enum State
    {
        FollowController,   // обычный режим
        StuckToObject,      // прилипла к объекту
        Returning           // плавно летит обратно к контроллеру
    }

    private State state = State.FollowController;
    private Transform controllerTransform;
    private Transform stickyTarget;

    // Смещение руки относительно точки захвата (чтобы не "прыгала")
    private Vector3 grabPosOffset;
    private Quaternion grabRotOffset;
    private bool useOffset;

    // ────────────────────────────────────────────
    //  Глобальный реестр (для быстрого поиска)
    // ────────────────────────────────────────────

    private static readonly Dictionary<XRBaseInteractor, VRHandVisual> s_Hands = new();

    /// <summary>Найти VRHandVisual по интерактору из события захвата.</summary>
    public static VRHandVisual FindByInteractor(IXRSelectInteractor selectInteractor)
    {
        if (selectInteractor is XRBaseInteractor baseInteractor
            && s_Hands.TryGetValue(baseInteractor, out var hand))
            return hand;
        return null;
    }

    // ────────────────────────────────────────────
    //  Жизненный цикл
    // ────────────────────────────────────────────

    private void OnEnable()
    {
        if (interactor == null)
        {
            Debug.LogError($"[VRHandVisual] Не назначен Interactor на «{name}»!", this);
            enabled = false;
            return;
        }
        controllerTransform = interactor.transform;
        s_Hands[interactor] = this;
    }

    private void OnDisable()
    {
        if (interactor != null)
            s_Hands.Remove(interactor);
    }

    private void LateUpdate()
    {
        switch (state)
        {
            // -------- Обычное следование --------
            case State.FollowController:
                SetPose(controllerTransform, 1f);
                break;

            // -------- Прилипание к объекту --------
            case State.StuckToObject:
                if (stickyTarget != null)
                {
                    float t = stickSpeed > 0f
                        ? Mathf.Clamp01(Time.deltaTime * stickSpeed)
                        : 1f;
                    SetPoseWithOffset(stickyTarget, t);
                }
                break;

            // -------- Плавный возврат --------
            case State.Returning:
                if (controllerTransform != null)
                {
                    float t = returnSpeed > 0f
                        ? Mathf.Clamp01(Time.deltaTime * returnSpeed)
                        : 1f;
                    SetPose(controllerTransform, t);

                    float dist = Vector3.Distance(transform.position,
                                                  controllerTransform.position);
                    if (dist < snapThreshold)
                        state = State.FollowController;
                }
                break;
        }
    }

    // ────────────────────────────────────────────
    //  Публичный API
    // ────────────────────────────────────────────

    /// <summary>
    /// Прилепить руку к целевой точке на объекте.
    /// </summary>
    /// <param name="target">Transform, за которым будет следить рука.</param>
    /// <param name="preserveOffset">
    ///   true  — рука сохраняет свой текущий сдвиг относительно target
    ///           (хват «где схватил»).
    ///   false — рука встаёт точно в target (фиксированный grip-pose).
    /// </param>
    public void StickTo(Transform target, bool preserveOffset = false)
    {
        stickyTarget = target;
        useOffset = preserveOffset;

        if (preserveOffset && target != null)
        {
            // Запоминаем текущее смещение
            grabPosOffset = Quaternion.Inverse(target.rotation)
                            * (transform.position - target.position);
            grabRotOffset = Quaternion.Inverse(target.rotation)
                            * transform.rotation;
        }
        else
        {
            grabPosOffset = Vector3.zero;
            grabRotOffset = Quaternion.identity;
        }

        state = State.StuckToObject;
    }

    /// <summary>Отлепить руку, плавно вернуть к контроллеру.</summary>
    public void Unstick()
    {
        stickyTarget = null;
        useOffset = false;
        state = State.Returning;
    }

    // ────────────────────────────────────────────
    //  Хелперы
    // ────────────────────────────────────────────

    private void SetPose(Transform target, float t)
    {
        if (target == null) return;
        if (t >= 0.999f)
        {
            transform.SetPositionAndRotation(target.position, target.rotation);
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position,
                                              target.position, t);
            transform.rotation = Quaternion.Slerp(transform.rotation,
                                                  target.rotation, t);
        }
    }

    private void SetPoseWithOffset(Transform target, float t)
    {
        if (target == null) return;

        Vector3 goalPos = target.position + target.rotation * grabPosOffset;
        Quaternion goalRot = target.rotation * grabRotOffset;

        if (t >= 0.999f)
        {
            transform.SetPositionAndRotation(goalPos, goalRot);
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, goalPos, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, goalRot, t);
        }
    }
}