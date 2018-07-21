﻿using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MQTTnet.Adapter;
using MQTTnet.Client;
using MQTTnet.Diagnostics;
using MQTTnet.Exceptions;
using MQTTnet.Packets;

namespace MQTTnet.Core.Tests
{
    [TestClass]
    public class MqttClientTests
    {

        [TestMethod]
        public async Task ClientDisconnectException()
        {
            var factory = new MqttFactory();
            var client = factory.CreateMqttClient();

            Exception ex = null;
            client.Disconnected += (s, e) =>
            {
                ex = e.Exception;
            };

            try
            {
                await client.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("wrong-server").Build());
            }
            catch
            {
            }

            Assert.IsNotNull(ex);
            Assert.IsInstanceOfType(ex, typeof(MqttCommunicationException));
            Assert.IsInstanceOfType(ex.InnerException, typeof(SocketException));
        }

        [TestMethod]
        public async Task ClientCleanupOnAuthentificationFails()
        {
            var channel = new TestMqttCommunicationAdapter();
            var channel2 = new TestMqttCommunicationAdapter();
            channel.Partner = channel2;
            channel2.Partner = channel;

            Task.Run(async () => {
                var connect = await channel2.ReceivePacketAsync(TimeSpan.Zero, CancellationToken.None);
                await channel2.SendPacketAsync(new MqttConnAckPacket() { ConnectReturnCode = Protocol.MqttConnectReturnCode.ConnectionRefusedNotAuthorized }, CancellationToken.None);
            });


            
            var fake = new TestMqttCommunicationAdapterFactory(channel);

            var client = new MqttClient(fake, new MqttNetLogger());
            
            try
            {
                await client.ConnectAsync(new MqttClientOptionsBuilder().WithTcpServer("any-server").Build());
            }
            catch (Exception ex)
            {
                Assert.IsInstanceOfType(ex, typeof(MqttConnectingFailedException));
            }

            var packetReceiverTaskInfo = typeof(MqttClient).GetField("_packetReceiverTask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var keepAliveMessageSenderTaskInfo = typeof(MqttClient).GetField("_keepAliveMessageSenderTask", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            var packetReceiverTask = packetReceiverTaskInfo.GetValue(client) as Task;
            var keepAliveMessageSenderTask = keepAliveMessageSenderTaskInfo.GetValue(client) as Task;

            Assert.IsTrue(packetReceiverTask == null || packetReceiverTask.IsCompleted, "receive loop not completed");
            Assert.IsTrue(keepAliveMessageSenderTask == null || keepAliveMessageSenderTask.IsCompleted, "keepalive loop not completed");
        }
    }
}
