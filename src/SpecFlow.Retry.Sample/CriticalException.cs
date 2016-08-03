using System;
using System.Runtime.Serialization;

namespace Specflow.Retry.Sample
{
    [Serializable]
    public class CriticalException : Exception
    {
        public CriticalException()
        {
        }

        public CriticalException(string message) : base(message)
        {
        }

        public CriticalException(string message, Exception inner) : base(message, inner)
        {
        }

        protected CriticalException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}
