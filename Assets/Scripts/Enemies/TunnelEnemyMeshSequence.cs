using UnityEngine;

namespace GMTKCountdown.Enemies
{
    public class TunnelEnemyMeshSequence : MonoBehaviour
    {
        [Header("Tunnel Sync")]
        [SerializeField] private GMTKCountdown.Tunnel.TunnelPolygonSequenceManager tunnelManager;
        [SerializeField] private SkinnedMeshRenderer targetSkinnedMeshRenderer;
        [SerializeField] private Mesh[] enemyMeshes;

        private int currentLevelIndex = -1;

        private void Awake()
        {
            if (targetSkinnedMeshRenderer == null)
                targetSkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        private void OnEnable()
        {
            BindToTunnelManager();
        }

        private void Start()
        {
            ApplyCurrentTunnelLevel();
        }

        private void OnDisable()
        {
            UnbindFromTunnelManager();
        }

        private void OnDestroy()
        {
            UnbindFromTunnelManager();
        }

        private void BindToTunnelManager()
        {
            if (tunnelManager == null)
                tunnelManager = FindAnyObjectByType<GMTKCountdown.Tunnel.TunnelPolygonSequenceManager>();

            if (tunnelManager == null)
                return;

            tunnelManager.LevelChanged -= HandleTunnelLevelChanged;
            tunnelManager.LevelChanged += HandleTunnelLevelChanged;
        }

        private void UnbindFromTunnelManager()
        {
            if (tunnelManager != null)
                tunnelManager.LevelChanged -= HandleTunnelLevelChanged;
        }

        private void HandleTunnelLevelChanged(int tunnelLevelIndex)
        {
            ApplyLevel(tunnelLevelIndex);
        }

        private void ApplyCurrentTunnelLevel()
        {
            if (tunnelManager == null)
                BindToTunnelManager();

            if (tunnelManager == null)
                return;

            ApplyLevel(tunnelManager.CurrentLevelIndex);
        }

        private void ApplyLevel(int levelIndex)
        {
            if (targetSkinnedMeshRenderer == null || enemyMeshes == null || enemyMeshes.Length == 0)
                return;

            currentLevelIndex = Mathf.Clamp(levelIndex, 0, enemyMeshes.Length - 1);
            targetSkinnedMeshRenderer.sharedMesh = enemyMeshes[currentLevelIndex];
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (targetSkinnedMeshRenderer == null)
                targetSkinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }
#endif
    }
}
