﻿using Microsoft.Xna.Framework.Graphics;
using StarlightRiver.Content.Abilities;
using StarlightRiver.Content.Biomes;
using StarlightRiver.Content.Buffs;
using StarlightRiver.Content.Physics;
using StarlightRiver.Core.Systems.CameraSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ID;

namespace StarlightRiver.Content.NPCs.Moonstone
{
	internal class Dreambeast : ModNPC, IHintable
	{
		public enum AIState : int
		{
			Idle,
			Rest,
			Charge,
			Shoot
		}

		public VerletChain[] chains = new VerletChain[6];

		public Vector2 homePos;
		public int flashTime;
		public int projChargeTime;
		public int frameCounter = 0;
		public bool idle = true;
		public bool driftClockwise = true;

		private bool AppearVisible => Main.LocalPlayer.GetModPlayer<LunacyPlayer>().Insane;
		private Player Target => Main.player[NPC.target];

		public AIState Phase
		{
			get => (AIState)NPC.ai[0];
			set => NPC.ai[0] = (float)value;
		}

		public ref float Timer => ref NPC.ai[1];
		public ref float AttackTimer => ref NPC.ai[2];
		public ref float RandomTime => ref NPC.ai[3];

		public int TelegraphTime => 40;
		public Vector2 OrbPos => NPC.Center + NPC.rotation.ToRotationVector2() * 80;

		public override string Texture => AssetDirectory.MoonstoneNPC + "Dreambeast";

		public override void SetDefaults()
		{
			NPC.width = 66;
			NPC.height = 66;
			NPC.noGravity = true;
			NPC.noTileCollide = true;
			NPC.aiStyle = -1;
			NPC.lifeMax = 666666;
			NPC.damage = 66;
			NPC.dontTakeDamage = true;
			NPC.immortal = true;
			NPC.knockBackResist = 0;
		}

		public override ModNPC Clone(NPC newEntity)
		{
			ModNPC clone = base.Clone(newEntity);
			(clone as Dreambeast).chains = chains;
			return clone;
		}

		public override void OnSpawn(IEntitySource source)
		{
			for (int k = 0; k < chains.Length; k++)
			{
				VerletChain chain = chains[k];

				if (chain is null)
				{
					chains[k] = new VerletChain(24 + 2 * Main.rand.Next(4), true, NPC.Center, 5, false)
					{
						constraintRepetitions = 10,//defaults to 2, raising this lowers stretching at the cost of performance
						drag = 1.2f,//this number defaults to 1, is very sensitive
						forceGravity = -Vector2.UnitX,
						scale = 0.6f,
						parent = NPC
					};
				}
			}
		}

		public override void AI()
		{
			Timer++;
			AttackTimer++;

			if (!AppearVisible)
				flashTime = 0;

			if (homePos == default)
				homePos = NPC.Center;

			if (flashTime < 30 && Phase != 0)
				flashTime++;

			for (int k = 0; k < chains.Length; k++)
			{
				VerletChain chain = chains[k];
				chain.forceGravity = -NPC.rotation.ToRotationVector2();
				chain?.UpdateChain(NPC.Center - NPC.rotation.ToRotationVector2() * 30 + NPC.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * (k - chains.Length / 2 + 1) * 15);

				for (int i = 0; i < chain.ropeSegments.Count; i++)
				{
					chain.ropeSegments[i].posNow += NPC.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * (float)Math.Sin(Main.GameUpdateCount * 0.04f + 251 % (k + 1) + i / 4f) * i / 30;
				}
			}

			if (Main.player.Count(n => n.active && n.GetModPlayer<LunacyPlayer>().lunacy > 0 && n.position.Distance(NPC.position) < 2000) == 0)
				NPC.active = false;

			if (Phase == AIState.Idle)
				PassiveBehavior();
			else if (Phase == AIState.Rest)
				AttackRest();
			else if (Phase == AIState.Charge)
				AttackCharge();
			else if (Phase == AIState.Shoot)
				AttackShoot();

			if (idle && AttackTimer % 4 == 0)
			{
				frameCounter = ++frameCounter % 7;
			}
		}

		public override bool CanHitPlayer(Player target, ref int cooldownSlot)
		{
			return NPC.Opacity > 0.5f && target.GetModPlayer<LunacyPlayer>().lunacy > 20;
		}

