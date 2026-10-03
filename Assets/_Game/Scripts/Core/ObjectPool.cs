using Unity.VisualScripting.Dependencies.NCalc;
using UnityEngine;

namespace Game.Core
{
    public class ObjectPool<T> where T : Component
    {
        private readonly T _prefab;//池化物体原型
        private readonly Transform _parent;//放在哪
        private readonly System.Collections.Generic.Stack<T> _free = new();

        public int TotalInstantiated { get; private set; }//数量

        public ObjectPool(T prefab, int prewarm, Transform parent)
        {
            _prefab = prefab;
            _parent = parent;
            for(int i=0; i < prewarm; i++)//预热
            {
                T obj = Object.Instantiate(_prefab, _parent);
                obj.gameObject.SetActive(false);
                _free.Push(obj);
                TotalInstantiated++;
            }
        }

        public T Get()//从池中取
        {
            if (_free.Count > 0)
            {
                var obj = _free.Pop();
                obj.gameObject.SetActive(true);
                return obj;
            }
            TotalInstantiated++;
            return Object.Instantiate(_prefab, _parent);
        }

        public void Release(T obj)//释放，归还池
        {
            obj.gameObject.SetActive(false);
            _free.Push(obj);
        }
    }
}