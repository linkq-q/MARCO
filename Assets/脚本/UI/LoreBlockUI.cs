using UnityEngine;
using TMPro;

public class LoreBlockUI : MonoBehaviour
{
    public TextMeshProUGUI text;

    public void SetText(string s)
    {
        if (text) text.text = s;
    }

    // 可选：做一个删除按钮调用
    public void OnClickDelete()
    {
        Destroy(gameObject);
    }
}
