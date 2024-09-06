using VRage;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Sorter
{
    /// <summary>
    /// The ItemLimit class defines the limits for items in a conveyor sorter system. 
    /// It includes the amount of the item requested, the amount at which the item is triggered, and 
    /// a flag indicating if the over-limit condition is triggered.
    /// </summary>
    internal class ItemLimit
    {
        /// <summary>
        /// The amount of the item requested.
        /// </summary>
        public MyFixedPoint ItemRequestedAmount;

        /// <summary>
        /// The trigger amount for the item, which sets when an action is triggered.
        /// </summary>
        public MyFixedPoint ItemTriggerAmount;

        /// <summary>
        /// Flag indicating whether the over-limit trigger is active.
        /// </summary>
        public bool OverLimitTrigger;
    }
}