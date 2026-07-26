namespace GMTKCountdown.Tunnel
{
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.SceneManagement;
    using TMPro;
    using EasyPeasyFirstPersonController;

    public class SpeedrunTimerManager : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Parent panel or canvas group containing the end screen text elements.")]
        public GameObject endScreenPanel;

        [Tooltip("TextMeshPro component to display 'Time taken: XX:YY:ZZ'.")]
        public TMP_Text timeTakenText;

        [Tooltip("TextMeshPro component to display 'Press R to restart'.")]
        public TMP_Text restartText;

        [Tooltip("Optional TextMeshPro component for live timer during gameplay.")]
        public TMP_Text liveTimerText;

        [Header("Timer & Trigger Settings")]
        [Tooltip("Tag of the player object. If empty or not matched, component lookup is used as fallback.")]
        public string playerTag = "Player";

        [Tooltip("Should the timer start automatically when the scene starts?")]
        public bool autoStartTimer = true;

        [Header("Restart Settings")]
        [Tooltip("Key used to restart the level.")]
        public KeyCode restartKey = KeyCode.R;

        [Tooltip("Allow restarting at any time, even before reaching the finish trigger.")]
        public bool allowRestartAnytime = false;

        private float elapsedTime = 0f;
        private bool isTimerRunning = false;
        private bool isFinished = false;

        public float ElapsedTime => elapsedTime;
        public bool IsFinished => isFinished;

        private void Start()
        {
            elapsedTime = 0f;
            isFinished = false;
            isTimerRunning = autoStartTimer;

            if (endScreenPanel != null)
            {
                endScreenPanel.SetActive(false);
            }

            if (restartText != null)
            {
                restartText.text = "Press R to restart";
            }

            if (liveTimerText != null)
            {
                liveTimerText.gameObject.SetActive(true);
                liveTimerText.text = FormatTime(elapsedTime);
            }
        }

        private void Update()
        {
            if (isTimerRunning)
            {
                elapsedTime += Time.deltaTime;

                if (liveTimerText != null)
                {
                    liveTimerText.text = FormatTime(elapsedTime);
                }
            }

            // Listen for restart key input via Unity Input System
            bool isRPressed = Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
            if ((isFinished || allowRestartAnytime) && isRPressed)
            {
                RestartLevel();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (isFinished) return;

            // Check if the collider belongs to the player
            bool isPlayer = false;
            if (!string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
            {
                isPlayer = true;
            }
            else if (other.GetComponentInParent<FirstPersonController>() != null || other.GetComponent<CharacterController>() != null)
            {
                isPlayer = true;
            }

            if (isPlayer)
            {
                FinishLevel();
            }
        }

        public void FinishLevel()
        {
            if (isFinished) return;

            isFinished = true;
            isTimerRunning = false;

            if (liveTimerText != null)
            {
                liveTimerText.gameObject.SetActive(false);
            }

            string formattedTimeStr = FormatTime(elapsedTime);

            if (timeTakenText != null)
            {
                timeTakenText.text = "Time taken: " + formattedTimeStr;
                timeTakenText.gameObject.SetActive(true);
            }

            if (restartText != null)
            {
                restartText.text = "Press R to restart";
                restartText.gameObject.SetActive(true);
            }

            if (endScreenPanel != null)
            {
                endScreenPanel.SetActive(true);
            }
        }

        public void OnPlayerDeath()
        {
            isTimerRunning = false;
            isFinished = true; // Mark finished on death so R restart works

            if (liveTimerText != null)
            {
                liveTimerText.gameObject.SetActive(false);
            }
        }

        public void RestartLevel()
        {
            // Reset timescale in case slow-mo or pause was active
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
            // Debug.Log("Restarting level...");
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.buildIndex >= 0)
            {
                SceneManager.LoadScene(activeScene.buildIndex);
            }
            else if (!string.IsNullOrEmpty(activeScene.name))
            {
                SceneManager.LoadScene(activeScene.name);
            }
        }

        public string FormatTime(float timeInSeconds)
        {
            int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
            int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
            int milliseconds = Mathf.FloorToInt((timeInSeconds * 100f) % 100f); // 2-digit milliseconds (hundredths)

            return string.Format("{0:00}:{1:00}:{2:00}", minutes, seconds, milliseconds);
        }
    }
}
