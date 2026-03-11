using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LocalSnarkBank", menuName = "AI/Local Snark Bank")]
public class LocalSnarkBank : ScriptableObject
{
    [Header("通用")]
    [TextArea(1, 3)] public List<string> genericSnark = new();

    [Header("拾取触发")]
    [TextArea(1, 3)] public List<string> pickupSnark = new();

    [Header("打开背包")]
    [TextArea(1, 3)] public List<string> inventorySnark = new();

    [Header("发呆闲聊")]
    [TextArea(1, 3)] public List<string> idleSnark = new();

    [Header("事件节点")]
    [TextArea(1, 3)] public List<string> eventSnark = new();
}
