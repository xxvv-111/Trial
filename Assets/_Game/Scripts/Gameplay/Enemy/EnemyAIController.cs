using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 敌人 AI 共用层（M3.2）。在纯 C# 的 <see cref="EnemyStateMachine{TState}"/> 之上补齐
    /// **两种敌人都会用到的服务**：
    /// <list type="bullet">
    ///   <item>组件与数值：<see cref="Health"/> / <see cref="Anim"/> / <see cref="Hitboxes"/> /
    ///         <see cref="Perception"/> / <see cref="Loco"/></item>
    ///   <item>目标：找玩家（tag <c>Player</c>）、距离查询</item>
    ///   <item>受击反应：闪白 → 打断（小怪）/ 只闪白（Boss）→ 转交子类的 <see cref="OnInterrupted"/></item>
    ///   <item>动画触发：**先查参数是否存在**再 Set，避免控制器缺参数时控制台刷屏
    ///         （M3.6 之前敌人动画只有 2 个状态、且参数表是空的，现在是 5 个状态 + 4 个参数）</item>
    ///   <item>移动 → 动画：每帧把移动层的实时速度写给 <c>Speed</c>，动画状态机据此切「待机 ↔ 移动」</item>
    /// </list>
    ///
    /// ⚠️ 本类**刻意不是泛型**：
    ///    Unity 的序列化不支持泛型组件，泛型基类里的 <c>[SerializeField]</c>（尤其 <c>_config</c>）
    ///    有丢引用导致敌人完全不动、且 Inspector 里看不到字段的风险。
    ///    所以强类型状态机由子类自己持有（见 <see cref="EnemyMeleeAI"/> 的 <c>_fsm</c>），
    ///    基类只通过 <see cref="EnemyFsmBase"/> 这个非泛型外壳驱动它（Tick / IsLocked）。
    ///
    /// 子类只需要「建表 + Attach 状态机 + Start」，这就是 GDD §6.4 要的「加第三种敌人几乎只是纯配置」。
    /// ⚠️ 子类覆写 <c>Awake</c> / <c>Start</c> / <c>OnEnable</c> / <c>OnDisable</c> 时**务必先调 base**。
    /// </summary>
    public abstract class EnemyAIController : MonoBehaviour
    {
        [Header("敌人配置")]
        [Tooltip("数值来源。留空会导致敌人不动、不追玩家 —— 换预制体后请确认这里没丢。")]
        [SerializeField] protected EnemyAIConfig _config;

        [Tooltip("勾上后每次状态切换都在 Console 打印（排查「敌人为什么不动」时很有用）。")]
        [SerializeField] protected bool _logTransitions;

        // ==================== 组件 ====================

        protected EnemyHealth Health { get; private set; }
        protected Animator Anim { get; private set; }
        protected HitboxController Hitboxes { get; private set; }
        protected EnemyPerception Perception { get; private set; }
        protected EnemyLocomotion Loco { get; private set; }

        /// <summary>子类持有的状态机（非泛型外壳，由 <see cref="AttachStateMachine"/> 传入）。</summary>
        protected EnemyFsmBase Fsm { get; private set; }

        /// <summary>状态机是否已锁死（= 已进死状态）。</summary>
        protected bool IsStateLocked { get { return Fsm != null && Fsm.IsLocked; } }

        /// <summary>当前目标（玩家）。找不到时每 1 秒重试一次。</summary>
        protected Transform Target { get; private set; }

        /// <summary>目标的玩家状态机（判断「无敌 / 已死」用）。可能为 null。</summary>
        protected PlayerFSM TargetFsm { get; private set; }

        public EnemyAIConfig Config { get { return _config; } }

        // ==================== 受击闪白 ====================

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("受击反馈")]
        [Tooltip("受击瞬间叠加的颜色。做法是临时把渲染器的材质换成这个颜色的克隆，闪完立刻换回原材质。")]
        [SerializeField] protected Color _hitFlashColor = new Color(1f, 0.28f, 0.22f, 1f);

        [Tooltip("闪白时把自发光拉亮多少倍（材质没有 _EmissionColor 时忽略）。0 = 不加发光。")]
        [SerializeField] protected float _hitFlashEmission = 2f;

        // ⚠️⚠️ 为什么**不能**用 MaterialPropertyBlock（这里踩过坑，别改回去）：
        //   本工程敌人的材质用的是一套 ASE（Amplify Shader Editor）生成的 URP shader
        //   （`Polygonmaker/SkinCutout DoubleSided` —— 注意它**没有 `_BaseColor`**，只有 `_Color` / `_ColorR/G/B`）。
        //   实测：只要给这些渲染器挂上**非空** MaterialPropertyBlock，**整个模型就会明显变暗**；
        //   而且 `_BaseColor = 红` 与 `_BaseColor = 白` 出来的画面**逐字节完全相同**
        //   ⇒ 参与渲染的不是颜色值，而是"挂 MPB 这个动作本身"（它改变了 URP / SRP Batcher 的常量缓冲绑定）。
        //   摘掉 MPB（`SetPropertyBlock(null)`）后画面**逐字节复原**。
        //   换成"临时替换 sharedMaterials 为红色克隆材质"后：实测能正确变红，且还原后与原样逐字节一致。
        //
        // ⚠️ 代价：闪白期间那几个渲染器会掉出 SRP Batcher（0.15 s，可接受）；
        //   克隆材质**只在 Awake 建一次**并缓存，不是每次受击都 new。
        private Renderer[] _flashRens;
        private Material[][] _flashNormalMats;
        private Material[][] _flashHitMats;
        private readonly List<Material> _flashClones = new List<Material>();
        private float _flashTimer;

        private void InitFlash()
        {
            Renderer[] found = GetComponentsInChildren<Renderer>(true);

            // 同一个源材质（例如所有部件共用的 Wraith_4.mat）只克隆一次
            var cloneOf = new Dictionary<Material, Material>();
            var rens = new List<Renderer>();
            var normals = new List<Material[]>();
            var hits = new List<Material[]>();

            for (int i = 0; i < found.Length; i++)
            {
                Renderer r = found[i];
                if (r == null) continue;

                Material[] normal = r.sharedMaterials;//⚠️ 这个 getter 每次都会分配新数组，所以只在这里取一次
                if (normal == null || normal.Length == 0) continue;

                var hit = new Material[normal.Length];
                bool any = false;
                for (int k = 0; k < normal.Length; k++)
                {
                    Material src = normal[k];
                    if (src == null) { hit[k] = null; continue; }

                    Material clone;
                    if (!cloneOf.TryGetValue(src, out clone))
                    {
                        clone = MakeFlashMaterial(src);
                        cloneOf[src] = clone;
                        if (clone != null) _flashClones.Add(clone);
                    }

                    if (clone == null) { hit[k] = src; continue; }//没有可用基色属性 → 这个材质不闪
                    hit[k] = clone;
                    any = true;
                }
                if (!any) continue;

                rens.Add(r);
                normals.Add(normal);
                hits.Add(hit);
            }

            _flashRens = rens.ToArray();
            _flashNormalMats = normals.ToArray();
            _flashHitMats = hits.ToArray();
        }

        /// <summary>
        /// 造一份"受击色"材质克隆。**没有可用基色属性的材质返回 null** ——
        /// 宁可不闪，也不要挂 MPB 把整个模型搞坏（见上方长注释）。
        /// </summary>
        private Material MakeFlashMaterial(Material src)
        {
            string baseProp = null;
            if (src.HasProperty(BaseColorId)) baseProp = "_BaseColor";   // URP/Lit 等
            else if (src.HasProperty(ColorId)) baseProp = "_Color";      // Polygonmaker 那套 ASE shader
            if (baseProp == null) return null;

            var clone = new Material(src);
            clone.name = src.name + "_HitFlash";
            clone.SetColor(Shader.PropertyToID(baseProp), _hitFlashColor);

            if (_hitFlashEmission > 0f && clone.HasProperty(EmissionColorId))
                clone.SetColor(EmissionColorId, _hitFlashColor * _hitFlashEmission);

            return clone;
        }

        /// <summary>
        /// 受击闪白：把所有渲染器的材质换成"受击色克隆"。
        /// ⚠️ 覆盖**全部**渲染器（早期版本只改了 `GetComponentInChildren` 的第一个，模型分件多 ⇒ 只有一小块在闪）。
        /// </summary>
        protected void FlashHit()
        {
            if (_flashRens == null) return;

            for (int i = 0; i < _flashRens.Length; i++)
                if (_flashRens[i] != null) _flashRens[i].sharedMaterials = _flashHitMats[i];

            _flashTimer = FlashTime;
        }

        private void RestoreColor()
        {
            if (_flashRens == null) return;

            for (int i = 0; i < _flashRens.Length; i++)
                if (_flashRens[i] != null && _flashNormalMats[i] != null)
                    _flashRens[i].sharedMaterials = _flashNormalMats[i];
        }

        // ==================== 其它 ====================

        private float _retargetTimer;//找玩家的重试计时
        private HashSet<int> _animTriggers;//Animator 上确实存在的 Trigger（按 hash 存）
        private HashSet<int> _animBools;
        private HashSet<int> _animFloats;
        private readonly HashSet<int> _warnedParams = new HashSet<int>();//同一个参数只警告一次

        // ==================== 数值（统一从 EnemyAIConfig 读，调参只在一处） ====================

        protected float AggroRange { get { return _config != null ? _config.aggroRange : 8f; } }
        protected float AttackRange { get { return _config != null ? _config.attackRange : 1f; } }
        protected float MoveSpeed { get { return _config != null ? _config.moveSpeed : 3f; } }
        protected float WindupTime { get { return _config != null ? _config.windupTime : 1f; } }
        protected float RecoverTime { get { return _config != null ? _config.recoverTime : 1.5f; } }
        protected int Damage { get { return _config != null ? _config.damage : 10; } }
        protected float HitStunTime { get { return _config != null ? _config.hitStunTime : 0.4f; } }
        protected float KnockBackDistance { get { return _config != null ? _config.knockBackDistance : 0.4f; } }
        protected float FlashTime { get { return _config != null ? _config.flashTime : 0.15f; } }
        protected bool CanBeInterrupted { get { return _config == null || _config.canBeInterrupted; } }
        protected float LoseTargetTime { get { return _config != null ? _config.loseTargetTime : 0f; } }
        protected float AlertTime { get { return _config != null ? _config.alertTime : 0.35f; } }
        protected float RepositionDistance { get { return _config != null ? _config.repositionDistance : 1.5f; } }

        // ---- 远程（法师）专用（M3.5）----
        protected float ComfortDistance { get { return _config != null ? _config.comfortDistance : 6f; } }
        protected float CastRangeMin { get { return _config != null ? _config.castRangeMin : 4f; } }
        protected float CastCooldown { get { return _config != null ? _config.castCooldown : 2.2f; } }
        protected float ProjectileSpeed { get { return _config != null ? _config.projectileSpeed : 14f; } }

        // ==================== 生命周期 ====================

        protected virtual void Awake()
        {
            if (_config == null)
                Debug.LogError("[" + GetType().Name + "] Enemy AIConfig 未赋值 —— 敌人不会有任何行为。" +
                               "请在预制体（Boxer / Gunner）上把 Config 字段指到对应的 EnemyAIConfig 资产。", this);

            Health = GetComponent<EnemyHealth>();
            Anim = GetComponent<Animator>();
            Hitboxes = GetComponent<HitboxController>();

            //视野感知：没挂就自动补一个，省去逐个预制体加组件（参数统一由 EnemyAIConfig 下发）
            Perception = GetComponent<EnemyPerception>();
            if (Perception == null) Perception = gameObject.AddComponent<EnemyPerception>();

            if (_config != null)
            {
                Perception.Configure(
                    _config.viewDistance > 0f ? _config.viewDistance : _config.aggroRange,
                    _config.viewAngle,
                    _config.requireLineOfSight,
                    _config.proximityRange,
                    _config.eyeHeight,
                    _config.aimHeight);
            }

            //移动层：有 NavMeshAgent 就用 Agent，没有就退回直线位移
            Loco = new EnemyLocomotion(transform, MoveSpeed, AgentStoppingDistance);

            InitFlash();
            CacheAnimatorParams();
        }

        /// <summary>
        /// 由子类在 Awake 里调用，把强类型状态机交给基类驱动。
        /// 顺便把状态机日志接到 UnityEngine（纯 C# 的状态机不引用 UnityEngine）。
        /// </summary>
        protected void AttachStateMachine(EnemyFsmBase fsm)
        {
            Fsm = fsm;
            if (fsm == null) return;

            fsm.LogTransitions = _logTransitions;
            fsm.Logger = message => Debug.Log(message, this);
        }

        /// <summary>
        /// Agent 的 <c>stoppingDistance</c> 就等于「攻击范围」（NAVMESH-GUIDE §7.2 ③）：
        /// 设成攻击范围的 90%，Agent 会自己停在够得着的位置，不用再手算距离。
        /// 弓兵要不同的停靠距离时覆写本属性即可。
        /// </summary>
        protected virtual float AgentStoppingDistance
        {
            get { return Mathf.Max(0.05f, AttackRange * 0.9f); }
        }

        protected virtual void OnEnable()
        {
            if (Health != null) Health.Damaged += HandleDamaged;   //T21：受击通知接通点

            if (Loco != null) Loco.SyncWithNavMesh();              //被 SetActive(true) 复活后吸附回网格
        }

        protected virtual void OnDisable()
        {
            if (Health != null) Health.Damaged -= HandleDamaged;

            //⚠️ 闪白中途被隐藏 / 死亡 / 房间切换的话，红色克隆材质会**留在渲染器上**，
            //  物体被复用（SetActive(true) 复活）时就会顶着一身红出场 ⇒ 这里兜一下。
            RestoreColor();
        }

        private void OnDestroy()
        {
            if (_flashClones == null) return;

            for (int i = 0; i < _flashClones.Count; i++)
            {
                Material m = _flashClones[i];
                if (m == null) continue;
                if (Application.isPlaying) Destroy(m); else DestroyImmediate(m);
            }
            _flashClones.Clear();
        }

        protected virtual void Start()
        {
            _retargetTimer = 0f;//让第一次 Update 立刻找一次玩家
        }

        // ==================== 每帧 ====================

        private void Update()
        {
            EnsureTarget();
            TickFlash();
            OnAIUpdate();//子类的每帧逻辑（本类的 Update 是私有的，子类靠这个钩子）
            UpdateLocomotionAnim();//把移动层的实时速度写给动画（待机 ↔ 移动 两个动画状态的切换依据）

            if (Fsm != null) Fsm.Tick();
        }

        /// <summary>
        /// **移动 → 动画**：把 <see cref="EnemyLocomotion"/> 的实时水平速度写给 Animator 参数 <c>Speed</c>。
        /// 动画状态机用 <c>Speed &gt; 0.1</c> / <c>Speed &lt; 0.1</c> 在「待机」与「移动」两个状态间切
        /// （对应 GDD §6.4.1 的 Idle ↔ Run 映射，两个敌人**共用同一套参数名**）。
        ///
        /// ⚠️ 死状态锁死后强制写 0 —— 否则尸体停在原地时还留着"移动中"，死亡动画结束后若被复用会接错状态。
        /// </summary>
        private void UpdateLocomotionAnim()
        {
            if (Anim == null || Loco == null) return;

            SetAnimFloat("Speed", IsStateLocked ? 0f : Loco.CurrentSpeed);
        }

        /// <summary>
        /// 子类的每帧逻辑，在状态机 <c>Tick</c> **之前**执行。默认什么都不做。
        /// ⚠️ 为什么会需要它：本类的 <c>Update</c> 是**私有**的（不能被覆写），
        ///    但法师需要一个"与状态无关的冷却计时"（施法冷却要在 Chase 之外的其它状态下也走）。
        /// </summary>
        protected virtual void OnAIUpdate() { }

        private void EnsureTarget()
        {
            if (Target != null) return;

            _retargetTimer -= Time.deltaTime;
            if (_retargetTimer > 0f) return;
            _retargetTimer = 1f;

            GameObject go = GameObject.FindGameObjectWithTag("Player");
            if (go == null) return;

            Target = go.transform;
            TargetFsm = go.GetComponent<PlayerFSM>();
            if (Perception != null) Perception.Target = Target;
        }

        private void TickFlash()
        {
            if (_flashTimer <= 0f) return;

            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f) RestoreColor();
        }

        // ==================== 受击反应（T21 接通点） ====================

        /// <summary>
        /// 规则（GDD §6.1）：
        /// · **小怪**（canBeInterrupted = true）：硬直 + 闪白 + 击退，**攻击被打断**
        /// · **Boss**（canBeInterrupted = false）：**只闪白**，不硬直、不打断前摇
        /// · **致死一击**：闪白之后直接进死状态（<see cref="OnKilled"/>），不做硬直
        /// </summary>
        private void HandleDamaged(EnemyHealth h, int dmg)
        {
            if (IsStateLocked) return;//已进死状态，不再反应

            FlashHit();

            //⭐ 致死一击：切 Death 状态（由子类实现）。
            //   ⚠️ 这里**必须**在 EnemyHealth.Die() 把物体隐藏掉之前跑完 ——
            //      时序上 TakeDamage 先广播 Damaged（本方法），再判断死亡并调 Die()，
            //      所以死亡动画的 IsDead 参数是在物体被关掉之前设下去的。
            if (Health != null && Health.IsDead)
            {
                OnKilled();
                return;
            }

            if (!CanBeInterrupted) return;//Boss：只闪白

            OnInterrupted(dmg);
        }

        /// <summary>
        /// **致死一击的接通点**。基类不能是泛型、拿不到子类的状态枚举，所以只能留钩子：
        /// 子类在这里 <c>_fsm.Change(EState.Death)</c>。
        /// 不接的话敌人被打死时行为状态机会一直停在 Chase，死亡动画也不会播。
        /// </summary>
        protected virtual void OnKilled() { }

        /// <summary>小怪被受击打断时要做的事（硬直 / 退回 Hit 状态）。Boss 不会走到这里。</summary>
        protected virtual void OnInterrupted(int damage) { }

        /// <summary>
        /// **受击动画的接通点**。两个敌人共用同一套动画参数名，所以实现放在基类，
        /// 子类在 <c>OnInterrupted</c> 里调一次即可。
        /// ⚠️ 必须**同时复位 <c>Attack</c> 触发器**：攻击被打断时那个触发器可能还没被状态机消费掉，
        ///    留在原地会让"受击动画一播完就自动补挥一刀"（视觉上的诈尸挥砍）。
        /// </summary>
        protected void PlayHitAnim()
        {
            ResetAnimTrigger("Attack");
            SetAnimTrigger("Hit");
        }

        /// <summary>
        /// **死亡动画的接通点**（两个敌人共用同一套参数名，所以放在基类）。
        /// <c>IsDead</c> 在动画状态机里驱动 AnyState → 死亡状态 的转移。
        ///
        /// ⚠️ 顺手把两个 Trigger 都清掉：死亡状态的优先级虽然最高，但残留的触发器会在
        ///    "物体被重新 SetActive(true) 复用"时立刻把动画顶到攻击/受击上去。
        /// </summary>
        protected void PlayDeathAnim()
        {
            ResetAnimTrigger("Attack");
            ResetAnimTrigger("Hit");
            SetAnimBool("IsDead", true);
        }

        // ==================== 距离 ====================

        protected float SqrDistanceToTarget
        {
            get
            {
                if (Target == null) return float.MaxValue;
                return (Target.position - transform.position).sqrMagnitude;
            }
        }

        protected float DistanceToTarget
        {
            get
            {
                if (Target == null) return float.MaxValue;
                return Vector3.Distance(Target.position, transform.position);
            }
        }

        /// <summary>本帧是否看得见目标（距离 + 扇形 + 视线遮挡，见 <see cref="EnemyPerception"/>）。</summary>
        protected bool CanSeeTarget
        {
            get { return Perception != null && Target != null && Perception.CanSeeTarget; }
        }

        // ==================== 动画触发（带参数存在性检查） ====================

        /// <summary>
        /// 触发一个 Trigger。⚠️ 参数不存在时**只警告一次并跳过**，不会每帧往控制台刷屏
        /// —— 敌人动画状态机（M3.6）补齐前必然遇到这个情况。
        /// </summary>
        protected void SetAnimTrigger(string paramName)
        {
            if (Anim == null) return;

            int hash = Animator.StringToHash(paramName);
            if (_animTriggers != null && !_animTriggers.Contains(hash))
            {
                WarnMissingParam(hash, paramName, "Trigger");
                return;
            }

            Anim.SetTrigger(hash);
        }

        /// <summary>设置一个 Bool，同样带参数存在性检查。</summary>
        protected void SetAnimBool(string paramName, bool value)
        {
            if (Anim == null) return;

            int hash = Animator.StringToHash(paramName);
            if (_animBools != null && !_animBools.Contains(hash))
            {
                WarnMissingParam(hash, paramName, "Bool");
                return;
            }

            Anim.SetBool(hash, value);
        }

        /// <summary>设置一个 Float，同样带参数存在性检查。目前只有 <c>Speed</c>（驱动待机/移动切换）。</summary>
        protected void SetAnimFloat(string paramName, float value)
        {
            if (Anim == null) return;

            int hash = Animator.StringToHash(paramName);
            if (_animFloats != null && !_animFloats.Contains(hash))
            {
                WarnMissingParam(hash, paramName, "Float");
                return;
            }

            Anim.SetFloat(hash, value);
        }

        /// <summary>
        /// 复位一个 Trigger。⚠️ **受击打断攻击时必调**（见 <see cref="PlayHitAnim"/>）：
        /// 触发器是"按下待消费"的语义，攻击动作被打断时如果没被状态机消费掉，它会一直挂着，
        /// 等受击动画播完就立刻又接一次攻击动画。
        /// </summary>
        protected void ResetAnimTrigger(string paramName)
        {
            if (Anim == null) return;
            Anim.ResetTrigger(Animator.StringToHash(paramName));
        }

        private void WarnMissingParam(int hash, string paramName, string type)
        {
            if (!_warnedParams.Add(hash)) return;

            Debug.LogWarning("[" + GetType().Name + "] Animator 上没有名为 \"" + paramName + "\" 的 " + type +
                             " 参数 —— 该动作不会切换动画（检查 Art/Animations/Enemy/Monster1|Monster2 下的控制器参数表）。", this);
        }

        private void CacheAnimatorParams()
        {
            if (Anim == null) return;

            _animTriggers = new HashSet<int>();
            _animBools = new HashSet<int>();
            _animFloats = new HashSet<int>();

            AnimatorControllerParameter[] ps = Anim.parameters;
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].type == AnimatorControllerParameterType.Trigger) _animTriggers.Add(ps[i].nameHash);
                else if (ps[i].type == AnimatorControllerParameterType.Bool) _animBools.Add(ps[i].nameHash);
                else if (ps[i].type == AnimatorControllerParameterType.Float) _animFloats.Add(ps[i].nameHash);
            }
        }
    }
}
