namespace EasyPeasyFirstPersonController
{
    using GMTKCountdown.Enemies;
    using UnityEngine;

    public class PlayerSlidingState : PlayerBaseState
    {
        private float slideTimer;
        private Vector3 slideDirection;
        private float currentTackleBonusSpeed;
        private int tackleComboCount;
        private bool tackledThisFrame;

        public float CurrentSpeed { get; private set; }

        public PlayerSlidingState(FirstPersonController currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory) { }

        public override void EnterState()
        {
            slideTimer = ctx.slideDuration;
            currentTackleBonusSpeed = 0f;
            tackleComboCount = 0;
            tackledThisFrame = false;
            CurrentSpeed = ctx.slideSpeed;
            ctx.StartSlideAudio();

            if (!ctx.enableSmoothCrouch)
            {
                float crouchHeight = ctx.crouchingCharacterControllerHeight;
                ctx.characterController.height = crouchHeight;
                ctx.characterController.center = new Vector3(0, crouchHeight / 2f, 0);
            }

            slideDirection = ctx.transform.forward;
        }

        public void RegisterTackle(Vector3 contactPoint, TunnelEnemyAI enemy)
        {
            if (enemy == null || enemy.IsDefeated)
                return;

            tackledThisFrame = true;
            tackleComboCount++;
            currentTackleBonusSpeed = Mathf.Min(currentTackleBonusSpeed + ctx.tackleSpeedBoost, ctx.maxTackleBonusSpeed);
            slideTimer = Mathf.Max(slideTimer + ctx.tackleDurationBonus, ctx.slideDuration);

            // Re-align slide momentum in the look direction so speed carries forward seamlessly
            slideDirection = ctx.transform.forward;

            ctx.PlayTackleSound();
            ctx.TriggerCameraShake(ctx.tackleCameraShakeIntensity, ctx.tackleCameraShakeDuration, ctx.transform.forward);
            ctx.TriggerTackleHitPause();

            enemy.DefeatByTackle(contactPoint, ctx.tackleVfxPrefab);
        }

        public override void UpdateState()
        {
            if (ctx.enableSlopeSliding)
            {
                HandleSlopeFriction();
            }
            else
            {
                slideTimer -= Time.deltaTime;
            }

            if (ctx.enableSmoothCrouch)
            {
                ctx.characterController.height = Mathf.MoveTowards(
                    ctx.characterController.height,
                    ctx.crouchingCharacterControllerHeight,
                    Time.deltaTime * ctx.crouchTransitionSpeed
                );

                ctx.characterController.center = Vector3.MoveTowards(
                    ctx.characterController.center,
                    new Vector3(0, ctx.crouchingCharacterControllerHeight / 2f, 0),
                    Time.deltaTime * (ctx.crouchTransitionSpeed / 2f)
                );
            }

            float progress = Mathf.Clamp01(slideTimer / ctx.slideDuration);

            ctx.targetFov = ctx.sprintFov + (ctx.slideFovBoost * progress);
            ctx.currentBobIntensity = 0;
            ctx.targetTilt = -5f * progress;
            ctx.targetCameraY = ctx.crouchingCameraHeight;

            HandleSlideMovement(progress);
            CheckSwitchStates();
        }

        private void HandleSlopeFriction()
        {
            if (Physics.Raycast(ctx.transform.position, Vector3.down, out RaycastHit hit, 2f, ctx.groundMask))
            {
                float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
                Vector3 projectedSlideDir = Vector3.ProjectOnPlane(slideDirection, hit.normal).normalized;

                if (slopeAngle > 5f && projectedSlideDir.y < -0.05f)
                {
                    // Downhill: Regain timer (infinite slide)
                    slideTimer += Time.deltaTime;
                    slideTimer = Mathf.Min(slideTimer, ctx.slideDuration);
                }
                else if (slopeAngle > 5f && projectedSlideDir.y > 0.05f)
                {
                    // Uphill: Fast stop
                    slideTimer -= Time.deltaTime * ctx.slideUphillFriction;
                }
                else
                {
                    // Flat ground
                    slideTimer -= Time.deltaTime;
                }
                
                // Keep the sliding direction parallel to the slope
                slideDirection = projectedSlideDir;
            }
            else
            {
                slideTimer -= Time.deltaTime;
            }
        }

        public override void ExitState()
        {
            ctx.StopSlideAudio();
        }

        public override void CheckSwitchStates()
        {
            if (ctx.input.jump && ctx.isGrounded && !ctx.HasCeiling())
            {
                CheckAndApplySlideJumpBoost();
                SwitchState(factory.Jumping());
                return;
            }

            if (slideTimer <= 0 || !ctx.isGrounded)
            {
                if (ctx.HasCeiling() || ctx.input.crouch)
                {
                    SwitchState(factory.Crouching());
                }
                else
                {
                    SwitchState(factory.Grounded());
                }
            }
        }

