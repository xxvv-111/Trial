using UnityEngine;
using Game.Gameplay;

namespace Game.Gameplay
{
    // 本周临时测试键，周末收口时删除
    public class _DebugCheatsW7 : MonoBehaviour
    {
        [SerializeField] private PlayerHealth _hp;
        [SerializeField] private PlayerEnergy _energy;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.J)) _hp.ApplyDamage(10);   // 看红条掉
            if (Input.GetKeyDown(KeyCode.E))                       // 看蓝条掉
            {
                bool ok = _energy.TrySpend(30f);
                Debug.Log(ok ? "放了假技能，-30 蓝" : "蓝不够！");
            }
            if (Input.GetKeyDown(KeyCode.K)) KillNearestEnemy();   // 一键秒最近敌人（Step6 测清房用）
        }

        private void KillNearestEnemy()
        {
            EnemyHealth nearest = null;
            float best = float.MaxValue;
            foreach (var e in FindObjectsOfType<EnemyHealth>())
            {
                if (!e.gameObject.activeInHierarchy) continue;
                float d = (e.transform.position - transform.position).sqrMagnitude;
                if (d < best) { best = d; nearest = e; }
            }
            if (nearest != null)
            {
                nearest.TakeDamage(99999);
                Debug.Log("K：秒了一只怪");
            }
        }
    }
}