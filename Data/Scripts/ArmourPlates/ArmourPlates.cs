
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Screens.Helpers;
using Sandbox.Game.World;
using Sandbox.Game.WorldEnvironment.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using IMyCubeGrid = VRage.Game.ModAPI.IMyCubeGrid;
using IMyEntity = VRage.ModAPI.IMyEntity;
using IMyInventoryItem = VRage.Game.ModAPI.IMyInventoryItem;

namespace ArmourPlates
{

    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]

    public class ArmourPlates : MySessionComponentBase
    {

        public bool setupRun = false;

        public static bool isClient => !(isServer && isDedicated);
        public static bool isDedicated => MyAPIGateway.Utilities.IsDedicated;
        public static bool isServer => MyAPIGateway.Multiplayer.IsServer;
        public static bool isActive => MyAPIGateway.Multiplayer.MultiplayerActive;
        public static bool isPlayer => MyAPIGateway.Session.Player != null;
        public static IMyPlayer myPlayer => MyAPIGateway.Session.Player;

        public int ticks = 0;

        public override void UpdateAfterSimulation()
        {
            if (!isServer)
            {
                return;
            }

            if (!setupRun)
            {
                //// MyLog.Defaultog.Default.WriteLineAndConsole($"Setup Run.");
                MyAPIGateway.Session.DamageSystem.RegisterBeforeDamageHandler(11, ArmourPlateMitigation);
                //// MyLog.Defaultog.Default.WriteLineAndConsole($"Registered Damage Handler");
                setupRun = true;
            }
        }

        private void ArmourPlateMitigation(object target, ref MyDamageInformation damageInformation)
        {
            // MyLog.Defaultog.Default.WriteLineAndConsole($"Damage Handler of type:{damageInformation.Type.ToString()}");
            if (damageInformation.Type != MyDamageType.Bullet && damageInformation.Type != MyDamageType.Rocket && damageInformation.Type != MyDamageType.Explosion && damageInformation.Type != MyDamageType.Environment)
            { return; }

            if (target == null)
            {
                //// MyLog.Defaultog.Default.WriteLineAndConsole("Got no Target");
                return;
            }

            if (target is IMyCharacter)
            {
                var engineer = target as IMyCharacter;
                //// MyLog.Defaultog.Default.WriteLineAndConsole("Check if they're dead");
                if (engineer == null || engineer.IsDead)
                {
                    return;
                }

                //// MyLog.Defaultog.Default.WriteLineAndConsole("Get controlling entity");
                var player = MyAPIGateway.Players.GetPlayerControllingEntity(target as IMyEntity);
                if (player == null)
                {
                    return;
                }
                //// MyLog.Defaultog.Default.WriteLineAndConsole("Create Items");
                var ceramicPlatesItem = MyDefinitionId.Parse("MyObjectBuilder_Component/CeramicPlates");

                var steelPlatesItem = MyDefinitionId.Parse("MyObjectBuilder_Component/SteelArmourPlates");

                var ballisticFiber = MyDefinitionId.Parse("MyObjectBuilder_Component/BallisticFiber");

                //// MyLog.Defaultog.Default.WriteLineAndConsole("Get inventory");
                var ceramicItemAmount = MyVisualScriptLogicProvider.GetPlayersInventoryItemAmount(player.IdentityId, ceramicPlatesItem);
                var steelItemAmount = MyVisualScriptLogicProvider.GetPlayersInventoryItemAmount(player.IdentityId, steelPlatesItem);
                
                var ballisticFiberItemAmount = MyVisualScriptLogicProvider.GetPlayersInventoryItemAmount(player.IdentityId, ballisticFiber);

                // MyLog.Defaultog.Default.WriteLineAndConsole($"Got inventory Ceramic:{ceramicItemAmount}, steel: {steelItemAmount}, kevlar:{ballisticFiberItemAmount}");

                if (ceramicItemAmount == 0 && steelItemAmount == 0 && ballisticFiberItemAmount == 0)
                {
                    return;
                }
                var damageResult = damageInformation.Amount;
                // MyLog.Defaultog.Default.WriteLineAndConsole($"\nDamage incoming is:{damageInformation.Amount}\n");
                MyVisualScriptLogicProvider.PlaySingleSoundAtPosition("ArcWepShipGatlingImpRock", engineer.GetPosition());
                //check for ceramic plates
                if (ballisticFiberItemAmount > 0)
                    damageResult = ReduceDamagePercentage(player, ballisticFiber, ballisticFiberItemAmount, 0.01f, 20, damageResult);
                if (steelItemAmount > 0)
                    damageResult = ReduceDamage(player, steelPlatesItem, steelItemAmount, 50, damageResult, 50);
                if (ceramicItemAmount > 0)
                    damageResult = ReduceDamage(player, ceramicPlatesItem, ceramicItemAmount, 100, damageResult, 120);
                


               // // MyLog.Defaultog.Default.WriteLineAndConsole($"\nDamage was reduced to:{damageResult*2}\n");
                //damageInformation.Amount = (float)(damageResult * 2);
                damageInformation.Amount = 0;
                engineer.DoDamage(damageResult, MyStringHash.GetOrCompute("DamageReduction"), true, null,damageInformation.AttackerId);
            }
        }

        private float ReduceDamagePercentage(IMyPlayer myPlayer, MyDefinitionId objectDefinition, int objectAmount, float protectionAmount, float maxProtectionPer, float damageInformation)
        {
            // MyLog.Defaultog.Default.WriteLineAndConsole($"% Damage Handler");
            if (damageInformation == 0 || objectAmount == 0)
            {
                return damageInformation;
            }

            var damage = 0;

            int objectCounter = 0;
           // // MyLog.Defaultog.Default.WriteLineAndConsole($"\n\nHandler for:{objectDefinition.ToString()}\nAmount:{objectAmount}\nProtection Per:{protectionAmount}% Reducition, max {maxProtectionPer}\nIncoming Damage:{damageInformation}\n");

            //if there's damage coming in. Which there should be...
            if (damageInformation > 0)
            {
                
                
                //check if our total protection value is more than incoming damage.
                if (objectAmount * maxProtectionPer > damageInformation)
                {
                    //we can protect more than is coming in, so figure how how many get hit up
                    objectCounter = Math.Max((int)Math.Round(damageInformation / (objectAmount * maxProtectionPer)), 1);
                    
                    var altDamage = damageInformation * protectionAmount;
                    damage = (int)Math.Max(altDamage,1);
                    //// MyLog.Defaultog.Default.WriteLineAndConsole($"\n\nCan guard all. Using:{objectCounter}\n based on:({runningDamage}/{objectAmount} * {maxProtectionPer}),Min 1\n\nMin damage is {runningDamage - minDamage}, based on: ({runningDamage}-({maxProtectionPer}*{objectCounter})*{protectionAmount})\n\n");
                    //damage is set to min damage.
                   // // MyLog.Defaultog.Default.WriteLineAndConsole($"\n\nDoing:{damageInformation}\n\n");
                    
                }
                else
                {
                    //we don't have enough. How much CAN we block?
                    //var damageCount = objectAmount * protectionAmount;

                    ////this is going to use up all remaining plates.
                    //objectCounter = objectAmount;
                    ////how much bleedthrough?
                    //minDamage = damageCount - (damageCount * protectionAmount);
                    ////reduce what we can.
                    //runningDamage -= (damageCount - minDamage);
                }
            }
            //// MyLog.Defaultog.Default.WriteLineAndConsole($"\n\nDamage was reduced to:{runningDamage}\n Removing:{objectCounter}\n");


            //damageInformation.Amount = runningDamage;
            //if (objectCounter <= minDamage)
            //{
            //    objectCounter = (int)Math.Min(minDamage, objectAmount);
            //}
            MyVisualScriptLogicProvider.RemoveFromPlayersInventory(myPlayer.IdentityId, objectDefinition, objectCounter);
            return damage;

        }
        /// <summary>
        /// Reduce incoming damage
        /// </summary>
        /// <param name="myPlayer">The player with the items</param>
        /// <param name="objectDefinition">Defintiion of the item, example: MyDefinitionId.Parse("MyObjectBuilder_Component/BallisticFiber")</param>
        /// <param name="objectAmount">How many of this item does the person have</param>
        /// <param name="protectionAmount">How much does this item protect?</param>
        /// <param name="damageInformation">The Damage Information object</param>
        /// <param name="minDamage">How much damage will this take at a minimum?</param>
        private float ReduceDamage(IMyPlayer myPlayer, MyDefinitionId objectDefinitionref, int objectAmount, int protectionAmount, float damageInformation, int minDamage = 0)
        {
            // MyLog.Defaultog.Default.WriteLineAndConsole($"Flat Damage Handler");
            if (damageInformation == 0 || objectAmount == 0 )
            {
                return damageInformation;
            }
            int objectCounter = 0;
            float runningDamage = damageInformation;

            // MyLog.Defaultog.Default.WriteLineAndConsole($"\n\nHandler for:{objectDefinitionref.ToString()}\nAmount:{objectAmount}\nProtection Per:{protectionAmount}\nMin Damage:{minDamage}\nIncoming Damage:{runningDamage}\n");

            //if there's damage coming in. Which there should be...
            if (runningDamage > 0)
            {
                //check if our total protection value is more than incoming damage.
                if (objectAmount * protectionAmount > runningDamage)
                {
                    //we can protect more than is coming in, so figure how how many get hit up
                    objectCounter = (int)Math.Round(runningDamage/ (objectAmount * protectionAmount));
                   // // MyLog.Defaultog.Default.WriteLineAndConsole($"\n\nCan guard all. Using:{objectCounter}\n based on:{runningDamage} / ({objectAmount} * {protectionAmount}) \n");
                    //damage is set to 0.
                    runningDamage = 0;
                }else
                {
                    //we don't have enough. How much CAN we block?
                    var damageCount = objectAmount * protectionAmount;

                    //this is going to use up all remaining plates.
                    objectCounter = objectAmount;
                    //reduce what we can.
                    runningDamage -= damageCount;
                }
            }
            // MyLog.Defaultog.Default.WriteLineAndConsole($"\n\nDamage was reduced to:{runningDamage}\n Removing:{objectCounter}\n");


            if (objectCounter <= minDamage)
            {
                objectCounter = Math.Min(minDamage, objectAmount);
            }
            MyVisualScriptLogicProvider.RemoveFromPlayersInventory(myPlayer.IdentityId, objectDefinitionref, objectCounter);
            return runningDamage;
        }
    }
}
