using UnityEngine;

namespace Game.Data
{
    [CreateAssetMenu(fileName = "EnemyAIConfig", menuName = "Game/EnemyAIConfig")]
    public class EnemyAIConfig : ScriptableObject
    {
        public string displayName = "拳击手";
        public int hp = 30;
        public float aggroRange = 8f;//追击距离
        public float attackRange = 1f;//攻击距离
        public float moveSpeed = 3f;//追击速度
        public float windupTime = 1f;//前摇
        public float recoverTime = 1.5f;//后摇
        public int damage = 10;
        public GameObject prefab;//可选,按表生成怪用
        public GameObject hitEffect;//受击特效

        [Header("受击反应（GDD §6.1：小怪会被打断，Boss 不会）")]
        [Tooltip("是否会被受击打断（硬直）。\n⚠️ 小怪 true；**Boss 必须设 false** —— Boss 只闪白、不硬直。")]
        public bool canBeInterrupted = true;

        [Tooltip("受击硬直时长（秒）。仅在 canBeInterrupted = true 时生效。")]
        public float hitStunTime = 0.4f;

        [Tooltip("受击后退距离（米）。0 = 不击退。")]
        public float knockBackDistance = 0.4f;

        [Tooltip("受击闪白时长（秒）。")]
        public float flashTime = 0.15f;

        [Header("近战节奏（M3.4 · GDD §6.2）")]
        [Tooltip("Alert 停顿时长（秒）：看见玩家后先站住把身体转向玩家，再开始追。\n" +
                 "0 = 看见就追（等价于 M3.4 之前的行为）。")]
        public float alertTime = 0.35f;

        [Tooltip("攻击结束后主动拉开的后退距离（米，GDD §6.2「不应贴脸」）。\n" +
                 "0 = 不后退（等价于 M3.4 之前的行为）。")]
        public float repositionDistance = 1.5f;

        [Header("远程（法师）专用（M3.5 · GDD §6.3）")]
        [Tooltip("舒适距离（米）：**小于此值就后撤**。法师在这个距离到「施法距离上限」之间会站住施法。")]
        public float comfortDistance = 6f;

        [Tooltip("施法距离下限（米）：**小于此值不施法**（先拉开距离）。\n" +
                 "⚠️ 上限用的是上面的 `attackRange`（法师填 9）。")]
        public float castRangeMin = 4f;

        [Tooltip("施法冷却（秒）：两次施法之间的间隔。")]
        public float castCooldown = 2.2f;

        [Tooltip("法术弹飞行速度（米/秒）。")]
        public float projectileSpeed = 14f;

        [Header("视野感知（M3.3 · GDD §6.1：距离 + 扇形 + 视线遮挡）")]
        [Tooltip("视野距离（米）。填 0 = 直接沿用上面的 aggroRange。")]
        public float viewDistance = 0f;

        [Tooltip("视野扇形的**全角**（度）。360 = 退化成纯距离感知（等价于接入感知之前的行为）。")]
        [Range(1f, 360f)] public float viewAngle = 160f;

        [Tooltip("是否要求视线无遮挡：被墙 / 柱子挡住 → 看不见。")]
        public bool requireLineOfSight = true;

        [Tooltip("贴脸感知半径（米）：玩家进到这个距离内，**即使在他背后也会被发现**" +
                 "（对应 GDD §6.3「玩家贴脸 → 优先脱离」）。0 = 关闭。")]
        public float proximityRange = 2f;

        [Tooltip("眼睛高度（米）：视线射线的起点抬升量。")]
        public float eyeHeight = 1.2f;

        [Tooltip("瞄准点高度（米）：视线射线的终点在玩家身上抬升多少（对着躯干打，不是脚底）。")]
        public float aimHeight = 1.0f;

        [Tooltip("丢失目标后继续追击的秒数。**0 = 永不放弃**（等价于旧行为）。" +
                 "GDD §6.1 把「丢失目标后如何搜索」标为待定，所以默认关闭，需要时再打开。")]
        public float loseTargetTime = 0f;
    }
}
