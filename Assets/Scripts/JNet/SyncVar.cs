
using System;
using UnityEngine;

namespace JNetworking
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class SyncVarAttribute : PropertyAttribute
    {
        /// <summary>
        /// Set to the name of a public method to invoke the method when the syncvar recieves a new value from
        /// the server. The hook is only ever called when not on the server.
        /// </summary>
        public string Hook { get; set; }

        /// <summary>
        /// When true, this syncvar is only serialized when seriazing for a 'first' event such as spawning or sending the
        /// world state to a new client. This syncvar being changed will not cause the object to be reserialized.
        /// Useful for properties that need to be synchronized but don't change after the object is spawned.
        /// </summary>
        public bool FirstOnly { get; set; }
    }
}
