using UnityEngine;

namespace EasyPeasyFirstPersonController
{
    public class AutoRunnerGameMode : MonoBehaviour, IInputManager
    {
        [Header("Game Mode Toggle")]
        [Tooltip("Toggle this Auto-Runner & Downhill-Slide mode on/off in the inspector.")]
        [SerializeField] private bool enableAutoRunnerMode = true;

        [Header("References")]
        [SerializeField] private FirstPersonController playerController;
        [SerializeField] private LayerMask groundMask = ~0;

        [Header("Downhill Slope Settings")]
        [Tooltip("Minimum slope angle (in degrees) to trigger automatic sliding.")]
        [SerializeField] private float minDownhillAngle = 10f;
        [Tooltip("Maximum slope angle for max slide speed calculation.")]
        [SerializeField] private float maxSlopeAngle = 45f;

        [Header("Slope Slide Speed Settings")]
        [Tooltip("Slide speed on shallowest valid downhill slope.")]
        [SerializeField] private float minSlopeSlideSpeed = 6f;
        [Tooltip("Slide speed on steepest downhill slope.")]
        [SerializeField] private float maxSlopeSlideSpeed = 18f;
        [Tooltip("Acceleration rate from 0 to target slope slide speed.")]
        [SerializeField] private float slopeAccelerationRate = 25f;

        private IInputManager baseInput;
        private float originalSlideSpeed;
        private float currentSlopeSpeed;
        private bool isDownhill;
        private float currentSlopeAngle;

        public bool EnableAutoRunnerMode
        {
            get => enableAutoRunnerMode;
            set
            {
                enableAutoRunnerMode = value;
                UpdateModeState();
            }
        }

        public Vector2 moveInput
        {
            get
            {
                if (!enableAutoRunnerMode || !enabled)
                    return baseInput != null ? baseInput.moveInput : Vector2.zero;

                // Always auto-sprint forward (y = 1), player retains left/right steering (x)
                float strafe = baseInput != null ? baseInput.moveInput.x : 0f;
                return new Vector2(strafe, 1f).normalized;
            }
        }

        public Vector2 lookInput => baseInput != null ? baseInput.lookInput : Vector2.zero;

        public bool jump => baseInput != null && baseInput.jump;

        public bool sprint => enableAutoRunnerMode && enabled ? true : (baseInput != null && baseInput.sprint);

        public bool crouch => (enableAutoRunnerMode && enabled) ? (isDownhill || (baseInput != null && baseInput.crouch)) : (baseInput != null && baseInput.crouch);

        public bool slide => (enableAutoRunnerMode && enabled) ? (isDownhill || (baseInput != null && baseInput.slide)) : (baseInput != null && baseInput.slide);

        private void Awake()
        {
            EnsureReferences();
        }

        private void Start()
        {
            EnsureReferences();
            UpdateModeState();
        }

        private void OnEnable()
        {
            EnsureReferences();
            UpdateModeState();
        }

        private void OnDisable()
        {
            RestoreOriginalState();
        }

        private void Update()
        {
            if (playerController == null)
                EnsureReferences();

            if (!enableAutoRunnerMode || !enabled || playerController == null)
                return;

            // Ensure input binding priority over FirstPersonController.Awake
            if (playerController.input != (IInputManager)this)
            {
                playerController.input = this;
            }

            // Check downhill slope condition
            isDownhill = CheckDownhillSlope(out currentSlopeAngle);

            // Handle dynamic slope slide speed acceleration
            if (isDownhill)
            {
                float t = Mathf.Clamp01((currentSlopeAngle - minDownhillAngle) / Mathf.Max(0.1f, maxSlopeAngle - minDownhillAngle));
                float targetSpeed = Mathf.Lerp(minSlopeSlideSpeed, maxSlopeSlideSpeed, t);

                // Accelerate current slope speed from 0 up to target speed
                currentSlopeSpeed = Mathf.MoveTowards(currentSlopeSpeed, targetSpeed, Time.deltaTime * slopeAccelerationRate);
                playerController.slideSpeed = currentSlopeSpeed;
            }
            else
            {
                currentSlopeSpeed = 0f;
                if (originalSlideSpeed > 0f)
                {
                    playerController.slideSpeed = originalSlideSpeed;
                }
            }
        }

        private void EnsureReferences()
        {
            if (playerController == null)
                playerController = GetComponent<FirstPersonController>();

            if (playerController == null)
                playerController = FindAnyObjectByType<FirstPersonController>();

            if (playerController != null)
            {
                var inputManager = playerController.GetComponent<InputManager>();
                if (inputManager != null && inputManager != (IInputManager)this)
                {
                    baseInput = inputManager;
                }

                if (originalSlideSpeed <= 0f && playerController.slideSpeed > 0f)
                {
                    originalSlideSpeed = playerController.slideSpeed;
                }
            }
        }

        private void UpdateModeState()
        {
            if (playerController == null)
                return;

            if (enableAutoRunnerMode && enabled)
            {
                if (playerController.input != (IInputManager)this)
                {
                    playerController.input = this;
                }
            }
            else
            {
                RestoreOriginalState();
            }
        }

        private void RestoreOriginalState()
        {
            if (playerController != null)
            {
                if (baseInput != null)
                {
                    playerController.input = baseInput;
                }

                if (originalSlideSpeed > 0f)
                {
                    playerController.slideSpeed = originalSlideSpeed;
                }
            }

            currentSlopeSpeed = 0f;
            isDownhill = false;
        }

        private bool CheckDownhillSlope(out float slopeAngle)
        {
            slopeAngle = 0f;
            if (playerController == null)
                return false;

            Vector3 rayOrigin = playerController.transform.position + Vector3.up * 0.5f;
            if (playerController.groundCheck != null)
                rayOrigin = playerController.groundCheck.position + Vector3.up * 0.2f;

            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, Vector3.down, 3.0f, groundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null || hit.collider.transform == playerController.transform || hit.collider.transform.IsChildOf(playerController.transform))
                    continue;

                slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                Vector3 forwardOnSlope = Vector3.ProjectOnPlane(playerController.transform.forward, hit.normal).normalized;

                // Downhill check: slopeAngle >= minDownhillAngle and forward vector points downhill (y < -0.01)
                if (slopeAngle >= minDownhillAngle && forwardOnSlope.y < -0.01f)
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            minDownhillAngle = Mathf.Clamp(minDownhillAngle, 1f, 89f);
            maxSlopeAngle = Mathf.Clamp(maxSlopeAngle, minDownhillAngle + 1f, 89f);
            minSlopeSlideSpeed = Mathf.Max(0.1f, minSlopeSlideSpeed);
            maxSlopeSlideSpeed = Mathf.Max(minSlopeSlideSpeed, maxSlopeSlideSpeed);
            slopeAccelerationRate = Mathf.Max(1f, slopeAccelerationRate);
        }
#endif
    }
}
