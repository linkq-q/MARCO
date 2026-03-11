using System.Threading.Tasks;

public interface IAIResponder
{
    Task<string> GenerateAsync(AIContext ctx);
}
