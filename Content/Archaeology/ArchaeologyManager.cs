﻿//Artifact general TODO:
//Better spawning method that checks to make sure the entire thing is buried
//Better tilechecck call method
//Better detour for drawing
//Uncomment buggy auroracle arena spawning
//Make archaeologists map obtainable


using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StarlightRiver.Content.GUI;
using StarlightRiver.Core;
using StarlightRiver.Packets;
using StarlightRiver.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.Map;
using Terraria.UI;

namespace StarlightRiver.Content.Archaeology
{
    public class ArchaeologyManager : ModSystem
    { 
        public override void Load()
        {
            On.Terraria.Main.DrawTiles += DrawArtifacts;
        }

        public override void Unload()
        {
            On.Terraria.Main.DrawTiles -= DrawArtifacts;
        }

        public override void PreUpdateDusts()
        {
            foreach (var item in TileEntity.ByID)
            {
                if (item.Value is Artifact artifact && artifact.IsOnScreen())
                {
                    artifact.CreateSparkles();
                }
            }
        }

        public void DrawArtifacts(On.Terraria.Main.orig_DrawTiles orig, Main self, bool solidLayer, bool forRenderTargets, bool intoRenderTargets, int waterStyleOverride = -1)
        {
            foreach (var item in TileEntity.ByID)
            {
                if (item.Value is Artifact artifact && artifact.IsOnScreen())
                {
                    artifact.Draw(Main.spriteBatch);
                }
            }
            orig(self, solidLayer, forRenderTargets, intoRenderTargets, waterStyleOverride);
        }
    }

    public class ArchaeologyMapLayer : ModMapLayer
    {
        public override void Draw(ref MapOverlayDrawContext context, ref string text)
        {
            var toDraw = TileEntity.ByID.Where(x => x.Value is Artifact artifact && artifact.displayedOnMap);
            foreach (var drawable in toDraw)
            {
                Artifact artifact = (Artifact) drawable.Value;
                Texture2D mapTex = ModContent.Request<Texture2D>(artifact.MapTexturePath).Value;

                if (context.Draw(mapTex, artifact.Position.ToVector2(), Color.White, new SpriteFrame(1, 1, 0, 0), 1, 1, Alignment.Center).IsMouseOver)
                    text = "Artifact";
            }
        }
    }
}