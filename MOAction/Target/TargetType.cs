using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOAction.Target
{
    public abstract class TargetType
    {
        public delegate IntPtr PtrFunc();
        public PtrFunc getPtr;

        public TargetType(PtrFunc function)
        {
            getPtr = function;
        }

        public abstract uint GetTargetActorId();
        public abstract bool IsTargetValid();
    }
}
