using UnityEngine;

namespace Game.Gameplay
{
    /// <summary>房间触发器：玩家进入时开门或开战。挂在房间入口的 Trigger 碰撞体上。</summary>
    public class RoomTrigger : MonoBehaviour
    {
        public enum EMode { OpenEntryDoor, StartFight }
        [SerializeField] private RoomController _room;
        [SerializeField] private EMode _mode = EMode.StartFight;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;
            if (_room == null) return;

            if (_mode == EMode.OpenEntryDoor) _room.OpenEntryDoor();//进门
            else _room.OnPlayerEntered();//关门战斗
        }
    }
}