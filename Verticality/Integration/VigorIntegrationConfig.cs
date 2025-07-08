using ProtoBuf;

namespace Verticality.Integration
{
    /// <summary>
    /// Configuration for Vigor mod integration
    /// </summary>
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class VigorIntegrationConfig
    {
        // ChargedJump stamina costs
        public float ChargedJumpStaminaCostBase = 0.0f;  // Base stamina cost for minimal charged jump
        public float ChargedJumpStaminaCostMax = 30.0f;  // Maximum stamina cost for fully charged jump

        // Climb stamina costs
        public float ClimbStaminaCostPerSecond = 40.0f;  // Stamina cost per second while climbing
        public float ClimbJumpStaminaCost = 20.0f;      // Stamina cost for jumping while climbing
        
        // General settings
        public bool EnableStaminaCosts = true;  // Master switch for all stamina costs
    }
}
