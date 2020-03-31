using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MOAction.Target
{
    public class ActorTarget : TargetType
    {
        public ActorTarget(PtrFunc func) : base(func) { }
        public override uint GetTargetActorId()
        {
            IntPtr ptr = getPtr();
            if (IsTargetValid())
                return (uint)Marshal.ReadInt32(ptr);
            return 0;
        }

        public override bool IsTargetValid()
        {
            IntPtr ptr = getPtr();
            uint val = (uint)Marshal.ReadInt32(ptr);
            return val != 0xe0000000 || val != 0;
        }
    }
}
