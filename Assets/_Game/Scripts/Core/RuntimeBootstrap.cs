using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 运行时引导：**每次进入播放**时把全局静态状态归位。
    ///
    /// <para>━━ 为什么需要 ━━</para>
    /// <para>
    /// <c>Time.timeScale</c> 是**全局静态状态**，退出 Play 模式、切场景都**不会自动重置**。
    /// 一旦某个面板（<c>UIWindow</c> / <c>PauseMenu</c>）在**打开状态下被中断**
    /// —— 停止 Play、切换场景、父级被 <c>SetActive(false)</c> ——
    /// 它设的 <c>timeScale = 0</c> 就没人恢复，会原样留给下一次播放。
    /// </para>
    ///
    /// <para>━━ 症状为什么极具误导性 ━━</para>
    /// <para>
    /// 表现为 **「相机能转，角色完全不动，Console 干净无报错」**，看起来像输入坏了。
    /// 实则输入完全正常（<c>GameplayInputEnabled = true</c>，<c>walkSpeed</c> 也在正常值），
    /// 差别只在两者对时间的用法：
    /// </para>
    /// <list type="bullet">
    ///   <item>相机转向 <c>_yaw += look.x * sensitivity</c> —— **不乘 deltaTime**，照常转；</item>
    ///   <item>玩家位移 <c>_cc.Move(dir * speed * Time.deltaTime)</c> —— <c>deltaTime = 0</c> ⇒ 位移恒为 0。</item>
    /// </list>
    ///
    /// <para>
    /// 2026-10-23 实际踩到过一次：编辑器里 <c>timeScale</c> 停在 0，
    /// 在酒馆按 Play 后角色不动、只能转视角，排查了半天输入链路。
    /// </para>
    /// <para>
    /// ⚠️ 本方法只是**兜底**，不能替代正确的配对恢复（<c>UIWindow.Close</c> / <c>PauseMenu.Resume</c>）。
    /// 它保证的是「最坏情况下下一次播放仍是好的」。
    /// </para>
    /// </summary>
    public static class RuntimeBootstrap
    {
        /// <summary>
        /// 进入播放时把时间缩放归位。
        ///
        /// <para>用 <c>BeforeSceneLoad</c> 而不是 <c>AfterSceneLoad</c>：要**早于**所有游戏对象的
        /// <c>Awake</c> / <c>Start</c>，否则首帧的位移与动画仍会按 0 跑一帧。</para>
        /// <para>用 <c>RuntimeInitializeOnLoadMethod</c> 而不是场景里的某个组件：它**不依赖任何场景对象**，
        /// 即使关闭 Domain Reload（快速进入播放模式）也照样执行。</para>
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetGlobalState()
        {
            Time.timeScale = 1f;
        }
    }
}
