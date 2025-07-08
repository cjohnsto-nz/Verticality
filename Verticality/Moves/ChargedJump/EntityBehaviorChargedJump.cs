using System;
using Verticality.Integration;
using Verticality.Lib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace Verticality.Moves.ChargedJump
{
    internal class EntityBehaviorChargedJump : EntityBehavior
    {
        private static float jumpForceAdd
        {
            get
            {
                return VerticalityModSystem.Config.modConfig.chargedJumpAddForce;
            }
        }
        private static float jumpChargeTime
        {
            get
            {
                return VerticalityModSystem.Config.modConfig.chargedJumpChargeTime;
            }
        }

        private float jumpForce
        {
            get
            {
                // Vintagestory.API.Common.Entities.PModuleOnGround
                return (GlobalConstants.BaseJumpForce + jumpForceAdd) * MathF.Sqrt(MathF.Max(1f, entity.Stats.GetBlended("jumpHeightMul"))) / 60f;
            }
        }
        float t;
        public EntityBehaviorChargedJump(Entity entity) : base(entity) { }

        public override string PropertyName()
        {
            return "chargedjump";
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);

            if (entity.Api.Side != EnumAppSide.Client) return;

            EntityPlayer player = (EntityPlayer)entity;

            ICoreClientAPI capi = player.Api as ICoreClientAPI;

            // Don't allow charged jumps if exhausted
            bool canChargeJump = !VigorIntegration.IsExhausted(player);

            if (player.Controls.Sneak && player.OnGround && canChargeJump)
            {
                if (capi.Input.IsHotKeyPressed("jump"))
                {
                    t += dt;
                    
                    // Add some visual feedback about charging
                    if (t > 0.1f)
                    {
                        float chargePercent = Math.Min(1.0f, t / jumpChargeTime);
                        // Could add particle effects or other visual indicators based on chargePercent
                    }
                }
                else
                {
                    if (t > 0.1f)
                    {
                        // Calculate charge percentage (0.0 - 1.0), clamped to prevent overcharging
                        float chargePercent = GameMath.Clamp(t / jumpChargeTime, 0f, 1f);
                        
                        // Calculate jump force based on charge time, with explicit clamp
                        float actualJumpForce = GameMath.Clamp(GameMath.Lerp(0, jumpForce, chargePercent), 0, jumpForce);
                        
                        // Calculate stamina cost proportional to the jump force, using config values
                        float staminaCostBase = VerticalityModSystem.Config.modConfig.chargedJumpStaminaCostBase;
                        float staminaCostMax = VerticalityModSystem.Config.modConfig.chargedJumpStaminaCostMax;
                        
                        // Explicitly clamp the stamina cost to match the same scale as the jump force
                        float staminaCost = GameMath.Clamp(GameMath.Lerp(staminaCostBase, staminaCostMax, chargePercent), staminaCostBase, staminaCostMax);
                        
                        // Debug logging
                        capi.Logger.Event("[Verticality:ChargedJump] Attempting charged jump with {0:F1}% charge, {1:F2} stamina cost",
                            100 * chargePercent, staminaCost);
                        
                        // Only do the jump if we have enough stamina
                        // The network-aware VigorIntegration will handle client-server communication
                        if (VigorIntegration.ConsumeStamina(player, staminaCost))
                        {
                            player.Pos.Motion.Y += actualJumpForce;
                            capi.Logger.Event("[Verticality:ChargedJump] Jump executed with force {0:F2}", actualJumpForce);
                        }
                        else
                        {
                            capi.Logger.Event("[Verticality:ChargedJump] Jump prevented due to stamina check");
                        }
                    }
                    t = 0;
                }
            }
            else t = 0;
        }
    }
}
