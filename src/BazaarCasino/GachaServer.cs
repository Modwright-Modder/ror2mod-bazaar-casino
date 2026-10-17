using System;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BazaarCasino
{
    public sealed partial class CasinoPlugin
    {
        private ConfigEntry<bool> gachaEnabled, mirrorGreenPrice;
        private ConfigEntry<int> gachaBaseCost, commonWeight, uncommonWeight, legendaryWeight;
        private ConfigEntry<float> gachaPriceMultiplier;
        private bool priceWarning;
        private void ConfigureGacha()
        {
            gachaEnabled = Config.Bind("Gacha", "Enabled", true, "Enable the Lunar Cache machine inside the lounge.");
            mirrorGreenPrice = Config.Bind("Gacha", "MatchBiggerBazaarUncommon", true, "Mirror Bigger Bazaar's green gold-shop price if installed, including its difficulty/scaling settings.");
            gachaBaseCost = Config.Bind("Gacha", "BaseGoldCost", 50, new ConfigDescription("Fallback difficulty-scaled gold cost when Bigger Bazaar pricing is unavailable.", new AcceptableValueRange<int>(1,1000000)));
            gachaPriceMultiplier = Config.Bind("Gacha", "PriceMultiplier", 1f, new ConfigDescription("Multiplier applied after the scaled uncommon/fallback gold price.", new AcceptableValueRange<float>(.1f,100f)));
            commonWeight = Config.Bind("Gacha", "CommonWeight", 70, new ConfigDescription("Relative common chance; unavailable tiers are excluded.", new AcceptableValueRange<int>(0,10000)));
            uncommonWeight = Config.Bind("Gacha", "UncommonWeight", 27, new ConfigDescription("Relative uncommon chance.", new AcceptableValueRange<int>(0,10000)));
            legendaryWeight = Config.Bind("Gacha", "LegendaryWeight", 3, new ConfigDescription("Relative legendary chance.", new AcceptableValueRange<int>(0,10000)));
        }
        internal int CasePrice()
        {
            int price = Run.instance ? Run.instance.GetDifficultyScaledCost(gachaBaseCost.Value) : gachaBaseCost.Value;
            if (mirrorGreenPrice.Value && Run.instance && Chainloader.PluginInfos.TryGetValue("com.MagnusMagnuson.BiggerBazaar", out var info) && info.Instance)
            {
                try
                {
                    // Optional, isolated integration. No copied Bigger Bazaar source
                    // or dependency DLL is included; read the live pricing settings.
                    var type = info.Instance.GetType(); var flags = BindingFlags.Public | BindingFlags.NonPublic;
                    var configType = type.Assembly.GetType("BiggerBazaar.ModConfig");
                    var multi = configType?.GetField("tier2Cost",flags|BindingFlags.Static)?.GetValue(null) as ConfigEntry<float>;
                    var next = configType?.GetField("nextLevelChestPriceScaling",flags|BindingFlags.Static)?.GetValue(null) as ConfigEntry<bool>;
                    if (multi == null || next == null) throw new MissingMemberException("Bigger Bazaar pricing settings changed.");
                    int chest;
                    if (next.Value) chest = Run.instance.GetDifficultyScaledCost(25);
                    else
                    {
                        var bazaar = type.GetField("bazaar",flags|BindingFlags.Instance)?.GetValue(info.Instance);
                        var coefficient = bazaar?.GetType().GetProperty("CurrentDifficultyCoefficient",flags|BindingFlags.Instance)?.GetValue(bazaar);
                        if (!(coefficient is float difficulty) || difficulty <= 0) throw new InvalidOperationException("Bigger Bazaar pricing is not ready.");
                        chest = (int)Math.Min(100000000,25d*Mathf.Pow(difficulty,1.25f));
                    }
                    price = (int)Math.Max(1,Math.Min(100000000,(double)((float)chest*multi.Value)));
                }
                catch (Exception error)
                { if(!priceWarning){priceWarning=true;Logger.LogWarning("Gacha uses fallback difficulty pricing: "+error.Message);} }
            }
            return GachaRules.ScalePrice(price,gachaPriceMultiplier.Value);
        }
        private ItemDef[][] CasePool()
        {
            var pools = new ItemDef[3][];
            var tiers = new[] {ItemTier.Tier1, ItemTier.Tier2, ItemTier.Tier3};
            var drops=new[]{Run.instance.availableTier1DropList,Run.instance.availableTier2DropList,Run.instance.availableTier3DropList};
            for(int i=0;i<3;i++)
            {
                var tier=tiers[i];
                pools[i]=drops[i].Select(p=>PickupCatalog.GetPickupDef(p)).Where(p=>p!=null).Select(p=>ItemCatalog.GetItemDef(p.itemIndex))
                    .Where(d=>d&&!d.hidden&&d.canRemove&&d.tier==tier&&
                    GachaRules.NormalReward(d.name,d.ContainsTag(ItemTag.WorldUnique),d.ContainsTag(ItemTag.IgnoreForDropList),d.ContainsTag(ItemTag.Scrap),
                        RoR2.Artifacts.CommandArtifactManager.IsCommandArtifactEnabled&&d.ContainsTag(ItemTag.CommandArtifactBlacklist)))
                    .Distinct().ToArray();
            }
            return pools;
        }
        private int[] CaseWeights(ItemDef[][] pools)=>new[] {pools[0].Length>0?commonWeight.Value:0,pools[1].Length>0?uncommonWeight.Value:0,pools[2].Length>0?legendaryWeight.Value:0};
        private string RedeemCase(CharacterMaster master,Session session,CasinoState state,CasinoRequest request)
        {
            if(!gachaEnabled.Value||!lounge||!lounge.Cache)return "Lunar Cache is unavailable.";
            if(Vector3.Distance(master.GetBody().corePosition,lounge.Cache.transform.position)>8)return "Move closer to the Lunar Cache machine.";
            if(Time.unscaledTime<session.caseReadyAt)return "Your case is still opening.";
            if(session.casePending)return "Your reward is dispensing.";
            if(!master.inventory)return "Inventory is unavailable.";
            int count=request.number;
            if(!CaseBatchRules.ValidCount(count))return "Choose 1x, 5x or 10x cases.";
            int price=CasePrice();
            int total=CaseBatchRules.Total(price,count);
            if(request.wager!=total)return "Case price updated. Review the new price and open again.";
            if(master.money<(uint)total)return "Not enough gold to open these cases.";
            var pools=CasePool();var weights=CaseWeights(pools);
            var rewards=new int[count];var available=pools.Select(p=>p.Length>0).ToArray();
            for(int i=0;i<count;i++)
            {
                int tier=GachaRules.ChooseTier(rng.NextDouble(),weights,available);
                if(tier<0)return "No case rewards are available in this run.";
                rewards[i]=(int)pools[tier][rng.Next(pools[tier].Length)].itemIndex;
            }
            master.money-=(uint)total;
            session.caseToken++;session.caseRewards=rewards;session.caseItem=rewards[0];session.caseSeed=rng.Next();session.caseStarted=Time.unscaledTime;
            session.caseReadyAt=Time.unscaledTime+SpinTiming.Case;
            session.casePending=true;session.caseCost=total;session.caseUnitPrice=price;
            session.caseCommand=RoR2.Artifacts.CommandArtifactManager.IsCommandArtifactEnabled;
            state.game=5;state.wager=total;state.message="Cases redeemed.";
            return null;
        }
        private void UpdateCaseDrops()
        {
            if(!NetworkServer.active||!InBazaar||SceneExitController.isRunning||!lounge||!lounge.Cache)return;
            foreach(var pair in sessions)
            {
                var session=pair.Value;
                if(!session.casePending||Time.unscaledTime<session.caseReadyAt)continue;
                session.casePending=false; // Commit the paid batch once, with one droplet per reward.
                for(int i=0;i<session.caseRewards.Length;i++)try
                {
                    var cabinet=lounge.Cache.transform;
                    float spread=(i-(session.caseRewards.Length-1)*.5f)*.24f;
                    PickupDropletController.CreatePickupDroplet(new UniquePickup{pickupIndex=PickupCatalog.FindPickupIndex((ItemIndex)session.caseRewards[i])},
                        cabinet.TransformPoint(new Vector3(spread,.8f,-1.4f)),cabinet.TransformDirection(new Vector3(spread*1.4f,5+(i%3)*.2f,-2.5f)),false);
                    // Native CreatePickupDroplet applies the Command artifact flag.
                    // Keep ownership/collection behaviour identical to normal drops.
                }
                catch(Exception error)
                {if(pair.Key)Credit(pair.Key,session.caseUnitPrice);Logger.LogError("Cache reward "+(i+1)+" could not drop; its unit price was refunded: "+error);}
            }
        }
        private void RefundPendingCases(bool settle)
        {
            foreach(var pair in sessions)
            {
                if(settle&&NetworkServer.active&&pair.Key&&pair.Value.casePending)Credit(pair.Key,pair.Value.caseCost);
                pair.Value.casePending=false;
            }
        }
        private void FillCaseState(CasinoState state,Session session)
        {
            state.caseEnabled=gachaEnabled.Value;state.casePrice=CasePrice();
            if(state.tableReply||state.raceReply)return;
            var pools=CasePool();state.caseWeights=CaseWeights(pools);
            state.caseItems=pools.SelectMany(p=>p.Select(d=>(int)d.itemIndex)).ToArray();
            state.caseToken=session.caseToken;state.caseItem=session.caseItem;state.caseSeed=session.caseSeed;
            state.caseRewards=session.caseRewards;
            state.caseCommand=session.caseCommand;
            state.caseElapsed=Math.Max(0,Time.unscaledTime-session.caseStarted);
        }
        internal void OpenCaseMachine(Interactor actor,WorldCaseMachine machine)
        {
            var body=LocalMaster?LocalMaster.GetBody():null;
            if(body&&actor&&actor.gameObject==body.gameObject&&InBazaar&&ui.CanOpenInteraction&&Vector3.Distance(body.corePosition,machine.transform.position)<8)ui.OpenCase(machine);
        }
    }
}
