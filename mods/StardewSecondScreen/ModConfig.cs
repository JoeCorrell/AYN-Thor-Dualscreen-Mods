namespace StardewSecondScreen
{

    internal sealed class ModConfig
    {

        public bool HideGameHud { get; set; } = true;

        public bool AllowInventoryEdits { get; set; } = true;

        public bool AllowQuestCancel { get; set; } = true;

        public bool FarmerMarker { get; set; } = true;

        public bool SendCrops { get; set; } = true;
        public bool SendMachines { get; set; } = true;
        public bool SendAnimals { get; set; } = true;
        public bool SendBundles { get; set; } = true;
        public bool SendVillagers { get; set; } = true;

        public bool SendMap { get; set; } = true;
    }
}
