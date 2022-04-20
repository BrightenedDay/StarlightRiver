﻿using Microsoft.Xna.Framework;
using StarlightRiver.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace StarlightRiver.Content.Bosses.SquidBoss
{
	public partial class SquidBoss : ModNPC
    {
        private void RandomizeTarget()
        {
            List<int> possible = new List<int>();

            for (int k = 0; k < Main.maxPlayers; k++)
            {
                Player Player = Main.player[k];

                if (Player.active && StarlightWorld.SquidBossArena.Contains((Player.Center / 16).ToPoint()))
                    possible.Add(Player.whoAmI);
            }

            if (possible.Count == 0)
            {
                NPC.active = false;
                return;
            }

            NPC.target = possible[Main.rand.Next(possible.Count - 1)];

            NPC.netUpdate = true;
        }

        private void SpawnTell(Vector2 start, Vector2 end)
		{
            int i = Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), start, Vector2.Zero, ModContent.ProjectileType<TentacleTell>(), 0, 0, Main.myPlayer);
            var proj = Main.projectile[i];

            if (proj.ModProjectile is TentacleTell)
                (proj.ModProjectile as TentacleTell).endPoint = end;
        }

        private void ResetAttack() => AttackTimer = 0;

        private void ShufflePlatforms()
        {
            int n = platforms.Count(); //fisher yates
            while (n > 1)
            {
                n--;
                int k = Main.rand.Next(n + 1);
                NPC value = platforms[k];
                platforms[k] = platforms[n];
                platforms[n] = value;
            }
        }

        #region phase 1
        private void TentacleSpike()
        {
            NPC.rotation = NPC.velocity.X * 0.01f;

            if (AttackTimer < 30)
                Opacity = 1 - (AttackTimer / 30f * 0.5f);

            for (int k = 0; k < 4; k++)
            {
                Tentacle tentacle = tentacles[k].ModNPC as Tentacle;

                if (AttackTimer == k * 100 || (k == 0 && AttackTimer == 1)) //teleport where needed
                {
                    RandomizeTarget();
                    
                    int adj = (int)Main.player[NPC.target].velocity.X * 60; if (adj > 200) adj = 200;
                    tentacles[k].Center = new Vector2(Main.player[NPC.target].Center.X + adj, spawnPoint.Y - 50);
                    tentacle.BasePoint = tentacles[k].Center;
                    tentacle.MovementTarget = tentacles[k].Center + new Vector2(0, -950);
                    tentacle.NPC.netUpdate = true;

                    if(tentacle.State != 2)
                        tentacle.State = 1;

                    savedPoint = new Vector2(Main.player[NPC.target].Center.X + adj + (Main.rand.NextBool() ? 150 : -150), Main.player[NPC.target].Center.Y);

                    SpawnTell(tentacle.MovementTarget + new Vector2(0, -64), tentacle.BasePoint);

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Drown, NPC.Center);
                }

                if(AttackTimer > k * 100 && AttackTimer < k * 100 + 50)
				{
                    tentacle.DownwardDrawDistance += 2;
                    NPC.velocity = (savedPoint - NPC.Center) * 0.035f * Math.Min(1, (AttackTimer - k * 100) / 10f); //visually pursue the player
				}

                if (AttackTimer > k * 100 + 30 && AttackTimer < k * 100 + 70) //shooting up, first 30 frames are for tell
                {
                    if (AttackTimer > k * 100 + 50)
                        NPC.velocity *= 0.92f; //slow down from movement

                    int time = (int)AttackTimer - (k * 100 + 30);

                    tentacle.StalkWaviness = 0;
                    tentacle.ZSpin = (time / 30f * 6.28f);

                    if (AttackTimer == k * 100 + (Phase == (int)AIStates.FirstPhase ? 40 : 57))
                    {
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Splash, NPC.Center);
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item81, NPC.Center);

                        SplashDust(k);
                    }

                    tentacles[k].Center = Vector2.SmoothStep(tentacle.BasePoint, tentacle.MovementTarget, time / 40f);
                }

                if (AttackTimer == k * 100 + 70) //impact
                {
                    NPC.velocity *= 0f; //stop

                    if (tentacle.State != 2 && (Phase == (int)AIStates.FirstPhaseTwo || tentacles.Count(n => n.ai[0] == 2) < 2))
                    {
                        tentacle.State = 0;

                        for (int i = 0; i < 20; i++)
                            Dust.NewDustPerfect(tentacle.NPC.Center, ModContent.DustType<Dusts.Glow>(), Vector2.One.RotatedByRandom(6.28f) * Main.rand.NextFloat(5), 1, new Color(255, Main.rand.Next(0, 155), 0), 0.5f);
                    }

                    Main.LocalPlayer.GetModPlayer<StarlightPlayer>().Shake += 20; //TODO: Find the right player instances

                    Helpers.Helper.PlayPitched("ArenaHit", 0.5f, 1f, tentacles[k].Center);
                }

                if (AttackTimer > k * 100 + 70 && AttackTimer < k * 100 + 300) //retracting
                {
                    int time = (int)AttackTimer - (k * 100 + 70);
                    tentacles[k].Center = Vector2.SmoothStep(tentacle.MovementTarget, tentacle.BasePoint, time / 190f);
                    tentacle.StalkWaviness = Math.Min(1.5f, time / 30f);
                    tentacle.ZSpin = 0;

                    if(AttackTimer == k * 100 + 250)
                        if (tentacle.State != 2)
                            tentacle.State = 1;

                    if (AttackTimer > k * 100 + 250)
                        tentacle.DownwardDrawDistance -= 2;

                    if (AttackTimer == k * 100 + (Phase == (int)AIStates.FirstPhase ? 260 : 205))
                    {
                        SplashDustSmall(k);
                    }
                }

                if (AttackTimer == k * 100 + 300)
                    tentacle.DownwardDrawDistance = 28;
            }

            if (AttackTimer == 540)
                savedPoint = NPC.Center;

            if (AttackTimer > 540) //return home
            {
                NPC.Center = Vector2.SmoothStep(savedPoint, spawnPoint + new Vector2(0, -600), (AttackTimer - 540) / 60f);
                Opacity = 0.5f + (AttackTimer - 540) / 60f;
            }

            if (AttackTimer == 600) 
                ResetAttack();
        }

        private void InkBurst()
        {
            if (AttackTimer < 30)
                NPC.velocity = (Main.player[NPC.target].Center + new Vector2(0, -200) - NPC.Center) * 0.05f;

            if (AttackTimer > 30 && AttackTimer < 60)
                NPC.velocity *= 0.95f;

            if (AttackTimer > 90)
                NPC.velocity *= 0;

            if (AttackTimer > 100 && AttackTimer <= 180)
            {
                float angle = (Main.player[NPC.target].Center - NPC.Center).ToRotation();

                int delay = Main.masterMode ? 10 : Main.expertMode ? 15 : 20;

                if (AttackTimer % delay == 0)
                {
                    Vector2 speed = new Vector2(Main.masterMode ? 12 : 8, 0).RotatedBy(angle + Main.rand.NextFloat(-0.5f, 0.5f));
                    Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, 100), speed, ModContent.ProjectileType<InkBlob>(), 10, 0.2f, 255, 0, Main.rand.NextFloat(6.28f));
                }

                if (AttackTimer % delay == 0)
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item95, NPC.Center);
            }

            if (AttackTimer == 240) 
                ResetAttack();

            return;
        }

        private void InkBurstAlt()
		{
            if(AttackTimer == 60)
			{
                Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, 100), new Vector2(0, -15).RotatedBy(-0.5f), ModContent.ProjectileType<InkBlobGravity>(), 10, 0.2f, 255, 0, Main.rand.NextFloat(6.28f));
                Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, 100), new Vector2(0, -15).RotatedBy(-0.25f), ModContent.ProjectileType<InkBlobGravity>(), 10, 0.2f, 255, 0, Main.rand.NextFloat(6.28f));

                Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, 100), new Vector2(0, -15).RotatedBy(0.5f), ModContent.ProjectileType<InkBlobGravity>(), 10, 0.2f, 255, 0, Main.rand.NextFloat(6.28f));
                Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, 100), new Vector2(0, -15).RotatedBy(0.25f), ModContent.ProjectileType<InkBlobGravity>(), 10, 0.2f, 255, 0, Main.rand.NextFloat(6.28f));

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item95, NPC.Center);

                savedPoint = NPC.Center;
            }

            if(AttackTimer > 80 && AttackTimer <= 130)
			{
                float prog = Helpers.Helper.SwoopEase((AttackTimer - 80) / 50f);
                NPC.Center = Vector2.Lerp(savedPoint, spawnPoint + new Vector2(0, -200), prog);
			}

            if(AttackTimer == 130)
			{
                Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, -150), new Vector2(0, -20).RotatedBy(-0.25f), ModContent.ProjectileType<InkBlobGravity>(), 10, 0.2f, 255, 0, Main.rand.NextFloat(6.28f));
                Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, -150), new Vector2(0, -20).RotatedBy(-0.125f), ModContent.ProjectileType<InkBlobGravity>(), 10, 0.2f, 255, 0, Main.rand.NextFloat(6.28f));

                Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, -150), new Vector2(0, -20).RotatedBy(0.25f), ModContent.ProjectileType<InkBlobGravity>(), 10, 0.2f, 255, 0, Main.rand.NextFloat(6.28f));
                Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, -150), new Vector2(0, -20).RotatedBy(0.125f), ModContent.ProjectileType<InkBlobGravity>(), 10, 0.2f, 255, 0, Main.rand.NextFloat(6.28f));

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item95, NPC.Center);

                savedPoint = spawnPoint + new Vector2(0, -250);
                Main.NewText("Sploosh");
			}

            if(AttackTimer > 160)
			{
                float prog = (AttackTimer - 160) / 30f;
                NPC.Center = Vector2.SmoothStep(spawnPoint + new Vector2(0, -200), savedPoint, prog);
            }

            if (AttackTimer == 190)
                ResetAttack();
		}

        private void SpawnAdds()
		{
            RandomizeTarget();

            if(AttackTimer == 1)
			{
                savedPoint = Main.player[NPC.target].Center + new Vector2(0, 200);
			}

            if(AttackTimer > 1 && AttackTimer < 60)
			{
                NPC.velocity = (savedPoint - NPC.Center) * 0.035f * Math.Min(1, (AttackTimer) / 10f); //visually pursue the player
            }

            if(AttackTimer > 60 && AttackTimer < 80)
			{
                NPC.velocity *= 0.95f;
			}

            if(AttackTimer > 120)
			{
                NPC.velocity *= 0;

                if (AttackTimer % 5 == 0)
                {
                    int i = NPC.NewNPC(NPC.GetSpawnSourceForNPCFromNPCAI(), (int)NPC.Center.X, (int)NPC.Center.Y + 220, ModContent.NPCType<Auroraling>());
                    Main.npc[i].velocity += Vector2.UnitY.RotatedByRandom(1) * 20;

                    for (int k = 0; k < 20; k++)
                        Dust.NewDustPerfect(NPC.Center + new Vector2(0, 220), ModContent.DustType<Dusts.Glow>(), Vector2.UnitY.RotatedByRandom(1) * Main.rand.NextFloat(5), 0, new Color(100, 255, 255), 0.25f);
                }
			}

            if (AttackTimer == 160)
                ResetAttack();
		}

        private void PlatformSweep()
        {
            Tentacle tentacleL = tentacles[0].ModNPC as Tentacle;
            Tentacle tentacleR = tentacles[3].ModNPC as Tentacle;

            if (AttackTimer == 1) //start by randomizing the platform order and assigning targets
            {
                ShufflePlatforms();

                tentacleL.NPC.Center = spawnPoint + new Vector2(-800, 0);
                tentacleR.NPC.Center = spawnPoint + new Vector2(800, 0);
                tentacleL.BasePoint = tentacleL.NPC.Center;
                tentacleR.BasePoint = tentacleR.NPC.Center;
                tentacleL.MovementTarget = tentacleL.NPC.Center + new Vector2(0, -1050);
                tentacleR.MovementTarget = tentacleR.NPC.Center + new Vector2(0, -1050);
                tentacleL.StalkWaviness = 0;
                tentacleR.StalkWaviness = 0;

                SpawnTell(tentacleL.MovementTarget + new Vector2(0, 128), tentacleL.BasePoint);
                SpawnTell(tentacleR.MovementTarget + new Vector2(0,128), tentacleR.BasePoint);

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Drown, NPC.Center);
            }

            if (AttackTimer < 60)
            {
                Opacity = 1 - AttackTimer / 60f * 0.5f;

                tentacleL.DownwardDrawDistance++;
                tentacleR.DownwardDrawDistance++;
            }

            if (AttackTimer > 60 && AttackTimer < 100) //rising
            {
                if (AttackTimer == 61)
                {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Splash, NPC.Center);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item81, NPC.Center);
                }

                var timer = (AttackTimer - 60) / 40f;

                tentacles[0].Center = Vector2.SmoothStep(tentacleL.BasePoint, tentacleL.MovementTarget, timer);
                tentacles[3].Center = Vector2.SmoothStep(tentacleR.BasePoint, tentacleR.MovementTarget, timer);

                tentacleL.ZSpin = timer * 6.28f * 2;
                tentacleR.ZSpin = timer * 6.28f * 2;
            }

            if(AttackTimer == 100)
			{
                Helpers.Helper.PlayPitched("ArenaHit", 0.75f, 1f, NPC.Center);

                tentacleL.ZSpin = 0;
                tentacleR.ZSpin = 0;

                Projectile.NewProjectile(NPC.GetSpawnSource_ForProjectile(), new Vector2(platforms[0].Center.X, spawnPoint.Y - 1000), Vector2.Zero, ModContent.ProjectileType<SqueezeTell>(), 0, 0, Main.myPlayer);
            }

            if(AttackTimer > 100 && AttackTimer < 130)
			{
                tentacles[0].Center += Vector2.UnitY * 2;
                tentacles[3].Center += Vector2.UnitY * 2;
            }

            if (AttackTimer > 130 && AttackTimer < 380) //move in to crush
            {
                var timer = Helpers.Helper.BezierEase((AttackTimer - 130) / 250f);

                tentacleL.StalkWaviness = timer * 0.45f;
                tentacleR.StalkWaviness = timer * 0.45f;

                tentacleL.BasePoint.X = Helpers.Helper.LerpFloat(tentacleL.MovementTarget.X, platforms[0].Center.X - 100, timer);
                tentacleR.BasePoint.X = Helpers.Helper.LerpFloat(tentacleR.MovementTarget.X, platforms[0].Center.X + 100, timer);

                tentacleL.NPC.Center = new Vector2(Helpers.Helper.LerpFloat(tentacleL.MovementTarget.X, platforms[0].Center.X - 100, timer), tentacleL.NPC.Center.Y);
                tentacleR.NPC.Center = new Vector2(Helpers.Helper.LerpFloat(tentacleR.MovementTarget.X, platforms[0].Center.X + 100, timer), tentacleR.NPC.Center.Y);
            }

            if (AttackTimer == 260)
                savedPoint = NPC.Center;

            if (AttackTimer > 260 && AttackTimer < 340) //going to the side
            {
                Vector2 targetPoint = new Vector2(platforms[0].Center.X, spawnPoint.Y - 400);
                NPC.Center = Vector2.SmoothStep(savedPoint, targetPoint, (AttackTimer - 260) / 80f);
            }

            if (AttackTimer == 360)
                savedPoint = new Vector2(platforms[0].Center.X, spawnPoint.Y - 400);

            if (AttackTimer > 360 && AttackTimer < 520)
			{
                if (AttackTimer % 30 == 0)
                {
                    var vel = Vector2.UnitX * Main.rand.Next(-5, 5);
                    Projectile.NewProjectile(NPC.GetSpawnSource_ForProjectile(), savedPoint + new Vector2(0, 50), vel, ModContent.ProjectileType<SpewBlob>(), 20, 1, Main.myPlayer);

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                }
			}

            if(AttackTimer == 520)
			{
                tentacleL.MovementTarget = tentacleL.NPC.Center;
                tentacleR.MovementTarget = tentacleR.NPC.Center;
            }

            if(AttackTimer > 520 && AttackTimer < 580)
			{
                tentacleL.NPC.Center = Vector2.SmoothStep(tentacleL.MovementTarget, tentacleL.BasePoint, (AttackTimer - 520) / 60f);
                tentacleR.NPC.Center = Vector2.SmoothStep(tentacleR.MovementTarget, tentacleR.BasePoint, (AttackTimer - 520) / 60f);
            }

            if(AttackTimer > 580 && AttackTimer < 640)
			{
                Opacity = 0.5f + (AttackTimer - 580) / 60f * 0.5f;

                tentacleL.DownwardDrawDistance--;
                tentacleR.DownwardDrawDistance--;
            }

            if (AttackTimer == 660)
                ResetAttack();
        }

        private void ArenaSweep()
        {
            if (AttackTimer == 1)
                savedPoint = NPC.Center;

            if(AttackTimer > 1 && AttackTimer < 120)
			{
                NPC.Center = Vector2.SmoothStep(savedPoint, spawnPoint + new Vector2(0, -1300), AttackTimer / 120f);
			}

            for (int k = 0; k < 4; k++)
            {
                Tentacle tentacle = tentacles[k].ModNPC as Tentacle;


            }

            if (AttackTimer >= 660) ResetAttack();
        }
        #endregion

        #region phase 2
        private void Spew()
        {
            if (AttackTimer % 100 == 0)
            {
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);

                if (Main.expertMode) //spawn more + closer together on expert
                {
                    for (int k = 0; k < 14; k++)
                        Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, 100), new Vector2(-100 + k * 14, 0), ModContent.ProjectileType<SpewBlob>(), 10, 0.2f);
                }
                else
                {
                    for (int k = 0; k < 10; k++)
                        Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, 100), new Vector2(-100 + k * 20, 0), ModContent.ProjectileType<SpewBlob>(), 10, 0.2f);
                }
            }

            if (AttackTimer == 300) ResetAttack();
        }

        private void Laser()
        {
            GlobalTimer++;

            if (AttackTimer == 1) //set movement points
            {
                savedPoint = NPC.Center;
                NPC.velocity *= 0;
                NPC.rotation = 0;
            }

            if (AttackTimer < 60) //move to left of the arena
            {
                NPC.Center = Vector2.SmoothStep(savedPoint, spawnPoint + new Vector2(-800, -500), AttackTimer / 60f);
                NPC.rotation += 3.14f / 59f;
            }

            if (AttackTimer == 60)
            {
                savedPoint = NPC.Center; //leftmost point of laser
                Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, -200), Vector2.Zero, ModContent.ProjectileType<Laser>(), 10, 0.2f, 255, 0, AttackTimer * 0.1f);
            }

            int laserTime = Main.expertMode ? 450 : 600; //faster in expert

            if (AttackTimer > 60 && AttackTimer < 60 + laserTime) //lasering
            {
                if (AttackTimer % 10 == 0) Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCHit53, NPC.Center);
                NPC.Center = Vector2.Lerp(savedPoint, spawnPoint + new Vector2(800, -500), (AttackTimer - 60) / laserTime);
            }

            if (AttackTimer == 60 + laserTime) savedPoint = NPC.Center; //end of laser

            if (AttackTimer > 60 + laserTime && AttackTimer < 120 + laserTime) //return to center of arena
            {
                NPC.Center = Vector2.SmoothStep(savedPoint, spawnPoint + new Vector2(0, -300), (AttackTimer - (laserTime + 60)) / 60f);
                NPC.rotation -= 3.14f / 59f;
            }

            if (AttackTimer >= 120 + laserTime) ResetAttack();
        }

        private void Leap()
        {
            if (AttackTimer == 1)
            {
                savedPoint = NPC.Center;
                NPC.velocity *= 0;
                NPC.rotation = 0;

                for (int k = 0; k < 4; k++) //tentacles
                {
                    Tentacle tentacle = tentacles[k].ModNPC as Tentacle;
                    int off;

                    switch (k)
                    {
                        case 0: off = -430; break;
                        case 1: off = -150; break;
                        case 2: off = 150; break;
                        case 3: off = 430; break;
                        default: off = 0; break;
                    }

                    tentacles[k].Center = new Vector2(spawnPoint.X + off, spawnPoint.Y - 100);
                    tentacle.BasePoint = tentacles[k].Center;
                    tentacle.MovementTarget = tentacles[k].Center + new Vector2(off * 0.45f, -900);

                    for (int n = 0; n < 40; n++)
                        Dust.NewDustPerfect(Vector2.Lerp(tentacle.BasePoint, tentacle.MovementTarget, n / 30f), DustID.Fireworks, Vector2.Zero);

                }
            }

            if (AttackTimer < 120) //go to center
            {
                NPC.Center = Vector2.SmoothStep(savedPoint, spawnPoint + new Vector2(0, -500), AttackTimer / 120f);

                for (int k = 0; k < 4; k++) //tentacles
                {
                    Tentacle tentacle = tentacles[k].ModNPC as Tentacle;
                    tentacles[k].Center = Vector2.SmoothStep(tentacle.BasePoint, tentacle.MovementTarget, AttackTimer / 120f);

                    if (AttackTimer == 110)
                        SplashDust(k);
                }
            }

            if (AttackTimer == 120) NPC.velocity.Y = -15; //jump

            if (AttackTimer == 150) //spawn Projectiles
            {
                Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath24, NPC.Center);

                for (float k = 0; k <= 3.14f; k += 3.14f / 4f)
                    Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, 100), new Vector2(-10, 0).RotatedBy(k), ModContent.ProjectileType<InkBlob>(), 10, 0.2f, 255, 0, Main.rand.NextFloat(6.28f));
            }

            if (AttackTimer > 120 && AttackTimer < 220) NPC.velocity.Y += 0.16f; //un-jump

            if (AttackTimer > 120)
            {
                for (int k = 0; k < 4; k++) //tentacles
                {
                    tentacles[k].Center = new Vector2(tentacles[k].Center.X + (float)Math.Sin(AttackTimer / 10f + k) * 4f, tentacles[k].Center.Y + (float)Math.Cos(AttackTimer / 10f + k) * 2f);
                }
            }

            if (AttackTimer > 540)
            {
                for (int k = 0; k < 4; k++) //tentacles
                {
                    Tentacle tentacle = tentacles[k].ModNPC as Tentacle;
                    tentacles[k].Center = Vector2.SmoothStep(tentacle.MovementTarget, tentacle.BasePoint, (AttackTimer - 540) / 60f);
                }
            }

            if (AttackTimer == 600) ResetAttack();
        }

        private void Eggs()
        {
            if (AttackTimer == 1)
            {
                savedPoint = NPC.Center;
                ShufflePlatforms();
            }

            if (AttackTimer < 60) NPC.Center = Vector2.SmoothStep(savedPoint, platforms[0].Center + new Vector2(0, -150), AttackTimer / 60);

            if (AttackTimer == 60)
            {
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, 120), Vector2.Zero, ModContent.ProjectileType<SquidEgg>(), 10, 0.2f);
            }

            if (AttackTimer > 120 && AttackTimer < 180) NPC.Center = Vector2.SmoothStep(platforms[0].Center + new Vector2(0, -150), savedPoint, (AttackTimer - 120) / 60);

            if (AttackTimer == 180) ResetAttack();
        }

        private void LeapHard()
        {
            if (AttackTimer == 1)
            {
                savedPoint = NPC.Center;
                NPC.velocity *= 0;
                NPC.rotation = 0;

                for (int k = 0; k < 2; k++) //left
                {
                    Tentacle tentacle = tentacles[k].ModNPC as Tentacle;
                    tentacles[k].Center = spawnPoint + new Vector2(-600, -1100);
                    tentacle.BasePoint = tentacles[k].Center;
                }
                for (int k = 2; k < 4; k++) //right
                {
                    Tentacle tentacle = tentacles[k].ModNPC as Tentacle;
                    tentacles[k].Center = spawnPoint + new Vector2(600, -1100);
                    tentacle.BasePoint = tentacles[k].Center;
                }
            }

            if (AttackTimer < 120) NPC.Center = Vector2.SmoothStep(savedPoint, spawnPoint + new Vector2(0, -500), AttackTimer / 120f);

            if (AttackTimer == 120) NPC.velocity.Y = -15; //jump

            if (AttackTimer == 150) //spawn Projectiles
            {
                Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath24, NPC.Center);

                for (float k = 0; k <= 3.14f; k += 3.14f / 6f)
                    Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, 100), new Vector2(-10, 0).RotatedBy(k), ModContent.ProjectileType<InkBlob>(), 10, 0.2f, 255, 0, Main.rand.NextFloat(6.28f));
            }

            if (AttackTimer > 120 && AttackTimer < 220) NPC.velocity.Y += 0.16f; //un-jump

            if (AttackTimer <= 480)
            {
                float radius = (AttackTimer > 240 ? 240 - (AttackTimer - 240) : AttackTimer) * 2.5f;

                for (int k = 0; k < 2; k++) //left
                {
                    Tentacle tentacle = tentacles[k].ModNPC as Tentacle;
                    Vector2 off = (new Vector2(0, 1) * radius).RotatedBy(AttackTimer / 240f * 6.28f + (k == 0 ? 3.14f : 0));
                    tentacles[k].Center = tentacle.BasePoint + off;
                }
                for (int k = 2; k < 4; k++) //right
                {
                    Tentacle tentacle = tentacles[k].ModNPC as Tentacle;
                    Vector2 off = (new Vector2(0, -1) * radius).RotatedBy(1.57f + AttackTimer / 240f * 6.28f + (k == 2 ? 3.14f : 0));
                    tentacles[k].Center = tentacle.BasePoint + off;
                }
            }

            if (AttackTimer == 480) ResetAttack();
        }
        #endregion

        #region phase 3
        private void TentacleSpike2()
        {
            for (int k = 0; k < 4; k++)
            {
                Tentacle tentacle = tentacles[k].ModNPC as Tentacle;

                if (AttackTimer == k * 80 || (k == 0 && AttackTimer == 1)) //teleport where needed
                {
                    RandomizeTarget();

                    tentacles[k].Center = new Vector2(Main.npc.FirstOrDefault(n => n.active && n.ModNPC is ArenaActor).Center.X + (k % 2 == 0 ? -600 : 600), NPC.Center.Y + Main.rand.Next(-200, 200));
                    tentacle.BasePoint = tentacles[k].Center;
                    tentacle.MovementTarget = Main.player[NPC.target].Center;
                    tentacle.NPC.netUpdate = true;

                    SpawnTell(tentacle.MovementTarget, tentacle.BasePoint);

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Drown, NPC.Center);
                }

                if (AttackTimer > k * 80 + 30 && AttackTimer < k * 80 + 90) //shooting up, first 30 frames are for tell
                {
                    if (AttackTimer == k * 80 + 40)
                    {
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Splash, NPC.Center);
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item81, NPC.Center);
                    }

                    int time = (int)AttackTimer - (k * 80 + 30);
                    tentacles[k].Center = Vector2.SmoothStep(tentacle.BasePoint, tentacle.MovementTarget, time / 50f);
                    tentacles[k].ai[1] += 5f; //make it squirm faster
                }

                if (AttackTimer > k * 80 + 90 && AttackTimer < k * 80 + 150) //retracting
                {
                    int time = (int)AttackTimer - (k * 80 + 90);
                    tentacles[k].Center = Vector2.SmoothStep(tentacle.MovementTarget, tentacle.BasePoint, time / 60f);
                }
            }

            if (AttackTimer == 400 && !Main.expertMode) ResetAttack(); //stop on normal mode only

            for (int k = 0; k < 4; k++)
            {
                Tentacle tentacle = tentacles[k].ModNPC as Tentacle;

                if (AttackTimer == 401)
                {
                    RandomizeTarget();
                    Player Player = Main.player[NPC.target];

                    tentacles[k].Center = Player.Center + new Vector2(k % 2 == 0 ? -800 : 800, k > 1 ? 0 : -400);
                    tentacle.BasePoint = tentacles[k].Center;
                    tentacle.MovementTarget = Main.player[NPC.target].Center;

                    SpawnTell(tentacle.MovementTarget, tentacle.BasePoint);

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Drown, NPC.Center);
                }

                if (AttackTimer > 420 && AttackTimer < 460) //shooting out
                {
                    if (AttackTimer == 401)
                    {
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Splash, NPC.Center);
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item81, NPC.Center);
                    }

                    tentacles[k].Center = Vector2.SmoothStep(tentacle.BasePoint, tentacle.MovementTarget, (AttackTimer - 420) / 40f);
                    tentacles[k].ai[1] += 5f; //make it squirm faster
                }

                if (AttackTimer > 460 && AttackTimer < 520) //retracting
                {
                    tentacles[k].Center = Vector2.SmoothStep(tentacle.MovementTarget, tentacle.BasePoint, (AttackTimer - 460) / 60f);
                }
            }

            if (AttackTimer > 550) ResetAttack();
        }

        private void StealPlatform()
        {
            if (AttackTimer == 1)
            {
                ShufflePlatforms();

                Tentacle tentacle = tentacles[0].ModNPC as Tentacle;
                tentacles[0].Center = new Vector2(platforms[0].Center.X, spawnPoint.Y - 100);
                tentacle.BasePoint = tentacles[0].Center;
                tentacle.NPC.netUpdate = true;
            }

            if (AttackTimer < 90)
            {
                Dust.NewDust(platforms[0].position, 200, 16, DustID.Fireworks, 0, 0, 0, default, 0.7f);

                Tentacle tentacle = tentacles[0].ModNPC as Tentacle;
                tentacles[0].Center = Vector2.SmoothStep(tentacle.BasePoint, platforms[0].Center, AttackTimer / 90f);
            }

            if (AttackTimer == 90)
            {
                Tentacle tentacle = tentacles[0].ModNPC as Tentacle;
                tentacle.MovementTarget = tentacles[0].Center;
                platforms[0].ai[3] = 450; //sets it into fall mode
            }

            if (AttackTimer > 90)
            {
                Tentacle tentacle = tentacles[0].ModNPC as Tentacle;
                tentacles[0].Center = Vector2.SmoothStep(tentacle.MovementTarget, tentacle.BasePoint, (AttackTimer - 90) / 90f);
            }

            if (AttackTimer == 180) ResetAttack();
        }

        private void InkBurst2()
        {
            if (AttackTimer == 1)
            {
                NPC.velocity *= 0;
                NPC.velocity.Y = -10;
            }

            if (AttackTimer <= 61) NPC.velocity.Y += 10 / 60f;

            if (AttackTimer > 61)
            {
                if (AttackTimer == 64)
                {
                    for (float k = 0; k <= 3.14f; k += 2.14f / 3f)
                    {
                        Projectile.NewProjectile(NPC.GetSpawnSourceForProjectileNPC(), NPC.Center + new Vector2(0, 100), new Vector2(10, 0).RotatedBy(k), ModContent.ProjectileType<InkBlob>(), 10, 0.2f, 255, 0, Main.rand.NextFloat(6.28f));
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item95, NPC.Center);
                    }
                }
            }

            if (AttackTimer == 76) ResetAttack();
        }
        #endregion

        #region visual helpers
        private void SplashDustSmall(int k)
        {
            for (int i = 0; i < 30; i++)
            {
                Dust.NewDustPerfect(tentacles[k].Center + new Vector2(Main.rand.NextFloat(-20, 20), 0), 33, -Vector2.UnitY.RotatedByRandom(0.8f) * Main.rand.NextFloat(4), 0, default, 2);
                Dust.NewDustPerfect(tentacles[k].Center + new Vector2(Main.rand.NextFloat(-20, 20), 0), ModContent.DustType<Dusts.Starlight>(), -Vector2.UnitY.RotatedByRandom(0.8f) * Main.rand.NextFloat(40), 0, default, Main.rand.NextFloat());
            }
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Splash, tentacles[k].Center);
        }

        private void SplashDust(int k)
        {
            for (int i = 0; i < 50; i++)
            {
                Dust.NewDustPerfect(tentacles[k].Center + new Vector2(Main.rand.NextFloat(-20, 20), 0), 33, -Vector2.UnitY.RotatedByRandom(0.5f) * Main.rand.NextFloat(12), 0, default, 2);
                Dust.NewDustPerfect(tentacles[k].Center + new Vector2(Main.rand.NextFloat(-20, 20), 0), ModContent.DustType<Dusts.Starlight>(), -Vector2.UnitY.RotatedByRandom(0.5f) * Main.rand.NextFloat(80), 0, default, Main.rand.NextFloat());
            }
        }
        #endregion
    }
}