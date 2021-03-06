﻿namespace DataTool.ToolLogic.Extract {
    [Tool("extract-npcs", Description = "Extract npcs", TrackTypes = new ushort[] { 0x75 }, CustomFlags = typeof(ExtractFlags))]
    // ReSharper disable once InconsistentNaming
    public class ExtractNPCs : ExtractHeroUnlocks {
        protected override string RootDir => "NPCs";
        protected override bool NPCs => true;
    }
}