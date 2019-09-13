using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XeytanCSharpServer.Models;

namespace XeytanCSharpServer.Concurrent
{
    enum Target
    {
        InvalidTarget = -1,
        Application,
        Server,
        Client,
        Ui
    }

    enum Subject
    {
        InvalidSubject = -1,
        Interaction, // Used for console
        Connection,
        Desktop,
        Camera,
        Process,
        FileSystem,
        Download,
        Information,
        PacketReceived,
        Shell,
        Error,
    };

    enum Action
    {
        InvalidAction = -1,
        ListAvailable,
        Start,
        Update,
        Pause,
        Stop,
        Fetch = Start,
        Fetched = Start,
        Started = Fetched,
        Push = Update
    };

    class AppEvent
    {
        public Target Target { get; set; }
        public Subject Subject { get; set; }
        public Action Action { get; set; }
        public object Data { get; set; }
    }
}