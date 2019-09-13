namespace XeytanCSharpServer.Concurrent
{
    class AppNetDoubleQueueThreadChannel : DoubleQueueThreadChannel<AppEvent>
    {
        // Taking into account that I chosen App to be in the Left Side and Net in the right Side
        // App will be receiving events in its side(left), but sending events to the other(right) side
        public void SubmitToApp(AppEvent appEvent)
        {
            SubmitToLeft(appEvent);
        }

        public void SubmitToNet(AppEvent appEvent)
        {
            SubmitToRight(appEvent);
        }

        public AppEvent TakeFromApp()
        {
            // App dispatches events to the right side, that is where the events coming from App are stored
            // So if we want an event dispatched from App we go the the right side to find them
            return TakeFromRight();
        }

        public AppEvent TakeFromNet()
        {
            // Net dispatches events to the left side, that is where the events coming from App are stored
            // So if we want an event dispatched from Net we go the the left side to find them
            return TakeFromLeft();
        }
    }
}