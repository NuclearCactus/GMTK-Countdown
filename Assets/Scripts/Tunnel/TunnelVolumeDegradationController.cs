using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GMTKCountdown.Tunnel
{
    public class TunnelVolumeDegradationController : MonoBehaviour
    {
        [Header("Script Toggle")]
        [Tooltip("Toggle this volume degradation feature on or off in the inspector.")]
        [SerializeField] private bool enableVolumeDegradation = true;

        [Header("References")]
        [SerializeField] private TunnelPolygonSequenceManager sequenceManager;
        [SerializeField] private Volume globalVolume;

        [Header("Degradation Settings")]
        [Tooltip("Total number of polygon levels in your sequence manager (default 9).")]
        [SerializeField] private int totalLevels = 9;

        [Header("Volume Property Ranges (Level 0 -> Max Level)")]
        [Tooltip("Vignette Intensity Range (Default: 0.1 -> 0.4)")]
        [SerializeField] private Vector2 vignetteIntensityRange = new Vector2(0.1f, 0.4f);

        [Tooltip("Chromatic Aberration Intensity Range (Default: 0.0 -> 1.0)")]
        [SerializeField] private Vector2 chromaticAberrationRange = new Vector2(0f, 1f);

        [Tooltip("Panini Projection Distance Range (Default: 0.0 -> 5.0)")]
        [SerializeField] private Vector2 paniniDistanceRange = new Vector2(0f, 5f);

        [Header("Transition Polish")]
        [SerializeField] private float transitionSpeed = 8f;

        private Vignette vignette;
        private ChromaticAberration chromaticAberration;
        private PaniniProjection paniniProjection;

        private float currentRatio;
        private float targetRatio;

        public bool EnableVolumeDegradation
        {
            get => enableVolumeDegradation;
            set
            {
                enableVolumeDegradation = value;
                if (!enableVolumeDegradation)
                {
                    ResetToDefaults();
                }
            }
        }

        private void Awake()
        {
            EnsureReferences();
            InitializeVolumeComponents();
        }

        private void OnEnable()
        {
            EnsureReferences();
            InitializeVolumeComponents();
            BindSequenceManager();

            if (enableVolumeDegradation)
            {
                ApplyCurrentLevelRatio();
            }
        }

        private void OnDisable()
        {
            UnbindSequenceManager();
            ResetToDefaults();
        }

        private void Update()
        {
            if (sequenceManager == null)
                BindSequenceManager();

            if (!enableVolumeDegradation || !enabled)
                return;

            currentRatio = Mathf.MoveTowards(currentRatio, targetRatio, Time.deltaTime * transitionSpeed);
            UpdateVolumeProperties(currentRatio);
        }

        private void EnsureReferences()
        {
            if (sequenceManager == null)
                sequenceManager = FindAnyObjectByType<TunnelPolygonSequenceManager>();

            if (globalVolume == null)
                globalVolume = FindAnyObjectByType<Volume>();
        }

        private void InitializeVolumeComponents()
        {
            if (globalVolume == null || globalVolume.profile == null)
                return;

            VolumeProfile profile = globalVolume.profile;

            if (!profile.TryGet(out vignette))
                vignette = profile.Add<Vignette>(true);

            if (!profile.TryGet(out chromaticAberration))
                chromaticAberration = profile.Add<ChromaticAberration>(true);

            if (!profile.TryGet(out paniniProjection))
                paniniProjection = profile.Add<PaniniProjection>(true);
        }

        private void BindSequenceManager()
        {
            if (sequenceManager == null)
                return;

            sequenceManager.LevelChanged -= HandleLevelChanged;
            sequenceManager.LevelChanged += HandleLevelChanged;
        }

        private void UnbindSequenceManager()
        {
            if (sequenceManager != null)
                sequenceManager.LevelChanged -= HandleLevelChanged;
        }

        private void HandleLevelChanged(int levelIndex)
        {
            if (!enableVolumeDegradation)
                return;

            int maxLevels = Mathf.Max(2, totalLevels);
            targetRatio = Mathf.Clamp01((float)levelIndex / (maxLevels - 1));
        }

        private void ApplyCurrentLevelRatio()
        {
            int currentLevel = (sequenceManager != null) ? sequenceManager.CurrentLevelIndex : 0;
            HandleLevelChanged(currentLevel);
        }

        private void UpdateVolumeProperties(float ratio)
        {
            if (globalVolume == null)
                return;

            if (vignette != null)
            {
                vignette.intensity.overrideState = true;
                vignette.intensity.value = Mathf.Lerp(vignetteIntensityRange.x, vignetteIntensityRange.y, ratio);
            }

            if (chromaticAberration != null)
            {
                chromaticAberration.intensity.overrideState = true;
                chromaticAberration.intensity.value = Mathf.Lerp(chromaticAberrationRange.x, chromaticAberrationRange.y, ratio);
            }

            if (paniniProjection != null)
            {
                paniniProjection.distance.overrideState = true;
                paniniProjection.distance.value = Mathf.Lerp(paniniDistanceRange.x, paniniDistanceRange.y, ratio);
            }
        }

        public void ResetToDefaults()
        {
            targetRatio = 0f;
            currentRatio = 0f;
            UpdateVolumeProperties(0f);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            totalLevels = Mathf.Max(2, totalLevels);
        }
#endif
    }
}
