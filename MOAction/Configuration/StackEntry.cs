using MOAction.Target;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOAction.Configuration
{
    public class StackEntry
    {
        public uint actionID;
        public TargetType target { get; set; }

        public StackEntry(uint id, TargetType targ)
        {
            actionID = id;
            target = targ;
        }
    }
}
