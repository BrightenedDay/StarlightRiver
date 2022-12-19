﻿using StarlightRiver.Content.CustomHooks;

namespace StarlightRiver.Core.Systems.LightingSystem
{
	class LightingBufferHooks : HookGroup
	{
		//Creates a RenderTarget for the lighting buffer. Could potentially be performance havy but shouldn't be dangerous.
		public override void Load()
		{
			if (Main.dedServ)
				return;

			Main.OnPreDraw += LightingTarget;
			On.Terraria.Main.SetDisplayMode += RefreshLightingTarget;
		}

		public override void Unload()
		{
			Main.OnPreDraw -= LightingTarget;
		}

		private void RefreshLightingTarget(On.Terraria.Main.orig_SetDisplayMode orig, int width, int height, bool fullscreen)
		{
			if (!Main.gameInactive && width != Main.screenWidth || height != Main.screenHeight)
				StarlightRiver.lightingBufferInstance?.ResizeBuffers(width, height);

			orig(width, height, fullscreen);
		}

		private void LightingTarget(GameTime obj)
		{
			if (Main.dedServ)
				return;

			if (!Main.gameMenu)
				StarlightRiver.lightingBufferInstance.DebugDraw();

			Main.instance.GraphicsDevice.SetRenderTarget(null);
		}
	}
}