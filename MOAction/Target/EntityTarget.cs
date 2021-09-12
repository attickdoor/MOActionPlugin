using Dalamud.Game.ClientState.Objects.Types;
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
        public EntityTarget(PtrFunc func, string name) : base(func, name) { }
        public EntityTarget(PtrFunc func, string name, bool objneed) : base(func, name, objneed) { }
        public override uint GetTargetActorId()
        {
            GameObject obj = getPtr();
            if (IsTargetValid())
                return obj.ObjectId;
                //return (uint)Marshal.ReadInt32(ptr + 0x74);
            return 0;
        }

        public override bool IsTargetValid()
        {
            GameObject obj = getPtr();
            return obj != null;
            //return (ptr != IntPtr.Zero || (int)ptr != 0);
        }
    }
}
