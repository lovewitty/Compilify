﻿using System;
using System.Text;
using BookSleeve;
using Compilify.Models;
using Compilify.Web.EndPoints;
using Newtonsoft.Json;
using SignalR;

namespace Compilify.Web.Services {
    /// <summary>
    /// Processes messages sent by workers over Redis and forwards them to the client
    /// that originally initiated the request.</summary>
    public class JobDoneMessageRelay
    {

        private const string ChannelPattern = "workers:job-done:*";

        public JobDoneMessageRelay(RedisConnectionGateway redisConnectionGateway)
        {
            gateway = redisConnectionGateway;
            Subscribe();
        }

        private readonly RedisConnectionGateway gateway;
        private static RedisSubscriberConnection channel;

        private void Subscribe()
        {
            channel = gateway.GetConnection().GetOpenSubscriberChannel();
            channel.Closed += OnChannelClosed;

            channel.PatternSubscribe(ChannelPattern, OnMessageRecieved);
        }

        public void OnChannelClosed(object sender, EventArgs e)
        {
            if (channel != null) {
                channel.Closed -= OnChannelClosed;
                channel.Dispose();
                channel = null;
            }

            Subscribe();
        }
        
        /// <summary>
        /// Handle messages received from workers through Redis.</summary>
        /// <param name="key">
        /// The name of the channel on which the message was received.</param>
        /// <param name="message">
        /// A JSON message.</param>
        public void OnMessageRecieved(string key, byte[] message)
        {
            // Retrieve the client's connection ID from the key
            var parts = key.Split(new[] { ':' });
            var clientId = parts[parts.Length - 1];

            if (!string.IsNullOrEmpty(clientId))
            {
                var context = GlobalHost.ConnectionManager.GetConnectionContext<ExecuteEndPoint>();
                var response = WorkerResult.Deserialize(message);

                // Forward the message to the user's browser with SignalR
                context.Connection.Send(clientId, new { status = "ok", data = response });
            }
        }
    }
}