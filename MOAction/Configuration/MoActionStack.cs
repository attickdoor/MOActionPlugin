using Dalamud.Game.ClientState.Keys;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MOAction.Configuration;

public class MoActionStack : IEquatable<MoActionStack>, IComparable<MoActionStack>
{
    public static readonly VirtualKey[] AllKeys = [VirtualKey.NO_KEY, VirtualKey.SHIFT, VirtualKey.MENU, VirtualKey.CONTROL];
    public Lumina.Excel.Sheets.Action BaseAction { get; set; }
    public List<StackEntry> Entries { get; set; }
    public string Job { get; set; }
    //public string ModifierName { get; set; }
    public VirtualKey Modifier { get; set; }

    public MoActionStack(Lumina.Excel.Sheets.Action baseAction, List<StackEntry> list)
    {
        BaseAction = baseAction;
        Entries = list ?? [];
        Job = "Unset Job";
        Modifier = 0;
    }

    public bool Equals(ConfigurationEntry c)
    {
        if (c.Stack.Count != Entries.Count)
            return false;

        for (var i = 0; i < Entries.Count; i++)
        {
            var myEntry = Entries[i];
            var theirEntry = c.Stack[i];
            if (myEntry.Target.TargetName != theirEntry.Item1 && myEntry.Action.RowId != theirEntry.Item2)
                return false;
        }

        if (Modifier != c.Modifier)
            return false;

        if (BaseAction.RowId != c.BaseId)
            return false;

        return true;
    }

    public int CompareTo(MoActionStack other)
    {
        if (other == null)
            return 1;

        return string.Compare(BaseAction.Name.ExtractText(), other.BaseAction.Name.ExtractText(), StringComparison.Ordinal);
    }

    public override int GetHashCode()
    {
        return (int)(BaseAction.RowId + Job.GetHashCode() + (int)Modifier);
    }

    public override bool Equals(object obj)
    {
        if (obj == null || obj.GetType() != GetType())
            return false;

        var x = (MoActionStack)obj;
        return BaseAction.RowId == x.BaseAction.RowId && Job == x.Job;
    }

    public string GetJob()
    {
        if (Job == "Unset Job")
            return Job;

        return Sheets.ClassJobSheet.First(x => x.RowId.ToString() == Job).Abbreviation.ExtractText();
    }

    public bool Equals(MoActionStack other)
    {
        if (other == null)
            return false;

        return GetHashCode() == other.GetHashCode();
    }
}