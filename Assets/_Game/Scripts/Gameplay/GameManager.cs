using UnityEngine;
using Game.Core;
using System.Collections;
using UnityEngine.SceneManagement;

namespace Game.Gameplay
{
    /// <summary>
    /// 流程管理：订阅玩家死亡 / Boss 阵亡，弹出对应的结算面板。
    /// ⚠️ 结算面板显示时必须把 <c>Time.timeScale</c> 置 0，否则玩家在结算后仍能操作（原实现漏了这一步）。
    /// </summary>
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
            Time.timeScale = 1f;//防止上一次结算留下的 0 残留
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

            if (resultText != null)
                resultText.text = $"VICTORY! TIME:{Time.time - _startTime:F2}s";

            StartCoroutine(ShowPanelLater(victoryPanel, 0.5f));
        }

        /// <summary>延时后显示结算面板，并**暂停游戏**。</summary>
        private IEnumerator ShowPanelLater(GameObject panel, float delay)
        {
            //用 Realtime 版本：即便期间 timeScale 被改成 0，延时依然有效
            yield return new WaitForSecondsRealtime(delay);

            if (panel != null) panel.SetActive(true);
            Time.timeScale = 0f;//暂停：结算之后玩家不应再能操作
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
