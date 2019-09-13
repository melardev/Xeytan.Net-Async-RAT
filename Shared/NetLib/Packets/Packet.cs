using System;

namespace NetLib.Packets
{
    public enum PacketType
    {
        Connection,
        Presentation,
        Information,
        FileSystem,
        DesktopConfig,
        Desktop,
        CameraConfig,
        Camera,
        Process,
        Shell,
        Disconnect,
        Uninstall,
    }

    public enum ServiceAction
    {
        Start, Stop,
    }

    [Serializable]
    public class Packet
    {
        public virtual PacketType PacketType { get; set; }
    }
}