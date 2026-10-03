using UnityEngine;

namespace Game.Gameplay
{
    public class DoorController : MonoBehaviour 
    {
        [SerializeField] private GameObject _body;
        public void Close()
        {
            _body.SetActive(true);
        }

        public void Open()
        {
            _body.SetActive(false);
        }
    }
}