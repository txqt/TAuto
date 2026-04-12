using System;

namespace TAuto.Core
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ActionTypeIdentifierAttribute : Attribute
    {
        public string Identifier { get; }

        public ActionTypeIdentifierAttribute(string identifier)
        {
            Identifier = identifier;
        }
    }
}
