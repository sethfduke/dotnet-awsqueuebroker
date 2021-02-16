using Amazon;
using Amazon.SQS;
using Amazon.SQS.Model;
using System;
using System.Collections.Generic;

namespace AwsQueueBroker
{
    /// <summary>
    /// Represents an AWS SQS message.
    /// </summary>
    public class QMessage
    {
        /// <summary>
        /// Gets the MessageId.
        /// </summary>
        public string Id => _message.MessageId;

        /// <summary>
        /// Gets the underlying Amazon.Sqs.Model.Message.
        /// </summary>
        public Message AwsSqsMessage => _message;

        /// <summary>
        /// Gets the message body.
        /// </summary>
        public string Body => _message.Body;

        /// <summary>
        /// Gets the message attributes.
        /// </summary>
        public Dictionary<string, MessageAttributeValue> MessageAttributes => _message.MessageAttributes;

        /// <summary>
        /// Gets the message name attribute value.
        /// </summary>
        public string Name =>
            _message.MessageAttributes.ContainsKey(Constants.MessageAttributes.MessageName)
                ? _message.MessageAttributes[Constants.MessageAttributes.MessageName].StringValue
                : null;
        
        /// <summary>
        /// Gets the message version attribute value.
        /// </summary>
        public string Version =>
            _message.MessageAttributes.ContainsKey(Constants.MessageAttributes.MessageVersion)
                ? _message.MessageAttributes[Constants.MessageAttributes.MessageVersion].StringValue
                : null;

        private Message _message;

        /// <summary>
        /// Constructor. Create a QMessage from an Amazon.Sqs.Model.Message.
        /// </summary>
        /// <param name="message">Amazon.Sqs.Model.Message</param>
        /// <exception cref="ArgumentException">Throws if the message is missing the required name and version attributes</exception>
        public QMessage(Message message)
        {
            _message = message;
            
            if (!_message.MessageAttributes.ContainsKey(Constants.MessageAttributes.MessageName))
            {
                throw new ArgumentException("The message does not contain the name attribute.");
            }
            
            if (!_message.MessageAttributes.ContainsKey(Constants.MessageAttributes.MessageVersion))
            {
                throw new ArgumentException("The message does not contain the version attribute.");
            }
        }

        /// <summary>
        /// Constructor: Create a QMessage from the provided parameters. 
        /// </summary>
        /// <param name="name">The name value to assign to the name attribute.</param>
        /// <param name="body">The body of the message.</param>
        /// <param name="attributes">Additional message attributes to add.</param>
        public QMessage(
            string name,
            string body,
            params KeyValuePair<string, MessageAttributeValue>[] attributes)
        {
            _message = new Message();
            _message.Body = body;

            _message.MessageAttributes.Add(Constants.MessageAttributes.MessageName, new MessageAttributeValue()
            {
                DataType = "String",
                StringValue = name
            });
            
            _message.MessageAttributes.Add(Constants.MessageAttributes.MessageVersion, new MessageAttributeValue()
            {
                DataType = "String",
                StringValue = Constants.Library.Version
            });
            
            foreach (var (key, value) in attributes)
            {
                if (_message.MessageAttributes.ContainsKey(key))
                {
                    _message.MessageAttributes[key] = value;
                }
                else
                {
                    _message.MessageAttributes.Add(key, value);   
                }
            }
        }
    }
}