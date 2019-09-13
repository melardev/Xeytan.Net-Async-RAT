using System;

namespace NetLib.Packets
{
    [Serializable]
    public class PacketShell : Packet
    {
        public override PacketType PacketType => PacketType.Shell;
        public string Command { get; set; }
        public string Output { get; set; }
        public ServiceAction Action { get; set; }
    }
}