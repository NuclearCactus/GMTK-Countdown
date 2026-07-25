using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace GMTKCountdown.Tunnel
{
    public class TunnelPolygonSequenceManager : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField] private Transform player;

        [Header("Levels")]
        [SerializeField] private bool autoFindLevelsFromChildren = true;
        [SerializeField] private List<GameObject> tunnelLevels = new List<GameObject>();
        [SerializeField] private float levelSwapInterval = 1.5f;
        [SerializeField] private bool activateFirstLevelOnStart = true;

        [Header("Restart")]
        [SerializeField] private KeyCode restartKey = KeyCode.R;
        [SerializeField] private bool reloadActiveSceneOnRestart = true;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip levelChangeClip;
        [SerializeField] private AudioClip orbPickupClip;
        [SerializeField] private float levelChangeVolume = 1f;
        [SerializeField] private float orbPickupVolume = 1f;

        private int currentLevelIndex;
        private float swapTimer;
        private bool gameOver;

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            EnsurePlayerReference();

            if (autoFindLevelsFromChildren)
                CacheLevelsFromChildren();

            InitializeLevels();
        }

        private void Update()
        {
            if (gameOver)
            {
                if (Keyboard.current != null && restartKey == KeyCode.R && Keyboard.current.rKey.wasPressedThisFrame)
                    RestartScene();

                return;
            }

            if (tunnelLevels.Count <= 1)
                return;

            swapTimer += Time.deltaTime;
            while (swapTimer >= levelSwapInterval && !gameOver)
            {
                swapTimer -= levelSwapInterval;
                AdvanceToNextLevel();
            }
        }

        public void ResetToOriginalPolygonalLevel()
        {
            if (tunnelLevels.Count == 0)
                return;

            InitializeLevels();
        }

        private void InitializeLevels()
        {
            currentLevelIndex = 0;
            swapTimer = 0f;
            gameOver = false;

            for (int i = 0; i < tunnelLevels.Count; i++)
            {
                GameObject level = tunnelLevels[i];
                if (level == null)
                    continue;

                bool shouldBeActive = activateFirstLevelOnStart && i == 0;
                level.SetActive(shouldBeActive);
            }
        }

        private void AdvanceToNextLevel()
        {
            if (tunnelLevels.Count == 0)
                return;

            if (currentLevelIndex >= tunnelLevels.Count - 1)
            {
                SetLevelActive(currentLevelIndex, false);
                gameOver = true;
                return;
            }

            SetLevelActive(currentLevelIndex, false);
            currentLevelIndex++;
            SetLevelActive(currentLevelIndex, true);
            PlayLevelChangeSound();
        }

        private void SetLevelActive(int levelIndex, bool isActive)
        {
            if (levelIndex < 0 || levelIndex >= tunnelLevels.Count)
                return;

            GameObject level = tunnelLevels[levelIndex];
            if (level != null)
                level.SetActive(isActive);
        }

        private void CacheLevelsFromChildren()
        {
            tunnelLevels.Clear();

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                tunnelLevels.Add(child.gameObject);
            }
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

            var fps = FindAnyObjectByType<EasyPeasyFirstPersonController.FirstPersonController>();
            if (fps != null)
                player = fps.transform;
        }

        private void RestartScene()
        {
            if (reloadActiveSceneOnRestart)
            {
#if UNITY_EDITOR
                Selection.activeObject = null;
                EditorApplication.delayCall += () =>
                {
                    if (this != null)
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                };
#else
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
#endif
            }
            else
            {
                ResetToOriginalPolygonalLevel();
            }
        }

        private void PlayLevelChangeSound()
        {
            if (audioSource == null || levelChangeClip == null)
                return;

            audioSource.PlayOneShot(levelChangeClip, levelChangeVolume);
        }

        public void PlayOrbPickupSound()
        {
            if (audioSource == null || orbPickupClip == null)
                return;

            audioSource.PlayOneShot(orbPickupClip, orbPickupVolume);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            levelSwapInterval = Mathf.Max(0.05f, levelSwapInterval);
        }
#endif
    }
}
