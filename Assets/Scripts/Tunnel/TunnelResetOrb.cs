using UnityEngine;

namespace GMTKCountdown.Tunnel
{
    [RequireComponent(typeof(Collider))]
    public class TunnelResetOrb : MonoBehaviour
    {
        [SerializeField] private TunnelDegradationManager tunnelManager;
        [SerializeField] private bool destroyOnPickup = true;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
                col.isTrigger = true;
        }

        private void Awake()
        {
            if (tunnelManager == null)
                tunnelManager = FindFirstObjectByType<TunnelDegradationManager>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.GetComponent<EasyPeasyFirstPersonController.FirstPersonController>())
                return;

            if (tunnelManager != null)
                tunnelManager.NotifyOrbCollected();

            if (destroyOnPickup)
                Destroy(gameObject);
        }
    }
}
