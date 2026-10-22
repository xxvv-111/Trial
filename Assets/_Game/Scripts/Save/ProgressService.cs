using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Save
{
    /// <summary>
    /// 进度的唯一读写入口（跨场景常驻）。
    ///
    /// 铁律：
    ///  1. 只有这个类能写 PlayerProgress 并落盘，业务系统一律走 Instance.Progress + MarkDirty()。
    ///  2. 落盘策略是「标脏 + 0.5 s 防抖」，不是每帧写，也不是只在关键节点写。
    ///  3. 防抖必须用 unscaledDeltaTime —— 暂停 / 顿帧时 timeScale = 0，用 deltaTime 会永远等不到落盘。
    ///
    /// 为什么不用"在 MainMenu 场景里摆一个"：开发时经常直接打开 Castle / Tavern 按 Play，
    /// 那种启动路径不经过 MainMenu，服务就不存在。BeforeSceneLoad 保证任何启动方式都有，且只创建一次。
    /// </summary>
    public sealed class ProgressService : MonoBehaviour
    {
        public const float FlushDelay = 0.5f;

        public static ProgressService Instance { get; private set; }

        /// <summary>当前进度。改完字段记得调 MarkDirty()。</summary>
        public PlayerProgress Progress => _file != null ? _file.progress : null;

        public SaveFile File => _file;
        public bool IsLoaded { get; private set; }
        public LoadStatus LastLoadStatus { get; private set; }

        /// <summary>进度变化（含每次 MarkDirty）。UI 可以订阅刷新；OnEnable 订阅 / OnDisable 退订。</summary>
        public event Action Changed;

        SaveFile _file;
        SaveSystem _system;
        bool _dirty;
        float _idle;
        float _playTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[ProgressService]");
            DontDestroyOnLoad(go);
            go.AddComponent<ProgressService>();   // Awake 里完成读档
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 场景里被误摆了一份 —— 保留先来的，销毁多余的，避免两个服务各写各的
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            var path = System.IO.Path.Combine(Application.persistentDataPath, SaveSystem.DefaultFileName);
            _system = new SaveSystem(new DiskFileStore(), path);
            Load();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            _playTime += Time.unscaledDeltaTime;

            if (!_dirty) return;
            _idle += Time.unscaledDeltaTime;
            if (_idle < FlushDelay) return;
            SaveNow();
        }

        void OnApplicationQuit() => SaveNow();

        void OnApplicationPause(bool paused)
        {
            if (paused) SaveNow();   // 移动端切后台随时可能被系统回收
        }

        void OnDisable()
        {
            if (Instance == this) SaveNow();
        }

        // ───────────────────────────── 对外 API ─────────────────────────────

        public void Load()
        {
            LastLoadStatus = _system.TryLoad(out _file, out var message);
            _file = _file ?? SaveSystem.CreateDefault();
            SaveSystem.Normalize(_file);
            IsLoaded = true;

            Debug.Log($"[Save] load {LastLoadStatus} v{_file.version} ({message}) @ {_system.Path}");
            Changed?.Invoke();
        }

        /// <summary>标脏：0.5 s 后自动落盘。任何改动进度的地方都要调。</summary>
        public void MarkDirty()
        {
            _dirty = true;
            _idle = 0f;
            Changed?.Invoke();
        }

        /// <summary>立刻落盘（关键节点 / 退出时用）。</summary>
        public bool SaveNow()
        {
            if (!IsLoaded || _file == null) return false;

            _dirty = false;
            _idle = 0f;

            var scene = SceneManager.GetActiveScene().name;
            var ok = _system.Save(_file, scene, _playTime);
            if (ok)
                Debug.Log($"[Save] write v{_file.version} 场景={scene} 时长={_playTime:F0}s @ {_system.Path}");
            return ok;
        }

        /// <summary>调试用：内存里清空为默认进度（不删文件，下次 SaveNow 会覆盖）。</summary>
        public void ResetToDefault()
        {
            _file = SaveSystem.CreateDefault();
            _playTime = 0f;
            MarkDirty();
            Changed?.Invoke();
        }

        /// <summary>调试用：连文件一起删。</summary>
        public void DeleteSaveFile()
        {
            _system?.DeleteAll();
            ResetToDefault();
        }

        public string SavePath => _system != null ? _system.Path : "(未初始化)";
    }
}
