using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace XeytanCSharpServer.Concurrent
{
    interface ISingleThreadChannel<T>
    {
        T TakeSync();
        void SubmitSync(T value);
    }
}