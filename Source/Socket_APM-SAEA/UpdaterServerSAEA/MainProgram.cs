
using System;

namespace UpdaterServerSAEA
{
    class MainProgram
    {
        static void Main(string[] args)
        {
            var serverSocket = new ServerSocket(8885,100);
            serverSocket.StartServer();
            Console.WriteLine("Server has start!");
            Console.ReadLine();
        }
    }
}
