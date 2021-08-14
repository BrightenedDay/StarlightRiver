﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using StarlightRiver.Core;
using System;
using System.Reflection;
using System.Linq;
using Terraria;

namespace StarlightRiver.Content.CustomHooks
{
	class ZoomOutEnabler : HookGroup
	{
		//this is super hacky
		public override SafetyLevel Safety => SafetyLevel.OhGodOhFuck;

		public override void Load()
		{
			if (Main.dedServ)
				return;

			IL.Terraria.Main.DrawTiles += DrawZoomOut;
			IL.Terraria.Main.InitTargets_int_int += ChangeTargets;

			//This is... a thing
			IL.Terraria.Lighting.AddLight_int_int_float_float_float += ResizeLighting;

			IL.Terraria.Lighting.LightTiles += ResizeLighting;
			IL.Terraria.Lighting.PreRenderPhase += ResizeLighting;
			IL.Terraria.Lighting.PreRenderPhase += ResizeOcclusion;
			IL.Terraria.Lighting.Brightness += ResizeLighting;

			IL.Terraria.Lighting.GetBlackness += ResizeLighting;

			IL.Terraria.Lighting.GetColor_int_int += ResizeLighting;
			IL.Terraria.Lighting.GetColor_int_int_Color += ResizeLighting;

			IL.Terraria.Lighting.GetColor4Slice += ResizeLighting;
			IL.Terraria.Lighting.GetColor4Slice_New_int_int_refVertexColors_Color_float += ResizeLighting;
			IL.Terraria.Lighting.GetColor4Slice_New_int_int_refVertexColors_float += ResizeLighting;

			IL.Terraria.Lighting.GetColor9Slice += ResizeLighting;

			IL.Terraria.Lighting.Initialize += ResizeLightingBig;

			IL.Terraria.Lighting.doColors += HackSwipes;

			IL.Terraria.Main.DrawBlack += ResizeLighting;

			IL.Terraria.Lighting.GetColor_int_int += TestRed;

			On.Terraria.Lighting.PreRenderPhase += ReInit;
			On.Terraria.Lighting.LightTiles += MoveLighting;
		}

		private void MoveLighting(On.Terraria.Lighting.orig_LightTiles orig, int firstX, int lastX, int firstY, int lastY)
		{
			orig(firstX - AddExpansion() / 2, lastX + AddExpansion() / 2, firstY - AddExpansionY() / 2, lastY+ AddExpansionY() / 2);
		}

		private void TestRed(ILContext il)
		{
			ILCursor c = new ILCursor(il);
			c.TryGotoNext(MoveType.After, n => n.MatchCall<Color>("get_Black"));
			c.Emit(OpCodes.Pop);
			c.EmitDelegate<Func<Color>>(red);
		}

		Color red() => Color.Red;

		private void ResizeLightingBig(ILContext il)
		{
			var c = new ILCursor(il);

			c.TryGotoNext(MoveType.After, n => n.MatchLdsfld<Main>("screenWidth"));
			c.Emit(OpCodes.Pop);
			c.Emit(OpCodes.Ldc_I4, 6);
			c.EmitDelegate<Func<int, int>>(ReplaceDimension);

			c.TryGotoNext(MoveType.After, n => n.MatchLdsfld<Main>("screenHeight"));
			c.Emit(OpCodes.Pop);
			c.Emit(OpCodes.Ldc_I4, 7);
			c.EmitDelegate<Func<int, int>>(ReplaceDimension);
		}

		private void ResizeOcclusion(ILContext il)
		{
			var c = new ILCursor(il);

			c.TryGotoNext(n => n.MatchLdsfld<Lighting>("firstToLightX"));

			c.TryGotoNext(MoveType.After, n => n.MatchStloc(5));
			c.Emit(OpCodes.Ldloc, 5);
			c.EmitDelegate<Func<int>>(AddExpansion);
			c.Emit(OpCodes.Add);
			c.Emit(OpCodes.Stloc, 5);


			c.TryGotoNext(MoveType.After, n => n.MatchStloc(5));
			c.Emit(OpCodes.Ldloc, 7);
			c.EmitDelegate<Func<int>>(AddExpansionY);
			c.Emit(OpCodes.Add);
			c.Emit(OpCodes.Stloc, 7);
		}

