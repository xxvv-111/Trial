using Game.Core;
using Game.Data;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 玩家移动：读输入 → **按相机朝向换算"该转向哪里"** → 限速转向 → **朝角色正面位移** → 驱动动画混合树。
    ///
    /// ⚠️ **输入方向是「相对相机」的**（W = 屏幕上方）。相机可被鼠标旋转，
    ///    若用世界轴会变成「转视角后按 W 往斜里走」，手感直接崩。
    ///
    /// ⭐⭐ **位移方向 = 角色正面（`transform.forward`），不是输入方向**（2026-10-13 定案）。
    ///    输入方向只负责回答"朝哪儿转"。两者必须分开，是因为输入方向是**瞬时**的（相机一转就变），
    ///    而朝向要按 `turnSpeed` 慢慢转过去 —— 若位移直接跟输入，转身时就会出现
    ///    "身体朝前、人却在往后滑"的倒滑感。改走正面之后，转身变成走一道弧线。
    ///    ⚠️ 转弯半径 `r = v / ω`（ω = `turnSpeed` 的**弧度**值）。当前 720°/s 时：
    ///      走路 2 m/s → r ≈ 0.16 m；跑步 6 m/s → r ≈ 0.48 m。调大 `turnSpeed` 可让转身更干脆。
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
    /// ⭐⭐ **位移 100% 由代码驱动，与动画根运动彻底无关**（2026-10-12 定案）：
    ///    · 执行点 = <see cref="Update"/>，每帧**无条件**执行一次「<see cref="ComputeMoveDelta"/> + 贴地下压」；
    ///    · 位移来源 = 代码（移动取输入速度，攻击取 <c>WeaponConfig</c> 的三轴曲线）；
    ///    · ⛔ 本类**不再实现** <c>OnAnimatorMove</c>，`Apply Root Motion` 必须**关闭**（见 <see cref="Awake"/> 的防御检查）。
    ///
    ///    ❓ 关掉根运动为什么安全（2026-10-12 实测，Play 模式 + 手动 <c>Animator.Update</c> 逐帧采样）：
    ///      ① 这些 clip 的**整体水平位移全在 `RootT` 根曲线里，骨骼姿势并不含它**
    ///         —— 攻击段 RootT.z 净 0.965~2.25 m，而 Hips 的世界位置只动 0.098 m ⇒ 关掉后**不会滑步**；
    ///      ② 死亡动画的**倒地由骨骼姿势自己完成**（Hips 世界 Y 下沉 1.004 m），与根运动无关；
    ///      ③ `applyRootMotion` 取 true / false 时**骨骼姿势逐位相同** —— 它只决定「要不要把根位移写进 transform」。
    ///    ⚠️ 但**两件事必须一起做**：删掉 <c>OnAnimatorMove</c> 后若仍让 `applyRootMotion` 开着，
    ///      Unity 会**自动应用**根位移 ⇒ 跑动一圈前窜 3.18 m、冲刺 4.84 m，且绕过碰撞体穿墙。
    ///
    /// ✅ 已消除（T2）：玩家身上曾同时有 Rigidbody + CapsuleCollider + CharacterController，
    ///    现在只剩 CharacterController 一个。
    /// </summary>
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

        [Tooltip("⭐ 允许曲线在**下降段**产生「向后退」的位移。\n" +
                 "✅ 开启（默认）= 曲线怎么画就怎么走：上升段前进、下降段后退。\n" +
                 "   例「踏步前斩 → 再后退半步」的画法：(0,0) → (0.35,1) → (0.7,0.55) → (1,0.55)\n" +
                 "   —— 前 35% 时间冲到最远，之后退回 45%，最后停住。\n" +
                 "⛔ 关闭 = 只前进不后退（下降段原地不动，角色停在最远处）。用于对照排查。\n" +
                 "⚠️ 只影响攻击位移。")]
        [SerializeField] private bool allowLungeBackstep = true;

        [Tooltip("⭐ 垂直（Y 轴）位移的施加方式。\n" +
                 "· SkeletonRoot【默认，推荐】= 偏移**骨骼根 `Armature` 的 localPosition.y**（纯视觉）。\n" +
                 "   向下沉照样生效（不会被地面挡）、不影响碰撞与判定盒。\n" +
                 "· Ignore = 不施加垂直位移（等于关掉 Y 轴通道）。\n" +
                 "⚠️ 仅在攻击位移（<see cref=\"ComputeCodeLunge\"/>）里被读取。")]
        [SerializeField] private VerticalLungeMode lungeVerticalMode = VerticalLungeMode.SkeletonRoot;

        private CharacterController _cc;
        private Animator _anim;
        private PlayerAttack _attack;//只用来读"本段是否已出手"（方案 B 的转向窗口判据）
        private float walkSpeed;//走路速度（来自配置）
        private float runSpeed;//跑步速度（来自配置）

        /// <summary>本帧的「代码位移方向」（在 <see cref="Update"/> 里算好，供 <see cref="ComputeMoveDelta"/> 使用）。</summary>
        private Vector3 _moveDir;

        // ==================== 移动档位（走路 / 跑步）====================
        //
        // ⭐ 2026-10-13：移动从"单档"改成**双档**。
        //   档位 = 有输入 & 是否按住加速键，三选一：
        //     0          = 停下（无输入）
        //     walkSpeed  = 走路（有输入、没按加速键）
        //     runSpeed   = 跑步（有输入、按住加速键）
        //
        // ⚠️ 这两个值同时有两个用途，**必须一致**：
        //   ① 决定实际位移速度（本类的 `_cc.Move`）；
        //   ② 作为 Animator 混合树的输入参数（`speed`）—— 混合树的阈值就是 0 / walkSpeed / runSpeed。
        //   所以配置一改，混合树的阈值也要跟着改，否则动画会混在错误的档位上（例如阈值还是 0/1/5 时，
        //   走路会被混成"一半走路一半跑步"）。

        /// <summary>本帧的目标移动速度（米/秒）：0 = 停 / walkSpeed = 走路 / runSpeed = 跑步。</summary>
        private float _currentSpeed;

        /// <summary>本帧是否处于**跑步档**（有输入 且 按住加速键）。跳跃据此挑动画。</summary>
        private bool _isRunning;

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
            walkSpeed = config.walkSpeed;
            runSpeed = config.runSpeed;
            _anim = GetComponent<Animator>();
            _cc = GetComponent<CharacterController>();
            _attack = GetComponent<PlayerAttack>();//可能为 null（只影响方案 B 的窗口判据，见 CanTurnNow）

            //⭐ 位移已完全由代码驱动（见类注释）⇒ **必须关掉根运动**：
            //   本类不再实现 `OnAnimatorMove`，若 `applyRootMotion` 还开着，
            //   Unity 会把动画的根位移**自动写进 transform** ⇒ 跑动一圈前窜 3.18 m、冲刺 4.84 m，
            //   而且是"直接改 transform"、**绕过 CharacterController**（穿墙）。
            //   ⇒ 在 Awake 里做一道防御并把错误配置报出来（免得以后又被 Inspector 勾回去）。
            if (_anim != null && _anim.applyRootMotion)
            {
                Debug.LogWarning("[PlayerMotor] Animator 的 Apply Root Motion 被打开了 —— 本项目已改为**纯代码驱动位移**，"
                               + "根运动必须关闭（否则动画会自己把角色推着走、且绕过碰撞体）。已自动关闭；"
                               + "请把预制体上的该项也改成关闭。", this);
                _anim.applyRootMotion = false;
            }

            //⭐ Animator 被"剔除"（Culling）跳过更新时动画会停播（视觉上"角色不动了"）。
            //   ⚠️ 2026-10-12 起位移**不再依赖** Animator 回调 ⇒ 剔除**不会**再导致"角色移不动"；
            //   保留 AlwaysAnimate 只是为了动画本身的正确性。玩家只有一个角色，最省心。
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

        /// <summary>
        /// ⚠️ 只做一件事：**兜底还原垂直（Y 轴）偏移**。
        ///
        /// 若在攻击中途被禁用（受击 / 冲刺 / 死亡打断），<see cref="ComputeCodeLunge"/> 施加的垂直偏移会
        /// **留在骨骼根上**（表现为角色一直半蹲着）—— 这里还原。
        ///
        /// ℹ️ 2026-10-12 起这里**不再开合 `applyRootMotion`**：根运动已彻底不用
        ///   （`applyRootMotion` 恒为 false，见 <see cref="Awake"/>）；本组件被禁用时位移自然停
        ///   （<see cref="Update"/> 不跑），也就不需要再防"Animator 自动应用位移"。
        /// </summary>
        private void OnDisable()
        {
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

            //⭐ 档位（走路 / 跑步）—— 刻意放在 `_facingOnly` 判断**之前**统一计算：
            //   攻击中也要能知道"玩家此刻按不按加速键"，否则攻击中起跳会拿到过期的档位。
            //   · 无输入           → 0（停下）
            //   · 有输入、没按加速 → walkSpeed（走路）
            //   · 有输入、按住加速 → runSpeed（跑步）
            _isRunning = hasInput && input.SprintHeld;
            _currentSpeed = !hasInput ? 0f : (_isRunning ? runSpeed : walkSpeed);

            Vector3 dir = CameraRelative(axis);

            if (_facingOnly)
            {
                //⭐ 攻击中：**不产生「输入位移」**（本帧位移改由武器的三轴曲线提供，见 ComputeCodeLunge），
                //   但保留贴地下压 + 允许**限速**转向
                _moveDir = Vector3.zero;//位移归武器曲线管，这里只让字段有个确定值
                if (hasInput && CanTurnNow()) Face(dir, attackTurnSpeed);

                //⚠️ 刻意**不写** "speed" 动画参数：现在攻击期间该参数保持进入攻击前的水平，
                //   Attack→Locomotion 的 0.25 s 混合期才能正确混向 Run。
                //   若在这里写 0，会混向 Idle —— 表现为"跑动中砍完一刀先站一下"。
            }
            else
            {
                //⭐ ① 朝向：转向**输入方向**（相机相对），角速度上限 `turnSpeed`。
                if (hasInput) Face(dir, turnSpeed);

                //⭐⭐ ② 位移方向 = **转向之后的角色正面**，不再是输入方向。（2026-10-13 定案）
                //
                //   为什么必须改：输入方向是**瞬时**的（相机一转、方向键一变就立刻改），
                //   而朝向要按 `turnSpeed` 慢慢转过去。两者解耦 ⇒ 突然按反方向时，
                //   位移当帧就掉头、身体却还朝着原来那边 ⇒ "身体朝前、人却在往后滑"的倒滑感。
                //   取 `transform.forward` 之后，位移方向与朝向**永远是同一个**：
                //   转身时走一道弧线，而不是当帧掉头。
                //
                //   ⚠️ **顺序不能反**：必须在 `Face()` **之后**读 `transform.forward`，
                //      否则拿到的是"转身之前"的朝向，等于没改。
                //   ⚠️ 转弯半径 r = v / ω（ω = turnSpeed 的**弧度**值 = turnSpeed × π / 180）。
                //      当前 turnSpeed = 720°/s（ω ≈ 12.57 rad/s）时：
                //        走路 2 m/s → r ≈ 0.16 m；跑步 6 m/s → r ≈ 0.48 m。
                //      想让转身更干脆就调大 `turnSpeed`（半径与偏移反比缩小）。
                //   ℹ️ 攻击位移（`_facingOnly` 走武器曲线）与冲刺（`PlayerDash` 自带 forward）不受影响。
                _moveDir = hasInput ? transform.forward : Vector3.zero;

                //驱动动画混合树。⭐ 喂的是**实际速度**（0 / walkSpeed / runSpeed），
                //   与混合树阈值一一对应 ⇒ 走路就播走路、跑步就播跑步。
                _anim.SetFloat("speed", _currentSpeed, 0.15f, Time.deltaTime);
            }

            //⭐ **唯一的位移执行点**。必须放在本方法末尾：`_moveDir` 与转向都在上面算好了，
            //   这一帧的位移直接用它们，做到"算完就走、同帧一致"。
            //   ℹ️ 2026-10-12 之前这一步在 `OnAnimatorMove` 里（靠实现该回调夺走 Animator 的位移权）；
            //      现在改到 Update 里**无条件执行** ⇒ 位移与动画彻底解耦，不再依赖任何 Animator 回调。
            if (_cc != null && _cc.enabled)
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
        /// · 平时（Idle / Run / Hit / Special / Death）：取代码位移（**角色正面** × 移动速度）。
        ///   正面方向由 <see cref="Update"/> 里的 <c>Face()</c> 先转好（见类注释的"位移方向 = 角色正面"）。
        ///   ℹ️ Dash 不走这里 —— 它由 <c>PlayerDash.LateUpdate</c> 调 <see cref="StepMoveGrounded"/>。
        /// · 攻击中（<see cref="_facingOnly"/>）：走 <see cref="ComputeCodeLunge"/>（武器三轴曲线）。
        /// </summary>
        private Vector3 ComputeMoveDelta()
        {
            if (!_facingOnly)
            {
                //⚠️ 非攻击时**必须**把垂直偏移还原，否则"攻击中沉下去、收招后一直半蹲着"。
                SetSkeletonVerticalOffset(0f);

                //⭐ 速度取**本帧算好的档位**（`_currentSpeed`：0 / 走路 / 跑步），
                //   这样"松开加速键 → 当帧就降到走路速度"，不会有滞后一帧的顿挫。
                return _moveDir * (_currentSpeed * Time.deltaTime);
            }

            return ComputeCodeLunge();
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

        // ==================== 对外的移动档位查询（供 PlayerFSM / PlayerJump 用） ====================

        /// <summary>
        /// 本帧的目标移动速度（米/秒）：0 = 停 / <c>walkSpeed</c> = 走路 / <c>runSpeed</c> = 跑步。
        ///
        /// ⭐ 用途：<see cref="PlayerJump.BeginJump"/> 拿它当**起跳瞬间的水平速度**
        ///   （静止起跳 = 0 → 原地跳；走路起跳 = 走速；跑步起跳 = 跑速）。
        /// ⚠️ 与"实际位移了多少"不是一回事 —— 攻击中（`_facingOnly`）本值有档位但不产生输入位移。
        /// </summary>
        public float CurrentSpeed => _currentSpeed;

        /// <summary>本帧是否处于**跑步档**（有移动输入 且 按住加速键）。<see cref="PlayerFSM"/> 据此挑"站跳 / 跑跳"动画。</summary>
        public bool IsRunning => _isRunning;

        // ==================== 对外的位移入口（供其它组件发起一次性位移） ====================
        //
        // ⭐ **位置只有一个写入者**：本类是玩家身上唯一调 CharacterController.Move 的地方。
        //    其它组件（目前是 PlayerDash）要位移时**必须**走下面这两个方法，
        //    ❌ 不要自己写 `transform.position +=` —— 那是绕过碰撞体的**直接改坐标**，
        //       会穿墙、会陷进碰撞体里。2026-10-06 修的"冲刺穿墙"就是这个问题。

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
        /// 纯水平位移（不贴地），返回 <see cref="CollisionFlags"/>（跳跃用它检测落地）。
        /// ⚠️ 必须在有 `CharacterController` 时调用；<paramref name="delta"/> 是**本帧位移量**（已含 deltaTime）。
        /// 返回值可以忽略 —— <see cref="PlayerDash"/> 就是只管位移不看碰撞。
        /// </summary>
        public CollisionFlags StepMove(Vector3 delta)
        {
            if (_cc == null || !_cc.enabled) return 0;
            return _cc.Move(delta);
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

        /// <summary>
        /// **只做贴地下压、不产生任何水平位移** —— 落地后摇这类"定身、但仍要踩住地面"的场合用。
        ///
        /// ⭐ 为什么非要有这个方法：调用方（<see cref="PlayerJump"/> 的落地后摇）期间本组件是 **disabled** 的，
        ///    <see cref="Update"/> 不跑 ⇒ 全工程唯一那处贴地下压也停了 ⇒ 角色会浮在原地
        ///    （斜坡、台阶边缘上尤其明显）。这里把贴地动作单独暴露出来，让调用方代为执行。
        ///
        /// ⚠️ 与 <see cref="SetFacingOnly"/> 的区别：那个是"不移动、但**仍可**用 <c>attackTurnSpeed</c> 转向"；
        ///    本方法只管贴地，**朝向一动不动**（后摇要的是完全定身）。
        /// ⚠️ 与 <see cref="StepMoveGrounded"/> 的区别：那个是"水平位移 + 贴地"，本方法是"0 水平位移 + 贴地"。
        /// </summary>
        public void StepStickToGround()
        {
            StepMove(Vector3.down * (groundStickSpeed * Time.deltaTime));
        }
    }
}
