using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SpecFlow.Retry.Sample
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
