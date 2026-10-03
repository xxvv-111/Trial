using Game.Data;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 近战敌人 AI（switch 式状态机）。
    /// ⚠️ 两处待改造，见 Docs/ROADMAP.md：
    ///   1) M3 接入 NavMesh —— 把 MoveTowardPlayer() 的 transform 位移换成 NavMeshAgent，
    ///      并补 Alert / Reposition 两个状态（详见 Docs/NAVMESH-GUIDE.md §7）。
    ///   2) M1.2 接通受击链路（T21）—— 现在 EnemyHealth 受击**不会通知本脚本**，
    ///      所以 Hit 状态 / FlashRed / KnockBack 暂时不会被触发。属"**待接通**"，不是无用代码。
    /// </summary>
    public class EnemyMeleeAI : MonoBehaviour
    {
        public enum EState { Idle, Chase, Attack, Hit, Death }

        [Header("数值（来自 EnemyAIConfig）")]
        private float aggroRange;//追击距离
        private float attackRange;//攻击距离
        private float moveSpeed;//追击速度
        private float windupTime;//前摇（⚠️ 当前未使用，待 M3 接入）
        private float recoverTime;//后摇

        private Transform _player;//玩家位置
        private PlayerFSM _playerFsm;//玩家状态机
        private EState _st = EState.Idle;//当前状态

        private float _atkT;//进入攻击状态后经过的秒数
        private bool _damageDone;//这一刀是否已结算过伤害
        private float _stunT;//受击硬直剩余秒数
        private float _flashT;//受击闪白剩余秒数

        private Renderer _ren;//闪白
        private Color _normalColor;//原本颜色
        private CapsuleCollider _col;
        private Animator _anim;

        [SerializeField] private EnemyAIConfig _config;

        private void Awake()
        {
            aggroRange = _config.aggroRange;
            attackRange = _config.attackRange;
            moveSpeed = _config.moveSpeed;
            windupTime = _config.windupTime;
            recoverTime = _config.recoverTime;

            _col = GetComponent<CapsuleCollider>();
            _ren = GetComponentInChildren<Renderer>();//颜色一般在子物体/本体的 MeshRenderer 上
            _anim = GetComponent<Animator>();
            if (_ren != null) _normalColor = _ren.material.color;
        }

        private void Start()
        {
            _player = GameObject.FindWithTag("Player")?.transform;
            if (_player != null) _playerFsm = _player.GetComponent<PlayerFSM>();
        }

        private void Update()
        {
            //玩家距离（用平方比较，省一次开方）
            float distSqr = (_player != null)
                ? (_player.position - transform.position).sqrMagnitude
                : 9999f;

            //受击闪白计时：时间到就把颜色变回原色
            if (_flashT > 0f)
            {
                _flashT -= Time.deltaTime;
                if (_flashT <= 0f && _ren != null) _ren.material.color = _normalColor;
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
                        _damageDone = false;
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

                case EState.Hit:
                    _stunT -= Time.deltaTime;//硬直倒数
                    if (_stunT <= 0f) SetState(EState.Chase);
                    break;

                case EState.Death://死亡：不再行动
                    break;
            }
        }

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

        /// <summary>动画关键帧事件（挂在 enemy/combo_01_1 上）。</summary>
        private void TickAttack()
        {
            if (_damageDone) return;
            _damageDone = true;//一刀只结算一次

            float d = (_player != null)
                ? (_player.position - transform.position).sqrMagnitude
                : 9999f;

            //玩家仍在攻击距离内、且不在无敌中
            if (d <= attackRange * attackRange * 1.2f && _playerFsm != null && !_playerFsm.Invulnerable)
                _playerFsm.TakeDamage(_config.damage);
        }

        /// <summary>受击闪白。⚠️ 待 M1.2 接通（T21）。</summary>
        private void FlashRed()
        {
            if (_ren != null) { _ren.material.color = Color.red; _flashT = 0.15f; }
        }

        /// <summary>受击后退，避免贴脸。⚠️ 待 M1.2 接通（T21）。</summary>
        private void KnockBack()
        {
            transform.position -= transform.forward * 0.4f;
        }

        /// <summary>死亡收尾。⚠️ 待 M1.2 接通（T21）。</summary>
        private void OnDeath()
        {
            _col.enabled = false;//关碰撞
            Destroy(gameObject, 1.5f);//1.5 秒后销毁尸体
        }

        /// <summary>
        /// 切换状态。**进入状态时的一次性动作放这里**，不要写在 Update 里 ——
        /// 原先 SetTrigger("Attack") 写在 Update 中，会被**每帧触发**，导致攻击动画不断被重置。
        /// </summary>
        private void SetState(EState next)
        {
            if (next == _st) return;
            _st = next;

            if (next == EState.Attack)
                _anim.SetTrigger("Attack");
        }
    }
}
