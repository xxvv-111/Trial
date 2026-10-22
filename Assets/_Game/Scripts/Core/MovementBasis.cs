using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// **中立的「移动基准」** —— 用来打断 `CameraFollow` 与 `PlayerMotor` 之间的双向引用。
    ///
    /// <para><b>为什么需要它</b>：移动必须「相对相机」（按 W 是"屏幕上的前"），所以玩家要知道相机的水平角；
    /// 而相机要跟着玩家跑，所以相机要知道玩家在哪 —— 两边各持一个 `Transform` 引用就成环了。
    /// 其实真正流动的只有两个**数据**：</para>
    /// <list type="bullet">
    ///   <item>相机需要「玩家位置 / 朝向」—— 这一条保留，**相机单向引用玩家**；</item>
    ///   <item>玩家需要「相机水平角 yaw」—— 改成从这里读，**玩家对相机零引用**。</item>
    /// </list>
    ///
    /// <para>于是依赖只剩 <c>CameraFollow → 玩家</c> 一条单向边，两个类可以各自独立测试
    /// （玩家预制体不再需要一个相机才能动）。</para>
    ///
    /// <para>⭐ 写入方：<see cref="Game.Gameplay.CameraFollow"/> 每帧写一次（<c>Update</c>）。</para>
    ///
    /// <para>⚠️ 这是**静态状态**，跨场景不会自动清空。因为相机每帧都会覆写，实际影响只有
    /// "新场景第一帧可能读到上个场景的 yaw"—— 而 <see cref="HasValue"/> 在首次写入前是 false，
    /// 玩家会退回世界坐标轴并打一次警告，不会静默走错方向。</para>
    /// </summary>
    public static class MovementBasis
    {
        /// <summary>相机当前水平角（度，绕 Y）。</summary>
        public static float Yaw { get; private set; }

        /// <summary>是否已经有相机写过值。false 时 <see cref="Forward"/>/<see cref="Right"/> 退回世界坐标轴。</summary>
        public static bool HasValue { get; private set; }

        /// <summary>由 <see cref="Game.Gameplay.CameraFollow"/> 每帧调用。</summary>
        public static void SetYaw(float yawDegrees)
        {
            Yaw = yawDegrees;
            HasValue = true;
        }

        /// <summary>场景切换 / 相机销毁时清空，避免把上一场景的朝向带过来。</summary>
        public static void Clear()
        {
            HasValue = false;
        }

        /// <summary>水平面上的「屏幕上方」方向（已归一化，无 Y 分量）。</summary>
        public static Vector3 Forward
        {
            get { return HasValue ? Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward : Vector3.forward; }
        }

        /// <summary>水平面上的「屏幕右方」方向（已归一化，无 Y 分量）。</summary>
        public static Vector3 Right
        {
            get { return HasValue ? Quaternion.Euler(0f, Yaw, 0f) * Vector3.right : Vector3.right; }
        }
    }
}
