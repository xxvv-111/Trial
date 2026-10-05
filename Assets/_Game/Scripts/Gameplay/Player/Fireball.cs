using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 玩家特殊攻击的**火球**（GDD §4.6，2026-10-05 改定）。
    ///
    /// 行为：沿发射方向**直线飞行** → 命中敌人 / 撞到任何实体后**在原地爆炸** →
    ///      对**爆炸半径内所有可受击目标**造成伤害 → 自毁。飞到最大射程也自爆。
    ///
    /// ⚠️ **为什么"命中"和"范围伤害"合成一步**：
    ///   原计划是"命中者单吃伤害 + 周围吃范围伤害"，但那样**命中者会被算两次**（一次直接、一次范围）。
    ///   而爆炸中心就是命中点，直接命中者必然落在半径内 → 所以**只做 OverlapSphere 一次**
    ///   即可正确覆盖"直接命中 + 溅射"，且天然不重复。
    ///
    /// ⚠️ 参照 `Bullet.cs`（敌人子弹）的移动方式（`transform.position +=` 而非物理速度）——
    ///   本项目的投射物一律这么走，保持一致。火球速度 12 m/s、每帧位移约 0.2 m，
    ///   远小于敌人碰撞体半径（约 0.5 m），**不会发生穿透**。
    /// </summary>
    public class Fireball : MonoBehaviour
    {
        [Tooltip("兜底自毁时间（秒）。防止因未命中任何东西而永久残留。")]
        [SerializeField] private float lifeTime = 5f;

        [Tooltip("爆炸特效（可选，留空则只播逻辑）。")]
        [SerializeField] private GameObject explosionFx;

        private Vector3 _velocity;          //每帧位移方向与速度
        private Transform _owner;           //施法者（不打自己）
        private int _damage;
        private float _explosionRadius;
        private float _maxDistance;         //射程
        private float _traveled;            //已飞距离
        private bool _exploded;

        /// <summary>出手。由施法者在动画的出手帧调用。</summary>
        /// <param name="direction">飞行方向（一般用角色 forward；会自动归一化）。</param>
        /// <param name="speed">飞行速度（米/秒）。</param>
        /// <param name="maxDistance">最大飞行距离（米）。</param>
        /// <param name="damage">伤害（对爆炸范围内每个目标各结算一次）。</param>
        /// <param name="explosionRadius">爆炸半径（米）。</param>
        /// <param name="owner">施法者，范围伤害会排除它自己及其子物体。</param>
        public void Launch(Vector3 direction, float speed, float maxDistance,
                           int damage, float explosionRadius, Transform owner)
        {
            Vector3 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            _velocity = dir * Mathf.Max(0.01f, speed);
            _maxDistance = Mathf.Max(0.1f, maxDistance);
            _damage = damage;
            _explosionRadius = Mathf.Max(0.01f, explosionRadius);
            _owner = owner;
            _traveled = 0f;
            _exploded = false;

            Destroy(gameObject, lifeTime);
        }

        private void Update()
        {
            if (_exploded) return;

            Vector3 step = _velocity * Time.deltaTime;
            transform.position += step;
            _traveled += step.magnitude;

            //到射程尽头直接炸（不打空）
            if (_traveled >= _maxDistance) Explode();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_exploded) return;
            if (other == null) return;

            //不打自己（含自己身上的判定体/子物体）
            if (_owner != null && other.transform.IsChildOf(_owner)) return;

            Explode();
        }

        /// <summary>在当前位置爆炸：对半径内每个可受击目标结算一次伤害，然后自毁。</summary>
        private void Explode()
        {
            if (_exploded) return;
            _exploded = true;

            var hits = Physics.OverlapSphere(transform.position, _explosionRadius);
            //⚠️ 去重：同一个目标常挂多个碰撞体（本体 + 判定体），不加这个会被打多次
            var alreadyHit = new HashSet<IDamageable>();

            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null) continue;
                if (_owner != null && col.transform.IsChildOf(_owner)) continue;

                var target = col.GetComponentInParent<IDamageable>();
                if (target == null) continue;
                if (!alreadyHit.Add(target)) continue;

                target.TakeDamage(_damage);
            }

            if (explosionFx != null) Instantiate(explosionFx, transform.position, transform.rotation);
            Destroy(gameObject);
        }
    }
}
