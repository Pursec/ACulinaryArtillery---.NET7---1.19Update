﻿using ACulinaryArtillery.GUI;
using ACulinaryArtillery.Util;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.Common;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace ACulinaryArtillery
{
    public class ACulinaryArtillery : ModSystem
    {
        private static Harmony harmony;
        public static ILogger logger;
        ICoreAPI API;
        IClientNetworkChannel ClientChannel;
        IServerNetworkChannel ServerChannel;
        public override void Start(ICoreAPI api)
        {
            //base.Start(api);
            API = api;
            api.RegisterBlockClass("BlockMeatHooks", typeof(BlockMeatHooks));
            api.RegisterBlockEntityClass("MeatHooks", typeof(BlockEntityMeatHooks));

            api.RegisterBlockClass("BlockBottleRack", typeof(BlockBottleRack));
            api.RegisterBlockEntityClass("BottleRack", typeof(BlockEntityBottleRack));

            api.RegisterBlockClass("BlockMixingBowl", typeof(BlockMixingBowl));
            api.RegisterBlockEntityClass("MixingBowl", typeof(BlockEntityMixingBowl));

            api.RegisterBlockClass("BlockBottle", typeof(BlockBottle));
            api.RegisterBlockEntityClass("Bottle", typeof(BlockEntityBottle));

            api.RegisterBlockClass("BlockSpile", typeof(BlockSpile));
            api.RegisterBlockEntityClass("Spile", typeof(BlockEntitySpile));

            api.RegisterBlockClass("BlockSaucepan", typeof(BlockSaucepan));
            api.RegisterBlockEntityClass("Saucepan", typeof(BlockEntitySaucepan));

            //Old defunct oven classes
            //api.RegisterBlockClass("BlockExpandedClayOven", typeof(BlockExpandedClayOven));
            //api.RegisterBlockEntityClass("ExpandedOvenOLD", typeof(BlockEntityExpandedOvenOLD));

            api.RegisterBlockEntityClass("ExpandedOven", typeof(BlockEntityExpandedOven));

            api.RegisterItemClass("SuperFood", typeof(ItemSuperFood));
            api.RegisterItemClass("EggCrack", typeof(ItemEggCrack));
            api.RegisterItemClass("ExpandedRawFood", typeof(ItemExpandedRawFood));
            api.RegisterItemClass("ExpandedFood", typeof(ItemExpandedFood));
            api.RegisterItemClass("TransFix", typeof(ItemTransFix));
            api.RegisterItemClass("TransLiquid", typeof(ItemTransLiquid));
            api.RegisterItemClass("ExpandedLiquid", typeof(ItemExpandedLiquid));
            api.RegisterItemClass("ExpandedDough", typeof(ItemExpandedDough));
            ClassRegistry classRegistry = (api as ServerCoreAPI)?.ClassRegistryNative ?? (api as ClientCoreAPI)?.ClassRegistryNative;
            if (classRegistry != null)
            {
                classRegistry.RegisterInventoryClass("inventoryRecipeCreator", typeof(InventoryRecipeCreator));
            }
            //Check for Existing Config file, create one if none exists
            try
            {
                var Config = api.LoadModConfig<ACulinaryArtilleryConfig>("aculinaryartillery.json");
                if (Config != null)
                {
                    api.Logger.Notification("Mod Config successfully loaded.");
                    ACulinaryArtilleryConfig.Current = Config;
                }
                else
                {
                    api.Logger.Notification("No Mod Config specified. Falling back to default settings");
                    ACulinaryArtilleryConfig.Current = ACulinaryArtilleryConfig.GetDefault();
                }
            }
            catch
            {
                ACulinaryArtilleryConfig.Current = ACulinaryArtilleryConfig.GetDefault();
                api.Logger.Error("Failed to load custom mod configuration. Falling back to default settings!");
            }
            finally
            {
                api.StoreModConfig(ACulinaryArtilleryConfig.Current, "aculinaryartillery.json");
            }

            logger = api.Logger;

            if (harmony is null) {
                harmony = new Harmony("com.jakecool19.efrecipes.cookingoverhaul");
                harmony.PatchAll(Assembly.GetExecutingAssembly());
            }
            
        }
        public override void StartClientSide(ICoreClientAPI api)
        {   
            base.StartClientSide(api);
            ClientChannel = api.Network.RegisterChannel("clearRecipeCreatorInv").RegisterMessageType(typeof(bool));
            /*
            api.Event.PlayerJoin += (player) =>
            {
                if (player.InventoryManager.GetOwnInventory("inventoryRecipeCreator") == null)
                {
                    logger.Debug("CLIENT: Could not find recipeCreator Inventory on player join, attempting to make");
                    InventoryRecipeCreator inv = (api as ClientCoreAPI)?.ClassRegistryNative.CreateInventory("inventoryRecipeCreator", "inventoryRecipeCreator-" + player.PlayerUID, api) as InventoryRecipeCreator;
                    logger.Debug($"{inv.InventoryID}");
                    player.InventoryManager.Inventories.Add(inv.InventoryID, inv);
                    logger.Debug($"CLIENT: Found inv I just added?: {player.InventoryManager.GetOwnInventory("inventoryRecipeCreator") != null}");
                };
            };
            */
            (api as ICoreClientAPI).Gui.RegisterDialog(new GuiRecipeCreator(api));
            var meatHookTransformConfig = new TransformConfig
            {
                AttributeName = "meatHookTransform",
                Title = "On Meathook"
            };
            GuiDialogTransformEditor.extraTransforms.Add(meatHookTransformConfig);
        }

        public override void Dispose()
        {
            logger.Debug("Unpatching harmony methods");
            harmony.UnpatchAll(harmony.Id);
            harmony = null;
            //base.Dispose();
        }
        public override void StartServerSide(ICoreServerAPI api)
        {
            ServerChannel = api.Network.RegisterChannel("clearRecipeCreatorInv").RegisterMessageType(typeof(bool)).SetMessageHandler<bool>(ClearRecipeCreatorInv);
            api.Event.PlayerJoin += (player) =>
            {
                if(player.InventoryManager.GetOwnInventory("inventoryRecipeCreator") == null)
                {
                    InventoryRecipeCreator inv = new InventoryRecipeCreator("inventoryRecipeCreator", player.PlayerUID, player.Entity.Api);
                    (player.InventoryManager as PlayerInventoryManager).Inventories.Add(inv.InventoryID, inv);
                    var lines = player.InventoryManager.Inventories.Select(kvp => kvp.Key + ": " + kvp.Value.ToString());
                    string outputString = string.Join(Environment.NewLine, lines);
                    logger.Debug(outputString);
                }

            };
            /*
            api.Event.PlayerNowPlaying += (player) =>
            {
                if (player.InventoryManager.GetOwnInventory("inventoryRecipeCreator") == null)
                {
                    logger.Debug("SERVER: Could not find recipeCreator Inventory on player join, attempting to make");
                    InventoryRecipeCreator inv = (api as ServerCoreAPI)?.ClassRegistryNative.CreateInventory("inventoryRecipeCreator", "inventoryRecipeCreator-" + player.PlayerUID, api) as InventoryRecipeCreator;
                    logger.Debug($"{inv.InventoryID}");
                    player.InventoryManager.Inventories.Add("inventoryRecipeCreator", inv);
                    logger.Debug($"SERVER: Found inv I just added?: {player.InventoryManager.GetOwnInventory("inventoryRecipeCreator") != null}");
                };
            };
            */
            //base.StartServerSide(api);

            /*
            api.RegisterCommand("efremap", "Remaps items in Expanded Foods", "",
                //This can't possibly work XD
                (IServerPlayer player, int groupId, CmdArgs args) =>
                {
                    api.World.BlockAccessor.WalkBlocks(player.Entity.ServerPos.AsBlockPos.AddCopy(-10), player.Entity.ServerPos.AsBlockPos.AddCopy(10), (block, posX, posY, posZ) => {

                        BottleFix(new BlockPos(posX, posY, posZ), block, api.World);
                    });
                }, Privilege.chat);
            */
        }

        private void ClearRecipeCreatorInv(IServerPlayer fromPlayer, bool packet)
        {
            if(packet) 
            {
                logger.Debug("Received packet to clear inv");
                (fromPlayer.InventoryManager.GetOwnInventory("inventoryRecipeCreator") as InventoryRecipeCreator).DiscardAll();
            };
        }

        public void BottleFix(BlockPos pos, Block block, IWorldAccessor world)
        {
            BlockEntityContainer bc;
            if (block.Code.Path.Contains("bottle") && !block.Code.Path.Contains("burned") && !block.Code.Path.Contains("raw"))
            {
                Block replacement = world.GetBlock(new AssetLocation(block.Code.Domain + ":bottle-" + block.FirstCodePart(1) + "-burned"));

                if (replacement != null) world.BlockAccessor.SetBlock(replacement.BlockId, pos);
            }
            else if ((bc = world.BlockAccessor.GetBlockEntity(pos) as BlockEntityContainer) != null)
            {
                foreach (ItemSlot slot in bc.Inventory)
                {
                    if (slot.Itemstack?.Block != null && slot.Itemstack.Block.Code.Path.Contains("bottle") && !slot.Itemstack.Block.Code.Path.Contains("burned") && !slot.Itemstack.Block.Code.Path.Contains("raw"))
                    {
                        Block replacement = world.GetBlock(new AssetLocation(slot.Itemstack.Block.Code.Domain + ":bottle-" + slot.Itemstack.Block.FirstCodePart(1) + "-burned"));
                        if (replacement != null)
                        {
                            slot.Itemstack = new ItemStack(replacement, slot.Itemstack.StackSize);
                        }
                    }
                }
            }
        }
        public override void AssetsFinalize(ICoreAPI api)
        {
            base.AssetsFinalize(api);
            api.GetCookingRecipes().ForEach(recipe =>
            {
                if (!CookingRecipe.NamingRegistry.ContainsKey(recipe.Code))
                {
                    CookingRecipe.NamingRegistry[recipe.Code] = new acaRecipeNames();
                }
            });
            api.GetMixingRecipes().ForEach(recipe =>
            {
                if (!CookingRecipe.NamingRegistry.ContainsKey(recipe.Code))
                {
                    CookingRecipe.NamingRegistry[recipe.Code] = new acaRecipeNames();
                }
            });
        }
        internal static void LogError(string message) {
            logger?.Error("(ACulinaryArtillery): {0}", message);
        }

    }
}

