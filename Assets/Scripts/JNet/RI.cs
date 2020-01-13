
using System;

namespace JNetworking
{
    /// <summary>
    /// The base class for both <see cref="CmdAttribute"/> and <see cref="RpcAttribute"/>.
    /// Internal use only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public abstract class RIAttribute : Attribute
    {

    }
}
