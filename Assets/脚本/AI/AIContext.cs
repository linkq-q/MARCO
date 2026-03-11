using System.Collections.Generic;

public class AIContext
{
    public AITrigger trigger;

    // 全量语料
    public List<LoreRef> loreRefs;

    // 最近语料
    public List<LoreRef> recentLore;

    // 当前阶段
    public EchoStage stage;

    //当前状态
    public int subState;


    // 可选信息
    public string sceneName;
    public string playerAction;

}

