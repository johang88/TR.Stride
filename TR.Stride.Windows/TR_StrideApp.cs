using Stride.Engine;

namespace TR.Stride
{
    class TR_StrideApp
    {
        static void Main(string[] args)
        {
            using (var game = new Game())
            {
                game.Run();
            }
        }
    }
}
