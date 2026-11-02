using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>The final level's palette: the user's ambientCG wall/floor/ceiling sets (UserTextures) and the CC0
    /// exterior set downloaded for the campus (Assets/Campus/Textures).</summary>
    public static class CampusMats
    {
        public static Material BrickExt, BrickRed, Plaster, PlasterCool, WoodPanel, Concrete, ConcreteDark, Kerb,
            TileLobby, TileVest, WoodFloor, Planks, Carpet, CarpetRed, Ceiling, CeilingTiles,
            Asphalt, Paving, Gravel, Dirt, Grass, Hedge, Roof,
            Metal, MetalLight, Iron, Glass, GlassDark, DoorWood, Water, Marble, Rubber, Paint, CarBody, CarBody2, CarBody3, Tarp,
            GlowWarm, GlowCold, GlowSodium, GlowExit, GlowScreen, GlowRed, GlowGreen, GlowClock, Dark, Black;

        const string U = "Assets/HollowTowerRemastered/Textures/UserTextures/";
        const string C = "Assets/Campus/Textures/";

        static string UC(string cat, string n) { return U + cat + "/" + n + "_2K-PNG/" + n + "_2K-PNG_Color.png"; }
        static string UN(string cat, string n) { return U + cat + "/" + n + "_2K-PNG/" + n + "_2K-PNG_NormalGL.png"; }
        static string CC(string n) { return C + n + "/" + n + "_1K-JPG_Color.jpg"; }
        static string CN(string n) { return C + n + "/" + n + "_1K-JPG_NormalGL.jpg"; }

        public static void Load()
        {
            BrickExt = CampusKit.Tex("CM_BrickExterior", CC("Bricks076C"), CN("Bricks076C"), 2.6f, new Color(0.62f, 0.58f, 0.56f), 0.12f);
            BrickRed = CampusKit.Tex("CM_BrickRed", UC("floors", "Bricks066"), UN("floors", "Bricks066"), 2.4f, new Color(0.7f, 0.6f, 0.55f), 0.1f);
            Plaster = CampusKit.Tex("CM_Plaster", UC("walls", "PaintedBricks001"), UN("walls", "PaintedBricks001"), 3f, new Color(0.72f, 0.7f, 0.66f), 0.15f);
            PlasterCool = CampusKit.Tex("CM_PlasterCool", UC("walls", "PaintedBricks003"), UN("walls", "PaintedBricks003"), 3f, new Color(0.62f, 0.66f, 0.68f), 0.15f);
            WoodPanel = CampusKit.Tex("CM_WoodPanel", UC("walls", "WoodSiding008"), UN("walls", "WoodSiding008"), 2.4f, new Color(0.55f, 0.42f, 0.32f), 0.35f);
            Concrete = CampusKit.Tex("CM_Concrete", CC("Concrete034"), CN("Concrete034"), 4f, new Color(0.62f, 0.62f, 0.6f), 0.1f);
            ConcreteDark = CampusKit.Tex("CM_ConcreteDark", CC("Concrete034"), CN("Concrete034"), 4f, new Color(0.46f, 0.46f, 0.45f), 0.1f);
            Kerb = CampusKit.Tex("CM_Kerb", CC("Concrete042A"), CN("Concrete042A"), 2f, new Color(0.6f, 0.6f, 0.58f), 0.1f);
            TileLobby = CampusKit.Tex("CM_TileLobby", UC("floors", "Tiles143"), UN("floors", "Tiles143"), 2f, new Color(0.8f, 0.78f, 0.74f), 0.55f);
            TileVest = CampusKit.Tex("CM_TileVest", U + "floors/Tiles007_2K-JPG/Tiles007_2K-JPG_Color.jpg", U + "floors/Tiles007_2K-JPG/Tiles007_2K-JPG_NormalGL.jpg", 1.6f, new Color(0.75f, 0.75f, 0.72f), 0.4f);
            WoodFloor = CampusKit.Tex("CM_WoodFloor", UC("floors", "WoodFloor043"), UN("floors", "WoodFloor043"), 2.5f, new Color(0.75f, 0.68f, 0.6f), 0.4f);
            Planks = CampusKit.Tex("CM_Planks", UC("floors", "Planks021"), UN("floors", "Planks021"), 2.5f, new Color(0.62f, 0.52f, 0.42f), 0.3f);
            Carpet = CampusKit.Tex("CM_Carpet", CC("Concrete034"), CN("Concrete034"), 1.5f, new Color(0.42f, 0.42f, 0.4f), 0.04f);
            CarpetRed = CampusKit.Tex("CM_CarpetRed", CC("Concrete034"), CN("Concrete034"), 1.5f, new Color(0.5f, 0.22f, 0.18f), 0.04f);
            Ceiling = CampusKit.Tex("CM_Ceiling", UC("ceilings", "OfficeCeiling005"), UN("ceilings", "OfficeCeiling005"), 1.2f, new Color(0.7f, 0.7f, 0.68f), 0.1f);
            CeilingTiles = CampusKit.Tex("CM_CeilingTiles", UC("ceilings", "OfficeCeiling001"), UN("ceilings", "OfficeCeiling001"), 1.2f, new Color(0.7f, 0.7f, 0.68f), 0.1f);
            Asphalt = CampusKit.Tex("CM_Asphalt", CC("Asphalt026C"), CN("Asphalt026C"), 6f, new Color(0.55f, 0.55f, 0.56f), 0.25f);
            Paving = CampusKit.Tex("CM_Paving", CC("PavingStones092"), CN("PavingStones092"), 2.5f, new Color(0.62f, 0.6f, 0.57f), 0.2f);
            Gravel = CampusKit.Tex("CM_Gravel", CC("Gravel022"), CN("Gravel022"), 3f, new Color(0.62f, 0.6f, 0.56f), 0.1f);
            Dirt = CampusKit.Tex("CM_Dirt", CC("Ground054"), CN("Ground054"), 5f, new Color(0.55f, 0.5f, 0.45f), 0.05f);
            Grass = CampusKit.Tex("CM_Grass", CC("Grass004"), CN("Grass004"), 5f, new Color(0.55f, 0.6f, 0.5f), 0.05f);
            Hedge = CampusKit.Tex("CM_Hedge", CC("Grass004"), CN("Grass004"), 1.2f, new Color(0.25f, 0.33f, 0.22f), 0.05f);
            Roof = CampusKit.Tex("CM_Roof", CC("Concrete042A"), CN("Concrete042A"), 5f, new Color(0.3f, 0.3f, 0.31f), 0.15f);
            Marble = CampusKit.Tex("CM_Marble", UC("floors", "Tiles143"), UN("floors", "Tiles143"), 3f, new Color(0.85f, 0.84f, 0.82f), 0.6f);
            Metal = CampusKit.Flat("CM_Metal", new Color(0.18f, 0.19f, 0.2f), 0.5f, 0.8f);
            MetalLight = CampusKit.Flat("CM_MetalLight", new Color(0.55f, 0.56f, 0.58f), 0.55f, 0.85f);
            Iron = CampusKit.Flat("CM_Iron", new Color(0.05f, 0.05f, 0.055f), 0.45f, 0.75f);
            DoorWood = CampusKit.Flat("CM_DoorWood", new Color(0.28f, 0.18f, 0.11f), 0.35f);
            Rubber = CampusKit.Flat("CM_Rubber", new Color(0.03f, 0.03f, 0.03f), 0.2f);
            Paint = CampusKit.Flat("CM_LinePaint", new Color(0.75f, 0.74f, 0.66f), 0.2f);
            CarBody = CampusKit.Flat("CM_CarBody", new Color(0.2f, 0.06f, 0.05f), 0.55f, 0.4f);
            CarBody2 = CampusKit.Flat("CM_CarBody2", new Color(0.08f, 0.1f, 0.14f), 0.55f, 0.4f);
            CarBody3 = CampusKit.Flat("CM_CarBody3", new Color(0.42f, 0.42f, 0.4f), 0.55f, 0.4f);
            Tarp = CampusKit.Flat("CM_Tarp", new Color(0.32f, 0.33f, 0.3f), 0.1f);
            Dark = CampusKit.Flat("CM_Dark", new Color(0.03f, 0.03f, 0.035f), 0.2f);
            Black = CampusKit.Flat("CM_Black", new Color(0f, 0f, 0f), 0f);
            Glass = CampusKit.Glass("CM_Glass", new Color(0.14f, 0.18f, 0.2f, 0.32f));
            GlassDark = CampusKit.Glass("CM_GlassDark", new Color(0.04f, 0.05f, 0.06f, 0.85f));
            Water = CampusKit.Glass("CM_Water", new Color(0.02f, 0.04f, 0.05f, 0.92f), 0.98f);
            GlowWarm = CampusKit.Glow("CM_GlowWarm", new Color(1f, 0.72f, 0.42f), 2.2f);
            GlowCold = CampusKit.Glow("CM_GlowCold", new Color(0.82f, 0.9f, 1f), 2f);
            GlowSodium = CampusKit.Glow("CM_GlowSodium", new Color(1f, 0.55f, 0.18f), 2.6f);
            GlowExit = CampusKit.Glow("CM_GlowExit", new Color(0.15f, 1f, 0.35f), 2.4f);
            GlowScreen = CampusKit.Glow("CM_GlowScreen", new Color(0.45f, 0.6f, 0.85f), 1.4f);
            GlowRed = CampusKit.Glow("CM_GlowRed", new Color(1f, 0.08f, 0.05f), 3f);
            GlowGreen = CampusKit.Glow("CM_GlowGreen", new Color(0.2f, 1f, 0.3f), 2f);
            GlowClock = CampusKit.Glow("CM_GlowClock", new Color(0.95f, 0.9f, 0.7f), 0.9f);
        }
    }
}