		public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
		{
			target.GetModPlayer<LunacyPlayer>().ReturnSanity(10);
			modifiers.FinalDamage *= target.GetModPlayer<LunacyPlayer>().GetInsanityDamageMult();
		}

		/// <summary>
		/// picks a random valid target. Meaning a player within range of the beasts home base and that has the insanity debuff.
		/// </summary>
		private void PickTarget()
		{
			var possibleTargets = new List<Player>();
			float totalLunacy = 0;

			foreach (Player player in Main.player)
			{
				if (player.active && player.GetModPlayer<LunacyPlayer>().Insane && Vector2.Distance(player.Center, homePos) < 2000)
				{
					possibleTargets.Add(player);
					totalLunacy += player.GetModPlayer<LunacyPlayer>().lunacy;
				}
			}

			if (possibleTargets.Count <= 0)
			{
				NPC.target = -1;
				return;
			}

			float random = Main.rand.NextFloat(totalLunacy);

			foreach (Player player in possibleTargets)
			{
				if (random < player.GetModPlayer<LunacyPlayer>().lunacy)
				{
					NPC.target = player.whoAmI;
					break;
				}

				random -= player.GetModPlayer<LunacyPlayer>().lunacy;
			}
		}

		/// <summary>
		/// Teleports the beast, as well as all of his chains' points. 
		/// </summary>
		/// <param name="target">The position to teleport to</param>
		private void Teleport(Vector2 target)
		{
			Vector2 diff = target - NPC.Center;
			NPC.Center = target;
			NPC.velocity *= 0;

			if (Phase == AIState.Idle)
			{
				NPC.direction = Main.rand.NextBool() ? -1 : 1;
			}
			else
			{
				NPC.direction = (Target.Center - NPC.Center).X > 0 ? 1 : -1;
				NPC.rotation = (Target.Center - NPC.Center).ToRotation();
			}

			//We need to do this so the chains dont snap back like a rubber band
			foreach (VerletChain chain in chains)
			{
				chain.startPoint += diff;

				foreach (RopeSegment segment in chain.ropeSegments)
				{
					segment.posOld += diff;
					segment.posNow += diff;
				}
			}
		}

		/// <summary>
		/// What the NPC will be doing while its not actively attacking anyone.
		/// </summary>
		private void PassiveBehavior()
		{
			//logic for phase transition
			if (Main.player.Any(n => n.active && n.GetModPlayer<LunacyPlayer>().Insane && Vector2.Distance(n.Center, homePos) < 1000))
			{
				Phase = AIState.Rest;
				NPC.Opacity = 1;
				AttackTimer = 0;
				RandomTime = 30;
				return;
			}

			NPC.Center += Vector2.One.RotatedBy(Timer * 0.005f) * 0.25f;
			NPC.rotation = NPC.direction == 1 ? 0 : MathHelper.Pi;

			if (NPC.Opacity < 1)
				NPC.Opacity += 0.05f;

			if (AttackTimer > RandomTime)
			{
				NPC.Opacity = (20 - (AttackTimer - RandomTime)) / 20f;

				if (AttackTimer > RandomTime + 20)
				{
					AttackTimer = 0;
					RandomTime = Main.rand.Next(240, 360);

					Teleport(homePos + Vector2.One.RotatedByRandom(6.28f) * Main.rand.NextFloat(180, 420));
				}
			}
		}

		/// <summary>
		/// What the NPC does while it is attacking but isnt currently executing a specific attack. It chooses its attack from here.
		/// </summary>
		private void AttackRest()
		{
			NPC.velocity *= 0.99f;

			float targetRotation = NPC.direction == 1 ? 0 : MathHelper.Pi;

			float rotDifference = ((targetRotation - NPC.rotation) % MathHelper.TwoPi + MathHelper.Pi * 3) % MathHelper.TwoPi - MathHelper.Pi;
			NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.rotation + rotDifference, 0.005f);

			if (AttackTimer > RandomTime - 30)
			{
				NPC.Opacity = (20 - (AttackTimer - RandomTime + 30)) / 20f;

				if (AttackTimer > RandomTime)
				{
					AttackTimer = 0;
					PickTarget();

					if (NPC.target == -1)
					{
						Phase = 0;
						return;
					}

					RandomTime = Main.rand.Next(60, 120);
					Teleport(Target.Center + (Main.rand.NextBool() ? -1 : 1) * Vector2.UnitX.RotatedByRandom(MathHelper.PiOver4) * Main.rand.NextFloat(450, 600));
					Phase = Main.rand.NextBool(4) ? AIState.Shoot : AIState.Charge;
				}
			}
		}

