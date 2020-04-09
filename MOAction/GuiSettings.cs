using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOAction
{
    public class GuiSettings
    {
        public bool isOpen;
        public bool notDeleted;
        public int jobs;
        public int lastJob;
        public int baseAbility;
        public List<int> stackAbilities;
        public List<int> stackTargets;

        public GuiSettings(bool open, bool del, int job, int lastjob, int baseabil) : 
            this(open, del, job, lastjob, baseabil, new List<int>(), new List<int>())
        {
        }

        public GuiSettings(bool open, bool notdel, int job, int lastjob, int baseabil, List<int> stackabil, List<int> stacktarg)
        {
            isOpen = open;
            notDeleted = notdel;
            jobs = job;
            lastJob = lastjob;
            baseAbility = baseabil;
            stackAbilities = stackabil;
            stackTargets = stacktarg;
        }

        public GuiSettings() :
            this(true, true, -1, -1, -1, new List<int>(), new List<int>())
        { }
    }
}
