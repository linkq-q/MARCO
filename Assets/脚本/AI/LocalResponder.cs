using System.Threading.Tasks;
using UnityEngine;

public class LocalResponder : IAIResponder
{
    LocalSnarkBank bank;

    public LocalResponder(LocalSnarkBank bank)
    {
        this.bank = bank;
    }

    public Task<string> GenerateAsync(AIContext ctx)
    {
        if (!bank)
            return Task.FromResult("");

        // Ëæ±ã´Ó Generic ³éÒ»¾ä
        var list = bank.genericSnark;
        if (list == null || list.Count == 0)
            return Task.FromResult("");

        string line = list[Random.Range(0, list.Count)];
        return Task.FromResult(line);
    }
}
