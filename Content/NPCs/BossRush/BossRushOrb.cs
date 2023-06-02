﻿using ReLogic.Content;
using StarlightRiver.Content.Backgrounds;
using StarlightRiver.Core.Systems.CameraSystem;
using StarlightRiver.Core.Systems.ScreenTargetSystem;
using StarlightRiver.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.Effects;
using Terraria.ID;

namespace StarlightRiver.Content.NPCs.BossRush
{
	internal class BossRushOrb : ModNPC, ILoadable
	{
		const float MAX_CRACK_ANIMATION = 300;
		const float MAX_SUCC_ANIMATION = 300;

		public static EaseBuilder VoidEase;

		public static List<BossRushOrb> activeBossRushLocks = new();

		public Vector2 originalPos;

		public bool isPlayingCrack = false;
		public bool isPlayingWarp = false;
		public float crackAnimationTimer = 0;
		public float warpAnimationTimer = 0;
		public float freezeTimer = 0;

		public ParticleSystem starlight;
		public ParticleSystem starlightLines;

		public float starViewScale = 1;

		public float CrackAnimationProgress => crackAnimationTimer / MAX_CRACK_ANIMATION;

		public override string Texture => "StarlightRiver/Assets/NPCs/BossRush/BossRushOrb";

		public override void Load()
		{
			StarlightRiverBackground.DrawMapEvent += DrawMap;
			StarlightRiverBackground.DrawOverlayEvent += DrawOverlay;
			StarlightRiverBackground.CheckIsActiveEvent += () => activeBossRushLocks.Count > 0;

			VoidEase = new EaseBuilder();
			VoidEase.AddPoint(new Vector2(0, 0.25f), EaseFunction.EaseCubicIn);
			VoidEase.AddPoint(new Vector2(45, 1.5f), new CubicBezierEase(0.2f, 1.5f, .8f, 1.5f));
			VoidEase.AddPoint(new Vector2(75, 0.5f), new CubicBezierEase(0.15f, 0.6f, .5f, 1f));
			VoidEase.AddPoint(new Vector2(MAX_SUCC_ANIMATION, 0.1f), new CubicBezierEase(0.15f, 0.6f, .5f, 1f));
			VoidEase.AddPoint(new Vector2(MAX_SUCC_ANIMATION + 20, 2f), EaseFunction.EaseCubicInOut);
		}

		public override void SetDefaults()
		{
			NPC.lifeMax = 400;
			NPC.width = 42;
			NPC.height = 42;
			NPC.aiStyle = -1;
			NPC.noGravity = true;
			NPC.knockBackResist = 0f;

			NPC.HitSound = new SoundStyle($"{nameof(StarlightRiver)}/Sounds/Impacts/Clink");
			Music = MusicLoader.GetMusicSlot(Mod, "Sounds/Music/Moonstone");
		}

		public override void OnSpawn(IEntitySource source)
		{
			activeBossRushLocks.Add(this);
			originalPos = NPC.Center;
			starlight = new("StarlightRiver/Assets/Misc/DotTell", UpdateStars, ParticleSystem.AnchorOptions.World);
			starlightLines = new(AssetDirectory.Dust + "RoarLine", UpdateStars, ParticleSystem.AnchorOptions.World);
		}

