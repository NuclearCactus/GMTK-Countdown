using System.Collections.Generic;
using EasyPeasyFirstPersonController;
using UnityEngine;

namespace GMTKCountdown.Enemies
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    public class TunnelEnemyAI : MonoBehaviour
    {
        [Header("Randomized Ranges")]
        [SerializeField] private Vector2 loiterRadiusRange = new Vector2(1.5f, 3.5f);
        [SerializeField] private Vector2 loiterSpeedRange = new Vector2(0.75f, 1.75f);
        [SerializeField] private Vector2 chaseSpeedRange = new Vector2(2.5f, 5f);
        [SerializeField] private Vector2 chaseThresholdRange = new Vector2(4f, 8f);
        [SerializeField] private Vector2 grabRadiusRange = new Vector2(0.75f, 1.75f);
        [SerializeField] private Vector2 repathTimeRange = new Vector2(0.6f, 1.6f);

        [Header("Behaviour")]
        [SerializeField] private bool stationaryWhenIdle = false;
        [SerializeField] private float chaseExitMultiplier = 1.2f;
        [SerializeField] private float turnSpeed = 8f;
        [SerializeField] private bool keepHeightFixed = true;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float groundProbeHeight = 4f;
        [SerializeField] private float groundProbeDistance = 12f;
        [SerializeField] private float groundStickSpeed = 12f;
        [SerializeField] private float groundFallbackFallSpeed = 12f;
        [SerializeField] private float groundSurfaceOffset = 0.05f;

        [Header("Contact / Multi-Enemy Steering")]
        // How far from the player's pivot this enemy stops when chasing. Should
        // roughly match player capsule radius + this enemy's own radius so it
        // parks right at the surface instead of overlapping. Tune per prefab.
        [SerializeField] private Vector2 contactDistanceRange = new Vector2(0.9f, 1.2f);
        // Other enemies within this radius push this one sideways so a pack
        // doesn't collapse onto the same line/point when chasing.
        [SerializeField] private float separationRadius = 2f;
        [SerializeField] private float separationWeight = 1.5f;

        // Simple self-registering list so enemies can see each other for
        // separation steering without needing tags/layers. Cleared on disable.
        private static readonly List<TunnelEnemyAI> activeEnemies = new List<TunnelEnemyAI>();

        private Rigidbody rb;
        private FirstPersonController player;
        private Vector3 homePosition;
        private Vector3 wanderTarget;
        private float loiterRadius;
        private float loiterSpeed;
        private float chaseSpeed;
        private float chaseThreshold;
        private float grabRadius;
        private float contactDistance;
        private float repathTimer;
        private bool chasing;
        private EntityId threatId;
        private bool isDefeated;

        public bool IsDefeated => isDefeated;

        private Animator enemyAnimator;

        public virtual void DefeatByTackle(Vector3 contactPoint, GameObject vfxPrefab)
        {
            if (isDefeated)
                return;

            isDefeated = true;

            if (vfxPrefab != null)
            {
                Instantiate(vfxPrefab, contactPoint, Quaternion.identity);
            }

            Destroy(gameObject);
        }

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            enemyAnimator = GetComponent<Animator>();

            // This AI moves itself explicitly, it shouldn't be pushed around by
            // physics forces/gravity. Setting these here means the behaviour is
            // correct even if the Inspector values on a prefab get forgotten.
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            // Continuous (or Continuous Speculative) avoids tunneling through the
            // player at high chase speeds - keep whatever you already had here.
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            homePosition = rb.position;
            threatId = GetEntityId();
            RandomizeProfile();
            SnapToGroundImmediate();
            homePosition = rb.position;
            ResolvePlayer();
            PickNewWanderTarget();
        }

        private void OnEnable()
        {
            ResolvePlayer();

            if (!activeEnemies.Contains(this))
                activeEnemies.Add(this);
        }

        private void OnDisable()
        {
            if (player != null)
                player.ClearEnemyThreat(threatId);

            activeEnemies.Remove(this);
        }

        // All movement now happens on the physics tick via Rigidbody, in lockstep
        // with Unity's collision resolution, instead of stomping transform.position
        // every render frame. This is what actually removes the jitter.
        protected virtual void FixedUpdate()
        {
            if (player == null)
                ResolvePlayer();

            if (player == null)
                return;

            float distanceToPlayer = Vector3.Distance(rb.position, player.transform.position);
            UpdateThreat(distanceToPlayer);
            UpdateMovement(distanceToPlayer);
        }

        private void RandomizeProfile()
        {
            loiterRadius = Mathf.Max(0.1f, Random.Range(loiterRadiusRange.x, loiterRadiusRange.y));
            loiterSpeed = Mathf.Max(0.01f, Random.Range(loiterSpeedRange.x, loiterSpeedRange.y));
            chaseSpeed = Mathf.Max(loiterSpeed, Random.Range(chaseSpeedRange.x, chaseSpeedRange.y));
            chaseThreshold = Mathf.Max(0.1f, Random.Range(chaseThresholdRange.x, chaseThresholdRange.y));
            grabRadius = Mathf.Clamp(Random.Range(grabRadiusRange.x, grabRadiusRange.y), 0.1f, chaseThreshold * 0.9f);
            contactDistance = Mathf.Clamp(Random.Range(contactDistanceRange.x, contactDistanceRange.y), 0.1f, grabRadius);
            repathTimer = Random.Range(repathTimeRange.x, repathTimeRange.y);
        }

        private void ResolvePlayer()
        {
            player = FindAnyObjectByType<FirstPersonController>();
        }

        private void UpdateThreat(float distanceToPlayer)
        {
            float threat = 0f;
            if (distanceToPlayer <= grabRadius)
                threat = Mathf.InverseLerp(grabRadius, 0f, distanceToPlayer);

            player.ReportEnemyThreat(threatId, threat);
        }

        private void UpdateMovement(float distanceToPlayer)
        {
            float chaseReleaseDistance = chaseThreshold * chaseExitMultiplier;
            if (distanceToPlayer <= chaseThreshold)
                chasing = true;
            else if (distanceToPlayer > chaseReleaseDistance)
                chasing = false;

            Vector3 targetPosition;
            Vector3 facingPoint;
            float moveSpeed;

            if (chasing)
            {
                if (enemyAnimator != null)
                    enemyAnimator.SetBool("isChasing", true);
                targetPosition = ComputeChaseTargetPosition();
                facingPoint = player.transform.position;
                moveSpeed = chaseSpeed;
            }
            else
            {
                if (enemyAnimator != null)
                    enemyAnimator.SetBool("isChasing", false);

                if (stationaryWhenIdle)
                {
                    targetPosition = homePosition;
                    facingPoint = homePosition + transform.forward;
                    moveSpeed = 0f;
                }
                else
                {
                    repathTimer -= Time.fixedDeltaTime;
                    if (repathTimer <= 0f || Vector3.Distance(rb.position, wanderTarget) <= 0.25f)
                        PickNewWanderTarget();

                    targetPosition = wanderTarget;
                    facingPoint = wanderTarget;
                    moveSpeed = loiterSpeed;
                }
            }

            if (keepHeightFixed)
                facingPoint.y = rb.position.y;

            targetPosition = StickToGround(targetPosition);
            facingPoint = StickToGround(facingPoint);

            Vector3 facingVector = facingPoint - rb.position;
            if (keepHeightFixed)
                facingVector.y = 0f;

            if (facingVector.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(facingVector.normalized, Vector3.up);
                rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * turnSpeed));
            }

            Vector3 newPosition = Vector3.MoveTowards(rb.position, targetPosition, moveSpeed * Time.fixedDeltaTime);
            rb.MovePosition(newPosition);
        }

        private Vector3 StickToGround(Vector3 position)
        {
            if (TryGetGroundPoint(position, out Vector3 groundPoint))
            {
                float targetY = groundPoint.y + groundSurfaceOffset;
                position.y = Mathf.MoveTowards(position.y, targetY, groundStickSpeed * Time.fixedDeltaTime);
            }
            else
            {
                position.y -= groundFallbackFallSpeed * Time.fixedDeltaTime;
            }

            return position;
        }

        private void SnapToGroundImmediate()
        {
            if (TryGetGroundPoint(rb.position, out Vector3 groundPoint))
            {
                Vector3 snappedPosition = rb.position;
                snappedPosition.y = groundPoint.y + groundSurfaceOffset;
                rb.position = snappedPosition;
            }
        }

        private bool TryGetGroundPoint(Vector3 position, out Vector3 groundPoint)
        {
            Vector3 origin = position + Vector3.up * groundProbeHeight;
            float maxDistance = groundProbeHeight + groundProbeDistance;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, maxDistance, groundMask, QueryTriggerInteraction.Ignore);

            float closestDistance = float.MaxValue;
            groundPoint = default;

            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                if (hit.collider == null)
                    continue;

                if (hit.collider.transform == transform || hit.collider.transform.IsChildOf(transform))
                    continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    groundPoint = hit.point;
                }
            }

            return closestDistance < float.MaxValue;
        }

        private Vector3 ComputeChaseTargetPosition()
        {
            Vector3 toPlayer = player.transform.position - rb.position;
            if (keepHeightFixed)
                toPlayer.y = 0f;

            float distance = toPlayer.magnitude;
            Vector3 seekDirection = distance > 0.0001f ? toPlayer / distance : transform.forward;

            // Only "spend" seek distance down to the contact ring - this is the
            // actual jitter/footstep-spam fix. The enemy no longer tries to walk
            // to the player's exact pivot, so it never sits in constant, ever
            // deepening overlap with the CharacterController.
            float remainingApproach = Mathf.Max(0f, distance - contactDistance);
            Vector3 seekComponent = seekDirection * remainingApproach;

            // Independent of how close we are to the player, so a ring of enemies
            // parked at the contact distance can still jostle apart instead of
            // freezing shoulder-to-shoulder or clipping into each other.
            Vector3 separationComponent = ComputeSeparation() * separationWeight;

            return rb.position + seekComponent + separationComponent;
        }

        private Vector3 ComputeSeparation()
        {
            Vector3 push = Vector3.zero;

            for (int i = 0; i < activeEnemies.Count; i++)
            {
                TunnelEnemyAI other = activeEnemies[i];
                if (other == null || other == this)
                    continue;

                Vector3 offset = rb.position - other.rb.position;
                if (keepHeightFixed)
                    offset.y = 0f;

                float distance = offset.magnitude;
                if (distance > 0.0001f && distance < separationRadius)
                    push += (offset / distance) * (1f - distance / separationRadius);
            }

            return push;
        }

        private void PickNewWanderTarget()
        {
            Vector2 offset = Random.insideUnitCircle * loiterRadius;
            wanderTarget = homePosition + new Vector3(offset.x, 0f, offset.y);
            repathTimer = Random.Range(repathTimeRange.x, repathTimeRange.y);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Application.isPlaying ? homePosition : transform.position, Application.isPlaying ? loiterRadius : loiterRadiusRange.y);

            Gizmos.color = Color.red;
            float drawGrabRadius = Application.isPlaying ? grabRadius : Mathf.Min(grabRadiusRange.y, chaseThresholdRange.y * 0.9f);
            Gizmos.DrawWireSphere(transform.position, drawGrabRadius);

            Gizmos.color = Color.cyan;
            float drawContactDistance = Application.isPlaying ? contactDistance : contactDistanceRange.y;
            Gizmos.DrawWireSphere(transform.position, drawContactDistance);
        }
#endif
    }
}