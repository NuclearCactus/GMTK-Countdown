using UnityEngine;

namespace GMTKCountdown.Tunnel
{
    [RequireComponent(typeof(Collider))]
    public class TunnelPolygonResetOrb : MonoBehaviour
    {
        [SerializeField] private TunnelPolygonSequenceManager tunnelManager;
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
                tunnelManager = FindAnyObjectByType<TunnelPolygonSequenceManager>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.GetComponent<EasyPeasyFirstPersonController.FirstPersonController>())
                return;

            if (tunnelManager != null)
            {
                tunnelManager.PlayOrbPickupSound();
                tunnelManager.ResetToOriginalPolygonalLevel();
            }

            if (destroyOnPickup)
                Destroy(gameObject);
        }
    }
}
