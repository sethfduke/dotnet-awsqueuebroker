namespace AwsQueueBroker
{
    /// <summary>
    /// Reply class with and outgoing QMessage and its destination queue url.
    /// </summary>
    public class QReply
    {
        /// <summary>
        /// The QMessage to send.
        /// </summary>
        public QMessage Message { get; set; }
        
        /// <summary>
        /// The queue url to send to.
        /// </summary>
        public string ReplyToQueueUrl { get; set; }
    }
}