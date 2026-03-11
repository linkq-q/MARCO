using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IEndingLockable
{
    void OnEndingLock();
    void OnEndingUnlock(); // 可选：测试或回到标题用
}
