using System;

namespace Contoso.Purchasing.Core
{
    public class PolicyException : Exception
    {
        public PolicyException(string message) : base(message)
        {
        }
    }
}