		private int AddExpansion()
		{
			return ((int)Math.Floor(((Main.screenPosition.X + (Main.screenWidth * (1f / Core.ZoomHandler.ExtraZoomTarget))) / 16f)) + 2) - ((int)Math.Floor(((Main.screenPosition.X + Main.screenWidth) / 16f)) + 2);
		}

		private int AddExpansionY()
		{
			return ((int)Math.Floor(((Main.screenPosition.Y + (Main.screenHeight * (1f / Core.ZoomHandler.ExtraZoomTarget))) / 16f)) + 2) - ((int)Math.Floor(((Main.screenPosition.Y + Main.screenHeight) / 16f)) + 2);
		}

		private void HackSwipes(ILContext il)
		{
			var c = new ILCursor(il);

			for (int k = 0; k < 4; k++)
			{
				//HackSwipe(c, "innerLoop1Start", k % 2 == 0, false); //if its X or Y should alternate every other patch
				HackSwipe(c, "innerLoop1End", k % 2 == 0, true);
				//HackSwipe(c, "innerLoop2Start", k % 2 == 0, true);
				HackSwipe(c, "innerLoop2End", k % 2 == 0, true);
				//HackSwipe(c, "outerLoopStart", k % 2 == 1, false);
				HackSwipe(c, "outerLoopEnd", k % 2 == 1, true);
			}
		}

		private void HackSwipe(ILCursor c, string name, bool y, bool add)
		{
			var types = typeof(Lighting).GetTypeInfo().DeclaredNestedTypes;
			var type = types.FirstOrDefault(n => n.FullName == "Terraria.Lighting+LightingSwipeData");

			c.TryGotoNext(MoveType.Before, n => n.MatchStfld(type, name));

			if(y)
				c.EmitDelegate<Func<int>>(AddExpansionY);

			else
				c.EmitDelegate<Func<int>>(AddExpansion);

			c.Emit(add ? OpCodes.Add : OpCodes.Sub);
		}

		private void ReInit(On.Terraria.Lighting.orig_PreRenderPhase orig)
		{
			Lighting.Initialize(true);
			//Buttfuckery.Lighting.Initialize();

			try
			{
				//Buttfuckery.Lighting.PreRenderPhase();
				orig();
			}
			catch
			{
				int a = 0;
			}
		}

		private void ResizeLighting(ILContext il)
		{
			var c = new ILCursor(il);

			c.TryGotoNext(MoveType.After, n => n.MatchLdsfld<Main>("screenWidth"));
			c.Emit(OpCodes.Pop);
			c.Emit(OpCodes.Ldc_I4, 4);
			c.EmitDelegate<Func<int, int>>(ReplaceDimension);

			c.TryGotoNext(MoveType.After, n => n.MatchLdsfld<Main>("screenHeight"));
			c.Emit(OpCodes.Pop);
			c.Emit(OpCodes.Ldc_I4, 5);
			c.EmitDelegate<Func<int, int>>(ReplaceDimension);
		}

		private void ChangeTargets(ILContext il)
		{
			var c = new ILCursor(il);

			SwapTarget(c, "waterTarget");
			SwapTarget(c, "backWaterTarget");
			SwapTarget(c, "blackTarget");
			SwapTarget(c, "tileTarget");
			SwapTarget(c, "tile2Target");
			SwapTarget(c, "wallTarget");
		}

		private void SwapTarget(ILCursor c, string name)
		{
			if (!c.TryGotoNext(MoveType.Before,
			i => i.MatchStfld<Main>(name)))
				return;

			c.Emit(OpCodes.Pop);
			c.EmitDelegate<Func<RenderTarget2D>>(returnNewTileTarget);
		}

