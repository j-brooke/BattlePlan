using System;

namespace BattlePlanEngine.Translator
{
    /// <summary>
    /// Exception related to translating between DTOs and domain objects.
    /// </summary>
    [System.Serializable]
    public class TranslatorException : System.Exception
    {
        public TranslatorException() { }
        public TranslatorException(string message) : base(message) { }
        public TranslatorException(string message, System.Exception inner) : base(message, inner) { }
        protected TranslatorException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}