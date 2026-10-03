using UnityEngine;

namespace Game.Core
{
    /// <summary>Vector3 的常用扩展方法。</summary>
    public static class VectorExt
    {
        public static Vector3 FlattenY(this Vector3 v)//压掉 y
        {
            return new Vector3(v.x, 0f, v.z);
        }

        public static Vector3 WithY(this Vector3 v, float y)//只改 y
        {
            return new Vector3(v.x, y, v.z);
        }
    }
}
