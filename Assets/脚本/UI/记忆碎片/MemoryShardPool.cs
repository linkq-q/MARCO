using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Memory Shard Pool", fileName = "MemoryShardPool_")]
public class MemoryShardPool : ScriptableObject
{
    [Tooltip("要参与抽取的记忆碎片 ItemData（kind=MemoryShard）")]
    public List<ItemData> shards = new List<ItemData>();
}