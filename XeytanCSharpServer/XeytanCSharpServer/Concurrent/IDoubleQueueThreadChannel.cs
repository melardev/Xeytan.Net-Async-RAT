using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XeytanCSharpServer.Concurrent
{
    interface IDoubleQueueThreadChannel<T>
    {
        T TakeFromLeft();
        T TakeFromRight();

        void SubmitToLeft(T value);
        void SubmitToRight(T value);
    }
}