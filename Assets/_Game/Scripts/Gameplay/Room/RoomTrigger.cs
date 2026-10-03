using UnityEngine;

namespace Game.Gameplay
{
    public class RoomTrigger : MonoBehaviour
    {
        public enum EMode { OpenEntryDoor,StartFight}
        [SerializeField] private RoomController _room;
        [SerializeField] private EMode _mode = EMode.StartFight;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player")) return;

            if (_mode == EMode.OpenEntryDoor) _room.OpenEntryDoor();//进门
            else _room.OnPlayerEntered();//关门战斗
        }
    }
}