using System;
using ILogger = Serilog.ILogger;

namespace AwsQueueBroker
{ 
    /// <summary>
    /// Settings for the QBroker class.
    /// </summary>
    public class QBrokerSettings
    {
        /// <summary>
        /// Sets the maximum number of messages to return.
        /// </summary>
        public int MaxNumberOfMessages
        {
            get => _maxNumberOfMessages;
            set => _maxNumberOfMessages = value < 1 || value > 10
                ? throw new ArgumentOutOfRangeException(nameof(MaxNumberOfMessages), "Value must be within the range of 1 to 10.")
                : value;
        }

        private int _maxNumberOfMessages = 5;
        
        /// <summary>
        /// Sets the length of time, in seconds, for which a ReceiveMessage action waits for a message to arrive.
        /// </summary>
        public int WaitTimeSeconds {
            get => _waitTimeSeconds;
            set => _waitTimeSeconds = value < 0 || value > 20
                ? throw new ArgumentOutOfRangeException(nameof(WaitTimeSeconds), "Value must be in the range of 0 and 20.")
                : value;
        }

        private int _waitTimeSeconds;
        
        /// <summary>
        /// Gets or sets the queue url to poll for messages.
        /// </summary>
        public string QueueUrl { get; set; }

        /// <summary>
        /// Gets or sets flag enabling polling to continue until queue has no more messages to be read.
        /// </summary>
        public bool FetchUntilEmpty { get; set; } = true;
        
        /// <summary>
        /// Gets or sets the optional ILogger instance to use.
        /// </summary>
        public ILogger Logger { get; set; }

        public bool DeleteIfSuccess { get; set; } = true;

        public bool DeleteIfInvalid { get; set; } = true;

        public bool DeleteIfError { get; set; } = true;
    }
}