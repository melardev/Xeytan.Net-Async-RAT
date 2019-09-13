namespace XeytanCSharpServer.Concurrent
{
    class AppUiDoubleQueueThreadChannel : DoubleQueueThreadChannel<AppEvent>
    {
        // Taking into account that I chosen App to be in the Right Side and Ui in the Left Side
        // App will be receiving events in its side(right), but sending events to the other(left) side
        public void SubmitToApp(AppEvent appEvent)
        {
            SubmitToRight(appEvent);
        }

        public void SubmitToUi(AppEvent appEvent)
        {
            SubmitToLeft(appEvent);
        }

        public AppEvent TakeFromApp()
        {
            // App dispatches events to the left side, that is where the events coming from App are stored
            // So if we want an event dispatched from App we go the the left side to find them
            return TakeFromLeft();
        }

        public AppEvent TakeFromUi()
        {
            // Ui dispatches events to the right side, that is where the events coming from App are stored
            // So if we want an event dispatched from Net we go the the right side to find them
            return TakeFromRight();
        }
    }
}