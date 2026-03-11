using System;
using System.Threading.Tasks;
using UnityEngine;

public class SmartResponder : IAIResponder
{
    IAIResponder cloud;
    IAIResponder local;
    bool log;

    public SmartResponder(IAIResponder cloud, IAIResponder local, bool log = false)
    {
        this.cloud = cloud;
        this.local = local;
        this.log = log;
    }

    public async Task<string> GenerateAsync(AIContext ctx)
    {
        if (cloud != null)
        {
            try
            {
                string s = await cloud.GenerateAsync(ctx);
                if (!string.IsNullOrWhiteSpace(s))
                    return s;
            }
            catch (Exception e)
            {
                if (log)
                    Debug.LogWarning("[SmartResponder] Cloud failed, fallback. " + e.Message);
            }
        }

        if (local != null)
            return await local.GenerateAsync(ctx);

        return "";
    }
}