		public override void AI()
		{
			if (isPlayingWarp)
			{
				if (warpAnimationTimer == 0)
				{
					for (int i = 0; i < 100; i++)
					{
						Vector2 rand = Vector2.One.RotateRandom(MathHelper.TwoPi) * 20;

						Dust.NewDustPerfect(NPC.Center + rand, DustID.Obsidian, rand / 2 * (0.5f + Main.rand.NextFloat() * 0.5f));
						Dust.NewDustPerfect(NPC.Center, ModContent.DustType<Dusts.Starlight>(), rand * 7.5f * (0.5f + Main.rand.NextFloat() * 0.5f));
					}

					for (int i = 0; i < 12; i++)
					{
						Vector2 rand = -Vector2.One.RotateRandom(MathHelper.TwoPi) * 10;
						Projectile.NewProjectile(NPC.GetSource_FromThis(), NPC.Center + rand, rand * (0.5f + Main.rand.NextFloat() * 0.5f), ModContent.ProjectileType<BossRushOrbGoreProjectile>(), -1, 0);
					}
				}

				if (warpAnimationTimer < 45)
				{
					float warpStrength = warpAnimationTimer / 45;
					Filters.Scene.Activate("Shockwave", NPC.Center).GetShader().UseProgress(2f).UseIntensity(100 - (1 - warpStrength) * 100).UseDirection(new Vector2(1, 2) * 0.1f * warpStrength);
				}
				else if (warpAnimationTimer > MAX_SUCC_ANIMATION + 30)
				{
					if (Filters.Scene["Shockwave"].IsActive())
						Filters.Scene.Deactivate("Shockwave");

					if (Filters.Scene["ColorSucker"].IsActive())
						Filters.Scene.Deactivate("ColorSucker");
				}
				else if (warpAnimationTimer > 250)
				{
					float grey = (warpAnimationTimer - 250f) / 30f;
					Filters.Scene.Activate("ColorSucker").GetShader().UseIntensity(grey);
				}
				else if (warpAnimationTimer < 180)
				{
					for (int i = 0; i < Math.Pow(warpAnimationTimer / 40, 2); i++)
					{
						Vector2 pos = NPC.Center + Vector2.One.RotatedByRandom(MathHelper.TwoPi) * Main.screenWidth / 2 * Main.rand.NextFloat();
						var starColor = new Color(150, Main.rand.Next(150, 255), 255)
						{
							A = 0
						};
						starlight.AddParticle(new Particle(pos, Vector2.Zero, 0, Main.rand.NextFloat(0.2f), starColor, 600, Vector2.One * Main.rand.NextFloat(3f), default, 1));

						if (Main.rand.NextBool())
						{
							pos = NPC.Center + Vector2.One.RotatedByRandom(MathHelper.TwoPi) * Main.screenWidth / 2 * (Main.rand.NextFloat() + 0.7f);
							starlightLines.AddParticle(new Particle(pos, Vector2.Zero, 0, Main.rand.NextFloat(0.5f), starColor, 600, Vector2.One * Main.rand.NextFloat(3f), default, 1));
						}
					}
				}

				warpAnimationTimer++;

				if (warpAnimationTimer > MAX_SUCC_ANIMATION && warpAnimationTimer < MAX_SUCC_ANIMATION + 240)
				{
					float bgStarOpacity = Math.Min((warpAnimationTimer - MAX_SUCC_ANIMATION - 120) / 60f, 1);

					var starColor = new Color(150, Main.rand.Next(150, 255), 255)
					{
						A = 0
					};

					for (int i = 0; i < 6 * (1 - bgStarOpacity); i++)
					{
						Vector2 direction = Vector2.One.RotateRandom(MathHelper.TwoPi) * Main.rand.NextFloat(0.9f, 1.1f);

						Vector2 pos = NPC.Center - Main.screenPosition + direction * 20;
						StarlightRiverBackground.stars.AddParticle(new Particle(pos, direction * 30 * (0.6f + (1 - bgStarOpacity) * 0.2f), 0, 0, starColor, 60, Vector2.One * Main.rand.NextFloat(3f, 3.3f), default, 1, 1));
					}

					StarlightRiverBackground.starOpacity = bgStarOpacity;
				}
				else if (warpAnimationTimer < MAX_SUCC_ANIMATION)
				{
					StarlightRiverBackground.starOpacity = 0;
				}
				else if (warpAnimationTimer == MAX_SUCC_ANIMATION) {

					Helper.PlayPitched("BossRush/ArmillaryExplode", 1, 0f, NPC.Center);
				}

				starViewScale = VoidEase.Ease(warpAnimationTimer);

				Lighting.AddLight(NPC.Center, new Vector3(1, 1, 2));

				if (warpAnimationTimer > 600)
					NPC.Kill();
			}
			else if (isPlayingCrack)
			{
				CameraSystem.DoPanAnimation(800, originalPos);

				if (CrackAnimationProgress >= 1)
					isPlayingWarp = true;

				if (crackAnimationTimer % 100 == 0)
				{
					NPC.velocity = Vector2.Zero;
					CameraSystem.shake += 30;

					for (int i = 0; i < 50; i++)
					{
						Vector2 rand = Main.rand.NextVector2Circular(10, 10);
						Dust.NewDustPerfect(NPC.Center + rand, DustID.Obsidian, rand);
						Dust.NewDustPerfect(NPC.Center, ModContent.DustType<Dusts.Starlight>(), rand * 2);
					}
				}

				NPC.velocity = Vector2.Lerp((originalPos - NPC.Center) * 0.01f, NPC.velocity, 0.95f);
				Vibrate(0.5f + CrackAnimationProgress * 0.5f);

				Lighting.AddLight(NPC.Center, new Vector3(0.5f, 0.5f, 1f) * (1 + CrackAnimationProgress));

				if (crackAnimationTimer == 0)
				{
					Music = MusicLoader.GetMusicSlot(Mod, "ThisSoundDoesNotExist");
					Helper.PlayPitched("BossRush/ArmillaryCrack1", 1, 0f, NPC.Center);
				}
				else if (crackAnimationTimer == 170)
				{
					Helper.PlayPitched("BossRush/ArmillaryCrack2", 1, 0f, NPC.Center);
				}
				else if (crackAnimationTimer == 255)
				{
					Helper.PlayPitched("BossRush/ArmillaryBreak", 1, 0f, NPC.Center);
				}

				crackAnimationTimer++;
			}
			else
			{
				if (NPC.life < NPC.lifeMax / 2)
				{
					Vibrate(1f - NPC.life / (NPC.lifeMax / 2f));
				}

				// starViewScale = 1 + 0.5f * (1f - (float)NPC.life / NPC.lifeMax);
				starViewScale = 0.25f;

				NPC.velocity = Vector2.Lerp((originalPos - NPC.Center) * 0.1f, NPC.velocity, 0.9f);

				Lighting.AddLight(NPC.Center, new Vector3(0.5f, 0.5f, 1f));
			}
		}

