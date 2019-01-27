using System;

namespace BattlePlan.Resolver
{
    public class InvalidScenarioException : ApplicationException
    {
        public InvalidScenarioException()
        {
        }

        public InvalidScenarioException(string msg)
            : base(msg)
        {
        }

        public InvalidScenarioException(string msg, Exception innerException)
            : base(msg, innerException)
        {
        }
    }
}