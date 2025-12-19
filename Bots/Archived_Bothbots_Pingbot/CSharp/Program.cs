using System.Threading.Tasks;

namespace PingbotCSharp
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            var bot = new Bot();
            await bot.InitializeAsync();
            await Task.Delay(-1);
        }
    }
}
