using TMPro;
using UnityEngine;

public class RiskPopupView : MonoBehaviour
{
    public TextMeshProUGUI text;
    public float lifeSeconds = 3f;

    public void Show(string msg)
    {
        if (text != null) text.text = msg;
        if (lifeSeconds > 0f) Destroy(gameObject, lifeSeconds);
    }
}
