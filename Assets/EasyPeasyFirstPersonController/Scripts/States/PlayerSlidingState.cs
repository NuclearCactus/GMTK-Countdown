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

        public PlayerSlidingState(FirstPersonController currentContext, PlayerStateFactory playerStateFactory)
            : base(currentContext, playerStateFactory) { }

        public override void EnterState()
        {
            slideTimer = ctx.slideDuration;
            currentTackleBonusSpeed = 0f;
            tackleComboCount = 0;
            ctx.PlaySlideSound();

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

            tackleComboCount++;
            currentTackleBonusSpeed = Mathf.Min(currentTackleBonusSpeed + ctx.tackleSpeedBoost, ctx.maxTackleBonusSpeed);
            slideTimer = Mathf.Max(slideTimer + ctx.tackleDurationBonus, ctx.slideDuration);

            // Re-align slide momentum in the look direction so speed carries forward seamlessly
            slideDirection = ctx.transform.forward;

            ctx.PlayTackleSound();
            ctx.TriggerCameraShake(ctx.tackleCameraShakeIntensity, ctx.tackleCameraShakeDuration, ctx.transform.forward);

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

        public override void ExitState() { }

        public override void CheckSwitchStates()
        {
            if (ctx.input.jump && ctx.isGrounded && !ctx.HasCeiling())
            {
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

        private void HandleSlideMovement(float progress)
        {
            float speedCurve = Mathf.Pow(progress, 0.5f);
            float speed = (ctx.slideSpeed + currentTackleBonusSpeed) * Mathf.Lerp(0.5f, 1f, speedCurve) * ctx.GetEnemySpeedMultiplier();

            // Allow the player to steer left/right while sliding
            float strafeInput = ctx.input.moveInput.x;
            Vector3 steerVector = ctx.transform.right * strafeInput * ctx.slideSteerControl;
            
            // Combine forward sliding momentum with sideways steering
            Vector3 finalMove = (slideDirection * speed) + steerVector;
            
            // Update currentVelocity so momentum carries over if they jump
            ctx.currentVelocity = finalMove;

            ctx.characterController.Move(finalMove * Time.deltaTime);

            // Crash Detection (Did we hit a wall while sliding?)
            Vector3 actualVelocity = ctx.characterController.velocity;
            actualVelocity.y = 0; // Only care about horizontal crashes
            
            float intendedSpeed = finalMove.magnitude;
            float actualSpeed = actualVelocity.magnitude;

            // If we were sliding fast but our actual speed is near zero, we smashed into a wall!
            if (intendedSpeed > 4f && actualSpeed < intendedSpeed * 0.2f)
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

            if (ctx.isGrounded) ctx.moveDirection.y = -20f;
            else ctx.moveDirection.y = 0;
            ctx.characterController.Move(new Vector3(0, ctx.moveDirection.y, 0) * Time.deltaTime);
        }
    }
}