using Dalamud.Game.ClientState.Objects.Types;
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
        public ActorTarget(PtrFunc func, string name) : base(func, name) { }

        public ActorTarget(PtrFunc func, string name, bool objneed) : base(func, name, objneed) {  }

        public override uint GetTargetActorId()
        {
            GameObject obj = getPtr();
            if (IsTargetValid())
                return obj.ObjectId;
                //return (uint)Marshal.ReadInt32(ptr);
            return 0;
        }

        public override bool IsTargetValid()
        {
            GameObject obj = getPtr();
            if (obj == null) return false;
            return obj.ObjectId != 0xe0000000 || obj.ObjectId != 0;
            //uint val = (uint)Marshal.ReadInt32(ptr);
            //return val != 0xe0000000 || val != 0;
        }
    }
}
