using UnityEngine;
using Game.Core;
using System.Collections;
using UnityEngine.SceneManagement;

namespace Game.GamePlay
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("UI面板")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private GameObject victoryPanel;
        [SerializeField] private TMPro.TextMeshProUGUI resultText;

        private float _startTime;//开始时间
        private bool _ended;//结束标志

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _startTime = Time.time;
            _ended = false;
            gameOverPanel.SetActive(false);
            victoryPanel.SetActive(false);
        }

        private void OnEnable()
        {
            GameEvents.PlayerDied += OnPlayerDied;
            GameEvents.BossDied += OnVictory;
        }

        private void OnDisable()
        {
            GameEvents.PlayerDied -= OnPlayerDied;
            GameEvents.BossDied -= OnVictory;
        }

        private void OnPlayerDied()
        {
            if (_ended) return;
            _ended = true;
            StartCoroutine(ShowPanelLater(gameOverPanel, 2f));
        }

        private void OnVictory()
        {
            if (_ended) return;
            _ended = true;
            
            if(resultText!=null)
                resultText.text=$"VICTPRY! TIME:{Time.time-_startTime:F2}s";

            StartCoroutine(ShowPanelLater(victoryPanel, 0.5f));
        }

        private IEnumerator ShowPanelLater(GameObject panel,float delay)
        {
            yield return new WaitForSeconds(delay);
            panel.SetActive(true);
        }

        public void RestartRun()
        {
            Time.timeScale = 1f;

            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void BackToMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }
    }
}