using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MOAction
{
    public class GuiSettings
    {

        public int jobs;
        public int lastJob;
        public int baseAbility;
        public List<int> stackAbilities;
        public List<int> stackTargets;

        public GuiSettings(int job, int lastjob, int baseabil) : 
            this(job, lastjob, baseabil, new List<int>(new int[] { -1 }), new List<int>(new int[] { -1 }))
        {
        }

        public GuiSettings(int job, int lastjob, int baseabil, List<int> stackabil, List<int> stacktarg)
        {
            jobs = job;
            lastJob = lastjob;
            baseAbility = baseabil;
            stackAbilities = stackabil;
            stackTargets = stacktarg;
        }
    }
}
