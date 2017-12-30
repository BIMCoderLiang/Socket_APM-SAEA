
namespace UpdaterServerAPM
{
    class MainProgram
    {
        static void Main(string[] args)
        {
            ServerSocket.StartServer(8885, 100);
        }
    }
}
