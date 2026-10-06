using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 玩家移动：读输入 → **按相机朝向换算方向** → CharacterController 移动 → 驱动动画混合树。
    ///
    /// ⚠️ **移动是「相对相机」的**（W = 屏幕上方）。相机现在可被鼠标旋转，
    ///    若用世界轴会变成「转视角后按 W 往斜里走」，手感直接崩。
    ///
    /// ⭐ **本类是玩家身上唯一调用 `CharacterController.Move` 的地方**
    ///    （"位置只有一个写入者"）。其它组件要位移就走 <see cref="StepMove"/> /
    ///    <see cref="StepMoveGrounded"/>，禁止各自写 `transform.position +=`。
    ///
    /// ⚠️ 已知问题（T1）：PlayerFSM 用 <c>_motor.enabled = false</c> 来停止移动（冲刺 / 施法 / 受击 / 死亡），
    ///    这会把本脚本一起停掉，连贴地位移也停了（跳跃已取消，故降为低优先级）。
    ///    ℹ️ 被 disabled 时**公开方法仍可被调用** —— `PlayerDash` 正是靠这点在冲刺期间借用本类位移。
    /// ✅ **攻击状态已改用 <see cref="SetFacingOnly"/>**（2026-10-06 手感优化）：
    ///    攻击中"不移动但仍可限速转向"，同时贴地下压不再被中断。
    ///    ⭐ 默认走 **方案 B（转向窗口）**：命中帧之前可转向、出手之后锁定（见 <see cref="CanTurnNow"/>）；
    ///    把 `gateTurnByHitFrame` 关掉即退回 **方案 A（整段都可转）**。
    ///
    /// ⭐ **攻击的位移改用「动画根运动」**（2026-10-06，方案 B 的配套实现）：
    ///    本类实现 <see cref="OnAnimatorMove"/> ⇒ **Animator 不再自己写 transform**，
    ///    位移权 100% 收归本类；再由 <see cref="ComputeMoveDelta"/> 决定这一帧该走多少 ——
    ///    攻击时取 <c>Animator.deltaPosition</c>（动作自带的前冲），平时取代码位移。
    ///    ⚠️ **这就是"劫持根运动"**：`Apply Root Motion` 必须**保持勾选**（否则 <c>deltaPosition</c> 恒为 0），
    ///       而"不想让它自动写位置"靠的就是"实现了 OnAnimatorMove"。
    ///    ⚠️ 本组件被 `enabled = false` 时 OnAnimatorMove **不会**被调用，那时 Animator 会**恢复自动应用根运动**
    ///       ⇒ 所以 <see cref="OnDisable"/> 里会把 `applyRootMotion` 关掉（否则冲刺会额外叠加 4.8 m 的根运动且绕过碰撞体）。
    /// ✅ 已消除（T2）：玩家身上曾同时有 Rigidbody + CapsuleCollider + CharacterController，
    ///    现在只剩 CharacterController 一个。
    /// </summary>
    /// <summary>攻击期间的**位移来源**（见 <see cref="PlayerMotor"/> 上的 `attackDisplacement`）。</summary>
    public enum AttackDisplacementMode
    {
        /// <summary>
        /// ⭐ 代码驱动的前冲（2026-10-07 定案，**推荐**）：
        /// 按武器配置的「每段总距离 + 进度曲线」精确推进，与动画根曲线无关。
        /// </summary>
        CodeLunge = 0,

        /// <summary>劫持动画自带的根运动（<c>Animator.deltaPosition</c>）。上一版实现，保留用于对照。</summary>
        RootMotion = 1,

        /// <summary>攻击期间完全不位移（纯"定身"）。</summary>
        None = 2,

        /// <summary>
        /// ⚠️ **不劫持**：让 Animator **自己**应用根运动（本项目最原始的形态，用于 A/B 对照）。
        ///
        /// 该模式下 <see cref="PlayerFSM"/> 会把 <see cref="PlayerMotor"/> <c>enabled = false</c> ——
        /// 只有这样 <see cref="PlayerMotor.OnAnimatorMove"/> 才**不被调用**，位移权才交还给 Animator。
        ///
        /// ⚠️ **四个必然的副作用**（选了它就得接受）：
        /// <list type="number">
        ///   <item><b>攻击中完全不能转向</b> —— 组件被禁用，`Face()` 不跑（方案 B 的转向窗口一起失效）；</item>
        ///   <item><b>前冲绕过 `CharacterController`</b> ⇒ <b>会穿墙</b>（正是 T27 的成因）；</item>
        ///   <item><b>「只取正向增量」的过滤失效</b> ⇒ 动画根曲线的<b>回撤</b>会原样播放（"冲一下又被拉回"）；</item>
        ///   <item>攻击期间的<b>贴地下压也停</b>（与 T1 同源）。</item>
        /// </list>
        /// </summary>
        NativeRootMotion = 3,
    }

    /// <summary>
    /// 垂直（Y 轴）位移的**施加方式** —— 见 <see cref="PlayerMotor"/> 的 `lungeVerticalMode`。
    /// </summary>
    public enum VerticalLungeMode
    {
        /// <summary>不施加垂直位移（等于关掉这个通道）。</summary>
        Ignore = 0,

        /// <summary>
        /// ⭐ **默认**：把垂直位移施加到**骨骼根**（本工程 = `Armature`）的 <c>localPosition.y</c>。
        ///
        /// 这是**纯视觉**的位移 —— 不经过 `CharacterController`，因此：
        /// <list type="bullet">
        ///   <item>不会被地面挡住（向下沉照样生效）；</item>
        ///   <item>不影响碰撞、寻路、判定盒（Hitbox 挂在玩家根下，不随骨架沉浮）；</item>
        ///   <item><b>蒙皮网格会跟着骨架一起动</b> —— 蒙皮变换 = 骨骼矩阵 × 绑定姿势，<b>与渲染器自身的 transform 无关</b>
        ///         （2026-10-07 实测：`Armature.localPosition.y` 与全部骨骼世界 Y 严格 1:1）。</item>
        /// </list>
        ///
        /// ⚠️ 这也是唯一**能真正生效**的方式：若走 <c>CharacterController.Move</c>，
        ///    向下会被地面顶住（角色根本沉不下去），向上又会被每帧的贴地下压拉回来。
        /// </summary>
        SkeletonRoot = 1,
    }

    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private PlayerConfig config;

        [Tooltip("相机 Transform。留空则自动取 Camera.main。用于把输入换算成「相对相机」的方向。")]
        [SerializeField] private Transform _camera;

        [Tooltip("贴地下压速度（米/秒）。每帧实际下压 = 该值 × deltaTime。\n⚠️ 不要直接写「每帧下压的米数」——那样下压量会随帧率变化，导致角色位置抖动。")]
        [SerializeField] private float groundStickSpeed = 2f;
        [Tooltip("一般移动时的转身速度（度/秒）。")]
        [SerializeField] private float turnSpeed = 720f;
        [Tooltip("⚠️ 攻击中的转身速度（度/秒）—— 比一般移动慢得多，用来实现「攻击时不能移动、但可以有限调整朝向」。\n" +
                 "参考：一般移动 720。\n" +
                 "⚠️ 方案 B（命中帧后锁定）下窗口很短，180 往往偏小：Attack1 窗口仅 0.233 s → 最多只能转 42°。\n" +
                 "   建议 300~360（0.233 s 可转 70~84°，Attack3 的 0.567 s 可转 170~204°）。")]
        [SerializeField] private float attackTurnSpeed = 180f;
        [Tooltip("✅ 开启 = 【方案 B】转向窗口：命中帧之前可以转向，出手之后朝向锁定（判定与视觉不会错位）。\n" +
                 "⛔ 关闭 = 【方案 A】整段攻击都可限速转向（更宽松，但判定体可能被带着转）。\n" +
                 "开关只影响攻击期间的行为，其它状态不受影响。")]
        [SerializeField] private bool gateTurnByHitFrame = true;

        [Tooltip("⭐ 攻击期间的位移**来源**（三选一）。\n" +
                 "· CodeLunge【默认，推荐】= 代码驱动：按武器配置的「每段总距离 + 进度曲线」精确推进。\n" +
                 "   距离精确可控、每次结果一致、曲线只影响形状，且**永远不会被动画拉回**。\n" +
                 "· RootMotion = 劫持动画自带的根运动（上一版实现，保留用于对照）。\n" +
                 "   实测各段自带水平位移：Attack1 1.259 / Attack2 0.884 / Attack3 0.988 / Attack4 2.255 m。\n" +
                 "· None = 攻击期间完全不位移。")]
        [SerializeField] private AttackDisplacementMode attackDisplacement = AttackDisplacementMode.CodeLunge;

        [Tooltip("攻击根运动的缩放系数。1 = 原样；< 1 = 前冲更短（配合武器/节奏调）；> 1 = 更远。\n" +
                 "⚠️ 各段本来就不一样（Attack4 约为 Attack2 的 2.5 倍），要逐段精调就得按段配曲线，这里只给一个总缩放。")]
        [SerializeField] private float attackRootMotionScale = 1f;

        [Tooltip("⭐【强烈建议保持勾选】丢弃根运动里**向后 / 侧向**的分量，只保留沿角色正前方的推进。\n" +
                 "❓ 为什么需要它：这些攻击动画的根曲线是「冲出去 + 回撤」，逐帧累加 deltaPosition 会把回撤也放出来，\n" +
                 "   表现就是「冲一下又被拉回来」。本项目实测（RootT.z 首→尾）：\n" +
                 "     · Attack2 `2_atk_sword04` 净位移 **0**（冲到 0.875 m 又完全回到原处）\n" +
                 "     · Attack1 中途回退 **0.29 m**、Attack3 中途回退 **0.68 m**\n" +
                 "     · Attack4 单调（净 2.25 m），本身不拉回\n" +
                 "⛔ 取消勾选 = 完全照抄动画（含回撤），用于对照排查。")]
        [SerializeField] private bool attackRootMotionForwardOnly = true;

        [Tooltip("⭐ 允许曲线在**下降段**产生「向后退」的位移。\n" +
                 "✅ 开启（默认）= 曲线怎么画就怎么走：上升段前进、下降段后退。\n" +
                 "   例「踏步前斩 → 再后退半步」的画法：(0,0) → (0.35,1) → (0.7,0.55) → (1,0.55)\n" +
                 "   —— 前 35% 时间冲到最远，之后退回 45%，最后停住。\n" +
                 "⛔ 关闭 = 只前进不后退（下降段原地不动，角色停在最远处）。用于对照排查。\n" +
                 "⚠️ 只影响 CodeLunge 模式。")]
        [SerializeField] private bool allowLungeBackstep = true;

        [Tooltip("⭐ 垂直（Y 轴）位移的施加方式。\n" +
                 "· SkeletonRoot【默认，推荐】= 偏移**骨骼根 `Armature` 的 localPosition.y**（纯视觉）。\n" +
                 "   向下沉照样生效（不会被地面挡）、不影响碰撞与判定盒。\n" +
                 "· Ignore = 不施加垂直位移（等于关掉 Y 轴通道）。\n" +
                 "⚠️ **只有 CodeLunge 模式**会读这个开关（RootMotion / None / NativeRootMotion 都不走这里）。")]
        [SerializeField] private VerticalLungeMode lungeVerticalMode = VerticalLungeMode.SkeletonRoot;

        private CharacterController _cc;
        private Animator _anim;
        private PlayerAttack _attack;//只用来读"本段是否已出手"（方案 B 的转向窗口判据）
        private float speed;//移动速度（来自配置）

        /// <summary>本帧的「代码位移方向」（在 <see cref="Update"/> 里算好，供 <see cref="ComputeMoveDelta"/> 使用）。</summary>
        private Vector3 _moveDir;

        /// <summary>一次性警告开关：见 <see cref="ComputeMoveDelta"/>（避免每帧刷屏）。</summary>
        private bool _warnedRootMotionOff;

        /// <summary>
        /// ⭐ **只转向模式**（攻击 / 施法这类"定身动作"用）。
        /// 该模式下：**不产生水平位移**、**保留贴地下压**、**仍然可以限速转向**（用 attackTurnSpeed）。
        /// 由 <see cref="PlayerFSM"/> 在进入 / 离开攻击状态时开合。
        /// ⚠️ **必须在离开动作时关掉**（见 PlayerFSM 的 `_exit[Attack]`），否则之后只有朝向没有位移。
        /// </summary>
        private bool _facingOnly;

        // ==================== 代码驱动前冲（CodeLunge）的运行时状态 ====================

        /// <summary>本段**已完成**的**前向（Z）**位移（米）。与「目标累计进度」取差量用，保证总位移精确。</summary>
        private float _lungeDoneZ;

        /// <summary>本段**已完成**的**横向（X）**位移（米）。口径同 <see cref="_lungeDoneZ"/>。</summary>
        private float _lungeDoneX;

        /// <summary>
        /// 上一次见到的段编号（见 <see cref="PlayerAttack.SegmentId"/>）。
        /// 编号一变 = 换段 → 前冲进度**归零**，每段各自从头算。
        /// </summary>
        private int _lungeSegmentId = -1;

        // ==================== 垂直（Y 轴）位移：纯视觉通道 ====================

        /// <summary>
        /// **骨骼根**（本工程 = `Armature`，即整副骨架挂在玩家根下的那一级）。
        /// ⚠️ 垂直位移只改它的 <c>localPosition.y</c>：这是**视觉**位移，
        ///    不碰 `CharacterController`，所以既不会被地面挡、也不影响碰撞/判定盒。
        /// </summary>
        private Transform _skeletonRoot;

        /// <summary>骨骼根的**原始** localPosition（每次施加偏移都基于它，避免误差累积）。</summary>
        private Vector3 _skeletonRootBase;

        /// <summary>当前已施加的垂直偏移（米）。相同则跳过写入。</summary>
        private float _verticalOffset;

        private void Awake()
        {
            speed = config.moveSpeed;
            _anim = GetComponent<Animator>();
            _cc = GetComponent<CharacterController>();
            _attack = GetComponent<PlayerAttack>();//可能为 null（只影响方案 B 的窗口判据，见 CanTurnNow）

            //⭐ 这一项是**两种位移模式共同的依赖**（不只是 RootMotion 模式）：
            //   ① 本类实现了 `OnAnimatorMove` ⇒ Animator 把位移的**写入权**交给我们
            //      （它不会自己写 transform，也就不会绕过 CharacterController 穿墙）；
            //   ② 该回调的触发与「根运动是否开启」有关 —— 关掉根运动时不保证它还会被调用。
            //      保险起见无论用哪种位移模式，这个勾都保持打开（关掉的风险是角色彻底动不了）。
            if (_anim != null && !_anim.applyRootMotion)
            {
                Debug.LogWarning("[PlayerMotor] Animator 的 Apply Root Motion 未勾选 —— 位移回调（OnAnimatorMove）可能不再触发，角色将无法移动。"
                               + "本组件已自动把它打开；若你确实不想让动画影响位移，请把 `attackDisplacement` 设为 CodeLunge 或 None，而不是关这里。", this);
                _anim.applyRootMotion = true;
            }

            //⭐ 本类在**唯一写入者**的前提下，位移依赖 `OnAnimatorMove` **每帧都被调用**。
            //   而 Animator 被"剔除"（Culling）跳过更新时，这个回调就不会被调用 ⇒
            //   角色会**时不时移不动**（典型现象：把镜头转开、角色跑出视野后卡住）。
            //   玩家只有一个角色，锁定 AlwaysAnimate 最省心。
            if (_anim != null && _anim.cullingMode != AnimatorCullingMode.AlwaysAnimate)
                _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            CacheSkeletonRoot();
        }

        /// <summary>
        /// 缓存**骨骼根** —— 垂直（Y 轴）位移的施加对象。
        ///
        /// 做法：从 Hips 骨骼一路往上，找到「父物体就是玩家根」的那一级（本工程叫 `Armature`）。
        /// 为什么不用 `transform` 本身：那是**物理体**的位置，动它要走 `CharacterController`
        /// （向下会被地面顶住）；改骨骼根则纯视觉、想看多低就看多低。
        ///
        /// ⚠️ 找不到就放弃该通道（打一条警告），不影响 Z/X 两个通道。
        /// </summary>
        private void CacheSkeletonRoot()
        {
            _skeletonRoot = null;
            if (_anim == null) return;

            if (!_anim.isHuman || _anim.avatar == null)
            {
                Debug.LogWarning("[PlayerMotor] Animator 不是 Humanoid（或缺 Avatar）⇒ 垂直（Y 轴）位移通道不可用，"
                               + "攻击将不会有下蹲/起伏。", this);
                return;
            }

            Transform hips = _anim.GetBoneTransform(HumanBodyBones.Hips);
            if (hips == null)
            {
                Debug.LogWarning("[PlayerMotor] 找不到 Hips 骨骼 ⇒ 垂直（Y 轴）位移通道不可用。", this);
                return;
            }

            //往上走到「父物体 = 玩家根」的那一级 —— 即整副骨架的根
            Transform t = hips;
            while (t.parent != null && t.parent != transform) t = t.parent;

            if (t == transform)
            {
                Debug.LogWarning("[PlayerMotor] 骨骼直接挂在玩家根下、找不到独立的骨骼根 ⇒ 垂直（Y 轴）位移通道不可用。", this);
                return;
            }

            _skeletonRoot = t;
            _skeletonRootBase = t.localPosition;
            _verticalOffset = 0f;
        }

        /// <summary>
        /// 施加垂直偏移（米，正 = 向上）。**唯一**写骨骼根 localPosition 的地方。
        ///
        /// ⚠️ 每次都用 <see cref="_skeletonRootBase"/> 重算（而不是 `+=`），避免浮点误差累积。
        /// ⚠️ <paramref name="y"/> 为 0 时会把骨骼根**还原**成原样，所以「收招」必须调一次。
        /// </summary>
        private void SetSkeletonVerticalOffset(float y)
        {
            if (_skeletonRoot == null) return;
            if (_verticalOffset == y) return;

            _verticalOffset = y;
            Vector3 lp = _skeletonRootBase;
            lp.y += y;
            _skeletonRoot.localPosition = lp;
        }

        private void OnEnable()
        {
            //恢复"根运动数据可用"（被禁用期间会被本类的 OnDisable 关掉，原因见那里）
            if (_anim != null) _anim.applyRootMotion = true;
        }

        /// <summary>
        /// ⚠️ **这一步是方案 B 的关键**：本组件被 <c>enabled = false</c>（冲刺 / 受击 / 施法 / 死亡）时，
        /// Unity **不会**再调 <see cref="OnAnimatorMove"/> ⇒ Animator 会**恢复自动应用根运动**。
        /// 那时位移本该只由代码（或完全不动）驱动，不关掉就会出现两个后果：
        /// <list type="number">
        ///   <item>冲刺 = 代码位移 4 m **加上** `roll_front` 自带的 **4.836 m**（实测），一冲 8.8 m；</item>
        ///   <item>根运动是**直接写 transform**，**绕过 `CharacterController`** ⇒ 又变回"冲刺穿墙"（T27）。</item>
        /// </list>
        /// </summary>
        private void OnDisable()
        {
            //⚠️ 「原生根运动」模式**必须保持 applyRootMotion 开着** —— 那个模式的位移就是靠
            //   Animator 自己应用根运动实现的，关掉等于把位移一起关了。
            //   其余模式才需要在禁用时关掉它（否则冲刺会额外叠加 4.8 m 根运动、且绕过碰撞体）。
            if (_anim != null && attackDisplacement != AttackDisplacementMode.NativeRootMotion)
                _anim.applyRootMotion = false;

            //⭐ 兜底：若在攻击中途被禁用（受击 / 冲刺 / 死亡打断），垂直偏移会**留在骨骼根上**
            //   （表现为角色一直半蹲着）→ 这里还原。
            SetSkeletonVerticalOffset(0f);
        }

        private void Update()
        {
            //⚠️ 防御：退出 Play 模式 / 卸载场景时 `InputService` 可能**先一步**被销毁
            //   （它在 `OnDestroy` 里把 `Instance` 清成 null），而本组件的 `Update` 可能还会跑一帧 ——
            //   不判空就会在控制台留下一条 NullReferenceException（改前就存在，只是容易被忽略）。
            var input = InputService.Instance;
            if (input == null) return;

            Vector2 axis = input.Move;
            bool hasInput = axis.sqrMagnitude > 0.01f;

            Vector3 dir = CameraRelative(axis);
            _moveDir = hasInput ? dir : Vector3.zero;//OnAnimatorMove 里要用

            if (_facingOnly)
            {
                //⭐ 攻击中：**不产生代码位移**（位移改由动画根运动提供，见 ComputeMoveDelta），
                //   但保留贴地下压 + 允许**限速**转向
                if (hasInput && CanTurnNow()) Face(dir, attackTurnSpeed);

                //⚠️ 刻意**不写** "speed" 动画参数：现在攻击期间该参数保持进入攻击前的水平，
                //   Attack→Locomotion 的 0.25 s 混合期才能正确混向 Run。
                //   若在这里写 0，会混向 Idle —— 表现为"跑动中砍完一刀先站一下"。
            }
            else
            {
                //朝向：转向移动方向（不是转向相机方向）
                if (hasInput) Face(dir, turnSpeed);

                //驱动动画混合树
                _anim.SetFloat("speed", hasInput ? speed : 0f, 0.15f, Time.deltaTime);
            }

            //⭐ 兜底：没有 Animator / 没挂控制器时 `OnAnimatorMove` 不会被调用，
            //   位移就没人执行了（角色彻底动不了）→ 这时退回在 Update 里走同一条计算。
            //   正常情况（有控制器）**只有 OnAnimatorMove 那个写入者**，不会重复位移。
            if (!HijackActive)
                _cc.Move(ComputeMoveDelta() + Vector3.down * (groundStickSpeed * Time.deltaTime));
        }

        private void Start()
        {
            if (_camera == null && Camera.main != null)
                _camera = Camera.main.transform;
        }

        /// <summary>
        /// 本帧的**位移增量**（不含贴地下压）—— ⭐ **唯一的位移计算出口**。
        ///
        /// · 攻击中（<see cref="_facingOnly"/>）：取**动画的根运动** <c>deltaPosition</c>（动作自带的前冲）。
        /// · 平时：取代码位移（相机相对方向 × 移动速度）。
        ///
        /// ⚠️ <c>deltaPosition</c> 是**世界空间**的**本帧增量**（不是总位移），所以直接给 `CharacterController.Move`。
        /// ⚠️ Y 分量一律清零 —— 交给贴地下压/重力，否则根运动的抬升会把人顶到半空。
        /// </summary>
        /// <summary>
        /// 本帧的**位移增量**（不含贴地下压）—— ⭐ **唯一的位移计算出口**。
        ///
        /// · 攻击中（<see cref="_facingOnly"/>）：按 <see cref="attackDisplacement"/> 三选一
        ///   （代码前冲 / 根运动 / 不动）。
        /// · 平时：取代码位移（相机相对方向 × 移动速度）。
        /// </summary>
        private Vector3 ComputeMoveDelta()
        {
            if (!_facingOnly)
            {
                //⚠️ 非攻击时**必须**把垂直偏移还原，否则"攻击中沉下去、收招后一直半蹲着"。
                SetSkeletonVerticalOffset(0f);
                return _moveDir * (speed * Time.deltaTime);
            }

            switch (attackDisplacement)
            {
                case AttackDisplacementMode.None:
                    SetSkeletonVerticalOffset(0f);
                    return Vector3.zero;

                case AttackDisplacementMode.RootMotion:
                    SetSkeletonVerticalOffset(0f);
                    return ComputeRootMotionDelta();

                default:
                    return ComputeCodeLunge();
            }
        }

        /// <summary>
        /// ⭐ **代码驱动的三轴位移**：前冲（Z） + 横摆（X） + 起伏（Y）。（2026-10-07 由单轴扩展而来）
        ///
        /// 三个通道**共用同一条时间轴**（<c>lungeDuration</c>），但施加方式不同：
        /// <list type="bullet">
        ///   <item><b>Z 前向</b> → <c>transform.forward</c>，走 <c>CharacterController</c>（会被墙挡，正确）；</item>
        ///   <item><b>X 横向</b> → <c>transform.right</c>，同样走 <c>CharacterController</c>（会被墙挡，正确）；</item>
        ///   <item><b>Y 垂直</b> → 偏移**骨骼根**，**纯视觉**（见 <see cref="VerticalLungeMode"/>）。</item>
        /// </list>
        ///
        /// 三个通道都用**「目标位移 − 已完成量」取差量**，而不是"每帧给一个速度"：
        /// <code>
        ///   目标位移 = 曲线(本段已过秒数)        ← 横轴秒、纵轴米，曲线值就是位移量
        ///   本帧增量 = 目标位移 − 已走完的量
        /// </code>
        /// 三个好处：
        /// <list type="number">
        ///   <item><b>位置精确</b> —— 任一瞬间的位置就是曲线的读数，不受帧率影响（掉帧也不会走偏）；</item>
        ///   <item><b>与动画逐帧对齐</b> —— 曲线照抄逐帧表画出来，角色就走得和动画一模一样；</item>
        ///   <item><b>不会莫名抖动</b> —— 差量小于 0.1 mm 就当 0，避免浮点噪声变成高频抖动。</item>
        /// </list>
        ///
        /// ⚠️ 方向取 <c>transform.forward</c> / <c>transform.right</c> —— 与方案 B 的转向窗口天然对齐：
        ///    出手后朝向已锁定，所以"这一刀的方向"在出手那一刻就确定了，不会边走边飘。
        ///
        /// ⚠️ 三个通道**互相独立**：某段没配 Z 也照样能有 X / Y 位移，反之亦然。
        /// </summary>
        private Vector3 ComputeCodeLunge()
        {
            if (_attack == null) { SetSkeletonVerticalOffset(0f); return Vector3.zero; }

            WeaponConfig weapon = _attack.Weapon;
            if (weapon == null) { SetSkeletonVerticalOffset(0f); return Vector3.zero; }

            //换段 → 进度归零（用自增编号判断；不能用 _lastAttackTime，它在命中帧也会被刷新）
            int seg = _attack.SegmentId;
            if (seg != _lungeSegmentId)
            {
                _lungeSegmentId = seg;
                _lungeDoneZ = 0f;
                _lungeDoneX = 0f;
            }

            int idx = _attack.ComboIndex;

            //⭐ 时间轴 = **本段已过的秒数**（三条曲线的横轴都是「秒」），并钳到本段时长内。
            //   超过时长后曲线不再外推 ⇒ 位移自然冻结在终值，不会越滑越远。
            float duration = weapon.GetLungeDuration(idx);
            float t = Mathf.Clamp(Time.time - _attack.SegmentStartTime, 0f, duration);

            // ---------------- X 轴：横向（参与碰撞） ----------------
            //⭐ 曲线的纵轴就是「米」，**允许负值**（左右摇摆本来就要两边走）。
            float xTarget = LungeTargetAt(weapon.GetLungeLateralCurve(idx), weapon.GetLungeLateral(idx), t, duration);
            float xDelta = xTarget - _lungeDoneX;
            _lungeDoneX = xTarget;//跟随目标：曲线下降时它也跟着降，下一帧的差量才算得对
            if (Mathf.Abs(xDelta) < 0.0001f) xDelta = 0f;

            // ---------------- Y 轴：垂直（纯视觉，偏移骨骼根） ----------------
            //⚠️ 刻意**不走** CharacterController：向下会被地面顶住、向上会被贴地下压拉回，两头都白做。
            float yTarget = 0f;
            if (lungeVerticalMode == VerticalLungeMode.SkeletonRoot)
                yTarget = LungeTargetAt(weapon.GetLungeVerticalCurve(idx), weapon.GetLungeVertical(idx), t, duration);
            SetSkeletonVerticalOffset(yTarget);

            // ---------------- Z 轴：前向（参与碰撞） ----------------
            float zTarget = LungeTargetAt(weapon.GetLungeCurve(idx), weapon.GetLungeDistance(idx), t, duration);
            float zDelta = zTarget - _lungeDoneZ;
            _lungeDoneZ = zTarget;//跟随目标：曲线下降时它也跟着降，下一帧的差量才算得对

            //⚠️ **zDelta 可以为负** —— 那是曲线在下降，语义就是「向后退」。
            //   例：想表达「踏步前斩 → 再后退半步」，把曲线画成
            //   (0,0) → (0.35,1) → (0.7,0.55) → (1,0.55)：
            //   前 35% 时间冲到最远，之后退回 45%，最后停住。
            //
            //   ⚠️ 2026-10-07 修正：原实现无条件 `if (delta <= 0f) return Vector3.zero;`，
            //      把所有下降段都变成"原地不动" ⇒ 曲线里画了后退也不生效、角色会**卡在峰值处**。
            //      当时加它是为了防「冲出去又被拉回」，但那条规则不该由代码一刀切 ——
            //      该由**曲线**表达意图（不想后退就画一条单调曲线）。
            //      需要旧行为时把 `allowLungeBackstep` 取消勾选。
            if (!allowLungeBackstep && zDelta < 0f) zDelta = 0f;
            if (Mathf.Abs(zDelta) < 0.0001f) zDelta = 0f;

            return transform.forward * zDelta + transform.right * xDelta;
        }

        /// <summary>
        /// 取该段在 <paramref name="t"/> **秒**时的**目标位移**（米）。
        ///
        /// ⭐ 优先用**曲线**：横轴 = 秒、纵轴 = 米，**曲线的值就是位移量**（不再乘任何系数）。
        /// ⛔ 没画曲线时回落到「<paramref name="amplitude"/> × <see cref="WeaponConfig.DefaultLungeCurve"/>」——
        ///    那条兜底曲线是 0~1 的归一化进度，所以要先把时间归一化（<c>t / duration</c>）。
        /// </summary>
        private static float LungeTargetAt(AnimationCurve curve, float amplitude, float t, float duration)
        {
            if (curve != null && curve.length > 0) return curve.Evaluate(t);

            if (amplitude <= 0.0001f) return 0f;
            float tn = duration > 0.01f ? Mathf.Clamp01(t / duration) : 0f;
            return amplitude * WeaponConfig.DefaultLungeCurve.Evaluate(tn);
        }

        /// <summary>
        /// **根运动分支**（上一版实现，保留用于对照）：直接把动画的 <c>deltaPosition</c> 当位移。
        ///
        /// ⚠️ <c>deltaPosition</c> 是**世界空间**的**本帧增量**（不是总位移），所以能直接喂 `CharacterController.Move`。
        /// ⚠️ Y 分量一律清零 —— 交给贴地下压 / 重力，否则根运动的抬升会把人顶到半空。
        /// ⚠️ 这些攻击动画的根曲线是「冲出去 + 回撤」，见 <see cref="attackRootMotionForwardOnly"/>。
        /// </summary>
        private Vector3 ComputeRootMotionDelta()
        {
            if (_anim == null) return Vector3.zero;

            if (!_anim.applyRootMotion && !_warnedRootMotionOff)
            {
                _warnedRootMotionOff = true;
                Debug.LogWarning("[PlayerMotor] attackDisplacement = RootMotion，但 Animator 的 Apply Root Motion 被关掉了 —— "
                               + "deltaPosition 恒为 0，攻击将**完全没有位移**。", this);
            }

            Vector3 rm = _anim.deltaPosition;
            rm.y = 0f;

            if (attackRootMotionForwardOnly)
            {
                //⭐ 只保留「沿角色正前方」的推进量：
                //   ① 向后的帧直接丢弃 —— 否则动画的**回撤**会把角色拖回来（本项目 Attack2 净位移为 0、
                //      Attack1/3 中途各有 0.29 / 0.68 m 回退，实测）；
                //   ② 侧向分量也一并丢掉（用投影而不是原向量），避免前冲时横向飘。
                float forward = Vector3.Dot(rm, transform.forward);
                if (forward <= 0f) return Vector3.zero;
                return transform.forward * (forward * attackRootMotionScale);
            }

            return rm * attackRootMotionScale;
        }

        /// <summary>
        /// ⭐ **劫持根运动的实现**（方案 B 的核心，一行代码都没有"应用根运动"的味道，作用却在这里）：
        ///
        /// **只要本组件实现了这个方法，Animator 就 <b>不再</b> 自己把根运动写进 transform**
        /// （Unity 的行为：谁实现 `OnAnimatorMove`，位移的"执行权"就归谁）。
        /// 于是位移权 100% 收归本类 —— 满足「位置只有一个写入者」，
        /// 同时 `deltaPosition` 仍然可读（因为 `applyRootMotion` 是开的），
        /// 我们就能**选择性地**用动画位移（只用在攻击段），并把 Y 掐掉、做缩放、过碰撞体。
        ///
        /// ⚠️ 少了这个方法的后果（就是"直接勾 Apply Root Motion"的做法）：
        ///    Animator 会用**直接写 transform** 的方式移动，绕过 `CharacterController` ⇒ 攻击前冲 / 冲刺都会**穿墙**。
        /// </summary>
        private void OnAnimatorMove()
        {
            if (_cc == null || !_cc.enabled) return;

            Vector3 stick = Vector3.down * (groundStickSpeed * Time.deltaTime);
            _cc.Move(ComputeMoveDelta() + stick);
        }

        /// <summary>
        /// 劫持是否真的生效 —— 需要 Animator 存在、启用、且挂着控制器。
        /// 不满足时 <see cref="OnAnimatorMove"/> 不会被调用，此时由 <see cref="Update"/> 兜底位移（否则角色彻底动不了）。
        /// </summary>
        private bool HijackActive
        {
            get { return _anim != null && _anim.enabled && _anim.runtimeAnimatorController != null; }
        }

        /// <summary>
        /// 把输入(Vector2)换算成世界方向：**以相机为参照系**。
        /// axis.y = 前/后（屏幕上下），axis.x = 左/右（屏幕左右）。
        /// </summary>
        private Vector3 CameraRelative(Vector2 axis)
        {
            if (axis.sqrMagnitude <= 0.01f) return Vector3.zero;

            Vector3 fwd = Vector3.forward;
            Vector3 right = Vector3.right;

            if (_camera != null)
            {
                //相机前向投影到水平面 —— 这就是「屏幕上方」在世界里的方向
                fwd = _camera.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;

                right = _camera.right;
                right.y = 0f;
                right = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
            }

            return (fwd * axis.y + right * axis.x).normalized;
        }

        /// <summary>朝给定方向限速转身。<paramref name="degPerSec"/> = 本次转身的最大角速度（度/秒）。</summary>
        private void Face(Vector3 dir, float degPerSec)
        {
            if (dir.sqrMagnitude <= 0.001f) return;

            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, degPerSec * Time.deltaTime);
        }

        /// <summary>
        /// 当前是否允许转向（只在"只转向"模式下被问）。
        ///
        /// · 【方案 B】<see cref="gateTurnByHitFrame"/> = true → 读 <see cref="PlayerAttack.CanTurn"/>：
        ///   **命中帧之前可以转，出手之后锁定**。
        /// · 【方案 A】= false → 整段都可转。
        ///
        /// ⚠️ 防御性放行：`PlayerAttack` 为空、或某段动画忘挂 `OnAttackHit` 事件时，**一律允许转向** ——
        ///   宁可"松"，也不要让玩家突然转不动（那种 bug 很难归因）。
        /// </summary>
        private bool CanTurnNow()
        {
            if (!gateTurnByHitFrame) return true;
            if (_attack == null) return true;
            return _attack.CanTurn;
        }

        // ==================== 对外的位移入口（供其它组件发起一次性位移） ====================
        //
        // ⭐ **位置只有一个写入者**：本类是玩家身上唯一调 CharacterController.Move 的地方。
        //    其它组件（目前是 PlayerDash）要位移时**必须**走下面这两个方法，
        //    ❌ 不要自己写 `transform.position +=` —— 那是绕过碰撞体的**直接改坐标**，
        //       会穿墙、会陷进碰撞体里。2026-10-06 修的"冲刺穿墙"就是这个问题。

        /// <summary>
        /// 是否使用「**原生根运动**」（Animator 自己应用位移）—— 见 <see cref="AttackDisplacementMode.NativeRootMotion"/>。
        ///
        /// ⚠️ 该模式下 <see cref="PlayerFSM"/> 会把本组件 <c>enabled = false</c>：**必须如此**，
        ///    因为只要本组件启用着，<see cref="OnAnimatorMove"/> 就会夺走位移的写入权。
        /// </summary>
        public bool UsesNativeRootMotion
        {
            get { return attackDisplacement == AttackDisplacementMode.NativeRootMotion; }
        }

        /// <summary>
        /// 开关 **只转向模式**（由 <see cref="PlayerFSM"/> 在攻击这类"定身动作"期间调用）。
        ///
        /// 开启后本组件**必须保持 enabled**：此时
        ///   · 不产生水平位移（但仍做贴地下压，所以不会离地）
        ///   · **仍然**按输入方向限速转身，角速度用 <c>attackTurnSpeed</c>（比一般移动慢）
        ///   · 是否"现在才允许转"由 <see cref="CanTurnNow"/> 决定（默认 = 方案 B 的转向窗口）
        ///
        /// ⚠️ **退出动作时一定要关掉**（`SetFacingOnly(false)`），否则角色之后只能转身、跑不动。
        /// ⚠️ 与 <c>enabled = false</c> 的区别：那是连贴地一起停掉（T1），本模式是"只停移动"。
        /// </summary>
        public void SetFacingOnly(bool value)
        {
            _facingOnly = value;

            //⭐ 离开动作时把垂直偏移**还原** —— 否则会出现"攻击中沉下去、收招后一直半蹲着"。
            //   （正常情况下 PlayerMotor 每帧都会走 ComputeMoveDelta 自动还原，这里是双保险：
            //     收招那一瞬间若本组件立刻被禁用，就不会再有下一帧了。）
            if (!value) SetSkeletonVerticalOffset(0f);
        }

        /// <summary>
        /// 纯水平位移（不贴地）。
        /// ⚠️ 必须在有 `CharacterController` 时调用；<paramref name="delta"/> 是**本帧位移量**（已含 deltaTime）。
        /// </summary>
        public void StepMove(Vector3 delta)
        {
            if (_cc == null || !_cc.enabled) return;
            _cc.Move(delta);
        }

        /// <summary>
        /// 水平位移 + 贴地下压 —— 冲刺 / 击退这类"高速位移"用。
        /// 贴地下压与 <see cref="Update"/> 里同一口径（<c>groundStickSpeed × deltaTime</c>），
        /// 保证冲刺期间也不会离地。
        /// </summary>
        public void StepMoveGrounded(Vector3 delta)
        {
            StepMove(delta + Vector3.down * (groundStickSpeed * Time.deltaTime));
        }
    }
}
