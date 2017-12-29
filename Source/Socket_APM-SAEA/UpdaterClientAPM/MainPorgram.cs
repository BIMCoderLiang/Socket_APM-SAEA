using System;
using System.IO;
using UpdaterShare.Model;
using UpdaterShare.Utility;

namespace UpdaterClientAPM
{
    public class MainPorgram
    {

        static void Main(string[] args)
        {
            var mainFolder = AppDomain.CurrentDomain.BaseDirectory.Replace("bin","TestFile");

            ClientBasicInfo basicInfo = new ClientBasicInfo()
            {
                ProductName = "Airforce094",
                RevitVersion = "Revit2016",
                CurrentProductVersion = "18.1.6"
            };

            var serverFilePath = Path.Combine(mainFolder, "Server\\Airforce094_Revit2016_18.1.6_18.1.7.zip");
            
            DownloadFileInfo dlInfo = new DownloadFileInfo()
            {
                LatestProductVersion = "18.1.7",
                DownloadFileMd5 = Md5Utils.GetFileMd5(serverFilePath),
                DownloadFileTotalSize = new FileInfo(serverFilePath).Length
            };

            ClientLinkInfo clInfo = new ClientLinkInfo()
            {
                IpString = "127.0.0.1",
                Port = 8885
            };

            var localSavePath = Path.Combine(mainFolder, "Local");
            var tempFilesDir = Path.Combine(mainFolder, "TempFile");
            var cs = new ClientSocket();
            var result = cs.StartClient(basicInfo, dlInfo, clInfo, localSavePath, tempFilesDir);
            Console.WriteLine(result);
            Console.ReadKey();
        }
    }
}
