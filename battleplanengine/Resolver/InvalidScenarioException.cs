using System;

namespace BattlePlanEngine.Resolver
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