		public override void OnHitByItem(Player player, Item item, NPC.HitInfo hit, int damageDone)
		{
			hit.HideCombatText = true;

			if (NPC.velocity.Length() < 3)
			{
				NPC.velocity += (NPC.Center - player.Center).SafeNormalize(Vector2.Zero);
			}
		}

		public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
		{
			hit.HideCombatText = true;

			if (NPC.velocity.Length() < 3)
			{
				NPC.velocity += (NPC.Center - projectile.Center).SafeNormalize(Vector2.Zero);
			}
		}

		public override bool CheckDead()
		{
			if (isPlayingWarp && warpAnimationTimer / 300 > 1)
				return true;

			NPC.life = 1;
			NPC.immortal = true;
			NPC.dontTakeDamage = true;
			NPC.active = true;

			isPlayingCrack = true;

			return false;
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			float crackProgress;
			starlight?.DrawParticles(spriteBatch);
			starlightLines?.DrawParticles(spriteBatch);

			if (isPlayingCrack)
			{
				int cracks = (int)crackAnimationTimer / 100;
				crackProgress = 0.1f + cracks * 0.2f;
			}
			else
			{
				crackProgress = (1f - NPC.life / (NPC.lifeMax / 2f)) * 0.1f;
			}

			if (isPlayingWarp)
			{
				DrawCollapse(0, 1, 0);
				DrawCollapse(40, 1.1f, 1.2f);
				DrawCollapse(75, 1.2f, 3.4f);
				DrawCollapse(105, 1.3f, 11.2f);
				DrawCollapse(135, 1.4f, 23.6f);
				DrawCollapse(160, 1.5f, 2.7f);
				DrawCollapse(180, 1.6f, 5.1f);
				DrawCollapse(195, 1.7f, 17.8f);
				DrawCollapse(205, 1.8f, 5.8f);
				DrawCollapse(210, 1.9f, 16.9f);
				return false;
			}

			if (isPlayingCrack || NPC.life < NPC.lifeMax / 2)
			{
				DrawBubbleCracks(crackProgress);
				DrawGlow(crackProgress);
				return false;
			}

			return true;
		}

