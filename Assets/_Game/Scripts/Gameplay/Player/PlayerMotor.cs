using Game.Core;
using Game.Data;
using UnityEngine;
namespace Game.Gameplay
{
    public class PlayerMotor : MonoBehaviour
    {
        //移动速度
        [SerializeField] private PlayerConfig config;
        CharacterController _cc;
        private float speed = 6f;
        private Animator _anim;
        //混合树参数
        private float currentSpeed;

        private PlayerAttack _attack;
        private PlayerDash _dash;

        private void Awake()
        {
            speed = config.moveSpeed;
            _anim = GetComponent<Animator>();
            _cc = GetComponent<CharacterController>();
            //_attack = GetComponent<PlayerAttack>();w6
            //_dash = GetComponent<PlayerDash>();w6
        }

        void Update()
        {
            //if (_attack != null && _dash != null && !_dash.IsDashing&&!_attack.isAttacking)w6
            //{
                //面向鼠标
                //FaceMouse();
                //移动
            Vector2 axis = InputService.Instance.Move;
            Vector3 move = new Vector3(axis.x, 0f, axis.y) * (speed * Time.deltaTime);
                //面向
            Face(axis);
            _cc.Move(move + Vector3.down * 0.1f);
            //transform.position += move;W7

            //动画混合树用
            currentSpeed = axis.sqrMagnitude > 0.01f ? speed : 0;
            _anim.SetFloat("speed", currentSpeed, 0.15f, Time.deltaTime);
            //}
        }

        //面向鼠标函数
        //private void FaceMouse()
        //{
        //    Plane ground = new Plane(Vector3.up, Vector3.zero);
        //    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        //    if (ground.Raycast(ray, out float enter))
        //    {
        //        Vector3 aim = ray.GetPoint(enter);
        //        Vector3 dir = (aim - transform.position).FlattenY();
        //        if (dir.sqrMagnitude > 0.001f)
        //        {
        //            transform.forward = dir;
        //        }
        //    }
        //}

        //面向函数
        private void Face(Vector2 a)
        {
            // ① 没输入就保持当前朝向，直接返回
            if (a.sqrMagnitude <= 0.01f) return;

            // ② 输入(Vector2) → 世界方向(Vector3)。俯视固定镜头下屏幕方向 = 世界轴
            Vector3 moveDir = new Vector3(a.x, 0f, a.y);

            // ③ 目标朝向 = 朝 moveDir，再叠加模型偏置
            Quaternion target = Quaternion.LookRotation(moveDir);

            // ④ 从当前朝向限速转过去
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, 720 * Time.deltaTime);
        }
    }
}