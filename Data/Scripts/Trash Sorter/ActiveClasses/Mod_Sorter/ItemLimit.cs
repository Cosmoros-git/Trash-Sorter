using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Sorter
{
    internal class ItemLimit
    {
        public MyFixedPoint ItemRequestedAmount;
        public MyFixedPoint ItemTriggerAmount;
        public bool OverLimitTrigger;
    }
}