using UnityEngine;

public class LifecyclePrinter : MonoBehaviour
{
    private int _u, _f, _l;   // 计数器：每种 Update 只打印前 3 次，免得刷屏

    void Awake() { Debug.Log($"[{name}] Awake     ——物体创建，仅一次"); }
    void OnEnable() { Debug.Log($"[{name}] OnEnable  ——物体启用，每次启用都会来"); }
    void Start() { Debug.Log($"[{name}] Start     ——所有 Awake 之后，仅一次"); }

    void FixedUpdate()
    {
        if (_f++ < 3) Debug.Log($"[{name}] FixedUpdate  帧={Time.frameCount}  fixedDeltaTime={Time.fixedDeltaTime:F3}");
    }
    void Update()
    {
        if (_u++ < 3) Debug.Log($"[{name}] Update       帧={Time.frameCount}  deltaTime={Time.deltaTime:F4}");
    }
    void LateUpdate()
    {
        if (_l++ < 3) Debug.Log($"[{name}] LateUpdate   帧={Time.frameCount}");
    }

    void OnDisable() { Debug.Log($"[{name}] OnDisable ——物体被禁用"); }
    void OnDestroy() { Debug.Log($"[{name}] OnDestroy ——物体被销毁"); }
}
