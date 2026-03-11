using UnityEngine;

public class SnarkBankTester : MonoBehaviour
{
    public LocalSnarkBank bank;

    void Update()
    {
        if (!bank) return;

        // 按 1：通用
        if (Input.GetKeyDown(KeyCode.Alpha1))
            Debug.Log(Pick(bank.genericSnark));

        // 按 2：拾取
        if (Input.GetKeyDown(KeyCode.Alpha2))
            Debug.Log(Pick(bank.pickupSnark));

        // 按 3：背包
        if (Input.GetKeyDown(KeyCode.Alpha3))
            Debug.Log(Pick(bank.inventorySnark));

        // 按 4：发呆
        if (Input.GetKeyDown(KeyCode.Alpha4))
            Debug.Log(Pick(bank.idleSnark));

        // 按 5：事件
        if (Input.GetKeyDown(KeyCode.Alpha5))
            Debug.Log(Pick(bank.eventSnark));
    }

    string Pick(System.Collections.Generic.List<string> list)
    {
        if (list == null || list.Count == 0) return "[Snark] list empty";
        return "[Snark] " + list[Random.Range(0, list.Count)];
    }
}
