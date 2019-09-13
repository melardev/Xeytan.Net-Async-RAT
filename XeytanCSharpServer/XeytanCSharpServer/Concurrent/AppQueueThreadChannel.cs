using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XeytanCSharpServer.Concurrent
{
    class AppQueueThreadChannel : QueueThreadChannel<AppEvent>
    {
        public void SubmitAppEvent(AppEvent appEvent)
        {
            SubmitSync(appEvent);
        }

        public AppEvent TakeEvent()
        {
            return TakeSync();
        }
    }
}