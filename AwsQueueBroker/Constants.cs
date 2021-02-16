using Semver;

namespace AwsQueueBroker
{
    /// <summary>
    /// Constants
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Library Constants
        /// </summary>
        public static class Library
        {
            /// <summary>
            /// Library Version
            /// </summary>
            public const string Version = "1.0.0";

            /// <summary>
            /// Gets the Version constant converted to a SemVersion instance.
            /// </summary>
            public static SemVersion SemVersion => SemVersion.Parse(Version);
        }
        
        /// <summary>
        /// 
        /// </summary>
        public static class MessageAttributes
        {
            /// <summary>
            /// Gets or sets the attribute key containing the unique name identifier for a message type.
            /// </summary>
            /// <value>
            /// Defaults to "qbMessageName"
            /// </value>
            public static string MessageName { get; set; } = "qbMessageName";
            
            /// <summary>
            /// Gets or sets the attribute key containing the message version number.
            /// </summary>
            public static string MessageVersion { get; set; } = "qbMessageVersion";
        }

        /// <summary>
        /// Reflection Method Name Constants
        /// </summary>
        public static class MethodNames
        {
            /// <summary>
            /// The name of the defined Received method.
            /// </summary>
            public const string Received = "Received";
            
            /// <summary>
            /// The name of the defined Validate method.
            /// </summary>
            public const string Validate = "Validate";
            
            /// <summary>
            /// The name of the defined Process method.
            /// </summary>
            public const string Process = "Process";
            
            /// <summary>
            /// The name of the defined Error method.
            /// </summary>
            public const string Error = "Error";
        }
    }
}