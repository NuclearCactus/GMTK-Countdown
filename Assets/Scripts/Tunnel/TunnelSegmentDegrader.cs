using System;
using System.Reflection;
using UnityEngine;

namespace GMTKCountdown.Tunnel
{
    public class TunnelSegmentDegrader : MonoBehaviour
    {
        [Header("Side Settings")]
        [SerializeField] private int startSides = 6;
        [SerializeField] private int minSides = 2;

        private int currentSides;
        private float shrinkTimer;

        private Collider cachedCollider;
        private Component proBuilderShapeComponent;
        private object shapeObject;
        private FieldInfo sidesField;
        private MethodInfo updateShapeMethod;

        public int CurrentSides => currentSides;

        private void Awake()
        {
            cachedCollider = GetComponent<Collider>();
            CacheProBuilderBindings();
            DetectInitialSides();
            ResetToStart();
        }

        public void Tick(Vector3 playerPosition, float activationDistance, float shrinkInterval)
        {
            if (!IsPlayerNear(playerPosition, activationDistance))
                return;

            shrinkTimer += Time.deltaTime;

            float interval = Mathf.Max(0.01f, shrinkInterval);
            while (shrinkTimer >= interval)
            {
                shrinkTimer -= interval;

                if (currentSides <= minSides)
                    break;

                currentSides--;
                ApplySides(currentSides);
            }
        }

        public bool IsPlayerNear(Vector3 playerPosition, float activationDistance)
        {
            float distance;

            if (cachedCollider != null)
            {
                Vector3 nearest = cachedCollider.ClosestPoint(playerPosition);
                distance = Vector3.Distance(playerPosition, nearest);
            }
            else
            {
                distance = Vector3.Distance(playerPosition, transform.position);
            }

            return distance <= activationDistance;
        }

        public void ResetToStart()
        {
            currentSides = Mathf.Max(minSides, startSides);
            shrinkTimer = 0f;
            ApplySides(currentSides);
        }

        private void CacheProBuilderBindings()
        {
            Type proBuilderShapeType = Type.GetType("UnityEngine.ProBuilder.Shapes.ProBuilderShape, Unity.ProBuilder");
            if (proBuilderShapeType == null)
                return;

            proBuilderShapeComponent = GetComponent(proBuilderShapeType);
            if (proBuilderShapeComponent == null)
                return;

            PropertyInfo shapeProperty = proBuilderShapeType.GetProperty("shape", BindingFlags.Instance | BindingFlags.Public);
            shapeObject = shapeProperty?.GetValue(proBuilderShapeComponent);
            if (shapeObject == null)
                return;

            Type shapeType = shapeObject.GetType();
            sidesField = shapeType.GetField("m_NumberOfSides", BindingFlags.Instance | BindingFlags.NonPublic);

            updateShapeMethod = proBuilderShapeType.GetMethod("UpdateShape", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private void DetectInitialSides()
        {
            if (sidesField == null || shapeObject == null)
            {
                startSides = Mathf.Max(minSides, startSides);
                return;
            }

            object value = sidesField.GetValue(shapeObject);
            if (value is int detectedSides)
                startSides = Mathf.Max(minSides, detectedSides);
            else
                startSides = Mathf.Max(minSides, startSides);
        }

        private void ApplySides(int sides)
        {
            if (sidesField == null || shapeObject == null || updateShapeMethod == null)
                return;

            sidesField.SetValue(shapeObject, sides);
            updateShapeMethod.Invoke(proBuilderShapeComponent, null);
        }
    }
}
