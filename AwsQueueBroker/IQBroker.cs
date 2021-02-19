using System;
using System.Threading.Tasks;

namespace AwsQueueBroker
{
    /// <summary>
    /// IQBroker Interface
    /// </summary>
    public interface IQBroker
    {
        /// <summary>
        /// Add a message type by name, model, and processor.
        /// </summary>
        /// <param name="name"></param>
        /// <typeparam name="TModel"></typeparam>
        /// <typeparam name="TProcessor"></typeparam>
        /// <returns></returns>
        public QBroker AddMessageType<TModel, TProcessor>(string name)
            where TProcessor : QProcessor<TModel>;

        /// <summary>
        /// Setup QBroker settings.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public QBroker Setup(
            Action<QBrokerSettings> settings);

        /// <summary>
        /// Delete a message.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        public Task DeleteAsync(
            QMessage message);

        /// <summary>
        /// Send a message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="queueUrl"></param>
        /// <returns></returns>
        public Task SendAsync(
            QMessage message,
            string queueUrl);

        /// <summary>
        /// Fetch and process messages.
        /// </summary>
        /// <returns></returns>
        public Task FetchAsync();
    }
}