using UnityEngine;

namespace Game.Movement
{
    public sealed class StaminaBar : MonoBehaviour
    {
        [SerializeField] private PlayerStamina stamina;
        [SerializeField] private RoundedStaminaGraphic fill;

        private void OnEnable()
        {
            if (!stamina || !fill)
            {
                Debug.LogError("Stamina Bar needs the player's stamina and fill image assigned.", this);
                enabled = false;
                return;
            }
            Refresh();
        }

        private void LateUpdate() => Refresh();
        private void Refresh() => fill.FillAmount = stamina.Fraction;
    }
}