		/// <summary>
		/// Charge at the targeted player
		/// </summary>
		private void AttackCharge()
		{
			idle = false;

			if (NPC.Opacity < 1)
				NPC.Opacity += 0.025f;

			// When not charging, adjust aim
			if (AttackTimer < TelegraphTime)
			{
				frameCounter = 0;
				NPC.Center += Vector2.One.RotatedBy(Timer * 0.005f) * 0.25f;

				float rotDifference = (((Target.Center - NPC.Center).ToRotation() - NPC.rotation) % MathHelper.TwoPi + MathHelper.Pi * 3) % MathHelper.TwoPi - MathHelper.Pi;
				NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.rotation + rotDifference, 0.1f);
			}

			// Funny animation numbers
			if (AttackTimer == TelegraphTime || AttackTimer == TelegraphTime + 2 || AttackTimer == TelegraphTime + 5 || AttackTimer == TelegraphTime + 12 || AttackTimer == TelegraphTime + 40)
				frameCounter++;

			if (AttackTimer == TelegraphTime + 45)
				frameCounter = 2;

			if (AttackTimer == TelegraphTime + 50 || AttackTimer == TelegraphTime + 55)
				frameCounter--;

			// Tentacle animation
			if (AttackTimer > TelegraphTime  && AttackTimer < TelegraphTime + 15)
			{
				for (int k = 0; k < chains.Length; k++)
				{
					VerletChain chain = chains[k];

					for (int i = 0; i < chain.ropeSegments.Count; i++)
					{
						chain.ropeSegments[i].posNow += NPC.rotation.ToRotationVector2() * 2 + NPC.rotation.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * (k - chains.Length / 2 + 1) * (float)(Math.Pow(2f * i / chain.ropeSegments.Count - 1, 2) + 1);
					}
				}
			}

			// Acceleration and deceleration controls
			if (AttackTimer > TelegraphTime + 2 && AttackTimer <  TelegraphTime + 12)
				NPC.velocity += NPC.rotation.ToRotationVector2() * 6f;

			if (AttackTimer >= TelegraphTime + 38 && AttackTimer < TelegraphTime + 43)
				NPC.velocity -= NPC.rotation.ToRotationVector2() * 4.8f;

			NPC.velocity *= 0.975f;

