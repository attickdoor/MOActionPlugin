using Dalamud.Game.ClientState.Keys;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin.Services;

namespace MOAction.Configuration
{
    public class MoActionStack : IEquatable<MoActionStack>, IComparable<MoActionStack>
    {
        public static VirtualKey[] AllKeys = { VirtualKey.NO_KEY, VirtualKey.SHIFT, VirtualKey.MENU, VirtualKey.CONTROL };
        public Lumina.Excel.GeneratedSheets.Action BaseAction
        {
            get; set;
        }
        public List<StackEntry> Entries { get; set; }
        public string Job { get; set; }
        //public string ModifierName { get; set; }
        public VirtualKey Modifier { get; set; }
        public MoActionStack(Lumina.Excel.GeneratedSheets.Action baseaction, List<StackEntry> list)
        {
            BaseAction = baseaction;
            if (list == null) Entries = new();
            else Entries = list;
            Job = "Unset Job";
            Modifier = 0;
        }

        public bool Equals(ConfigurationEntry c)
        {
            if (c.Stack.Count != Entries.Count) return false;
            for (int i = 0; i < Entries.Count; i++)
            {
                var myEntry = Entries[i];
                var theirEntry = c.Stack[i];
                if (myEntry.Target.TargetName != theirEntry.Item1 && myEntry.Action.RowId != theirEntry.Item2) return false;
            }
            if (Modifier != c.Modifier) return false;
            if (BaseAction.RowId != c.BaseId) return false;
            return true;
        }

        public int CompareTo(MoActionStack other)
        {
            if (other == null) return 1;
            return this.BaseAction.Name.ToString().CompareTo(other.BaseAction.Name.ToString());
        }

        public override int GetHashCode()
        {
            return (int)(BaseAction.RowId + Job.GetHashCode() + ((int)Modifier));
        }
        public override bool Equals(object obj)
        {
            if (obj == null || !obj.GetType().Equals(GetType()))
                return false;
            var x = (MoActionStack)obj;
            return BaseAction.RowId == x.BaseAction.RowId && Job == x.Job;
        }

        public string GetJob(IDataManager dm)
        {
            if (Job == "Unset Job") return Job;
            return dm.GetExcelSheet<Lumina.Excel.GeneratedSheets.ClassJob>().First(x => x.RowId.ToString() == Job).Abbreviation;
        }

        public bool Equals(MoActionStack other)
        {
            if (other == null) return false;
            return this.GetHashCode() == other.GetHashCode();
        }
    }
}