		private void Vibrate(float intensity)
		{
			Vector2 offset;
			offset = Main.rand.NextVector2Circular(2, 2) * intensity;

			if ((NPC.Center + offset - originalPos).Length() >= 20)
			{
				NPC.Center -= offset;
				return;
			}

			NPC.Center += offset;
		}

		private void DrawBubbleCracks(float crackProgress)
		{
			Main.spriteBatch.Draw(ModContent.Request<Texture2D>(Texture).Value, NPC.Center - Main.screenPosition, null, Color.White, 0, ModContent.Request<Texture2D>(Texture).Size() * 0.5f, NPC.scale, SpriteEffects.None, 0);

			Effect crack = Filters.Scene["OnyxCracks"].GetShader().Shader;
			crack.Parameters["sampleTexture2"].SetValue(ModContent.Request<Texture2D>(AssetDirectory.VitricBoss + "CrackMap").Value);
			crack.Parameters["sampleTexture3"].SetValue(ModContent.Request<Texture2D>(AssetDirectory.Glassweaver + "BubbleCrackProgression").Value);
			crack.Parameters["uTime"].SetValue(crackProgress);
			crack.Parameters["drawColor"].SetValue(Color.White.ToVector4());
			crack.Parameters["sourceFrame"].SetValue(new Vector4(0, 0, 128, 128));
			crack.Parameters["texSize"].SetValue(ModContent.Request<Texture2D>(Texture).Value.Size());

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(default, BlendState.NonPremultiplied, default, default, default, crack, Main.GameViewMatrix.TransformationMatrix);

			Main.EntitySpriteDraw(ModContent.Request<Texture2D>(Texture).Value, NPC.Center - Main.screenPosition, null, Color.White, 0, ModContent.Request<Texture2D>(Texture).Size() * 0.5f, NPC.scale, SpriteEffects.None, 0);

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(default, default, default, default, default, null, Main.GameViewMatrix.TransformationMatrix);
		}

		private void DrawGlow(float crackProgress)
		{
			Color color = Color.Cyan;
			color.R += 64;

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(default, BlendState.Additive, default, default, default, default, Main.GameViewMatrix.TransformationMatrix);

			Asset<Texture2D> glow = ModContent.Request<Texture2D>(Texture + "Glow");
			Main.spriteBatch.Draw(glow.Value, NPC.Center - Main.screenPosition, null, color * ((crackAnimationTimer - 250) / 50f) * 0.8f, 0, glow.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0);

			Asset<Texture2D> bloom = ModContent.Request<Texture2D>(Texture + "Bloom");
			Main.spriteBatch.Draw(bloom.Value, NPC.Center - Main.screenPosition, null, color * CrackAnimationProgress * 0.6f, 0, bloom.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0);

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.Default, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

			Effect curve = Filters.Scene["GodrayCurve"].GetShader().Shader;
			curve.Parameters["color"].SetValue(color.ToVector4() * crackProgress * 0.25f * (0.2f + CrackAnimationProgress * 0.8f));
			curve.Parameters["intensity"].SetValue(0.5f);
			curve.CurrentTechnique.Passes[0].Apply();

			Asset<Texture2D> godrayThin = ModContent.Request<Texture2D>(Texture + "GodrayThin");
			Main.spriteBatch.Draw(godrayThin.Value, NPC.Center - Main.screenPosition, null, Color.White, 0, godrayThin.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0);

			curve.Parameters["color"].SetValue(color.ToVector4() * (crackProgress - 0.2f) * 0.25f * (0.2f + CrackAnimationProgress * 0.8f));
			curve.Parameters["intensity"].SetValue(0.4f);
			curve.CurrentTechnique.Passes[0].Apply();

			Asset<Texture2D> godrayThick = ModContent.Request<Texture2D>(Texture + "GodrayThick");
			Main.spriteBatch.Draw(godrayThick.Value, NPC.Center - Main.screenPosition, null, Color.White, 0, godrayThick.Size() * 0.5f, NPC.scale, SpriteEffects.None, 0);

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(default, default, default, default, default, null, Main.GameViewMatrix.TransformationMatrix);

		}

