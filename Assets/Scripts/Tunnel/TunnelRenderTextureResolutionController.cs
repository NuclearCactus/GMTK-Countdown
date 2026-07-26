using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GMTKCountdown.Tunnel
{
    public class TunnelRenderTextureResolutionController : MonoBehaviour
    {
        [Header("Script Toggle")]
        [Tooltip("Toggle this feature on or off in the inspector.")]
        [SerializeField] private bool enableResolutionDegradation = true;

        [Header("References")]
        [SerializeField] private TunnelPolygonSequenceManager sequenceManager;
        [SerializeField] private Camera playerCamera;
        [SerializeField] private RawImage displayRawImage;

        [Header("Render Texture Sequence (High -> Low)")]
        [Tooltip("List of RenderTextures ordered from highest resolution (e.g. 1920x1080) down to lowest resolution (e.g. 256x224).")]
        [SerializeField] private List<RenderTexture> renderTextures = new List<RenderTexture>();

        [Header("UI Scaling Reference")]
        [SerializeField] private float referenceScreenHeight = 1080f;
        [SerializeField] private float referenceScreenWidth = 1920f;

        private RenderTexture defaultRenderTexture;
        private Vector2 defaultRawImageSize;

        public bool EnableResolutionDegradation
        {
            get => enableResolutionDegradation;
            set
            {
                enableResolutionDegradation = value;
                if (enableResolutionDegradation)
                {
                    ApplyCurrentLevelResolution();
                }
                else
                {
                    ResetToDefaultState();
                }
            }
        }

        private void Awake()
        {
            EnsureReferences();
            CacheDefaults();
        }

        private void OnEnable()
        {
            EnsureReferences();
            CacheDefaults();
            BindSequenceManager();

            if (enableResolutionDegradation)
            {
                ApplyCurrentLevelResolution();
            }
        }

        private void OnDisable()
        {
            UnbindSequenceManager();
            ResetToDefaultState();
        }

        private void EnsureReferences()
        {
            if (sequenceManager == null)
                sequenceManager = FindAnyObjectByType<TunnelPolygonSequenceManager>();

            if (playerCamera == null)
            {
                var fps = FindAnyObjectByType<EasyPeasyFirstPersonController.FirstPersonController>();
                if (fps != null && fps.playerCamera != null)
                {
                    playerCamera = fps.playerCamera.GetComponent<Camera>();
                }
                else
                {
                    playerCamera = Camera.main;
                }
            }

            if (displayRawImage == null)
                displayRawImage = FindAnyObjectByType<RawImage>();
        }

        private void CacheDefaults()
        {
            if (displayRawImage != null && defaultRawImageSize == Vector2.zero)
            {
                defaultRawImageSize = displayRawImage.rectTransform.sizeDelta;
                if (defaultRawImageSize.x <= 0 || defaultRawImageSize.y <= 0)
                {
                    defaultRawImageSize = new Vector2(referenceScreenWidth, referenceScreenHeight);
                }
            }

            if (defaultRenderTexture == null)
            {
                if (displayRawImage != null && displayRawImage.texture is RenderTexture rawRT)
                {
                    defaultRenderTexture = rawRT;
                }
                else if (playerCamera != null && playerCamera.targetTexture != null)
                {
                    defaultRenderTexture = playerCamera.targetTexture;
                }
                else if (renderTextures.Count > 0)
                {
                    defaultRenderTexture = renderTextures[0];
                }
            }
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
            if (!enableResolutionDegradation || !enabled)
                return;

            ApplyResolutionForLevel(levelIndex);
        }

        private void ApplyCurrentLevelResolution()
        {
            int currentLevel = (sequenceManager != null) ? sequenceManager.CurrentLevelIndex : 0;
            ApplyResolutionForLevel(currentLevel);
        }

        private void ApplyResolutionForLevel(int levelIndex)
        {
            if (renderTextures.Count == 0)
                return;

            int clampedIndex = Mathf.Clamp(levelIndex, 0, renderTextures.Count - 1);
            RenderTexture targetRT = renderTextures[clampedIndex];

            if (targetRT == null)
                return;

            // 1. Assign target RenderTexture to Player Camera output
            if (playerCamera != null)
            {
                playerCamera.targetTexture = targetRT;
            }

            // 2. Assign target RenderTexture to RawImage
            if (displayRawImage != null)
            {
                displayRawImage.texture = targetRT;

                // 3. Calculate and update width based on RenderTexture aspect ratio
                float aspectRatio = (float)targetRT.width / (float)targetRT.height;
                float calculatedWidth = referenceScreenHeight * aspectRatio;

                RectTransform rectTransform = displayRawImage.rectTransform;
                rectTransform.sizeDelta = new Vector2(calculatedWidth, referenceScreenHeight);
            }
        }

        public void ResetToDefaultState()
        {
            RenderTexture resetRT = (renderTextures.Count > 0 && renderTextures[0] != null) ? renderTextures[0] : defaultRenderTexture;

            if (playerCamera != null)
            {
                playerCamera.targetTexture = resetRT;
            }

            if (displayRawImage != null)
            {
                if (resetRT != null)
                    displayRawImage.texture = resetRT;

                if (defaultRawImageSize != Vector2.zero)
                {
                    displayRawImage.rectTransform.sizeDelta = defaultRawImageSize;
                }
                else
                {
                    displayRawImage.rectTransform.sizeDelta = new Vector2(referenceScreenWidth, referenceScreenHeight);
                }
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            referenceScreenHeight = Mathf.Max(100f, referenceScreenHeight);
            referenceScreenWidth = Mathf.Max(100f, referenceScreenWidth);
        }
#endif
    }
}
