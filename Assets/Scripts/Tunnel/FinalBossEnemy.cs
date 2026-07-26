namespace GMTKCountdown.Tunnel
{
    using UnityEngine;
    using EasyPeasyFirstPersonController;

    public class FinalBossEnemy : MonoBehaviour
    {
        [Header("Final Boss / End Game Settings")]
        [Tooltip("Audio clip played when the player contacts this boss.")]
        public AudioClip dialogueClip;

        [Tooltip("AudioSource component used to play the dialogue clip. If left unassigned, uses one on this GameObject.")]
        public AudioSource dialogueAudioSource;

        [Tooltip("UI Panel or Canvas element that fades the screen to black upon touching the boss.")]
        public GameObject fadeToBlackPanel;

        [Tooltip("Reference to SpeedrunTimerManager. Automatically located if unassigned.")]
        public SpeedrunTimerManager timerManager;

        private bool hasTriggeredFinish = false;

        private void Awake()
        {
            if (dialogueAudioSource == null)
            {
                dialogueAudioSource = GetComponent<AudioSource>();
            }
            if (dialogueAudioSource == null)
            {
                dialogueAudioSource = gameObject.AddComponent<AudioSource>();
            }

            if (timerManager == null)
            {
                timerManager = FindAnyObjectByType<SpeedrunTimerManager>();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            CheckPlayerTouch(collision.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            CheckPlayerTouch(other.gameObject);
        }

        private void CheckPlayerTouch(GameObject hitObject)
        {
            if (hasTriggeredFinish) return;

            if (hitObject.CompareTag("Player") ||
                hitObject.GetComponentInParent<FirstPersonController>() != null ||
                hitObject.GetComponent<CharacterController>() != null)
            {
                TriggerFinalSequence();
            }
        }

        public void TriggerFinalSequence()
        {
            if (hasTriggeredFinish) return;
            hasTriggeredFinish = true;

            // 1. Play dialogue clip
            if (dialogueClip != null && dialogueAudioSource != null)
            {
                dialogueAudioSource.PlayOneShot(dialogueClip);
            }

            // 2. Simply activate fade-to-black UI panel/object
            if (fadeToBlackPanel != null)
            {
                fadeToBlackPanel.SetActive(true);
            }

            // 3. Finish level and display end score
            if (timerManager == null)
            {
                timerManager = FindAnyObjectByType<SpeedrunTimerManager>();
            }
            if (timerManager != null)
            {
                timerManager.FinishLevel();
            }
        }
    }
}
