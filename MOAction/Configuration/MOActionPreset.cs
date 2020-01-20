using System;

namespace MOActionPlugin
{
    //CURRENT HIGHEST FLAG IS 51
    [Flags]
    public enum MOActionPreset : int
    {
        None = 0,

       
        [CustomComboInfo("Redoublement combo", "Replaces Redoublement with its combo chain, following enchantment rules", 35)]
        RedMageMeleeCombo = 1 << 1
    }

    public class CustomComboInfoAttribute : Attribute
    {
        internal CustomComboInfoAttribute(string fancyName, string description, byte classJob)
        {
            FancyName = fancyName;
            Description = description;
            ClassJob = classJob;
        }

        public string FancyName { get; }
        public string Description { get; }
        public byte ClassJob { get; }
    }
}
