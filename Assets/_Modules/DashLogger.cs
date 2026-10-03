using Game.Gameplay;
using UnityEngine;

public class DashLogger : MonoBehaviour
{
    private PlayerDash _dash;
    private void OnEnable()                        //∆Ù”√æÕ∂©
    {
        _dash = GetComponent<PlayerDash>();
        _dash.DashStarted += OnDash;               //∂©‘ƒ
    }
    private void OnDisable()                       //Ω˚”√æÕÕÀ
    {
        _dash.DashStarted -= OnDash;               //ÕÀ∂©
    }
    private void OnDash() => Debug.Log("[DashLogger] ≥Â¥Ã£°");
}
