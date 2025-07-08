using System;
using System.Reflection;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Common.Entities;

namespace Verticality.Integration
{
    /// <summary>
    /// Network packet for server-side stamina consumption
    /// </summary>
    [ProtoContract]
    public class StaminaConsumptionPacket
    {
        [ProtoMember(1)]
        public float Amount { get; set; }
    }
    
    /// <summary>
    /// Integration with Vigor API for stamina consumption
    /// </summary>
    public class VigorIntegrationSystem : ModSystem
    {
        private const string NETWORK_CHANNEL = "verticality:vigor";
        
        #region Shared
        // Cache API references by context (client/server) to reduce lookup overhead and logging
        private static object _clientApiCache = null;
        private static object _serverApiCache = null;
        private static bool _apiLookupAttempted = false;
        
        /// <summary>
        /// Register the network channel in the shared Start method
        /// </summary>
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            
            // Only register the channel and message types here (shared between client/server)
            api.Network.RegisterChannel(NETWORK_CHANNEL)
                .RegisterMessageType<StaminaConsumptionPacket>();
            
            api.Logger.Event("[Verticality:VigorIntegration] Base network channel registered");
        }
        
        /// <summary>
        /// Check if the Vigor mod is enabled
        /// </summary>
        public static bool IsVigorEnabled(ICoreAPI api)
        {
            return api.ModLoader.IsModEnabled("vigor");
        }
        
        /// <summary>
        /// Gets the appropriate Vigor API instance based on the current context
        /// Uses cached references after first successful lookup
        /// </summary>
        public static dynamic GetVigorAPI(ICoreAPI api)
        {
            // Early return if Vigor isn't enabled
            if (!IsVigorEnabled(api))
                return null;
            
            // Return cached API reference if available
            if (api.Side == EnumAppSide.Client && _clientApiCache != null)
            {
                return _clientApiCache;
            }
            else if (api.Side == EnumAppSide.Server && _serverApiCache != null)
            {
                return _serverApiCache;
            }
            
            // Only log once per session that we're looking up the API
            bool firstLookup = !_apiLookupAttempted;
            _apiLookupAttempted = true;
            
            if (firstLookup)
            {
                api.Logger.Event("[Verticality:VigorIntegration] First-time Vigor API lookup");
            }
            
            try
            {
                // Get the VigorModSystem using string-based approach
                var vigorModSystem = api.ModLoader.GetModSystem("Vigor.VigorModSystem");
                if (vigorModSystem == null)
                {
                    if (firstLookup) api.Logger.Warning("[Verticality:VigorIntegration] VigorModSystem not found via GetModSystem(\"Vigor.VigorModSystem\")");
                    return null;
                }
                
                // Log the type only on first lookup
                if (firstLookup) api.Logger.Debug("[Verticality:VigorIntegration] Found VigorModSystem, type: {0}", vigorModSystem.GetType().FullName);
                
                // Determine which API instance to use based on the context
                string apiPropertyName;
                
                if (api.Side == EnumAppSide.Server)
                {
                    apiPropertyName = "ServerAPI";
                    if (firstLookup) api.Logger.Debug("[Verticality:VigorIntegration] Server context detected, using ServerAPI");
                }
                else if (api.Side == EnumAppSide.Client)
                {
                    apiPropertyName = "ClientAPI";
                    if (firstLookup) api.Logger.Debug("[Verticality:VigorIntegration] Client context detected, using ClientAPI");
                }
                else
                {
                    // Fallback to general API if side is unknown (should never happen)
                    apiPropertyName = "API";
                    if (firstLookup) api.Logger.Warning("[Verticality:VigorIntegration] Unknown API side, using general API (not recommended)");
                }
                
                // Get the appropriate API property via reflection
                var apiProperty = vigorModSystem.GetType().GetProperty(apiPropertyName);
                if (apiProperty == null)
                {
                    if (firstLookup) api.Logger.Warning("[Verticality:VigorIntegration] {0} property not found on VigorModSystem", apiPropertyName);
                    return null;
                }
                
                // Get the API instance
                var vigorApi = apiProperty.GetValue(vigorModSystem);
                if (vigorApi == null)
                {
                    if (firstLookup) api.Logger.Warning("[Verticality:VigorIntegration] {0} property exists but value is null", apiPropertyName);
                    return null;
                }
                
                // Cache the API reference by context
                if (api.Side == EnumAppSide.Client)
                {
                    _clientApiCache = vigorApi;
                    if (firstLookup) api.Logger.Event("[Verticality:VigorIntegration] Successfully cached client-side API reference");
                }
                else if (api.Side == EnumAppSide.Server)
                {
                    _serverApiCache = vigorApi;
                    if (firstLookup) api.Logger.Event("[Verticality:VigorIntegration] Successfully cached server-side API reference");
                }
                
                return vigorApi;
            }
            catch (Exception ex)
            {
                api.Logger.Error("[Verticality:VigorIntegration] Error getting Vigor API: {0}", ex.ToString());
                return null;
            }
        }
        #endregion
        