		private void DrawCollapse(float delay, float scale, float rotation)
		{
			Main.spriteBatch.End();
			Main.spriteBatch.Begin(default, BlendState.Additive, default, default, default, default, Main.GameViewMatrix.TransformationMatrix);

			Asset<Texture2D> pressureWave = ModContent.Request<Texture2D>(AssetDirectory.ArtifactItem + "AztecDeathSaxophoneSoundwave");

			float start = delay + 45;

			if (warpAnimationTimer > start && warpAnimationTimer <= start + 30)
			{
				float progress = (warpAnimationTimer - start) / 30;
				Main.spriteBatch.Draw(pressureWave.Value, NPC.Center - Main.screenPosition, null, Color.Cyan * progress, rotation, pressureWave.Size() * 0.5f, MathHelper.SmoothStep(15, 0, progress) * scale, SpriteEffects.None, 0);
			}

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(default, default, default, default, default, null, Main.GameViewMatrix.TransformationMatrix);
		}

		public static void DrawOverlay(GameTime gameTime, ScreenTarget starsMap, ScreenTarget starsTarget)
		{
			foreach (BossRushOrb bossRushLock in activeBossRushLocks)
			{
				if (bossRushLock.isPlayingWarp)
				{
					if (bossRushLock.warpAnimationTimer > MAX_SUCC_ANIMATION)
					{
						Texture2D distortionMap = ModContent.Request<Texture2D>("StarlightRiver/Assets/Misc/StarViewWarpMap").Value;

						Effect mapEffect = Filters.Scene["StarViewWarp"].GetShader().Shader;
						mapEffect.Parameters["map"].SetValue(starsMap.RenderTarget);
						mapEffect.Parameters["distortionMap"].SetValue(distortionMap);
						mapEffect.Parameters["background"].SetValue(starsTarget.RenderTarget);

						Vector2 pos = bossRushLock.NPC.Center - Main.screenPosition;

						float intensity = (bossRushLock.warpAnimationTimer - MAX_SUCC_ANIMATION) / 30;

						mapEffect.Parameters["uIntensity"].SetValue(intensity);
						mapEffect.Parameters["uTargetPosition"].SetValue(pos);
						mapEffect.Parameters["uResolution"].SetValue(new Vector2(Main.screenWidth, Main.screenHeight));

						Main.spriteBatch.Begin(default, default, default, default, default, mapEffect, Main.GameViewMatrix.TransformationMatrix);

						Main.spriteBatch.Draw(starsMap.RenderTarget, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);

						Main.spriteBatch.End();
					}
					else
					{
						Effect mapEffect = Filters.Scene["StarMap"].GetShader().Shader;
						mapEffect.Parameters["map"].SetValue(starsMap.RenderTarget);
						mapEffect.Parameters["background"].SetValue(starsTarget.RenderTarget);

						Main.spriteBatch.Begin(default, default, default, default, default, mapEffect, Main.GameViewMatrix.TransformationMatrix);

						Main.spriteBatch.Draw(starsMap.RenderTarget, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);

						Main.spriteBatch.End();
					}
				}
			}
		}

