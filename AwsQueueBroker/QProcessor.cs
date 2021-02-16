using Amazon.SQS.Model;
using Serilog;
using System;
using System.Threading.Tasks;

namespace AwsQueueBroker
{
    /// <summary>
    /// Abstract class representing a message processor.
    /// </summary>
    /// <typeparam name="TModel">Type representing the content of the message body.</typeparam>
    public abstract class QProcessor<TModel>
    { 
        /// <summary>
        /// Method called when a message is first received.
        /// </summary>
        /// <param name="message">Copy of the Amazon.Sqs.Model.Message</param>
        /// <param name="logger">ILogger interface</param>
        /// <returns>Task</returns>
        public abstract Task Received(Message message, ILogger logger = null);
        
        /// <summary>
        /// Method called after Received.
        /// </summary>
        /// <remarks>
        /// This method provides the opportunity to conduct additional validation on the deserialized message body.
        /// Modifications can be made to the TModel body within this function if necessary. The method should return
        /// TModel if validation is considered successful. The method should return null if validation has failed
        /// and processing should be halted for this message. 
        /// </remarks>
        /// <param name="message">Copy of the Amazon.Sqs.Model.Message</param>
        /// <param name="body">The message body deserialized to TModel</param>
        /// <param name="logger">ILogger interface</param>
        /// <returns>Task&lt;object&gt;></returns>
        public abstract Task<object> Validate(Message message, TModel body, ILogger logger = null);
        
        /// <summary>
        /// Method called after Validation.
        /// </summary>
        /// <remarks>
        /// The primary processing logic for the message should occur within this method.
        /// If the message should trigger a reply, returning a QMessage will cause the broker to
        /// to automatically send a queue message to the specified queue url.
        /// </remarks>
        /// <param name="message">Copy of the Amazon.Sqs.Model.Message</param>
        /// <param name="model">TModel returned by the Validate method</param>
        /// <param name="logger">ILogger interface</param>
        /// <returns>Task&lt;QMessage&gt;</returns>
        public abstract Task<QReply> Process(Message message, TModel model, ILogger logger = null);
        
        /// <summary>
        /// Method called if any other method in the processing chain threw an exception.
        /// </summary>
        /// <remarks>
        /// Provides the opportunity to conduct additional processing if an exception was thrown.
        /// </remarks>
        /// <param name="message">Copy of the Amazon.Sqs.Model.Message</param>
        /// <param name="exception">The exception thrown</param>
        /// <param name="logger">ILogger interface</param>
        /// <returns>Task</returns>
        public abstract Task Error(Message message, Exception exception, ILogger logger = null);
    }
}