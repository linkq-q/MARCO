using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI 发言触发类型
/// 用于把“玩家行为 / 游戏事件”映射为 AI 的触发语义
/// </summary>
public enum AITrigger
{
    /// <summary>
    /// 默认 / 未指定
    /// </summary>
    None = 0,

    /// <summary>
    /// 拾取物品
    /// </summary>
    Pickup = 10,

    /// <summary>
    /// 打开背包 / UI
    /// </summary>
    OpenInventory = 20,

    /// <summary>
    /// 发呆 / 闲置
    /// </summary>
    Idle = 30,

    /// <summary>
    /// 剧情 / 事件节点
    /// </summary>
    EventNode = 40,
}


