using System.Collections.Generic;
using System.Net.Sockets;

namespace UpdaterServerSAEA
{
    public class SocketAsyncEventArgsPool
    {
        readonly Stack<SocketAsyncEventArgs> _pool;
        public int Count => _pool.Count;

        public SocketAsyncEventArgsPool(int capacity)
        {
            _pool = new Stack<SocketAsyncEventArgs>(capacity);
        }


        public void Push(SocketAsyncEventArgs item)
        {
            if (item != null)
            {
                lock (_pool)
                {
                    _pool.Push(item);
                }
            }
        }

        public SocketAsyncEventArgs Pop()
        {
            lock (_pool)
            {
                return _pool.Pop();
            }
        }
    }
}
