using System.Collections.Generic;
using UnityEngine;

namespace GMTKCountdown.Tunnel
{
    public class TunnelDegradationManager : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private Transform player;

        [Header("Degradation")]
        [SerializeField] private float activationDistance = 8f;
        [SerializeField] private float sideDecreaseInterval = 1.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip sideDecreaseClip;
        [SerializeField] private AudioClip orbPickupClip;
        [SerializeField] private float sideDecreaseVolume = 1f;
        [SerializeField] private float orbPickupVolume = 1f;

        [Header("Segments")]
        [SerializeField] private bool autoFindSegmentsInChildren = true;
        [SerializeField] private List<TunnelSegmentDegrader> segments = new List<TunnelSegmentDegrader>();

        private readonly List<int> lastKnownSides = new List<int>();

        private void Awake()
        {
            if (autoFindSegmentsInChildren)
                FindSegmentsInChildren();

            EnsurePlayerReference();
            RefreshSideCache();

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
        }

        private void Update()
        {
            if (player == null)
            {
                EnsurePlayerReference();
                if (player == null)
                    return;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                TunnelSegmentDegrader segment = segments[i];
                if (segment == null)
                    continue;

                int before = segment.CurrentSides;
                segment.Tick(player.position, activationDistance, sideDecreaseInterval);

                if (segment.CurrentSides < before)
                    PlaySideDecreaseSound();

                if (i < lastKnownSides.Count)
                    lastKnownSides[i] = segment.CurrentSides;
            }
        }

        public void ResetAllTunnels()
        {
            for (int i = 0; i < segments.Count; i++)
            {
                TunnelSegmentDegrader segment = segments[i];
                if (segment == null)
                    continue;

                segment.ResetToStart();
                if (i < lastKnownSides.Count)
                    lastKnownSides[i] = segment.CurrentSides;
            }
        }

        public void NotifyOrbCollected()
        {
            ResetAllTunnels();
            PlayOrbPickupSound();
        }

        private void FindSegmentsInChildren()
        {
            segments.Clear();
            segments.AddRange(GetComponentsInChildren<TunnelSegmentDegrader>(true));
        }

        private void EnsurePlayerReference()
        {
            if (player != null)
                return;

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
            {
                player = taggedPlayer.transform;
                return;
            }

            var fps = FindFirstObjectByType<EasyPeasyFirstPersonController.FirstPersonController>();
            if (fps != null)
                player = fps.transform;
        }

        private void RefreshSideCache()
        {
            lastKnownSides.Clear();
            for (int i = 0; i < segments.Count; i++)
            {
                TunnelSegmentDegrader segment = segments[i];
                lastKnownSides.Add(segment != null ? segment.CurrentSides : 0);
            }
        }

        private void PlaySideDecreaseSound()
        {
            if (audioSource == null || sideDecreaseClip == null)
                return;

            audioSource.PlayOneShot(sideDecreaseClip, sideDecreaseVolume);
        }

        private void PlayOrbPickupSound()
        {
            if (audioSource == null || orbPickupClip == null)
                return;

            audioSource.PlayOneShot(orbPickupClip, orbPickupVolume);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            activationDistance = Mathf.Max(0.1f, activationDistance);
            sideDecreaseInterval = Mathf.Max(0.05f, sideDecreaseInterval);
        }
#endif
    }
}
