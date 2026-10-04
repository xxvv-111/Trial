using UnityEngine;
using Game.Core;
using System.Collections;
using UnityEngine.SceneManagement;

namespace Game.Gameplay
{
    /// <summary>
    /// 流程管理：订阅玩家死亡 / Boss 阵亡，弹出对应的结算面板。
    ///
    /// ⚠️ 结束时要**同时**做两件事，缺一不可：
    ///   1. **锁游戏性输入**（<see cref="InputService.SetGameplayInputEnabled"/>）——
    ///      这才是真正拦住"鼠标还能转视角"的一步
    ///   2. **暂停时间**（<c>Time.timeScale = 0</c>）—— 让角色动画与逻辑停下
    ///   只做第 2 步是不够的：`Update()` 在 timeScale = 0 时仍每帧执行，
    ///   且 Input System 的鼠标输入完全不受 timeScale 影响。
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

        /// <summary>本局是否已结算（死亡 / 通关）。暂停菜单据此避免在结算后再打开。</summary>
        public bool HasEnded { get { return _ended; } }

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
            SetGameplayInput(true);//防止上一次结算留下的输入锁残留
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

            SetGameplayInput(false);//立刻交出控制权，不等结算面板
            StartCoroutine(ShowPanelLater(gameOverPanel, 2f));
        }

        private void OnVictory()
        {
            if (_ended) return;
            _ended = true;

            SetGameplayInput(false);

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

        /// <summary>统一开关游戏性输入（InputService 可能比本类晚/早初始化，故做空判）。</summary>
        private void SetGameplayInput(bool on)
        {
            if (InputService.Instance != null)
                InputService.Instance.SetGameplayInputEnabled(on);
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
