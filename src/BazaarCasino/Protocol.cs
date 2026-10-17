using System;
using UnityEngine.Networking;

namespace BazaarCasino
{
    internal enum ActionKind : byte { State, Deal, Hit, Stand, Double, Roulette, Slots, Sell, TableJoin, TableLeave, TableBet, TableStart, TableHit, TableStand, TableDouble, LoungeEnter, LoungeExit, TableSplit, CaseRedeem, RaceBet }
    internal sealed class CasinoRequest : MessageBase
    {
        public uint requestId;
        public uint masterId;
        public ActionKind action;
        public int wager;
        public int choice;
        public int number;
        public RouletteBet[] bets;
        public byte table;
        public override void Serialize(NetworkWriter w)
        {
            w.Write((ushort)200); w.Write(requestId); w.Write(masterId); w.Write((byte)action); w.Write(wager); w.Write(choice); w.Write(number); w.Write(table);
            w.Write((byte)(bets?.Length ?? 0));
            if (bets != null) foreach(var bet in bets) { w.Write(bet.choice); w.Write(bet.number); w.Write(bet.amount); }
        }
        public override void Deserialize(NetworkReader r)
        {
            if(r.ReadUInt16()!=200)throw new InvalidOperationException("Casino version mismatch; use 1.10.x on every player");
            requestId = r.ReadUInt32(); masterId = r.ReadUInt32(); action = (ActionKind)r.ReadByte(); wager = r.ReadInt32(); choice = r.ReadInt32(); number = r.ReadInt32(); table=r.ReadByte();
            int count = r.ReadByte(); if(count>49) throw new InvalidOperationException("Too many roulette spots");
            bets = new RouletteBet[count];
            for(int i=0;i<count;i++) bets[i]=new RouletteBet { choice=r.ReadInt32(), number=r.ReadInt32(), amount=r.ReadInt32() };
        }
    }
    internal sealed class TableReply : MessageBase
    {
        public string json;
        public override void Serialize(NetworkWriter w)=>w.Write(json);
        public override void Deserialize(NetworkReader r)=>json=r.ReadString();
    }
    internal sealed class CasinoReply : MessageBase
    {
        public string json;
        public override void Serialize(NetworkWriter w) => w.Write(json);
        public override void Deserialize(NetworkReader r) => json = r.ReadString();
    }
    [Serializable] internal class SaleItem { public int index; public int count; public int price; }
    [Serializable] internal class CasinoState
    {
        public uint requestId;
        public uint gold;
        public int minBet;
        public int maxBet;
        public int wager;
        public int payout;
        public string message;
        public int game;
        public bool tableReply;
        public bool loungeReply,loungeMoved;
        public uint loungeBodyId,loungeSceneId;
        public float loungeX,loungeY,loungeZ,loungeYaw;
        public bool raceReply;
        public RaceSnapshot raceState;
        public TableSnapshot tableState;
        public int wheelNumber = -1;
        public int spinSeed;
        public int[] reels;
        public bool handActive;
        public bool canDouble;
        public int handWager;
        public int[] player;
        public int[] dealer;
        public int playerTotal;
        public int dealerTotal;
        public SaleItem[] items;
        public bool caseEnabled;
        public bool caseCommand;
        public int casePrice;
        public int[] caseItems,caseWeights;
        public int[] caseRewards;
        public uint caseToken;
        public int caseItem=-1,caseSeed;
        public float caseElapsed;
    }
}
