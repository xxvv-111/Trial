using UnityEngine;//w4s2

public static class FieldInspector
{
    public static void Dump(object target)
    {
        var type = target.GetType();
        foreach (var f in type.GetFields(System.Reflection.BindingFlags.NonPublic
                                       | System.Reflection.BindingFlags.Instance
                                       | System.Reflection.BindingFlags.Public))
        {
            if (System.Attribute.IsDefined(f, typeof(UnityEngine.SerializeField))
                || f.IsPublic)
                Debug.Log($"{f.Name} = {f.GetValue(target)}");
        }
    }
}
