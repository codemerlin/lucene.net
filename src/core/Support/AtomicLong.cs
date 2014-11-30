using System.Threading;

namespace Lucene.Net.Support
{
    public class AtomicLong
    {
        private long _value;

        public long Get()
        {
             return Interlocked.Read(ref _value); 
        }

        public void Set(long value)
        {
            Interlocked.Exchange(ref _value,value); 
        }

        public long AddAndGet(int value)
        {
            return Interlocked.Add(ref _value, value);
        }
    }
}