        private void CheckAndApplySlideJumpBoost()
        {
            if (!ctx.enableSlideJumpBoost)
                return;

            // Check if player has tackled an enemy and gained boosted speed
            if (currentTackleBonusSpeed > 0f)
            {
                // Fixed boost factor (non-dynamic) to prevent flying off at extreme speeds
                float boostFactor = ctx.slideJumpBoostMultiplier;

                // Set horizontal velocity to a fixed boosted speed in the movement direction
                Vector3 moveDir = slideDirection.sqrMagnitude > 0.001f ? slideDirection : ctx.transform.forward;
                ctx.currentVelocity = moveDir.normalized * (ctx.slideSpeed * boostFactor);

                // Set flags for PlayerJumpingState and PlayerFallState to handle vertical boost and landing
                ctx.resumeSlideOnLand = true;
                ctx.activeJumpSpeedBoost = boostFactor;

                // Trigger dramatic slow-motion time dilation & camera slow down
                ctx.TriggerSlideJumpSlowMo(ctx.slideJumpSlowMoTimeScale, ctx.slideJumpSlowMoDuration);

                // Visual & Audio Feedback for landing the boosted slide jump
                ctx.TriggerCameraShake(0.2f, 0.25f, ctx.transform.forward);

                if (ctx.slideJumpBoostClip != null && ctx.footstepSource != null)
                {
                    ctx.footstepSource.PlayOneShot(ctx.slideJumpBoostClip, ctx.slideJumpBoostVolume);
                }
            }
        }

        private void HandleSlideMovement(float progress)
        {
            float speedCurve = Mathf.Pow(progress, 0.5f);
            float speed = (ctx.slideSpeed + currentTackleBonusSpeed) * Mathf.Lerp(0.5f, 1f, speedCurve) * ctx.GetEnemySpeedMultiplier();
            CurrentSpeed = speed;
            ctx.UpdateSlideAudio(speed, ctx.slideSpeed, ctx.slideSpeed + ctx.maxTackleBonusSpeed);

            // Smoothly rotate the slide direction vector towards the camera look direction based on steering scaling setting
            if (ctx.slideSteeringSpeedScaling > 0f)
            {
                float turnRate = Mathf.Lerp(0f, 10f, ctx.slideSteeringSpeedScaling);
                Vector3 targetForward = ctx.transform.forward;
                targetForward.y = slideDirection.y;
                if (targetForward.sqrMagnitude > 0.001f)
                {
                    slideDirection = Vector3.Slerp(slideDirection, targetForward.normalized, Time.deltaTime * turnRate).normalized;
                }
            }

            // Allow the player to steer left/right while sliding, scaling strafe force with speed to maintain full control
            float strafeInput = ctx.input.moveInput.x;
            float speedRatio = speed / Mathf.Max(0.1f, ctx.slideSpeed);
            float effectiveSteer = ctx.slideSteerControl * Mathf.Lerp(1f, speedRatio, ctx.slideSteeringSpeedScaling);
            Vector3 steerVector = ctx.transform.right * strafeInput * effectiveSteer;
            
            // Combine forward sliding momentum with sideways steering
            Vector3 finalMove = (slideDirection * speed) + steerVector;
            
            // Update currentVelocity so momentum carries over if they jump
            ctx.currentVelocity = finalMove;

            ctx.characterController.Move(finalMove * Time.deltaTime);

            // Crash Detection (Did we hit a static wall while sliding?)
            Vector3 actualVelocity = ctx.characterController.velocity;
            actualVelocity.y = 0; // Only care about horizontal crashes
            
            float intendedSpeed = finalMove.magnitude;
            float actualSpeed = actualVelocity.magnitude;

            // If we were sliding fast but hit a static wall, end slide forcefully (skip if we tackled an enemy this frame)
            if (!tackledThisFrame && intendedSpeed > 4f && actualSpeed < intendedSpeed * 0.2f)
            {
                // Calculate which side we hit
                Vector3 crashVector = finalMove - actualVelocity;
                Vector3 localCrashDirection = ctx.transform.InverseTransformDirection(crashVector);

                // Trigger a lighter, directional camera shake!
                ctx.TriggerCameraShake(0.15f, 0.4f, localCrashDirection);
                
                // End the slide forcefully since we crashed
                SwitchState(factory.Crouching());
                return;
            }

            tackledThisFrame = false;

            if (ctx.isGrounded) ctx.moveDirection.y = -20f;
            else ctx.moveDirection.y = 0;
            ctx.characterController.Move(new Vector3(0, ctx.moveDirection.y, 0) * Time.deltaTime);
        }
    }
}