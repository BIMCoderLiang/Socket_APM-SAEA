using System.Collections.Generic;
using System.Net.Sockets;

namespace UpdaterServerSAEA
{
    public class BufferManager
    {
        private byte[] _buffer;
        private readonly int _totalBytes;

        private readonly int _bufferSize;
        private int _currentIndex;
        private readonly Stack<int> _freeIndexPool;
        public BufferManager(int totalBytes, int bufferSize)
        {
            _totalBytes = totalBytes;
            _currentIndex = 0;
            _bufferSize = bufferSize;
            _freeIndexPool = new Stack<int>();
        }

        public void InitBuffer()
        {
            _buffer = new byte[_totalBytes];
        }

        public bool SetBuffer(SocketAsyncEventArgs args)
        {

            if (_freeIndexPool.Count > 0)
            {
                args.SetBuffer(_buffer, _freeIndexPool.Pop(), _bufferSize);
            }
            else
            {
                if ((_totalBytes - _bufferSize) < _currentIndex)
                {
                    return false;
                }
                args.SetBuffer(_buffer, _currentIndex, _bufferSize);
                _currentIndex += _bufferSize;
            }
            return true;
        }


        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            _freeIndexPool.Push(args.Offset);
            args.SetBuffer(null, 0, 0);
        }
    }
}
