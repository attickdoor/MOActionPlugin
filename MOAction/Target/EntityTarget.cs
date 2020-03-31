using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MOAction.Target
{
    public class EntityTarget : TargetType
    {
        public EntityTarget(PtrFunc func) : base(func) { }
        public override uint GetTargetActorId()
        {
            IntPtr ptr = getPtr();
            if (IsTargetValid())
                return (uint)Marshal.ReadInt32(ptr + 0x74);
            return 0;
        }

        public override bool IsTargetValid()
        {
            IntPtr ptr = getPtr();
            return (ptr != IntPtr.Zero || (int)ptr != 0);
        }
    }
}
