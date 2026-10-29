using System;

namespace Fabrikam.Housing.Common
{
    public class BusinessRuleException : Exception
    {
        public BusinessRuleException(string code, string message) : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }
}
