
using System;

namespace JNetworking.Internal
{
    public class JNetException : Exception
    {
        public JNetException(string message) : base(message)
        {            
        }

        public JNetException(string message, Exception internalException) : base(message, internalException)
        {
        }
    }
}
