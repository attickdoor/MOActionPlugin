using System;

namespace MOActionPlugin
{
    //CURRENT HIGHEST FLAG IS 51
    [Flags]
    public enum MOActionPreset : int
    {
        None = 0,

        // GENERAL
        [MoActionInfo(7568, "Esuna", false, 0)]
        GeneralEsuna = 1 << 1,
        [MoActionInfo(7571, "Rescue", false, 0)]
        GeneralRescue = 1 << 2,
        [MoActionInfo(17688, "Rescue", true, 0)]
        GeneralRescuePvP = 1 << 3,

        // ASTROLOGIAN
        [MoActionInfo(3594, "Benefic", false, 33)]
        AstrologianBenefic = 1 << 4,
        [MoActionInfo(3610, "Benefic II", false, 33)]
        AstrologianBenefic2 = 1 << 5,
        [MoActionInfo(3614, "Essential Dignity", false, 33)]
        AstrologianEssentialDignity = 1 << 6,
        [MoActionInfo(8916, "Essential Dignity", true, 33)]
        AstrologianEssentialDignityPvP = 1 << 7,
        [MoActionInfo(3595, "Aspected Benefic", false, 33)]
        AstrologianAspectedBenefic = 1 << 8,
        [MoActionInfo(17804, "Aspected Benefic", true, 33)]
        AstrologianAspectedBeneficPvP = 1 << 9,
        [MoActionInfo(16556, "Celestial Intersection", false, 33)]
        AstrologianCelestialIntersection = 1 << 10,
        [MoActionInfo(3612, "Synastry", false, 33)]
        AstrologianSynastry = 1 << 11,
        [MoActionInfo(3606, "Ascend", false, 33)]
        AstrologianAscend = 1 << 12,
        [MoActionInfo(4404, "Bole", false, 33)]
        AstrologianBole = 1 << 22,
        [MoActionInfo(4401, "Balance", false, 33)]
        AstrologianBalance = 1 << 23,
        [MoActionInfo(4403, "Spear", false, 33)]
        AstrologianSpear = 1 << 24,
        [MoActionInfo(4402, "Arrow", false, 33)]
        AstrologianArrow = 1 << 25,
        [MoActionInfo(4405, "Spire", false, 33)]
        AstrologianSpire = 1 << 26,
        [MoActionInfo(4406, "Ewer", false, 33)]
        AstrologianEwer = 1 << 27,
        [MoActionInfo(7444, "Lord", false, 33)]
        AstrologianLord = 1 << 28,
        [MoActionInfo(7445, "Lady", false, 33)]
        AstrologianLady = 1 << 29,
        // WHITE MAGE
        [MoActionInfo(120, "Cure", false, 24)]
        WhiteMageCure = 1 << 13,
        [MoActionInfo(135, "Cure II", false, 24)]
        WhiteMageCure2 = 1 << 14,
        [MoActionInfo(131, "Cure III", false, 24)]
        WhiteMageCure3 = 1 << 15,
        [MoActionInfo(137, "Regen", false, 24)]
        WhiteMageRegen = 1 << 16,
        [MoActionInfo(7432, "Divine Benison", false, 24)]
        WhiteMageDivineBenison = 1 << 17,
        [MoActionInfo(140, "Benediction", false, 24)]
        WhiteMageBenediction = 1 << 18,
        [MoActionInfo(3570, "Tetragrammaton", false, 24)]
        WhiteMageTetragrammaton = 1 << 19,
        [MoActionInfo(13975, "Tetragrammaton", true, 24)]
        WhiteMageTetragrammatonPvP = 1 << 20,
        [MoActionInfo(125, "Raise", false, 24)]
        WhiteMageRaise = 1 << 21,

        // SCHOLAR
        [MoActionInfo(190, "Physick", false, 24)]
        ScholarPhysick = 1 << 13,
        [MoActionInfo(185, "Adloquium", false, 24)]
        ScholarAdloquium = 1 << 13,
        [MoActionInfo(189, "Lustrate", false, 24)]
        ScholarLustrate = 1 << 13,
        [MoActionInfo(8909, "Lustrate", true, 24)]
        ScholarLustratePvP = 1 << 13,
        [MoActionInfo(7434, "Excogitation", false, 24)]
        ScholarExcogitation = 1 << 13,
        [MoActionInfo(18949, "Excogitation", true, 24)]
        ScholarExcogitationPvP = 1 << 13,
        [MoActionInfo(173, "Resurrection", false, 24)]
        ScholarResurrection = 1 << 13,

        EnableGuiMouseover = 1 << 22,
        EnableFieldMouseover = 1 << 23
    }

    public class MoActionInfoAttribute : Attribute
    {
        internal MoActionInfoAttribute(int actionId, string name, bool isPvP, byte classJob)
        {
            ActionId = actionId;
            Name = name;
            IsPvP = isPvP;
            ClassJob = classJob;
        }

        public int ActionId { get; }
        public string Name { get; }
        public bool IsPvP { get; }
        public byte ClassJob { get; }
    }
}
