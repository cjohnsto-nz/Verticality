using System;
using Verticality.Integration;
using Verticality.Lib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace Verticality.Moves.Climb
{
    internal class EntityBehaviorClimb : EntityBehavior
    {
        public static float minHeight
        {
            get
            {
                return VerticalityModSystem.Config.modConfig.climbMinHeight;
            }
        }

        public static float maxHeight
        {
            get
            {
                return VerticalityModSystem.Config.modConfig.climbMaxHeight;
            }
        }

        public static float grabDistance
        {
            get
            {
                return VerticalityModSystem.Config.modConfig.climbGrabDistance;
            }
        }

        public static float climbJumpHForce
        {
            get
            {
                return VerticalityModSystem.Config.modConfig.climbJumpHForce;
            }
        }
        public static float climbJumpVForce
        {
            get
            {
                return VerticalityModSystem.Config.modConfig.climbJumpVForce;
            }
        }
        public int climbJumpCooldown = 1000;

        public long climbJumpTime;

        public bool canClimbJump = true;

        public Grab grab;

        public bool ClimbKeyDown
        {
            get
            {
                return ((ICoreClientAPI)entity.Api).Input.IsHotKeyPressed("climb");
            }
        }

        public EntityBehaviorClimb(Entity entity) : base(entity) { }

        public override string PropertyName()
        {
            return "climb";
        }

        // Track if we're currently climbing and consuming stamina
        private bool isClimbing = false;
        
        public override void OnGameTick(float deltaTime)
        {
            base.OnGameTick(deltaTime);

            if (entity.World.Side != EnumAppSide.Client) return;

            EntityPlayer player = (EntityPlayer)entity;

            if (ClimbKeyDown)
            {
                // Check if player can perform stamina action before attempting to climb
                bool canClimb = VigorIntegration.CanPerformStaminaAction(player);
                
                if (!canClimb)
                {
                    // Player is exhausted, cannot climb
                    if (grab != null)
                    {
                        // Force stop climbing if already climbing and exhausted
                        entity.Api.Logger.Event("[Verticality:Climb] Player exhausted, forcing climb stop");
                        grab = null;
                        
                        // Stop stamina drain
                        if (isClimbing)
                        {
                            VigorIntegration.StopStaminaDrain(player, "climb");
                            isClimbing = false;
                        }
                    }
                    return;
                }
                
                if (grab == null)
                {
                    if (((ICoreClientAPI)entity.Api).ElapsedMilliseconds > climbJumpTime)
                    {
                        grab = Grab.TryGrab(player, null, null, (float?)(grabDistance * 1.5));
                        
                        // Start stamina drain if we successfully grabbed
                        if (grab != null && !isClimbing)
                        {
                            isClimbing = VigorIntegration.StartStaminaDrain(player, "climb", 
                                VerticalityModSystem.Config.modConfig.VigorConfig.ClimbStaminaCostPerSecond);
                            
                            if (isClimbing)
                            {
                                entity.Api.Logger.Event("[Verticality:Climb] Started climbing stamina drain");
                            }
                        }
                        
                        if (((ICoreClientAPI)entity.Api).Input.IsHotKeyPressed("jump"))
                        {
                            canClimbJump = false;
                        }
                    }
                }
                else
                {
                    if (grab.CanStillGrab())
                    {
                        //player.Properties.CanClimbAnywhere = true;
                        if (VerticalityModSystem.ClientConfig.showDebugParticles)
                        {
                            Grab.debugParticles.MinPos = grab.grabPos.FullPosition;
                            Grab.debugParticles.Color = ColorUtil.WhiteArgb;
                            player.World.SpawnParticles(Grab.debugParticles);
                        }

                        if (((ICoreClientAPI)entity.Api).Input.IsHotKeyPressed("jump"))
                        {
                            if (canClimbJump)
                            {
                                // Additional stamina cost for climb jump
                                float jumpStaminaCost = VerticalityModSystem.Config.modConfig.VigorConfig.ClimbJumpStaminaCost;
                                if (jumpStaminaCost > 0)
                                {
                                    VigorIntegration.ConsumeStamina(player, jumpStaminaCost);
                                }
                                
                                entity.Pos.Motion
                                    .Add(grab.grabPos.Face.Normald * climbJumpHForce / 60f)
                                    .Add(0, climbJumpVForce / 60f, 0);

                                // Stop stamina drain when jumping off
                                if (isClimbing)
                                {
                                    VigorIntegration.StopStaminaDrain(player, "climb");
                                    isClimbing = false;
                                }
                                
                                grab = null;
                                climbJumpTime = ((ICoreClientAPI)entity.Api).ElapsedMilliseconds + climbJumpCooldown;
                            }
                        } else
                        {
                            canClimbJump = true;
                        }
                    }
                    else
                    {
                        grab = Grab.TryGrab(player);
                        
                        // If we couldn't grab again, stop stamina drain
                        if (grab == null && isClimbing)
                        {
                            VigorIntegration.StopStaminaDrain(player, "climb");
                            isClimbing = false;
                            entity.Api.Logger.Event("[Verticality:Climb] Stopped climbing stamina drain (lost grab)");
                        }
                        // If we grabbed successfully but weren't draining, start drain
                        else if (grab != null && !isClimbing)
                        {
                            isClimbing = VigorIntegration.StartStaminaDrain(player, "climb", 
                                VerticalityModSystem.Config.modConfig.VigorConfig.ClimbStaminaCostPerSecond);
                            
                            if (isClimbing)
                            {
                                entity.Api.Logger.Event("[Verticality:Climb] Started climbing stamina drain");
                            }
                        }
                    }
                }
            }
            else
            {
                climbJumpTime = 0;
                if (grab != null)
                {
                    //player.Properties.CanClimbAnywhere = false;
                    grab = null;
                    
                    // Stop stamina drain when letting go
                    if (isClimbing)
                    {
                        VigorIntegration.StopStaminaDrain(player, "climb");
                        isClimbing = false;
                        entity.Api.Logger.Event("[Verticality:Climb] Stopped climbing stamina drain (key released)");
                    }
                }
            }
        }
    }
}
