using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOAction
{
    public class ApplicableAction
    {
        public uint ID { set; get; }
        public string AbilityName { set; get; }
        public bool IsRoleAction { set; get; }
        public bool CanTargetSelf { set; get; }
        public bool CanTargetParty { set; get; }
        public bool CanTargetFriendly { set; get; }
        public bool CanTargetHostile { set; get; }
        public byte ClassJobCategory { set; get; }
        public bool IsPvP { set; get; }

        public ApplicableAction(uint id, string aname, bool isrole, bool targself, bool targparty, bool targfriend, bool targenemy, byte cjc, bool ispvp)
        {
            ID = id;
            AbilityName = aname;
            IsRoleAction = isrole;
            CanTargetSelf = targself;
            CanTargetParty = targparty;
            CanTargetFriendly = targfriend;
            CanTargetHostile = targenemy;
            ClassJobCategory = cjc;
            IsPvP = ispvp;
        }
    }
}
