using Game.Data;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 近战敌人 AI（switch 式状态机）。
    ///
    /// **M1.2 已接通 T21**：此前 <see cref="EnemyHealth"/> 受击只扣血、从不通知 AI，
    /// 导致本类的受击反应（硬直 / 闪白 / 击退）全是死代码 —— 小怪挨打毫无反应。
    /// 现在通过订阅 <see cref="EnemyHealth.Damaged"/> 接通。
    ///
    /// ⚠️ 待改造：M3 接入 NavMesh —— 把 <see cref="MoveTowardPlayer"/> 的 transform 位移
    ///    换成 NavMeshAgent，并补 Alert / Reposition 两个状态（见 Docs/NAVMESH-GUIDE.md §7）。
    /// </summary>
    public class EnemyMeleeAI : MonoBehaviour
    {
        public enum EState { Idle, Chase, Attack, Hit, Death }

        //数值（来自 EnemyAIConfig）
        private float aggroRange;//追击距离
        private float attackRange;//攻击距离
        private float moveSpeed;//追击速度
        private float recoverTime;//后摇
        private float hitStunTime;//受击硬直时长
        private float knockBackDistance;//受击击退距离
        private float flashTime;//闪白时长
        private bool canBeInterrupted;//是否会被打断（Boss = false）

        [Tooltip("判定体开启后持续多久自动关闭（秒）。需与攻击动画的判定窗口匹配。")]
        [SerializeField] private float _hitboxActiveTime = 0.25f;

        [Tooltip("敌人攻击判定体的名字（挂在敌人预制体下、Layer = EnemyHitbox）。")]
        [SerializeField] private string _attackHitboxName = "Hitbox_Attack";

        [SerializeField] private EnemyAIConfig _config;

        private Transform _player;//玩家位置
        private PlayerFSM _playerFsm;//玩家状态机
        private EState _st = EState.Idle;//当前状态

        private float _atkT;//进入攻击状态后经过的秒数
        private float _stunT;//受击硬直剩余秒数
        private float _flashT;//受击闪白剩余秒数

        private Renderer _ren;//闪白用
        private MaterialPropertyBlock _mpb;//⚠️ 用它改色，避免 material 实例化泄漏
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private Color _normalColor;

        private CapsuleCollider _col;
        private Animator _anim;
        private EnemyHealth _health;
        private HitboxController _hitboxes;

        public EState State { get { return _st; } }

        private void Awake()
        {
            aggroRange = _config.aggroRange;
            attackRange = _config.attackRange;
            moveSpeed = _config.moveSpeed;
            recoverTime = _config.recoverTime;

            hitStunTime = _config.hitStunTime;
            knockBackDistance = _config.knockBackDistance;
            flashTime = _config.flashTime;
            canBeInterrupted = _config.canBeInterrupted;

            _col = GetComponent<CapsuleCollider>();
            _anim = GetComponent<Animator>();
            _health = GetComponent<EnemyHealth>();
            _hitboxes = GetComponent<HitboxController>();

            _ren = GetComponentInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
            if (_ren != null && _ren.sharedMaterial != null && _ren.sharedMaterial.HasProperty(BaseColorId))
                _normalColor = _ren.sharedMaterial.GetColor(BaseColorId);
            else
                _normalColor = Color.white;
        }

        private void OnEnable()
        {
            //接通 T21：受击通知
            if (_health != null) _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        private void Start()
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                _player = p.transform;
                _playerFsm = p.GetComponent<PlayerFSM>();
            }
        }

        private void Update()
        {
            //玩家距离（用平方比较，省一次开方）
            float distSqr = (_player != null)
                ? (_player.position - transform.position).sqrMagnitude
                : 9999f;

            //受击闪白计时：时间到就恢复原色
            if (_flashT > 0f)
            {
                _flashT -= Time.deltaTime;
                if (_flashT <= 0f) RestoreColor();
            }

            switch (_st)
            {
                case EState.Idle://玩家进入追击范围 → 追击
                    if (distSqr <= aggroRange * aggroRange) SetState(EState.Chase);
                    break;

                case EState.Chase://进入攻击距离 → 发起攻击
                    if (distSqr <= attackRange * attackRange)
                    {
                        _atkT = 0f;
                        SetState(EState.Attack);
                        break;
                    }
                    MoveTowardPlayer();
                    break;

                case EState.Attack:
                    FacePlayer();
                    _atkT += Time.deltaTime;
                    if (_atkT >= recoverTime) SetState(EState.Chase);//后摇结束回追击
                    break;

                case EState.Hit://受击硬直
                    _stunT -= Time.deltaTime;
                    if (_stunT <= 0f) SetState(EState.Chase);
                    break;

                case EState.Death://死亡：不再行动
                    break;
            }
        }

        // ==================== 受击反应（T21 接通点） ====================

        /// <summary>
        /// 受击回调。规则（GDD §6.1）：
        ///   · **小怪**（canBeInterrupted = true）：硬直 + 闪白 + 击退，**攻击被打断**
        ///   · **Boss**（canBeInterrupted = false）：**只闪白**，不硬直、不打断前摇
        /// </summary>
        private void OnDamaged(EnemyHealth h, int dmg)
        {
            if (_st == EState.Death) return;

            //致死一击：不做受击反应（死亡收尾由 EnemyHealth 负责）
            if (_health != null && _health.IsDead) return;

            FlashRed();

            if (!canBeInterrupted) return;//Boss：只闪白

            //打断当前动作：关掉判定体，避免「已经被打断了，但判定还生效」
            if (_hitboxes != null) _hitboxes.DisableAllHitboxes();

            KnockBack();

            _stunT = hitStunTime;
            SetState(EState.Hit);
        }

        /// <summary>受击闪白。用 MaterialPropertyBlock，**不产生材质实例**。</summary>
        private void FlashRed()
        {
            if (_ren == null) return;

            _ren.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, Color.red);
            _ren.SetPropertyBlock(_mpb);
            _flashT = flashTime;
        }

        private void RestoreColor()
        {
            if (_ren == null) return;

            _ren.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, _normalColor);
            _ren.SetPropertyBlock(_mpb);
        }

        /// <summary>
        /// 受击后退，避免贴脸。
        /// ⚠️ 直接改 transform 会**忽略碰撞**（敌人没有 Rigidbody / CharacterController）。
        ///    距离小（默认 0.4m）影响有限；M3 接入 NavMeshAgent 后应改为沿路径退避。
        /// </summary>
        private void KnockBack()
        {
            if (knockBackDistance <= 0f) return;
            transform.position -= transform.forward * knockBackDistance;
        }

        // ==================== 移动 / 朝向 ====================

        /// <summary>⚠️ M3 接入 NavMesh 后应**删除本方法**，改由 NavMeshAgent 驱动（见 NAVMESH-GUIDE §7）。</summary>
        private void MoveTowardPlayer()
        {
            if (_player == null) return;
            Vector3 dir = _player.position - transform.position;
            dir.y = 0f;//只在地面走
            if (dir.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized);
                transform.position += dir.normalized * (moveSpeed * Time.deltaTime);
            }
        }

        /// <summary>水平朝向玩家（不带俯仰）。</summary>
        private void FacePlayer()
        {
            if (_player == null) return;
            Vector3 toP = _player.position - transform.position;
            toP.y = 0f;
            if (toP.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(toP.normalized);
        }

        // ==================== 攻击命中（M1.2：改用判定体） ====================

        /// <summary>
        /// 动画关键帧事件（挂在 enemy/combo_01_1 上）。
        /// M1.2 起改为**开启敌人判定体**，与玩家侧规则对称（GDD 设计支柱 4）。
        /// </summary>
        private void TickAttack()
        {
            if (_hitboxes != null)
            {
                _hitboxes.EnableHitbox(_attackHitboxName, _hitboxActiveTime);
                return;
            }

            //兜底：未配判定体时退回旧的直接结算，避免「完全打不到玩家」
            Debug.LogWarning("[EnemyMeleeAI] 未配置 HitboxController，退回直接结算：" + name, this);

            if (_playerFsm == null) return;
            float d = (_player != null)
                ? (_player.position - transform.position).sqrMagnitude
                : 9999f;
            if (d <= attackRange * attackRange * 1.2f && !_playerFsm.Invulnerable)
                _playerFsm.TakeDamage(_config.damage);
        }

        // ==================== 状态机 ====================

        /// <summary>
        /// 切换状态。**进入状态时的一次性动作放这里**，不要写在 Update 里 ——
        /// 原先 SetTrigger("Attack") 写在 Update 中，会被**每帧触发**，导致攻击动画不断被重置。
        /// </summary>
        private void SetState(EState next)
        {
            if (next == _st) return;

            //离开 Attack 时清掉残留判定（正常收招 / 被打断都要清）
            if (_st == EState.Attack && _hitboxes != null) _hitboxes.DisableAllHitboxes();

            _st = next;

            if (next == EState.Attack) _anim.SetTrigger("Attack");
        }

        private void OnDrawGizmosSelected()
        {
            if (_config == null) return;
            Gizmos.color = new Color(1f, 0.6f, 0f, 1f);
            Gizmos.DrawWireSphere(transform.position, _config.aggroRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _config.attackRange);
        }
    }
}
