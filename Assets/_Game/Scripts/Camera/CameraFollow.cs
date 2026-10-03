using UnityEngine;

namespace Game.Gameplay
{
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 20f, -15f);
        [SerializeField] private float _smoothTime = 0.15f;

        private Vector3 _velocity;//必须是字段

        private void LateUpdate()
        {
            if (_target == null) return;

            Vector3 desired = _target.position + _offset;
            transform.position = Vector3.SmoothDamp(
                transform.position, desired, ref _velocity, _smoothTime);
        }
    }
}