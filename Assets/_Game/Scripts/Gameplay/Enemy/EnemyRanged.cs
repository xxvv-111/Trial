using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>
    /// 远程敌人（枪手）。
    ///
    /// ⚠️ 当前**完全没有状态机**：只有"距离检测 + 转向 + 冷却计时 → 开火"。
    ///    M3 需要从零搭完整状态机（Idle / Alert / Reposition / Aim / Shoot / Hit / Death），
    ///    并用 NavMesh 实现"保持距离（kiting）"。
    ///    ⚠️ 关键：kiting **不能**直接 <c>SetDestination(player.position)</c>，否则弓兵会冲向玩家；
    ///    必须先由状态机算出"远离玩家的目标点"，再用 NavMesh.SamplePosition 吸附 —— 见
    ///    Docs/NAVMESH-GUIDE.md §9。
    /// </summary>
    public class EnemyRanged : MonoBehaviour
    {
        [SerializeField] private GameObject bulletPrefab;//子弹
        [SerializeField] private Transform firePoint;//枪口

        [Header("参数")]
        [SerializeField] private float fireCooldown = 2.5f;//攻击间隔
        [SerializeField] private float detectRange = 15f;//攻击范围
        [SerializeField] private float bulletSpeed = 12f;//子弹速度

        private Transform _player;//玩家位置
        private float _timer;

        private void Start()
        {
            _timer = fireCooldown;//开场先冷却

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _player = p.transform;
        }

        private void Update()
        {
            if (_player == null) return;

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist > detectRange) return;//太远

            Vector3 dir = _player.position - transform.position;//转向
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized);

            _timer -= Time.deltaTime;//冷却
            if (_timer <= 0f)
            {
                Fire();
                _timer = fireCooldown;
            }
        }

        private void Fire()
        {
            if (bulletPrefab == null || firePoint == null) return;

            Vector3 from = firePoint.position;//出膛位置
            Vector3 to = _player.position;//朝玩家现在站的位置
            Vector3 velocity = (to - from).normalized * bulletSpeed;//算好速度向量

            GameObject bullet = Instantiate(bulletPrefab, from, Quaternion.identity);
            bullet.GetComponent<Bullet>().Launch(velocity, transform);//把速度交给子弹
        }
    }
}