			// Charge ends
			if (AttackTimer > TelegraphTime + 70)
			{
				AttackTimer = 0;
				Phase = AIState.Rest;
				idle = true;
				return;
			}
		}

		private void AttackShoot()
		{
			if (AttackTimer == 1)
				driftClockwise = !(NPC.rotation < 0 && NPC.rotation > -MathHelper.PiOver2 || NPC.rotation < MathHelper.Pi && NPC.rotation > MathHelper.PiOver2);

			idle = AttackTimer < TelegraphTime + 40;

			if (NPC.Opacity < 1)
				NPC.Opacity += 0.05f;

			// Unfun animation numbers
			if (AttackTimer == TelegraphTime + 40)
				frameCounter = 0;

			if (AttackTimer == TelegraphTime + 45 || AttackTimer == TelegraphTime + 48 || AttackTimer == TelegraphTime + 50 || AttackTimer == TelegraphTime + 300)
				frameCounter++;

			if (AttackTimer == TelegraphTime + 305)
				frameCounter = 2;

			if (AttackTimer == TelegraphTime + 243 || AttackTimer == TelegraphTime + 308 || AttackTimer == TelegraphTime + 310)
				frameCounter--;

			// Dreambeast orb charging
			if (AttackTimer > TelegraphTime + 45 && AttackTimer < TelegraphTime + 200)
			{
				projChargeTime++;

				Vector2 pos = Vector2.One.RotatedByRandom(MathHelper.TwoPi);
				pos.X /= 2;
				pos = pos.RotatedBy(NPC.rotation);
				Dust.NewDustDirect(OrbPos + pos * 50, 0, 0, ModContent.DustType<Dusts.GlowFastDecelerate>(), 0, 0, 35, new Color(150, 120, 255) * 0.5f, Main.rand.NextFloat(0.6f, 0.8f)).velocity = -pos * 3 - NPC.rotation.ToRotationVector2() * 3f;
			}

			// Major dreambeast orb charges
			if (AttackTimer == TelegraphTime + 45 || AttackTimer == TelegraphTime + 105 || AttackTimer == TelegraphTime + 165)
			{
				for (int i = 0; i < 32; i++)
				{
					Vector2 pos = Vector2.One.RotatedBy(MathHelper.TwoPi * i / 32);
					pos.X /= 2;
					pos = pos.RotatedBy(NPC.rotation);
					Dust.NewDustDirect(OrbPos + pos * 50, 0, 0, ModContent.DustType<Dusts.GlowFastDecelerate>(), 0, 0, 35, new Color(150, 120, 255), Main.rand.NextFloat(0.9f, 1.2f)).velocity = -pos * 3 - NPC.rotation.ToRotationVector2() * 6f;
				}

				NPC.velocity -= NPC.rotation.ToRotationVector2() * 3f;
			}

			// When the dreambeast bites down on the orb
			if (AttackTimer == TelegraphTime + 240)
			{
				frameCounter = 5;
				projChargeTime = 0;

				for (int i = 0; i < 32; i++)
				{
					Dust.NewDustDirect(OrbPos, 0, 0, ModContent.DustType<Dusts.GlowFastDecelerate>(), 0, 0, 35, new Color(150, 120, 255), Main.rand.NextFloat(1.5f, 2f)).velocity *= 2;
				}

				CameraSystem.shake += 8;
				NPC.velocity -= NPC.rotation.ToRotationVector2() * 10f;

				if (Main.netMode != NetmodeID.MultiplayerClient)
				{
					for (int i = 0; i < 3; i++)
					{
						Projectile.NewProjectile(NPC.GetSource_FromThis(), OrbPos, NPC.rotation.ToRotationVector2().RotatedBy(0.2f * (i - 1)) * 5, ModContent.ProjectileType<DreambeastProj>(), 66, 2);
					}

					for (int i = 0; i < 2; i++)
					{
						Projectile.NewProjectile(NPC.GetSource_FromThis(), OrbPos, NPC.rotation.ToRotationVector2().RotatedBy(i == 0 ? -MathHelper.PiOver2 : MathHelper.PiOver2).RotatedByRandom(MathHelper.PiOver4) * 10, ModContent.ProjectileType<DreambeastProjHome>(), 66, 2, -1, NPC.target);

					}
				}
			}

			// Dreambeast drift
			if (AttackTimer < TelegraphTime + 45)
				NPC.position += (NPC.rotation + MathHelper.PiOver2).ToRotationVector2() * (1 - AttackTimer / (TelegraphTime + 45)) * 2.5f * (driftClockwise ? 1 : -1);

			if (AttackTimer > TelegraphTime + 225)
				NPC.position += (NPC.rotation + MathHelper.PiOver2).ToRotationVector2() * (AttackTimer - TelegraphTime - 210) / 100 * 2f * (driftClockwise ? 1 : -1);

			NPC.position += NPC.rotation.ToRotationVector2() * AttackTimer / 120;

			float rotDifference = (((Target.Center - NPC.Center).ToRotation() - NPC.rotation) % MathHelper.TwoPi + MathHelper.Pi * 3) % MathHelper.TwoPi - MathHelper.Pi;
			NPC.rotation = MathHelper.Lerp(NPC.rotation, NPC.rotation + rotDifference, 0.1f);

			NPC.velocity += Target.velocity * 0.025f;

			NPC.velocity *= 0.975f;

			// Attack ends
			if (AttackTimer > 360)
			{
				NPC.velocity = NPC.position - NPC.oldPosition;
				AttackTimer = 0;
				projChargeTime = 0;
				Phase = AIState.Rest;
			}
		}

		public override void FindFrame(int frameHeight)
		{
			NPC.frame.Width = 244;
			NPC.frame.Height = 198;

			NPC.frame.X = 0;

			if (!idle)
				NPC.frame.X = 244;

			NPC.frame.Y = 198 * frameCounter;
		}

		public void DrawToMetaballs(SpriteBatch spriteBatch)
		{
			if (NPC.active)
			{
				Texture2D tex = ModContent.Request<Texture2D>(AssetDirectory.MoonstoneNPC + "Dreambeast").Value;

				foreach (VerletChain chain in chains)
				{
					foreach (RopeSegment segment in chain.ropeSegments)
					{
						spriteBatch.Draw(tex, segment.ScreenPos / 2, NPC.frame, Color.White * NPC.Opacity, 0, new Vector2(122, 99), 0.05f, 0, 0);
					}
				}

				if (NPC.Opacity > 0.8f)
					spriteBatch.Draw(tex, (NPC.Center - Main.screenPosition) / 2, NPC.frame, Color.Black, NPC.rotation + (NPC.direction == -1 ? MathHelper.Pi : 0), new Vector2(122, 99), 0.5f, NPC.direction == -1 ? SpriteEffects.FlipHorizontally : 0, 0);

				if (AppearVisible)
				{
					Effect effect = Filters.Scene["MoonstoneRunes"].GetShader().Shader;
					effect.Parameters["intensity"].SetValue(50f * MathF.Min(1 - NPC.Opacity, 1));
					effect.Parameters["time"].SetValue((float)Main.timeForVisualEffects * 0.1f);

					effect.Parameters["noiseTexture1"].SetValue(ModContent.Request<Texture2D>(AssetDirectory.Assets + "Noise/MiscNoise3").Value);
					effect.Parameters["noiseTexture2"].SetValue(ModContent.Request<Texture2D>(AssetDirectory.Assets + "Noise/MiscNoise4").Value);
					effect.Parameters["color1"].SetValue(Color.Lerp(Color.Magenta, Color.Gray, (NPC.Opacity - 0.9f) * 10).ToVector4());
					effect.Parameters["color2"].SetValue(Color.Lerp(Color.Cyan, Color.Gray, (NPC.Opacity - 0.9f) * 10).ToVector4());
					effect.Parameters["opacity"].SetValue(NPC.Opacity);

					effect.Parameters["screenWidth"].SetValue(tex.Width);
					effect.Parameters["screenHeight"].SetValue(tex.Height);
					effect.Parameters["screenPosition"].SetValue(NPC.position);
					effect.Parameters["drawOriginal"].SetValue(false);

					spriteBatch.End();
					spriteBatch.Begin(default, BlendState.Additive, default, default, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
				}

				spriteBatch.Draw(tex, (NPC.Center - Main.screenPosition) / 2, NPC.frame, Color.White * NPC.Opacity, NPC.rotation + (NPC.direction == -1 ? MathHelper.Pi : 0), new Vector2(122, 99), 0.5f, NPC.direction == -1 ? SpriteEffects.FlipHorizontally : 0, 0);

				if (AppearVisible)
				{
					float opacity = 1 - (float)Math.Pow(2 * NPC.Opacity - 1, 2);

					Effect effect = Filters.Scene["MoonstoneBeastEffect"].GetShader().Shader;
					effect.Parameters["baseTexture"].SetValue(tex);
					effect.Parameters["distortTexture"].SetValue(ModContent.Request<Texture2D>("StarlightRiver/Assets/Noise/MiscNoise2").Value);
					effect.Parameters["size"].SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
					effect.Parameters["time"].SetValue(Main.GameUpdateCount * 0.005f);
					effect.Parameters["opacity"].SetValue(opacity);
					effect.Parameters["noiseSampleSize"].SetValue(new Vector2(800, 800));
					effect.Parameters["noisePower"].SetValue(100f);

					spriteBatch.End();
					spriteBatch.Begin(default, BlendState.Additive, default, default, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
				}

				for (int i = 0; i < 6; i++)
				{
					spriteBatch.Draw(tex, (NPC.Center + Vector2.UnitY.RotatedBy((1 - NPC.Opacity) * MathHelper.Pi + MathHelper.TwoPi * i / 6) * (1 - NPC.Opacity) * 100 - Main.screenPosition) / 2, NPC.frame, Color.White * NPC.Opacity, NPC.rotation + (NPC.direction == -1 ? MathHelper.Pi : 0), new Vector2(122, 99), 0.5f, NPC.direction == -1 ? SpriteEffects.FlipHorizontally : 0, 0);
				}

				spriteBatch.End();
				spriteBatch.Begin(default, default, default, default, RasterizerState.CullNone, default, Main.GameViewMatrix.TransformationMatrix);
			}
		}

		public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
		{
			spriteBatch.End();
			spriteBatch.Begin(default, default, default, default, RasterizerState.CullNone, default, Main.GameViewMatrix.TransformationMatrix);

			if (AppearVisible && flashTime > 0)
			{
				Texture2D flashTex = ModContent.Request<Texture2D>("StarlightRiver/Assets/Keys/GlowAlpha").Value;
				Color color = Color.White * (1 - flashTime / 30f);
				color.A = 0;

				spriteBatch.Draw(flashTex, NPC.Center - Main.screenPosition, null, color, 0, flashTex.Size() / 2, flashTime, 0, 0);
			}

			if (AppearVisible && Phase == AIState.Shoot && projChargeTime > 0)
			{
				Effect effect = Filters.Scene["CrescentOrb"].GetShader().Shader;
				effect.Parameters["sampleTexture"].SetValue(ModContent.Request<Texture2D>("StarlightRiver/Assets/Items/Moonstone/CrescentQuarterstaffMap").Value);
				effect.Parameters["sampleTexture2"].SetValue(ModContent.Request<Texture2D>("StarlightRiver/Assets/Bosses/VitricBoss/LaserBallDistort").Value);
				effect.Parameters["uTime"].SetValue(Main.GameUpdateCount * 0.01f);
				effect.Parameters["opacity"].SetValue(1);

				spriteBatch.End();
				spriteBatch.Begin(default, BlendState.Additive, default, default, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

				spriteBatch.End();
				spriteBatch.Begin(default, BlendState.NonPremultiplied, default, default, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

				Texture2D orb = ModContent.Request<Texture2D>(AssetDirectory.MoonstoneItem + "CrescentOrb").Value;
				spriteBatch.Draw(orb, OrbPos - Main.screenPosition, null, Color.White * (projChargeTime  / 30f), Main.GameUpdateCount * 0.01f, orb.Size() / 2, projChargeTime / 150f, 0, 0);
			}

			spriteBatch.End();
			spriteBatch.Begin(default, BlendState.Additive, SamplerState.PointWrap, default, RasterizerState.CullNone, default, Main.GameViewMatrix.TransformationMatrix);

			Texture2D tex = ModContent.Request<Texture2D>("StarlightRiver/Assets/Keys/Glow").Value;
			var glowColor = new Color(78, 87, 191);
			spriteBatch.Draw(tex, OrbPos - Main.screenPosition, tex.Frame(), glowColor * Math.Min(projChargeTime / 30f, 1), 0, tex.Size() / 2, 1.8f * projChargeTime / 150f, 0, 0);

			spriteBatch.End();
			spriteBatch.Begin(default, default, default, default, RasterizerState.CullNone, default, Main.GameViewMatrix.TransformationMatrix);

			return false;
		}

		public string GetHint()
		{
			return "It's not real. It's not real. It's not real. IT'S NOT REAL. IT'S NOT REAL. IT'S NOT REAL.";
		}
	}

	public partial class LunacyPlayer : ModPlayer
	{
		public float lunacy = 0;
		private int sanityTimer = 0;

		public bool Insane => lunacy > 100;

		public override void PostUpdateBuffs()
		{
			if (sanityTimer > 0)
			{
				sanityTimer--;
				lunacy = MathHelper.Lerp(lunacy, 99, 0.1f);
			}
			if (Player.HasBuff<Overcharge>())
			{
				if (lunacy < 100)
					lunacy = MathHelper.Lerp(lunacy, 100, 0.05f);

				lunacy += 0.5f;
			}
			else if (Player.InModBiome<MoonstoneBiome>())
			{
				if (lunacy < 99)
					lunacy += 0.025f;
				else
					lunacy = MathHelper.Lerp(lunacy, 99, 0.01f);
			}
			else
			{
				lunacy = Math.Max(MathHelper.Lerp(lunacy, 0, 0.05f) - 0.25f, 0);
			}
		}

		/// <summary>
		/// Snaps you back to reality and exponentially lowers lunacy
		/// </summary>
		/// <param name="intensity"></param>
		public void ReturnSanity(int intensity)
		{
			sanityTimer = intensity;
		}

		/// <summary>
		/// Get damage multipler dealt to player by hallucinations
		/// </summary>
		/// <returns></returns>
		public float GetInsanityDamageMult()
		{
			if (lunacy < 100)
				return lunacy / 100;
			else if (lunacy < 500)
				return 1 + (lunacy - 100) / 200;
			else
				return 3 + (lunacy - 500) / 500;
		}
	}
}