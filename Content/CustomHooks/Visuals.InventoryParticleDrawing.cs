﻿using StarlightRiver.Content.Items.BaseTypes;

namespace StarlightRiver.Content.CustomHooks
{
	class InventoryParticleDrawing : HookGroup
	{
		//Just drawing some ParticleSystems over the inventory UI. Nothing bad.
		public override void Load()
		{
			if (Main.dedServ)
				return;

			Terraria.On_Main.DrawInterface_27_Inventory += DrawInventoryParticles;
		}

		private void DrawInventoryParticles(Terraria.On_Main.orig_DrawInterface_27_Inventory orig, Main self)
		{
			orig(self);
			CursedAccessory.CursedSystem.DrawParticles(Main.spriteBatch);
			CursedAccessory.ShardsSystem.DrawParticles(Main.spriteBatch);
		}
	}
}