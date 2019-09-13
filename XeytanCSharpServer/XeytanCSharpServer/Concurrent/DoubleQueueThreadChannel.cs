namespace XeytanCSharpServer.Concurrent
{
    class DoubleQueueThreadChannel<T> : IDoubleQueueThreadChannel<T>
    {
        private readonly QueueThreadChannel<T> _leftThreadChannel = new QueueThreadChannel<T>();
        private readonly QueueThreadChannel<T> _rightThreadChannel = new QueueThreadChannel<T>();

        public T TakeFromLeft()
        {
            return _leftThreadChannel.TakeSync();
        }

        public T TakeFromRight()
        {
            return _rightThreadChannel.TakeSync();
        }

        public void SubmitToLeft(T value)
        {
            _leftThreadChannel.SubmitSync(value);
        }

        public void SubmitToRight(T value)
        {
            _rightThreadChannel.SubmitSync(value);
        }
    }
}