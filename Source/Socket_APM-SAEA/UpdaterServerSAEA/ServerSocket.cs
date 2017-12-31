﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UpdaterShare.GlobalSetting;
using UpdaterShare.Model;
using UpdaterShare.Utility;

namespace UpdaterServerSAEA
{
    public class ServerSocket
    {
        private readonly int _port;
        private readonly int _backlog;
        private Socket _listenSocket;
        private const int opsToPreAlloc = 2;
        private readonly BufferManager _bufferManager;
        private readonly SocketAsyncEventArgsPool _readWritePool;
        private readonly Semaphore _maxNumberAcceptedClients;

        private string _serverPath;
        private static int _downloadChannelsCount;

        public ServerSocket(int port,int backlog)
        {
            _port = port;
            _backlog = backlog;

            _bufferManager = new BufferManager(ComObject.BufferSize * backlog * opsToPreAlloc, ComObject.BufferSize);
            _readWritePool = new SocketAsyncEventArgsPool(backlog);
            _maxNumberAcceptedClients = new Semaphore(backlog, backlog);

            _downloadChannelsCount = DownloadSetting.DownloadChannelsCount;
        }
    

        private void Init()
        {
            _bufferManager.InitBuffer();        
              
            for (var i = 0; i < _backlog; i++)
            {
                var readWriteEventArg = new SocketAsyncEventArgs();
                _bufferManager.SetBuffer(readWriteEventArg);
                _readWritePool.Push(readWriteEventArg);
            }
        }


        public void StartServer()
        {
            try
            {
                Init();
                IPAddress ipAddress = IPAddress.Any;
                IPEndPoint localEndPoint = new IPEndPoint(ipAddress, _port);
                _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _listenSocket.Bind(localEndPoint);
                _listenSocket.Listen(_backlog);
                StartAccept(null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += StartAccept_Completed;
            }
            else
            {
                acceptEventArg.AcceptSocket = null;
            }

            _maxNumberAcceptedClients.WaitOne();

            if (!_listenSocket.AcceptAsync(acceptEventArg))
            {
                ProcessAccept(acceptEventArg);
            }
        }

        private void StartAccept_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }


        private void ProcessAccept(SocketAsyncEventArgs e)
        {     
            if (e.SocketError == SocketError.Success)
            {
                var socket = e.AcceptSocket;
                if (socket.Connected)
                {
                    SocketAsyncEventArgs readEventArgs = _readWritePool.Pop();
                    readEventArgs.AcceptSocket = e.AcceptSocket;
                    readEventArgs.Completed += ProcessAccept_Completed;

                    if (!socket.ReceiveAsync(readEventArgs))
                    {
                        ProcessReceiveFindFileRequest(readEventArgs);
                    }
                    StartAccept(e);
                }
            }          
        }

        private void ProcessAccept_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessReceiveFindFileRequest(e);
        }


        private void ProcessReceiveFindFileRequest(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                var bytesRead = e.BytesTransferred;
                if (bytesRead > 0 && e.SocketError == SocketError.Success)
                {
                    var receiveData = e.Buffer.Skip(e.Offset).Take(bytesRead).ToArray();                  
                    var dataList = PacketUtils.SplitBytes(receiveData, PacketUtils.ClientFindFileInfoTag());
                    if (dataList != null && dataList.Any())
                    {
                        var request = PacketUtils.GetData(PacketUtils.ClientFindFileInfoTag(), dataList.FirstOrDefault());
                        string str = System.Text.Encoding.UTF8.GetString(request);
                        var infos = str.Split('_');
                        var productName = infos[0];
                        var revitVersion = infos[1];
                        var currentVersion = infos[2];

                        var mainFolder = AppDomain.CurrentDomain.BaseDirectory.Replace("bin", "TestFile");
                        var serverFileFolder = Path.Combine(mainFolder, "Server");
                        var serverFileFiles = new DirectoryInfo(serverFileFolder).GetFiles();

                        var updatefile = serverFileFiles.FirstOrDefault(x => x.Name.Contains(productName) && x.Name.Contains(revitVersion) && x.Name.Contains(currentVersion));
                        if (updatefile != null)
                        {
                            if (string.IsNullOrEmpty(updatefile.FullName) || !File.Exists(updatefile.FullName)) return;
                            _serverPath = updatefile.FullName;
                            
                            byte[] foundUpdateFileData = PacketUtils.PacketData(PacketUtils.ServerFoundFileInfoTag(), null);

                            _bufferManager.FreeBuffer(e);
                            e.SetBuffer(foundUpdateFileData,0, foundUpdateFileData.Length);

                            e.Completed -= ProcessAccept_Completed;
                            e.Completed += ProcessReceiveFindFileRequest_Completed;

                            if (!e.AcceptSocket.SendAsync(e))
                            {
                                ProcessFilePosition(e);
                            }
                        }
                    }
                }            
            }
        }


        private void ProcessReceiveFindFileRequest_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessFilePosition(e);
        }


        private void ProcessFilePosition(SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                var socket = e.AcceptSocket;
                if (socket.Connected)
                {
                    _bufferManager.FreeBuffer(e);
                    e.SetBuffer(null,0,0);
                    _bufferManager.SetBuffer(e);
                    e.Completed -= ProcessReceiveFindFileRequest_Completed;
                    e.Completed += ProcessFilePosition_Completed;
                    if (!socket.ReceiveAsync(e))
                    {
                        ProcessSendFile(e);
                    }
                }
            }
        }

        private void ProcessFilePosition_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessSendFile(e);
        }

        private void ProcessSendFile(SocketAsyncEventArgs e)
        {
            var bytesRead = e.BytesTransferred;
            if (bytesRead > 0 && e.SocketError == SocketError.Success)
            {
                var receiveData = e.Buffer.Skip(e.Offset).Take(bytesRead).ToArray();              
                var dataList = PacketUtils.SplitBytes(receiveData, PacketUtils.ClientRequestFileTag());
                if (dataList != null)
                {
                    foreach (var request in dataList)
                    {
                        if (PacketUtils.IsPacketComplete(request))
                        {
                            int startPosition = PacketUtils.GetRequestFileStartPosition(request);

                            var packetSize = PacketUtils.GetPacketSize(_serverPath, _downloadChannelsCount);
                            if (packetSize != 0)
                            {
                                byte[] filedata = FileUtils.GetFile(_serverPath, startPosition, packetSize);
                                byte[] packetNumber = BitConverter.GetBytes(startPosition/packetSize);
                                if (filedata != null)
                                {
                                    byte[] segmentedFileResponseData = PacketUtils.PacketData(PacketUtils.ServerResponseFileTag(), filedata,packetNumber);

                                    _bufferManager.FreeBuffer(e);
                                    e.SetBuffer(segmentedFileResponseData, 0, segmentedFileResponseData.Length);

                                    e.Completed -= ProcessFilePosition_Completed;
                                    e.Completed += ProcessSendFile_Completed;

                                    if (!e.AcceptSocket.SendAsync(e))
                                    {
                                        CloseClientSocket(e);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                CloseClientSocket(e);
            }
        }


        private void ProcessSendFile_Completed(object sender, SocketAsyncEventArgs e)
        {
            CloseClientSocket(e);
        }


        private void CloseClientSocket(SocketAsyncEventArgs e)
        {
            try
            {
                _bufferManager.FreeBuffer(e);
                e.AcceptSocket.Shutdown(SocketShutdown.Both);
                e.AcceptSocket.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                _maxNumberAcceptedClients.Release();
                _readWritePool.Push(e);
            }          
        }
    }
}
