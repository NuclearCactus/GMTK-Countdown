using UnityEngine;

namespace GMTKCountdown.Tunnel
{
    [ExecuteAlways]
    [RequireComponent(typeof(Camera))]
    public class TunnelRetroDegradationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TunnelPolygonSequenceManager sequenceManager;
        [SerializeField] private Material degradationMaterial;
        [SerializeField] private Shader degradationShader;

        [Header("Degradation Ranges (Min Level -> Max Level)")]
        [SerializeField] private Vector2 pixelSizeRange = new Vector2(1f, 10f);
        [SerializeField] private Vector2 scanlineIntensityRange = new Vector2(0f, 0.45f);
        [SerializeField] private Vector2 chromaticAberrationRange = new Vector2(0f, 0.025f);
        [SerializeField] private Vector2 crtCurvatureRange = new Vector2(0f, 0.06f);
        [SerializeField] private Vector2 vignetteIntensityRange = new Vector2(0f, 0.6f);
        [SerializeField] private Vector2 colorDepthRange = new Vector2(32f, 8f);

        [Header("Transition Polish")]
        [SerializeField] private float transitionSpeed = 8f;

        private float currentRatio;
        private float targetRatio;
        private Camera cam;

        private static readonly int PixelSizeId = Shader.PropertyToID("_PixelSize");
        private static readonly int ScanlineIntensityId = Shader.PropertyToID("_ScanlineIntensity");
        private static readonly int ChromaticAberrationId = Shader.PropertyToID("_ChromaticAberration");
        private static readonly int CRTCurvatureId = Shader.PropertyToID("_CRTCurvature");
        private static readonly int VignetteIntensityId = Shader.PropertyToID("_VignetteIntensity");
        private static readonly int ColorDepthId = Shader.PropertyToID("_ColorDepth");

        private void Awake()
        {
            cam = GetComponent<Camera>();
            EnsureMaterial();
            BindSequenceManager();
        }

        private void OnEnable()
        {
            EnsureMaterial();
            BindSequenceManager();
        }

        private void OnDisable()
        {
            UnbindSequenceManager();
        }

        private void Update()
        {
            if (sequenceManager == null)
                BindSequenceManager();

            // Smoothly lerp towards target ratio for fluid level swaps and instant orb resets
            currentRatio = Mathf.MoveTowards(currentRatio, targetRatio, Time.deltaTime * transitionSpeed);
            UpdateMaterialProperties(currentRatio);
        }



        public Material GetMaterial() => degradationMaterial;

        public void SetDegradationRatio(float ratio)
        {
            targetRatio = Mathf.Clamp01(ratio);
        }

        private void BindSequenceManager()
        {
            if (sequenceManager == null)
                sequenceManager = FindAnyObjectByType<TunnelPolygonSequenceManager>();

            if (sequenceManager == null)
                return;

            sequenceManager.LevelChanged -= HandleLevelChanged;
            sequenceManager.LevelChanged += HandleLevelChanged;

            // Initialize ratio with current level state
            HandleLevelChanged(sequenceManager.CurrentLevelIndex);
        }

        private void UnbindSequenceManager()
        {
            if (sequenceManager != null)
                sequenceManager.LevelChanged -= HandleLevelChanged;
        }

        private void HandleLevelChanged(int levelIndex)
        {
            int maxLevels = GetTotalLevelCount();
            if (maxLevels <= 1)
            {
                targetRatio = 0f;
                return;
            }

            targetRatio = Mathf.Clamp01((float)levelIndex / (maxLevels - 1));
        }

        private int GetTotalLevelCount()
        {
            if (sequenceManager == null) return 1;

            var field = typeof(TunnelPolygonSequenceManager).GetField("tunnelLevels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                var list = field.GetValue(sequenceManager) as System.Collections.IList;
                if (list != null && list.Count > 0)
                    return list.Count;
            }

            return 5; // Default fallback count if list not initialized
        }

        private void EnsureMaterial()
        {
            if (degradationMaterial == null)
            {
                if (degradationShader == null)
                    degradationShader = Shader.Find("Custom/PSX_CRT_Degradation");

                if (degradationShader != null)
                    degradationMaterial = new Material(degradationShader);
            }
        }

        private void UpdateMaterialProperties(float ratio)
        {
            if (degradationMaterial == null)
                return;

            float pixelSize = Mathf.Lerp(pixelSizeRange.x, pixelSizeRange.y, ratio);
            float scanline = Mathf.Lerp(scanlineIntensityRange.x, scanlineIntensityRange.y, ratio);
            float chromatic = Mathf.Lerp(chromaticAberrationRange.x, chromaticAberrationRange.y, ratio);
            float curvature = Mathf.Lerp(crtCurvatureRange.x, crtCurvatureRange.y, ratio);
            float vignette = Mathf.Lerp(vignetteIntensityRange.x, vignetteIntensityRange.y, ratio);
            float colorDepth = Mathf.Lerp(colorDepthRange.x, colorDepthRange.y, ratio);

            degradationMaterial.SetFloat(PixelSizeId, pixelSize);
            degradationMaterial.SetFloat(ScanlineIntensityId, scanline);
            degradationMaterial.SetFloat(ChromaticAberrationId, chromatic);
            degradationMaterial.SetFloat(CRTCurvatureId, curvature);
            degradationMaterial.SetFloat(VignetteIntensityId, vignette);
            degradationMaterial.SetFloat(ColorDepthId, colorDepth);
        }
    }
}
