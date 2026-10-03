#if UNITY_EDITOR           // 只在编辑器编译（运行时根本不带它）
using UnityEditor;
using UnityEngine;
using Game.Tools;

namespace Game.Tools.EditorTools
{
    public static class FieldInspectorMenu
    {
        [MenuItem("Tools/字段检查器：打印选中物体")]   // 菜单路径：Tools 菜单下会出现这一项
        private static void DumpSelected()
        {
            var go = Selection.activeGameObject;   // 你当前在 Hierarchy 选中的物体
            if (go == null) { Debug.Log("请先在 Hierarchy 选中一个物体"); return; }
            foreach (var mb in go.GetComponents<MonoBehaviour>())
                if (mb != null) FieldInspector.Dump(mb);   // 把这个物体上每个脚本都打印一遍
        }
    }
}
#endif

