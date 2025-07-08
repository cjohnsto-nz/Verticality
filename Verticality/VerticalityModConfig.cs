using ProtoBuf;

namespace Verticality
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class VerticalityModConfig
    {
        public float climbMaxHeight = 0.7f;
        public float climbMinHeight = 0.5f;
        public float climbGrabDistance = 0.5f;
        public float climbSpeed = 1.5f;
        public float climbJumpHForce = 5f;
        public float climbJumpVForce = 6f;

        public float chargedJumpChargeTime = 0.5f;
        public float chargedJumpAddForce = 1.9f;
        
        // Vigor integration - stamina costs
        public float chargedJumpStaminaCostBase = 0.0f;  // Base stamina cost for minimal charged jump
        public float chargedJumpStaminaCostMax = 30.0f;  // Maximum stamina cost for fully charged jump

        public float crawlSpeedReduction = -0.8f;
    }

    public class VerticalityClientModConfig
    {
        public bool showDebugParticles = false;
        
        public bool dedicatedCrawlKey = false;
        public bool combinationCrawlKeys = true;
        public bool standOnJump = true;
        public bool doubleTapSneakToCrawl = false;
        public int doubleTapSpeed = 500;
        public bool holdCrawl = false;
    }
}
