using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOAction
{
    public class GuiSettings : IEquatable<GuiSettings>, IComparable<GuiSettings>
    {
        public bool isOpen;
        public bool notDeleted;
        public int jobs;
        public int lastJob;
        public int refjob = -1;
        public int baseAbility;
        public List<int> stackAbilities;
        public List<int> stackTargets;

        public GuiSettings(bool open, bool del, int job, int lastjob, int rjob, int baseabil) : 
            this(open, del, job, lastjob, rjob, baseabil, new List<int>(), new List<int>())
        {
        }

        public GuiSettings(bool open, bool notdel, int job, int lastjob, int rjob, int baseabil, List<int> stackabil, List<int> stacktarg)
        {
            isOpen = open;
            notDeleted = notdel;
            jobs = job;
            lastJob = lastjob;
            baseAbility = baseabil;
            stackAbilities = stackabil;
            stackTargets = stacktarg;
            refjob = rjob;
        }

        public GuiSettings() :
            this(true, true, -1, -1, -1, -1, new List<int>(), new List<int>())
        { }

        public bool Equals(GuiSettings other)
        {
            if (other == null) return false;
            return jobs == other.jobs && baseAbility == other.baseAbility;
        }

        public int CompareTo(GuiSettings other)
        {
            if (other == null) return 1;
            if (jobs == other.jobs)
            {
                return baseAbility.CompareTo(other.baseAbility);
            }
            return jobs.CompareTo(other.jobs);
        }
    }
}
