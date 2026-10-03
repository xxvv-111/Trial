using UnityEngine;

public class Hero : MonoBehaviour
{
    private string name;
    public string Name { get { return name; } }

    private int _hp;
    public int hp 
    {  
        get { return _hp; }
        set { _hp = Mathf.Clamp(value, 0, 100); Debug.Log($"HP:{_hp}"); }
    }

    private int _mp;
    public int mp
    {
        get { return _mp; }
        set { _mp = Mathf.Clamp(value, 0, 100); Debug.Log($"MP:{_mp}"); }
    }
}