		private RenderTarget2D returnNewTileTarget()
		{
			return new RenderTarget2D(Main.graphics.GraphicsDevice, Main.screenWidth * 3, Main.screenHeight * 3, false, Main.graphics.GraphicsDevice.PresentationParameters.BackBufferFormat, DepthFormat.Depth24);
		}

		private void DrawZoomOut(ILContext il)
		{
			/*
            var c = new ILCursor(il);
            
            if (!c.TryGotoNext(MoveType.After,
                i => i.MatchSub(),
                i => i.MatchLdcR4(16),
                i => i.MatchDiv(),
                i => i.MatchLdcR4(1),
                i => i.MatchSub()))
                return;

            c.EmitDelegate<Func<float, float>>((returnvalue) =>
            {
                return (int)((Main.screenPosition.X - 1600) / 16f - 1f);
            });

            /*
                IL_00B5: add
                IL_00B6: ldc.r4    16
                IL_00BB: div
                IL_00BC: conv.i4
                IL_00BD: ldc.i4.2
                IL_00BE: add
                ---> here
            *//*
            if (!c.TryGotoNext(MoveType.After,
                i => i.MatchAdd(),
                i => i.MatchLdcR4(16),
                i => i.MatchDiv(),
                i => i.MatchConvI4(),
                i => i.MatchLdcI4(2),
                i => i.MatchAdd()))
                return;

            c.EmitDelegate<Func<int, int>>((returnvalue) =>
            {
                return (int)((Main.screenPosition.X + Main.screenWidth + 1600) / 16f + 2);
            });
            */
			var c = new ILCursor(il);

			if (!c.TryGotoNext(MoveType.After,
				i => i.MatchStloc(8)))
				return;

			c.Emit(OpCodes.Ldc_I4_0);
			c.EmitDelegate<Func<int, int>>(ReplaceDimension);
			c.Emit(OpCodes.Stloc, 5);

			c.Emit(OpCodes.Ldc_I4_1);
			c.EmitDelegate<Func<int, int>>(ReplaceDimension);
			c.Emit(OpCodes.Stloc, 6);

			c.Emit(OpCodes.Ldc_I4_2);
			c.EmitDelegate<Func<int, int>>(ReplaceDimension);
			c.Emit(OpCodes.Stloc, 7);

			c.Emit(OpCodes.Ldc_I4_3);
			c.EmitDelegate<Func<int, int>>(ReplaceDimension);
			c.Emit(OpCodes.Stloc, 8);
		}

		private int ReplaceDimension(int index)
		{
			Vector2 zero = new Vector2(Main.offScreenRange, Main.offScreenRange);

			if (Main.drawToScreen)
				zero = Vector2.Zero;

			switch (index)
			{
				case 0:
					return (int)((Main.screenPosition.X - zero.X) / 16f - 1f);

				case 1:
					return (int)((Main.screenPosition.X + Main.screenWidth * (1f / ZoomHandler.ExtraZoomTarget) + zero.X) / 16f) + 2;

				case 2:
					return (int)((Main.screenPosition.Y - zero.Y) / 16f - 1f);

				case 3:
					return (int)((Main.screenPosition.Y + Main.screenHeight * (1f / ZoomHandler.ExtraZoomTarget) + zero.Y) / 16f) + 5;

				case 4:
					return (int)(Main.screenWidth * (1f / ZoomHandler.ExtraZoomTarget));

				case 5:
					return (int)(Main.screenHeight * (1f / ZoomHandler.ExtraZoomTarget));

				case 6:
					return (int)(Main.screenWidth * (1f / ZoomHandler.ExtraZoomTarget)) * 2;

				case 7:
					return (int)(Main.screenHeight * (1f / ZoomHandler.ExtraZoomTarget)) * 2;

				default:
					return 0;
			}
		}
	}

}
