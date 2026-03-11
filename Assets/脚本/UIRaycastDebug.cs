using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIRaycastDebug : MonoBehaviour
{
    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current == null) { Debug.Log("No EventSystem"); return; }

        var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(ped, results);

        if (results.Count == 0)
        {
            Debug.Log("[UIRaycast] HIT NONE");
        }
        else
        {
            Debug.Log("[UIRaycast] TOP HIT = " + results[0].gameObject.name + " (count=" + results.Count + ")");
        }
    }
}