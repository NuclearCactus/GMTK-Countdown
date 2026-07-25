namespace EasyPeasyFirstPersonController
{
    using System.Collections.Generic;
    using UnityEngine;

    public partial class FirstPersonController : MonoBehaviour
    {
        [Header("Settings")]
        public float walkSpeed = 3f;
        public float sprintSpeed = 5f;
        public float crouchSpeed = 1.5f;
        public float jumpSpeed = 4f;
        public float gravity = 9.81f;
        public float slideDuration = 0.7f;
        public float slideSpeed = 6f;
        public float mouseSensitivity = 2f;
        public float strafeTiltAmount = 2f;

        [Header("Movement Polish")]
        public float groundAcceleration = 50f;
        public float groundDeceleration = 60f;
        [HideInInspector] public Vector3 currentVelocity;

        [Header("Advanced Mechanics")]
        public bool enableSmoothCrouch = true;
        public float crouchTransitionSpeed = 10f;
        public bool enableSlopeSliding = true;
        public float slideUphillFriction = 3f;
        public float slideSteerControl = 4f;

        [Header("References")]
        public Transform playerCamera;
        public Transform cameraParent;
        public Transform groundCheck;
        public LayerMask groundMask;

        [Header("Audio")]
        public AudioSource footstepSource;
        public AudioClip[] footstepClips;
        public AudioClip[] jumpClips;
        public AudioClip[] slideClips;
        public float footstepStepDistance = 1.6f;
        public float footstepMinSpeed = 0.15f;
        public float footstepVolume = 1f;
        public float jumpVolume = 1f;
        public float slideVolume = 1f;

        [HideInInspector] public CharacterController characterController;
        [HideInInspector] public IInputManager input;
        [HideInInspector] public Vector3 moveDirection;
        [HideInInspector] public bool isGrounded;

        private PlayerBaseState currentState;
        private PlayerStateFactory states;
        private float xRotation = 0f;
        private float currentTilt;
        private float tiltVelocity;
        private float lastBobFootstepPhase;
        private bool wasFootstepMoving;
        private int lastFootstepIndex = -1;

        public PlayerBaseState CurrentState { get => currentState; set => currentState = value; }

        [Header("Visual Settings")]
        public float normalFov = 60f;
        public float sprintFov = 75f;
        public float slideFovBoost = 5f;
        public float fovChangeSpeed = 8f;
        public float bobAmount = 0.03f;
        public float bobSpeed = 12f;
        public float recoilReturnSpeed = 5f;

        [HideInInspector] public Camera cam;
        [HideInInspector] public float targetFov;
        [HideInInspector] public float currentBobIntensity;
        [HideInInspector] public float currentBobSpeed;
        [HideInInspector] public float targetTilt;

        private float bobTimer;
        private float fovVelocity;
        private float originalCamY;

        [HideInInspector] public float cameraShakeTimer;
        [HideInInspector] public float cameraShakeIntensity;

        [Header("Height Settings")]
        public float standingCameraHeight = 1.75f;
        public float crouchingCameraHeight = 1f;
        public float crouchingCharacterControllerHeight = 1f;
        [HideInInspector] public float standingCharacterControllerHeight = 1.8f;
        [HideInInspector] public Vector3 standingCharacterControllerCenter = new Vector3(0, 0.9f, 0);
        [HideInInspector] public float targetCameraY;

        [Header("Ledge Settings")]
        public LayerMask ledgeLayer;
        public float ledgeDetectionDistance = 1f;
        public float climbDuration = 0.6f;
        public float climbHeightArc = 0.4f;
        public float climbTiltAmount = -7f;

        [Header("Swimming Settings")]
        public float swimSpeed = 4f;
        public float swimSprintSpeed = 6f;
        public float waterDrag = 2f;
        public LayerMask waterMask;
        [HideInInspector] public bool isInWater;
        [HideInInspector] public float currentLedgeCooldown;

        [Header("Visual Preferences")]
        public bool useFovKick = true;
        public bool useHeadBob = true;
        public bool useCameraTilt = true;
        public bool useClimbTilt = true;

        [Header("Enemy Pressure")]
        public float enemySlowMinMultiplier = 0.45f;
        public float enemyPressureRiseSpeed = 10f;
        public float enemyPressureFallSpeed = 3.5f;
        public float enemyCameraSinkAmount = 0.28f;
        public float enemyCameraSinkRiseSpeed = 12f;
        public float enemyCameraSinkFallSpeed = 5f;

        [Header("Debug")]
        public bool currentStateDebug = true;

        private readonly Dictionary<EntityId, float> enemyThreats = new Dictionary<EntityId, float>();
        private float enemyPressureTarget;
        private float enemyPressureCurrent;
        private float enemyCameraSinkCurrent;

        void OnGUI()
        {
            if (currentState != null && Application.isEditor && currentStateDebug)
                GUILayout.Label("Current State: " + currentState.GetType().Name);
        }

        private void Awake()
        {
            cam = playerCamera.GetComponent<Camera>();
            targetFov = normalFov;
            targetCameraY = standingCameraHeight;
            originalCamY = standingCameraHeight;

            if (footstepSource == null)
                footstepSource = GetComponent<AudioSource>();

            if (footstepSource == null)
                footstepSource = gameObject.AddComponent<AudioSource>();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            characterController = GetComponent<CharacterController>();
            standingCharacterControllerHeight = characterController.height;
            standingCharacterControllerCenter = characterController.center;
            input = GetComponent<IInputManager>();
            states = new PlayerStateFactory(this);

            currentState = states.Grounded();
            currentState.EnterState();
            lastBobFootstepPhase = 0f;
            wasFootstepMoving = false;
        }

        private void Update()
        {
            if (currentLedgeCooldown > 0)
                currentLedgeCooldown -= Time.deltaTime;

            isGrounded = characterController.isGrounded || Physics.CheckSphere(groundCheck.position, characterController.radius * 0.9f, groundMask, QueryTriggerInteraction.Ignore);

            UpdateEnemyPressure();

            currentState.UpdateState();
            HandleRotation();
            UpdateVisuals();
            HandleFootsteps();
        }

        private void HandleRotation()
        {
            float mouseX = input.lookInput.x * mouseSensitivity;
            float mouseY = input.lookInput.y * mouseSensitivity;

            transform.Rotate(Vector3.up * mouseX);

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            float strafeTilt = useCameraTilt ? (-input.moveInput.x * strafeTiltAmount) : 0;
            float combinedTargetTilt = (useCameraTilt ? targetTilt : 0) + strafeTilt;

            currentTilt = Mathf.SmoothDamp(currentTilt, combinedTargetTilt, ref tiltVelocity, 0.1f);
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0, currentTilt);
        }

        public void UpdateVisuals()
        {
            if (!useFovKick)
            {
                targetFov = normalFov;
            }
            cam.fieldOfView = Mathf.SmoothDamp(cam.fieldOfView, targetFov, ref fovVelocity, 1f / fovChangeSpeed);

            // Smoothly track the base camera height independent of headbob
            originalCamY = Mathf.Lerp(originalCamY, targetCameraY, Time.deltaTime * 8f);

            float targetBobOffset = 0f;
            if (useHeadBob && characterController.velocity.magnitude > 0.1f && isGrounded)
            {
                bobTimer += Time.deltaTime * currentBobSpeed;
                targetBobOffset = Mathf.Sin(bobTimer) * currentBobIntensity;
            }
            else
            {
                // Smoothly reset timer to prevent snapping when starting to walk again
                bobTimer = Mathf.Lerp(bobTimer, 0, Time.deltaTime * 10f);
            }

            // Smoothly transition the actual camera Y to include the bob offset
            float desiredY = originalCamY + targetBobOffset + enemyCameraSinkCurrent;
            
            // Apply Camera Shake (Realistic Directional Impact)
            if (cameraShakeTimer > 0)
            {
                cameraShakeTimer -= Time.deltaTime;
                
                float normalizedTime = cameraShakeTimer / 0.4f; 
                float shakeFactor = normalizedTime * normalizedTime * normalizedTime; 
                
                // 1. Sharp dip downwards based on frontal impact
                float frontalImpact = Mathf.Abs(cameraShakeDirection.z) + 0.5f;
                float dipY = -cameraShakeIntensity * shakeFactor * frontalImpact;
                
                // 2. Sharp rotational roll towards the impact side
                float sideImpact = cameraShakeDirection.x;
                float dipTilt = (cameraShakeIntensity * 15f) * sideImpact * shakeFactor;
                
                // If it's purely a frontal crash with no side impact, add a slight random tilt
                if (Mathf.Abs(sideImpact) < 0.1f) 
                    dipTilt = (cameraShakeIntensity * 5f) * shakeFactor * (Mathf.PerlinNoise(Time.time, 0) > 0.5f ? 1 : -1);
                
                // 3. Organic rattle (much lighter now)
                float rattle = (Mathf.PerlinNoise(Time.time * 30f, 0f) - 0.5f) * (cameraShakeIntensity * 0.2f) * shakeFactor;

                desiredY += dipY + rattle;
                currentTilt += dipTilt + (rattle * 5f); 
            }

            float smoothedY = Mathf.Lerp(cameraParent.localPosition.y, desiredY, Time.deltaTime * 15f);

            cameraParent.localPosition = new Vector3(cameraParent.localPosition.x, smoothedY, cameraParent.localPosition.z);
        }

        private void HandleFootsteps()
        {
            if (footstepSource == null || footstepClips == null || footstepClips.Length == 0)
            {
                wasFootstepMoving = false;
                lastBobFootstepPhase = 0f;
                return;
            }

            Vector3 horizontalVelocity = characterController.velocity;
            horizontalVelocity.y = 0f;

            // Require actual player input in addition to the velocity check.
            // characterController.velocity reflects ANY displacement resolved
            // during the last Move() call, including depenetration from an
            // overlapping collider (e.g. an enemy standing in the player) - not
            // just deliberate walking. Without this gate, a stationary player
            // being brushed/overlapped by an enemy could get a velocity blip
            // that repeatedly crosses footstepMinSpeed and machine-guns the
            // footstep sound. Gating on input makes footsteps only ever fire
            // from the player's own movement, regardless of what else nudges
            // the CharacterController.
            bool intentionalMovement = input.moveInput.sqrMagnitude > 0.01f;

            bool shouldPlayFootsteps = isGrounded && !isInWater && intentionalMovement && horizontalVelocity.magnitude >= footstepMinSpeed;
            if (!shouldPlayFootsteps)
            {
                wasFootstepMoving = false;
                lastBobFootstepPhase = 0f;
                return;
            }

            if (!wasFootstepMoving)
            {
                PlayFootstep();
                wasFootstepMoving = true;
                lastBobFootstepPhase = bobTimer;
                return;
            }

            const float stepPhase = Mathf.PI * 2f;
            float currentPhase = bobTimer;

            if (currentPhase < lastBobFootstepPhase)
                lastBobFootstepPhase = currentPhase;

            while (currentPhase - lastBobFootstepPhase >= stepPhase)
            {
                lastBobFootstepPhase += stepPhase;
                PlayFootstep();
            }
        }

        public void PlayJumpSound()
        {
            PlayRandomAudioClip(jumpClips, jumpVolume);
        }

        public void PlaySlideSound()
        {
            PlayRandomAudioClip(slideClips, slideVolume);
        }

        public float GetEnemySpeedMultiplier()
        {
            return Mathf.Lerp(1f, enemySlowMinMultiplier, enemyPressureCurrent);
        }

        public void ReportEnemyThreat(EntityId sourceId, float threat)
        {
            threat = Mathf.Clamp01(threat);

            if (threat <= 0f)
                enemyThreats.Remove(sourceId);
            else
                enemyThreats[sourceId] = threat;
        }

        public void ClearEnemyThreat(EntityId sourceId)
        {
            enemyThreats.Remove(sourceId);
        }

        private void UpdateEnemyPressure()
        {
            float target = 0f;
            foreach (float threat in enemyThreats.Values)
            {
                if (threat > target)
                    target = threat;
            }

            enemyPressureTarget = target;

            float pressureSpeed = enemyPressureTarget > enemyPressureCurrent ? enemyPressureRiseSpeed : enemyPressureFallSpeed;
            enemyPressureCurrent = Mathf.MoveTowards(enemyPressureCurrent, enemyPressureTarget, Time.deltaTime * pressureSpeed);

            float sinkTarget = -enemyCameraSinkAmount * enemyPressureCurrent;
            float sinkSpeed = sinkTarget < enemyCameraSinkCurrent ? enemyCameraSinkRiseSpeed : enemyCameraSinkFallSpeed;
            enemyCameraSinkCurrent = Mathf.MoveTowards(enemyCameraSinkCurrent, sinkTarget, Time.deltaTime * sinkSpeed);
        }

        private void PlayFootstep()
        {
            if (footstepClips == null || footstepClips.Length == 0 || footstepSource == null)
                return;

            int clipIndex = Random.Range(0, footstepClips.Length);
            if (footstepClips.Length > 1 && clipIndex == lastFootstepIndex)
                clipIndex = (clipIndex + 1) % footstepClips.Length;

            AudioClip clip = footstepClips[clipIndex];
            if (clip == null)
                return;

            lastFootstepIndex = clipIndex;
            footstepSource.PlayOneShot(clip, footstepVolume);
        }

        private void PlayRandomAudioClip(AudioClip[] clips, float volume)
        {
            if (footstepSource == null || clips == null || clips.Length == 0)
                return;

            int clipIndex = Random.Range(0, clips.Length);
            AudioClip clip = clips[clipIndex];
            if (clip == null)
                return;

            footstepSource.PlayOneShot(clip, volume);
        }

        [HideInInspector] public Vector3 cameraShakeDirection;
        public void TriggerCameraShake(float intensity, float duration, Vector3 direction = default)
        {
            cameraShakeIntensity = intensity;
            cameraShakeTimer = duration;
            cameraShakeDirection = direction.normalized;
        }

        public bool HasCeiling()
        {
            float radius = characterController.radius * 0.9f;
            Vector3 origin = transform.position + Vector3.up * (characterController.height - radius);
            float checkDistance = standingCharacterControllerHeight - characterController.height + 0.1f;

            return Physics.SphereCast(origin, radius, Vector3.up, out _, checkDistance, groundMask, QueryTriggerInteraction.Ignore);
        }
        public bool CheckLedge(out Vector3 climbPosition)
        {
            climbPosition = Vector3.zero;
            if (currentLedgeCooldown > 0) return false;

            RaycastHit wallHit;
            Vector3 wallOrigin = transform.position + Vector3.up * 1.5f;

            if (Physics.Raycast(wallOrigin, transform.forward, out wallHit, ledgeDetectionDistance, ledgeLayer, QueryTriggerInteraction.Ignore))
            {
                Vector3 ledgeOrigin = wallOrigin + Vector3.up * 0.6f + transform.forward * 0.2f;
                RaycastHit ledgeHit;

                if (!Physics.Raycast(ledgeOrigin, transform.forward, 0.5f, groundMask))
                {
                    if (Physics.Raycast(ledgeOrigin + transform.forward * 0.4f, Vector3.down, out ledgeHit, 1f, groundMask))
                    {
                        climbPosition = ledgeHit.point + Vector3.up * 1f;
                        return true;
                    }
                }
            }
            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (((1 << other.gameObject.layer) & waterMask) != 0)
            {
                isInWater = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (((1 << other.gameObject.layer) & waterMask) != 0)
            {
                isInWater = false;
            }
        }

    }
}