		public static void DrawMap(SpriteBatch sb)
		{
			activeBossRushLocks.RemoveAll(x => !x.NPC.active || !Main.npc.Contains(x.NPC));

			foreach (BossRushOrb bossRushLock in activeBossRushLocks)
			{
				Texture2D tex = Terraria.GameContent.TextureAssets.MagicPixel.Value;

				Texture2D starView = ModContent.Request<Texture2D>("StarlightRiver/Assets/Misc/StarView").Value;

				float time = Main.GameUpdateCount * 0.05f;

				if (bossRushLock.warpAnimationTimer > MAX_SUCC_ANIMATION - 60 && bossRushLock.warpAnimationTimer < MAX_SUCC_ANIMATION)
				{
					if (bossRushLock.freezeTimer == 0)
						bossRushLock.freezeTimer = time;

					time = bossRushLock.freezeTimer;
				}

				Effect wobble = Filters.Scene["StarViewWobble"].GetShader().Shader;
				wobble.Parameters["uTime"].SetValue(time);
				wobble.Parameters["uIntensity"].SetValue(1f);
				// wobble.Parameters["bobbleAngle"].SetValue(bossRushLock.bobbleDirection.ToRotation());
				// wobble.Parameters["bobbleMag"].SetValue(bossRushLock.bobbleDirection.Length());

				Vector2 pos = bossRushLock.NPC.Center - Main.screenPosition;

				Color color = Color.White;
				color.A = 0;

				int starViewWidth = (int)(600 * bossRushLock.starViewScale);

				sb.End();
				sb.Begin(default, BlendState.Additive, default, default, default, wobble, Main.GameViewMatrix.ZoomMatrix);
				sb.Draw(starView, new Rectangle((int)pos.X - starViewWidth / 2, (int)pos.Y - starViewWidth / 2, starViewWidth, starViewWidth), color);

				sb.End();
				sb.Begin(default, default, default, default, default, default, Main.GameViewMatrix.ZoomMatrix);
			}
		}

		public void UpdateStars(Particle particle)
		{
			particle.Timer--;
			particle.Position += particle.Velocity;

			Vector2 diff = NPC.Center - particle.Position;
			float mag = diff.Length() / 1000f;
			Vector2 dir = diff.SafeNormalize(Vector2.Zero);

			float timeMult = (float)Math.Pow(Math.Max(0, (580f - particle.Timer - mag * 10f) / 450f), 2.2f);

			particle.Velocity = (dir * mag + dir.RotatedBy(Math.PI / 2) * (float)Math.Pow(mag, 3)) * warpAnimationTimer * timeMult * 30f;
			particle.Rotation = particle.Velocity.ToRotation() + MathHelper.PiOver2;

			if (diff.Length() < 5)
				particle.Timer = 0;

			if (particle.Timer < 30)
				particle.Alpha = particle.Timer / 30f * mag;
			else if (particle.Timer > 570)
				particle.Alpha = (1 - (particle.Timer - 570) / 30f) * mag;
			else
				particle.Alpha = mag;
		}
	}

	public class BossRushOrbMusic : ModSceneEffect
	{
		public override int Music => GetMusic();

		public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

		public override bool IsSceneEffectActive(Player player)
		{
			foreach (BossRushOrb orb in BossRushOrb.activeBossRushLocks)
			{
				if (orb.NPC.position.Distance(player.position) < 2000)
					return true;
			}

			return false;
		}

		public static int GetMusic()
		{
			foreach (BossRushOrb orb in BossRushOrb.activeBossRushLocks)
			{
				if (orb.isPlayingCrack || orb.isPlayingWarp)
					return 0;
			}

			return MusicLoader.GetMusicSlot("StarlightRiver/Sounds/Music/Moonstone");
		}
	}
}
