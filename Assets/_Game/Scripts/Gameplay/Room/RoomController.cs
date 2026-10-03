using Game.Core;
using UnityEngine;

namespace Game.Gameplay
{
    public class RoomController : MonoBehaviour
    {
        [Header("入口")]
        [SerializeField] private DoorController _entryDoor;

        [Header("上一间房")]
        [SerializeField] private RoomController _prevRoom;
        [SerializeField] private bool isFinalRoom = false;

        [Header("敌人")]
        [SerializeField] private EnemyHealth[] _enemies;

        [Header("提示文字")]
        [SerializeField] private Game.UI.RoomHintText _hint;

        private int _alive;
        public bool Started { get; private set; }
        public bool Cleared { get; private set; }

        private void Start()
        {
            foreach (var e in _enemies)
                if (e != null) e.gameObject.SetActive(false);

            if (_entryDoor != null) _entryDoor.Close();
        }

        private void OnEnable() => EnemyHealth.Died += OnEnemyDied;
        private void OnDisable() => EnemyHealth.Died -= OnEnemyDied;

        public void OpenEntryDoor()//开门
        {
            if (Started) return;//已经开始就不开
            if (_prevRoom != null && !_prevRoom.Cleared) return;
            if (_entryDoor != null) _entryDoor.Open();
        }

        public void OnPlayerEntered()//关门，生成敌人
        {
            if (Started) return;
            Started = true;

            if (_entryDoor != null) _entryDoor.Close();
            _hint?.Show("Clean The Room");

            _alive = 0;
            foreach (var e in _enemies)
                if (e != null)
                {
                    e.gameObject.SetActive(true);
                    _alive++;
                }

            if (_alive <= 0) FinishClear();
        }

        private void OnEnemyDied(EnemyHealth enemy)//敌人死亡
        {
            if (!Started || Cleared) return;

            if (System.Array.IndexOf(_enemies, enemy) < 0) return;

            _alive--;
            if (_alive <= 0) FinishClear();
        }

        private void FinishClear()//清完
        {
            Cleared = true;
            _hint?.Show("Door Open");
            if(isFinalRoom)
            {
                GameEvents.RaiseBossDied();
            }
        }
    }
}