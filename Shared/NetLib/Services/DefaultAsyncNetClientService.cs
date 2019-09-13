using NetLib.Packets;
using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace NetLib.Services
{
    public abstract class DefaultAsyncNetClientService : BaseNetClientService
    {
        public bool IsAlreadySendingPacket { get; set; }
        private Queue<Packet> PacketsQueue { get; set; } = new Queue<Packet>();

        public override void SendPacket(Packet packet)
        {
            bool shouldScheduleAWriteOperation = false;
            lock (PacketsQueue)
            {
                if (!IsAlreadySendingPacket)
                {
                    shouldScheduleAWriteOperation = true;
                    IsAlreadySendingPacket = true;
                }
                else
                {
                    PacketsQueue.Enqueue(packet);
                }
            }

            if (shouldScheduleAWriteOperation)
            {
                base.SendPacket(packet);
            }
        }

        protected override void OnPacketSent()
        {
            Console.WriteLine("Packet sent");

            // If there are more notifications to send then take one and send it
            // Thread synchronization is VERY important in Asynchronous socket programming, why?
            // what would happen if one thread is sending a Packet, and before finishing sending it
            // another thread is also sending a packet, you will end up merging two packets
            // and the server would not know what to do with that, if the server is poorly written it may even
            // crash
            IsAlreadySendingPacket = false;
            Packet packet = null;
            lock (PacketsQueue)
            {
                if (!IsAlreadySendingPacket && PacketsQueue.Count > 0)
                {
                    packet = PacketsQueue.Dequeue();
                    IsAlreadySendingPacket = true;
                }
            }

            if (packet != null)
                base.SendPacket(packet);
        }
    }
}