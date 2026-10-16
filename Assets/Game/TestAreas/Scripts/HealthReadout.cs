using Game.Core;
using UnityEngine;

namespace Game.TestAreas
{
    /// <summary>Keeps a text label showing the remaining hit points of the damage receiver on its parent.</summary>
    [RequireComponent(typeof(TextMesh))]
    public sealed class HealthReadout : MonoBehaviour
    {
        IDamageable receiver;
        GameObject receiverObject;
        float maximum;
        TextMesh text;

        private void Start()
        {
            text = GetComponent<TextMesh>();
            receiver = GetComponentInParent<IDamageable>();
            receiverObject = (receiver as Component)?.gameObject;
            maximum = receiver != null ? receiver.HitPoints : 0f;
            Refresh(maximum);
            CombatEvents.Damaged += OnDamaged;
        }

        private void OnDestroy() => CombatEvents.Damaged -= OnDamaged;

        private void OnDamaged(DamageReport report)
        {
            if (report.Target == receiverObject) Refresh(report.Remaining);
        }

        private void Refresh(float remaining)
        {
            if (text) text.text = receiver == null ? "" : $"{remaining:0} / {maximum:0} HP";
        }
    }
}