        #region Server
        private ICoreServerAPI sapi;
        private IServerNetworkChannel serverChannel;
        
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            
            // Don't cache the API reference to ensure we always get the correct instance
            this.sapi = api;
            
            // Get the channel and register the server-side message handler
            serverChannel = api.Network.GetChannel(NETWORK_CHANNEL);
            serverChannel.SetMessageHandler<StaminaConsumptionPacket>(OnServerStaminaRequest);
            
            api.Logger.Event("[Verticality:VigorIntegration] SERVER: Registered message handler for server-side stamina consumption");
        }
        
        /// <summary>
        /// Server-side handler for stamina consumption requests
        /// </summary>
        private void OnServerStaminaRequest(IServerPlayer fromPlayer, StaminaConsumptionPacket packet)
        {
            sapi.Logger.Event("[Verticality:VigorIntegration] SERVER HANDLER: Received stamina consumption request from {0}: {1} stamina", 
                fromPlayer.PlayerName, packet.Amount);
            
            // Execute the actual stamina consumption on the server side
            bool success = ConsumeStaminaOnServer(fromPlayer.Entity as EntityPlayer, packet.Amount);
            sapi.Logger.Event("[Verticality:VigorIntegration] SERVER HANDLER: Stamina consumption result: {0}", success ? "SUCCESS" : "FAILED");
        }
        
        /// <summary>
        /// Consume stamina on the server using the Vigor API
        /// </summary>
        public bool ConsumeStaminaOnServer(EntityPlayer player, float amount)
        {
            if (player == null)
            {
                sapi.Logger.Warning("[Verticality:VigorIntegration] SERVER CONSUME: Called with null player");
                return true; // Allow action if invalid context
            }
            
            sapi.Logger.Event("[Verticality:VigorIntegration] SERVER CONSUME: Processing stamina consumption for player entity {0}, amount {1}", player.EntityId, amount);
            
            var api = VigorIntegrationSystem.GetVigorAPI(sapi);
            if (api == null) 
            {
                sapi.Logger.Warning("[Verticality:VigorIntegration] SERVER CONSUME: Vigor API not found on server, allowing action");
                return true; // Allow action if Vigor not enabled
            }
            
            try
            {
                sapi.Logger.Event("[Verticality:VigorIntegration] SERVER CONSUME: About to call Vigor API ConsumeStamina for player entity {0}, amount {1}", player.EntityId, amount);
                bool result = api.ConsumeStamina(player, amount, false);
                sapi.Logger.Event("[Verticality:VigorIntegration] SERVER CONSUME: Vigor API consumption result: {0}", result ? "SUCCESS" : "FAILED");
                return result;
            }
            catch (Exception ex)
            {
                sapi.Logger.Error("[Verticality:VigorIntegration] SERVER CONSUME: Error calling Vigor API: {0}", ex.ToString());
                return true; // Allow action if API call fails
            }
        }
        #endregion
        
        #region Client
        private ICoreClientAPI capi;
        private IClientNetworkChannel clientChannel;
        
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.capi = api;
            
            // Get the channel for client-side sending
            clientChannel = api.Network.GetChannel(NETWORK_CHANNEL);
            
            api.Logger.Event("[Verticality:VigorIntegration] CLIENT: Network channel ready for stamina consumption requests");
        }
        
        /// <summary>
        /// Client-side method to request stamina consumption on the server
        /// </summary>
        public bool ConsumeStamina(EntityPlayer player, float amount)
        {
            if (capi == null)
            {
                return true; // Allow action if no client API
            }
            
            capi.Logger.Event("[Verticality:VigorIntegration] Attempting to consume {0} stamina for player {1}", amount, player.ToString());
            
            // Check if Vigor is enabled
            if (!IsVigorEnabled(capi))
            {
                capi.Logger.Warning("[Verticality:VigorIntegration] Vigor mod not enabled, allowing action");
                return true; // Allow action if Vigor not enabled
            }
            
            try
            {
                // Client side - send to server
                capi.Logger.Event("[Verticality:VigorIntegration] Client-side request, sending to server: {0} stamina", amount);
                clientChannel.SendPacket(new StaminaConsumptionPacket { Amount = amount });
                return true; // Allow action for now, server will handle the actual consumption
            }
            catch (Exception ex)
            {
                capi.Logger.Error("[Verticality:VigorIntegration] Error sending stamina consumption request: {0}", ex.ToString());
                return true; // Allow action if sending fails
            }
        }
        #endregion
        
    }
    
    /// <summary>
    /// Helper class to provide static access to the vigor integration from other parts of the mod
    /// </summary>
    public static class VigorIntegration
    {
        /// <summary>
        /// Gets the current stamina value for an entity
        /// </summary>
        /// <returns>Current stamina or -1 if Vigor isn't enabled</returns>
        public static float GetCurrentStamina(EntityPlayer player)
        {
            var api = VigorIntegrationSystem.GetVigorAPI(player.Api);
            if (api == null) return -1;
            
            try
            {
                return api.GetCurrentStamina(player);
            }
            catch (Exception ex) { 
                player.Api.Logger.Warning("[Verticality:VigorIntegration] Error getting current stamina: {0}", ex.ToString());
                return -1; 
            }
        }
        
        /// <summary>
        /// Gets the maximum stamina value for an entity
        /// </summary>
        /// <returns>Maximum stamina or -1 if Vigor isn't enabled</returns>
        public static float GetMaxStamina(EntityPlayer player)
        {
            var api = VigorIntegrationSystem.GetVigorAPI(player.Api);
            if (api == null) return -1;
            
            try
            {
                return api.GetMaxStamina(player);
            }
            catch (Exception ex)
            {
                player.Api.Logger.Warning("[Verticality:VigorIntegration] Error getting max stamina: {0}", ex.ToString());
                return -1;
            }
        }
        
        /// <summary>
        /// Checks if the player is exhausted (has no stamina)
        /// </summary>
        public static bool IsExhausted(EntityPlayer player)
        {
            var api = VigorIntegrationSystem.GetVigorAPI(player.Api);
            if (api == null) return false;
            
            try
            {
                return api.IsExhausted(player);
            }
            catch (Exception ex)
            {
                player.Api.Logger.Warning("[Verticality:VigorIntegration] Error checking if exhausted: {0}", ex.ToString());
                return false;
            }
        }
        
        /// <summary>
        /// Consumes stamina if available - client/server compatible method
        /// </summary>
        /// <param name="amount">Amount of stamina to consume</param>
        /// <returns>True if successful, false if not enough stamina or Vigor not enabled</returns>
        public static bool ConsumeStamina(EntityPlayer player, float amount)
        {
            // Debug logging
            player.Api.Logger.Event("[Verticality:VigorIntegration] Attempting to consume {0} stamina for player {1}", amount, player.ToString());
            
            if (!VigorIntegrationSystem.IsVigorEnabled(player.Api))
            {
                player.Api.Logger.Debug("[Verticality:VigorIntegration] Vigor not enabled, skipping stamina consumption");
                return true; // Allow action if Vigor not enabled
            }
            
            // Handle client side differently - send network request to server
            if (player.Api.Side == EnumAppSide.Client)
            {
                player.Api.Logger.Event("[Verticality:VigorIntegration] Client-side request, sending to server: {0} stamina", amount);
                
                // For client side, send a packet to the server
                var clientApi = player.Api as ICoreClientAPI;
                if (clientApi != null)
                {
                    var integrationSystem = clientApi.ModLoader.GetModSystem<VigorIntegrationSystem>();
                    if (integrationSystem != null)
                    {
                        return integrationSystem.ConsumeStamina(player, amount);
                    }
                }
                
                return true; // Allow action if packet send fails (client API unavailable)
            }
            
            // On server side, do the actual consumption
            var serverApi = player.Api as ICoreServerAPI;
            if (serverApi != null)
            {
                var integrationSystem = serverApi.ModLoader.GetModSystem<VigorIntegrationSystem>();
                if (integrationSystem != null)
                {
                    // Call the ModSystem's server-side stamina consumption
                    var serverPlayer = serverApi.World.PlayerByUid(player.PlayerUID);
                    if (serverPlayer != null)
                    {
                        return integrationSystem.ConsumeStaminaOnServer(player, amount);
                    }
                }
            }
            
            return true; // Allow action if something fails
        }
        
        /// <summary>
        /// Drains stamina continuously over time
        /// </summary>
        /// <param name="player">The player</param>
        /// <param name="amountPerSecond">Drain amount per second</param>
        /// <param name="deltaTime">Time elapsed since last frame</param>
        /// <returns>True if stamina was drained successfully</returns>
        public static bool DrainStamina(EntityPlayer player, float amountPerSecond, float deltaTime)
        {
            // Check if stamina costs are enabled in config
            if (!VerticalityModSystem.Config.modConfig.VigorConfig.EnableStaminaCosts)
            {
                return true; // Allow action if stamina costs are disabled
            }
            
            var api = VigorIntegrationSystem.GetVigorAPI(player.Api);
            if (api == null) return true; // Allow action if Vigor not enabled
            
            try
            {
                // Adjust the drain amount if it's from climbing (using config value)
                if (amountPerSecond == 0 && VerticalityModSystem.Config.modConfig.VigorConfig.ClimbStaminaCostPerSecond > 0)
                {
                    // Use the configured value for climbing stamina cost
                    amountPerSecond = VerticalityModSystem.Config.modConfig.VigorConfig.ClimbStaminaCostPerSecond;
                    player.Api.Logger.Debug("[Verticality:VigorIntegration] Using configured climb stamina cost: {0}/sec", amountPerSecond);
                }
                
                return api.DrainStamina(player, amountPerSecond, deltaTime);
            }
            catch (Exception ex)
            {
                player.Api.Logger.Warning("[Verticality:VigorIntegration] Error draining stamina: {0}", ex.ToString());
                return true; // Allow action if API call fails
            }
        }
    }
}
