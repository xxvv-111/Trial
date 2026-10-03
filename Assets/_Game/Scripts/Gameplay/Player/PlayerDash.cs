using Game.Core;
using Game.Data;
using System;
using TMPro;
using UnityEngine;

namespace Game.Gameplay
{
    public class PlayerDash : MonoBehaviour
    {
        //事件
        public event Action DashStarted;

        [SerializeField] private PlayerConfig config;
        //速度
        private float dashSpeed;
        //时间
        private float dashTimer;
        //无敌时间
        private float iFrameTime;
        //组件
        // 剩余冲刺时间
        private float _dashTimer;
        private float dashDelay;
        //是否冲刺
        public bool IsDashing => _dashTimer > 0f;
        //是否无敌
        public bool IsInvulnerable => iFrameTime > 0f;
        private Animator _anim;

        private void Awake()
        {
            dashSpeed = config.dashSpeed;
            dashTimer = config.dashTimer;
            iFrameTime = config.iFrameTime;
            dashDelay = config.dashDelay;
            _anim = GetComponent<Animator>();
        }

        private void Update()
        {
            //冲刺剩余时间
            _dashTimer -= Time.deltaTime;
            iFrameTime -= Time.deltaTime;
            //if (InputService.Instance.DashPressedThisFrame && !IsDashing && CanDashNow())
            //{
            //    _dashTimer = dashTimer;
            //    DashStarted?.Invoke();
            //    if (_anim != null) _anim.SetTrigger("Dash");
            //}w6
        }

        //冲刺
        private void LateUpdate()
        {
            if (IsDashing && _dashTimer < (dashTimer - dashDelay)) 
                transform.position += transform.forward * (dashSpeed * Time.deltaTime);
        }

        //只有待机和跑步才能冲刺
        private bool CanDashNow()
        {
            if (_anim == null) return true;
            var st = _anim.GetCurrentAnimatorStateInfo(0);
            return st.IsName("Locomotion");
        }

        //给 PlayerFSM调用
        public void BeginDash()
        {
            _dashTimer = dashTimer;
            iFrameTime = config.iFrameTime;
            DashStarted?.Invoke();
            //transform.position = VectorExt.WithY(transform.position,-0.3f);
        }
        public void EndDash()
        {
            _dashTimer = 0f;
            iFrameTime = 0f;
            //transform.position = VectorExt.WithY(transform.position, 0);
        }
    }
}
