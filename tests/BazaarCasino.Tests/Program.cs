using System;
using System.Linq;
using BazaarCasino;

static class Program
{
    static int assertions;
    static void ManualCommandTests()
    {
        foreach(string text in new[]{"modwright_blueportal","/modwright_blueportal","  /MODWRIGHT_BLUEPORTAL  "})
            Check(ManualCommandText.TryParse(text,out var command,out int amount,out string error)&&command==ManualCommand.BluePortal&&amount==0&&error==null,"Blue portal chat alias with optional slash");
        foreach(string text in new[]{"modwright_gold 5000","/modwright_gold 5000"," /MODWRIGHT_GOLD\t5000 "})
            Check(ManualCommandText.TryParse(text,out var command,out int amount,out string error)&&command==ManualCommand.Gold&&amount==5000&&error==null,"Gold chat alias parses a bounded integer");
        foreach(string text in new[]{null,"","hello","/help","modwright_blueportal;quit","/modwright_gold_other 5"})
            Check(!ManualCommandText.TryParse(text,out _,out _,out _),"Ordinary chat and other commands remain untouched");
        foreach(string text in new[]{"/modwright_gold","modwright_gold 0","modwright_gold -1","modwright_gold 1000001","modwright_gold 999999999999999999","modwright_gold 5.5","modwright_gold 5000;quit","modwright_gold 5000\nquit","modwright_blueportal extra"})
            Check(ManualCommandText.TryParse(text,out _,out _,out string error)&&error!=null,"Malformed known commands are rejected without forwarding to console");
        foreach(int value in new[]{1,1000000})
            Check(ManualCommandText.TryParse("modwright_gold "+value,out _,out int amount,out string error)&&amount==value&&error==null,"Gold command accepts both boundary amounts");
    }
    static void CrabRaceTests()=>RaceChecks.Run(Check);
    static void Check(bool success, string description) { assertions++; if (!success) throw new Exception(description); }
    static BlackjackRound Hand(int[] player, int[] dealer, int[] nextCards, int wager = 10)
    {
        var hand = new BlackjackRound(new Random(1), wager);
        hand.player.Clear(); hand.player.AddRange(player); hand.dealer.Clear(); hand.dealer.AddRange(dealer);
        hand.deck.Clear(); hand.deck.AddRange(nextCards.Reverse()); hand.active = true; hand.payout = 0;
        return hand;
    }
    static void GachaTests()
    {
        var weights=new[]{70,27,3};var all=new[]{true,true,true};var counts=new int[3];
        for(int i=0;i<10000;i++)counts[GachaRules.ChooseTier((i+.5)/10000,weights,all)]++;
        Check(counts.SequenceEqual(new[]{7000,2700,300}),"Case odds are weighted by rarity, not by catalog size");
        Check(GachaRules.ChooseTier(.70,weights,all)==1&&GachaRules.ChooseTier(.97,weights,all)==2,"Rarity boundary transitions");
        Check(GachaRules.ChooseTier(.999,weights,new[]{true,true,false})==1,"Unavailable legendary tier is excluded");
        Check(GachaRules.ChooseTier(.5,new[]{0,0,0},all)==-1,"Empty chance configuration grants nothing");
        Check(GachaRules.ChooseTier(.5,weights,new[]{false,false,false})==-1,"No available reward pool rejects opening");
        Check(GachaRules.ChooseTier(.1,weights,new[]{false,false,true})==2,"Single remaining rarity remains selectable");
        Check(GachaRules.ScalePrice(50,1)==50&&GachaRules.ScalePrice(123,1.5f)==184,"Scaled uncommon price and configurable multiplier round down");
        Check(GachaRules.ScalePrice(100000000,100)==100000000&&GachaRules.ScalePrice(0,1)==1,"Case price safely bounded");
        float previous=-1;
        for(int step=0;step<=520;step++)
        {
            float time=step*.01f;float p=GachaRules.RevealProgress(time);
            Check(p>=previous&&p<=1,"Case scroll is monotonic and never overshoots");previous=p;
        }
        Check(GachaRules.RevealProgress(-1)==0&&GachaRules.RevealProgress(100)==1,"Late receipt replays settle immediately");
        foreach(float width in new[]{630f,640f,1280f,1920f})foreach(float stride in new[]{144f,180f})foreach(float jitter in new[]{-20f,0f,20f})
        {
            float offset=GachaRules.CardOffset(width,28,jitter,SpinTiming.Case,stride);
            float selectedCentre=offset+28*stride+stride*.5f;
            Check(Math.Abs(selectedCentre-width*.5f)<=20.01f,"Authoritative reward lands below marker at any viewport width");
        }
        Check(SpinTiming.Reels==3.8f&&SpinTiming.Celebration>0,"Spin readiness occurs at reveal; celebration has separate duration");
        Console.WriteLine("Gacha: normalized rarity odds, empty pools, scaling bounds, viewport-independent case reveal and separate spin/effect timing.");
    }
    static void Main()
    {
        ManualCommandTests();
        LoungeTravelTests.Run(Check);
        Check(Math.Abs((ReelLayout.Lower(1)+ReelLayout.Upper(1))/2-.5f)<.0001,"Stopped centre reel symbol is centred at payline");
        Check(ReelLayout.Upper(1)-ReelLayout.Lower(1)<1,"Symbol fits entirely in one reel cell");
        Check(ReelLayout.Upper(0)<0&&ReelLayout.Lower(2)>1,"Neighbouring stopped symbols are outside the viewport");
        PlacementTests();
        LoungeTests();
        CrabRaceTests();
        InteractionChecks.Run(Check);
        SeatLayoutTests();
        SharedTableTests();
        IntegratedTableTests();
        GachaTests();
        Check(Games.Wheel.Distinct().Count() == 37 && Games.Wheel.Min() == 0 && Games.Wheel.Max() == 36, "European wheel must contain all 37 pockets exactly once");
        for (int choice = 0; choice < 6; choice++)
        {
            Check(Games.RouletteMultiplier(0, choice, 0) == 0, "Zero loses outside bets");
            Check(Enumerable.Range(0,37).Count(n => Games.RouletteMultiplier(n,choice,0) == 2) == 18, "Every outside bet has 18 winning numbers");
        }
        for (int straight = 0; straight < 37; straight++)
        {
            Check(Games.RouletteMultiplier(straight,6,straight) == 36, "Straight payout includes stake");
            Check(Enumerable.Range(0,37).Count(n => Games.RouletteMultiplier(n,6,straight) > 0) == 1, "Exact bet has only one winner");
        }
        Check(Games.Value(new[] {0,13,9}) == 12, "Multiple aces correctly downgrade");
        Check(Games.Value(new[] {0,5}) == 17, "Soft 17 value");
        var hand = Hand(new[] {0,9},new[] {8,7},Array.Empty<int>()); hand.Stand(); Check(hand.payout == 25, "Natural blackjack pays 3:2 plus stake");
        hand = Hand(new[] {0,9},new[] {13,22},Array.Empty<int>()); hand.Stand(); Check(hand.payout == 10, "Two natural blackjacks push");
        hand = Hand(new[] {8,7},new[] {0,9},Array.Empty<int>()); hand.Stand(); Check(hand.payout == 0, "Dealer blackjack wins against non-natural");
        hand = Hand(new[] {9,7},new[] {8,7},Array.Empty<int>()); hand.Stand(); Check(hand.payout == 20, "Ordinary win returns 2x stake");
        hand = Hand(new[] {9,6},new[] {0,5},Array.Empty<int>()); hand.Stand(); Check(hand.payout == 10 && hand.dealer.Count == 2, "Dealer stands on soft17, push refunds wager");
        hand = Hand(new[] {4,5},new[] {9,6},new[] {9}); hand.Double(); Check(hand.wager == 20 && hand.payout == 40 && !hand.active, "Double charges a doubled wager and pays against doubled wager");
        hand = Hand(new[] {9,7},new[] {8,7},new[] {9}); hand.Hit(); Check(hand.payout == 0 && !hand.active, "Player bust is final");
        hand = Hand(new[] {9,6},new[] {9,5},new[] {9}); hand.Stand(); Check(hand.payout == 20, "Dealer bust wins");
        hand = Hand(new[] {9,5,1},new[] {9,6},Array.Empty<int>()); hand.Double(); Check(hand.active && hand.wager == 10, "Cannot double after hitting");
        hand = Hand(new[] {9,7},new[] {8,7},Array.Empty<int>()); hand.Stand(); int payout = hand.payout; hand.Stand(); hand.Hit(); Check(hand.payout == payout, "Resolved hand cannot be played again");
        Check(Games.SlotMultiplier(5,5,5) == 60, "Moon jackpot return");
        Check(Games.SlotMultiplier(0,0,1) == 1 && Games.SlotMultiplier(0,1,2) == 0, "Pair refunds and no match loses");
        int[] weights = {35,25,18,12,7,3}; double expectedReturn = 0;
        for (int a=0;a<6;a++) for(int b=0;b<6;b++) for(int c=0;c<6;c++)
            expectedReturn += weights[a]*weights[b]*weights[c]/1000000.0 * Games.SlotMultiplier(a,b,c);
        Check(expectedReturn > 0.6 && expectedReturn < 1, "Slot paytable has bounded expected return below 100%");
        var rng = new Random(875);
        for(int choice=7;choice<=12;choice++)
        {
            Check(Games.RouletteMultiplier(0,choice,0)==0,"Zero loses dozens and columns");
            Check(Enumerable.Range(1,36).Count(n=>Games.RouletteMultiplier(n,choice,0)==3)==12,"Each dozen/column covers exactly twelve numbers");
        }
        int total;
        var table=new[] { new RouletteBet {choice=6,number=7,amount=20},new RouletteBet {choice=0,amount=10},new RouletteBet {choice=7,amount=10} };
        Check(RouletteBets.Validate(table,10,1000,1000,out total)&&total==40,"Multi-spot chips aggregate wager");
        Check(RouletteBets.Payout(table,7)==770,"Overlapping winning spots all pay");
        Check(!RouletteBets.Validate(table,10,39,1000,out total),"Aggregate table limit");
        Check(!RouletteBets.Validate(table,10,1000,39,out total),"Aggregate affordability");
        Check(!RouletteBets.Validate(new[]{table[0],table[0]},10,1000,1000,out total),"Duplicate betting spots rejected");
        Check(!RouletteBets.Validate(new[]{new RouletteBet{choice=6,number=99,amount=int.MaxValue}},10,1000000,uint.MaxValue,out total),"Malformed/overflow wager rejected");
        Check(!RouletteBets.Validate(Array.Empty<RouletteBet>(),10,1000,1000,out total),"Empty table rejected");
        var pockets=new int[37]; double minTime=99,maxTime=0;
        for(int seed=0;seed<3000;seed++)
        {
            var spin=new RouletteSpin(seed); var replay=new RouletteSpin(seed);
            var end=spin.frames.Last();
            Check(spin.number==replay.number && spin.frames.Count==replay.frames.Count,"Physics seed replays consistently");
            Check(spin.pocket==RouletteSpin.Pocket((double)end.angle-end.wheel)&&spin.number==Games.Wheel[spin.pocket],"Outcome derives from final physical pocket");
            Check(spin.frames.All(f=>f.radius>.76f && f.radius<=.941f),"Ball remains in bowl");
            Check(spin.duration<20 && Math.Abs(Math.Sin(RouletteSpin.Wrap((double)end.angle-end.wheel)*37))<.06,$"Ball settles inside pocket before deadline: seed {seed}, time {spin.duration}, radius {end.radius}, offset {RouletteSpin.Wrap((double)end.angle-end.wheel)*37}");
            pockets[spin.pocket]++;minTime=Math.Min(minTime,spin.duration);maxTime=Math.Max(maxTime,spin.duration);
        }
        Check(pockets.All(c=>c>0),"Physical launch conditions can reach every pocket");
        Console.WriteLine($"Roulette: 3,000 simulated spins + replay; settle {minTime:0.00}–{maxTime:0.00}s; pocket counts {string.Join(',',pockets)}.");
        for (int i = 0; i < 50000; i++)
        {
            hand = new BlackjackRound(rng,10);
            while(hand.active && Games.Value(hand.player) < 17) hand.Hit();
            hand.Stand();
            Check(!hand.active && new[] {0,10,20,25}.Contains(hand.payout), "Random blackjack settles with legal payout");
            Check(hand.deck.Count > 270, "Six-deck shoe cannot exhaust in a normal round");
            Check(Games.SlotSymbol(rng) >= 0 && Games.SlotSymbol(rng) < 6, "Weighted slot output stays in bounds");
        }
        Console.WriteLine($"PASS: {assertions:N0} assertions. 50,000 blackjack rounds. Exact slot theoretical return: {expectedReturn:P3}.");
    }
    static void PlacementTests()
    {
        // Reproduce landmark coordinates and facing from the user's rejected-placement log.
        var newt=new SeatPoint(-123.70f,-43.23f);var seers=new SeatPoint(-131.28f,-8.50f);var facing=new SeatPoint(.81f,.58f);
        var side=PlacementGeometry.RoomSide(newt,seers,facing);
        float dx=seers.x-newt.x,dz=seers.z-newt.z,lengthSquared=dx*dx+dz*dz;
        Check(Math.Abs(side.x*dx+side.z*dz)<.0001f,"Room-facing search is perpendicular to the landmark corridor");
        Check(side.x*facing.x+side.z*facing.z>0,"Search faces the open room side");
        foreach(var kind in new[]{TableKind.Blackjack,TableKind.Roulette})
        {
            var points=PlacementGeometry.Candidates(kind,newt,seers,facing).ToArray();
            Check(points.Length==80,"Search expands to eighty candidates for each table");
            foreach(var point in points)
            {
                float along=((point.x-newt.x)*dx+(point.z-newt.z)*dz)/lengthSquared;
                float across=(point.x-newt.x)*side.x+(point.z-newt.z)*side.z;
                Check(along>=.229f&&along<=.834f,"Every candidate stays between Newt and the Seer pedestals");
                Check(across>=3.999f&&across<=18.001f,"Every candidate stays on the open-room side");
            }
        }
        var bj=PlacementGeometry.Candidates(TableKind.Blackjack,newt,seers,facing).First();
        var roulette=PlacementGeometry.Candidates(TableKind.Roulette,newt,seers,facing).First();
        Check(Math.Sqrt(Math.Pow(bj.x-roulette.x,2)+Math.Pow(bj.z-roulette.z,2))>10,"Preferred table positions have independent footprints");
        Check(PlacementGeometry.TryPlatformHeight(new[]{-25.4f,-25.1f,-24.8f,-24.55f,-25.3f},out var level)&&level>-24.55f,"Uneven Bazaar floor rejected by old flatness rule supports a level deck");
        Check(!PlacementGeometry.TryPlatformHeight(new[]{-25.4f,-25.1f,-24.8f,-21f,-25.3f},out _),"A cliff/raised platform cannot pass the floor support check");
        Check(!PlacementGeometry.TryPlatformHeight(new[]{float.NaN,-25f,-25f,-25f,-25f},out _),"Missing/invalid floor samples fail safely");
        Console.WriteLine($"Placement regression: candidates between log landmarks; preferred BJ {bj.x:0.00}, {bj.z:0.00}; roulette {roulette.x:0.00}, {roulette.z:0.00}; uneven floor supported.");
    }
    static void LoungeTests()
    {
        Check(!BazaarEntranceLayout.Contains(-113.86f,-21.37f),"Logged aisle placement cannot be accepted as a casino entrance");
        Check(!BazaarEntranceLayout.Contains(-133.89f,-19.99f),"Previous doorway in front of the corner is no longer an allowed spawn");
        var entrances=BazaarEntranceLayout.Candidates().ToArray();
        Check(entrances.Length==15&&entrances[0].x==BazaarEntranceLayout.AnchorX&&entrances[0].z==BazaarEntranceLayout.AnchorZ,"Entrance tries the far-wall anchor first");
        foreach(var entrance in entrances)
        {
            Check(BazaarEntranceLayout.Contains(entrance.x,entrance.z),"Every fallback remains inside the narrow wall alcove");
            Check(Math.Pow(entrance.x+133.89f,2)+Math.Pow(entrance.z+19.99f,2)>13*13,"Every candidate stays deeper in the corner than the previous door");
            Check(Math.Pow(entrance.x+113.86f,2)+Math.Pow(entrance.z+21.37f,2)>18*18,"No candidate can drift back into the logged central aisle");
        }
        foreach(var kind in new[]{TableKind.Blackjack,TableKind.Roulette})
        {
            var centre=LoungeLayout.Table(kind);
            float width=kind==TableKind.Blackjack?7.8f:9.6f;
            Check(Math.Abs(centre.x)+3.9f<LoungeLayout.Width/2-2,"Rotated deck leaves wall clearance in the lounge");
            foreach(var chair in Enumerable.Range(0,8).Select(i=>SeatLayout.Chair(kind,i)))
                Check(LoungeLayout.Contains(centre.x+chair.z,0,centre.z-chair.x),"Every rotated seat stays inside the lounge bounds");
            Check(centre.z-width/2>LoungeLayout.ArrivalZ+5,"Rotated tables leave a clear entrance hall");
        }
        var left=LoungeLayout.Table(TableKind.Blackjack);var right=LoungeLayout.Table(TableKind.Roulette);
        Check(right.x-3.9f-LoungeLayout.TrackWidth/2>4&&-LoungeLayout.TrackWidth/2-(left.x+3.9f)>4,"Full-size arena leaves broad viewing aisles beside the table areas");
        Check(LoungeLayout.Contains(0,.08f,LoungeLayout.ArrivalZ),"Door arrival stands safely on the lounge floor");
        Check(!LoungeLayout.Contains(0,-1,0)&&!LoungeLayout.Contains(LoungeLayout.Width/2+1,1,0),"Room containment excludes players under the floor or outside walls");
        var snapshot=new TableSnapshot{lounge=true,roomX=-96,roomY=59,roomZ=-19,roomYaw=257,doorX=-125,doorY=-26,doorZ=-15,doorYaw=257};
        var json=System.Text.Json.JsonSerializer.Serialize(snapshot,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        var copy=System.Text.Json.JsonSerializer.Deserialize<TableSnapshot>(json,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        Check(copy.lounge&&copy.roomY==59&&copy.doorZ==-15,"Late-join snapshots preserve both the room and its independent Bazaar entrance");
        var slotSnapshot=new TableSnapshot{machine=2,slotSpin=124,slotReels=new[]{5,5,5},slotNet=590,elapsed=4.5f};
        json=System.Text.Json.JsonSerializer.Serialize(slotSnapshot,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        copy=System.Text.Json.JsonSerializer.Deserialize<TableSnapshot>(json,new System.Text.Json.JsonSerializerOptions{IncludeFields=true});
        Check(copy.machine==2&&copy.slotSpin==124&&copy.slotNet==590&&copy.slotReels.SequenceEqual(new[]{5,5,5})&&copy.elapsed==4.5f,"Machine replay keeps authoritative result, spin identity, payout and elapsed time for spectators");
        foreach(float z in new[]{-3f,3f,9f})
        {
            Check(27.5f+.9f<LoungeLayout.Width/2&&Math.Abs(z)+1.9f<LoungeLayout.Depth/2,"Rotated slot cabinet and lever fit along the side wall");
            Check(27.5f-.95f-.275f>Math.Abs(left.x)+3.9f+3,"Slot bank leaves a walkable gap beside the tables");
        }
        Check(-27.2f+1.5f<LoungeLayout.Table(TableKind.Blackjack).z-4.8f-5,"Broker counter stays in the entrance wing away from the tables");
        Check(LoungeLayout.RaceZ+LoungeLayout.TrackLength/2+4<LoungeLayout.Depth/2,"Finish gate leaves clearance from the rear wall");
        Check(LoungeLayout.Height>15.2f&&LoungeLayout.Contains(0,15.2f,LoungeLayout.RaceZ),"Arena overview camera stays below the casino ceiling");
    }
    static void SeatLayoutTests()
    {
        foreach(var kind in new[]{TableKind.Blackjack,TableKind.Roulette})
        {
            var chairs=Enumerable.Range(0,8).Select(i=>SeatLayout.Chair(kind,i)).ToArray();
            Check(chairs.Select(p=>(p.x,p.z)).Distinct().Count()==8,"Each player is assigned a unique physical chair");
            foreach(var p in chairs)
                Check(kind==TableKind.Blackjack?p.x*p.x/(2.8f*2.8f)+p.z*p.z/(1.7f*1.7f)>1:Math.Abs(p.z)>2,"Chairs stay outside the solid table top");
            for(int i=0;i<8;i++)for(int j=i+1;j<8;j++)
                Check(Math.Pow(chairs[i].x-chairs[j].x,2)+Math.Pow(chairs[i].z-chairs[j].z,2)>1.0,"Adjacent player chairs have at least one metre of clearance");
        }
        bool rejected=false;try{SeatLayout.Chair(TableKind.Blackjack,8);}catch(ArgumentOutOfRangeException){rejected=true;}
        Check(rejected,"Untrusted out-of-range seat cannot index a physical chair");
    }
    static SharedBlackjack SplitHand(int[] cards,int[] dealer,int[] draws)
    {
        var table=new SharedBlackjack();table.Join(1,"A");table.Bet(1,10,0);table.Start(new Random(5),16);
        var seat=table.Find(1);seat.cards.Clear();seat.cards.AddRange(cards);seat.secondCards.Clear();
        seat.wager=seat.handWager=10;seat.secondWager=0;seat.activeHand=0;seat.firstDone=seat.secondDone=seat.done=seat.splitAces=false;
        table.dealer.Clear();table.dealer.AddRange(dealer);table.deck.Clear();table.deck.AddRange(draws.Reverse());
        table.phase=TablePhase.Playing;table.turn=1;table.deadline=41;return table;
    }
    static void IntegratedTableTests()
    {
        for(int choice=0;choice<=12;choice++)for(int number=0;number<(choice==6?37:1);number++)
        {
            var p=RouletteLayout.Centre(choice,number);var size=RouletteLayout.Size(choice,number);
            Check(RouletteLayout.Hit(p.x,p.z,out int c,out int n)&&c==choice&&n==number,"Every visible roulette cell targets its own bet");
            foreach(float dx in new[]{-.49f,.49f})foreach(float dz in new[]{-.49f,.49f})
                Check(RouletteLayout.Hit(p.x+dx*size.x,p.z+dz*size.z,out c,out n)&&c==choice&&n==number,"The full visible betting rectangle is clickable");
        }
        Check(!RouletteLayout.Hit(-2.4f,0,out _,out _),"Clicking the wheel cannot place a felt bet");
        Check(!RouletteLayout.Hit(float.NaN,0,out _,out _)&&!RouletteLayout.Hit(float.PositiveInfinity,0,out _,out _),"Invalid projections cannot produce a bet");
        var roulette=new SharedRoulette();roulette.Join(1,"A");roulette.Join(2,"Spectator");
        roulette.Bet(1,new[]{new RouletteBet{choice=0,amount=10}},10,100);
        Check(roulette.deadline==115,"First committed chip starts a visible 15-second countdown");
        roulette.Bet(1,new[]{new RouletteBet{choice=0,amount=20}},20,105);
        Check(roulette.deadline==115,"Adding chips does not indefinitely extend betting");
        roulette.Tick(114.9,new Random(1));Check(roulette.phase==TablePhase.Betting,"Wheel waits for countdown to close");
        roulette.Tick(115,new Random(1));Check(roulette.phase==TablePhase.Playing&&roulette.spin!=null,"Wheel launches automatically even with an unfunded spectator");
        roulette.Tick(roulette.deadline,new Random(1));Check(roulette.phase==TablePhase.Results&&roulette.number>=0,"Automatic round reaches a physical result");
        var bj=SplitHand(new[]{7,20},new[]{9,6},new[]{1,2,9,8});
        Check(!bj.Split(99,17)&&bj.CanSplit(1),"Split belongs to the current matching-pair owner");
        Check(bj.Split(1,17),"A matching pair becomes two hands");var seat=bj.Find(1);
        Check(seat.wager==20&&seat.handWager==10&&seat.secondWager==10&&seat.cards.Count==2&&seat.secondCards.Count==2,"Split reserves exactly one additional original stake and deals both hands");
        Check(!bj.CanSplit(1),"No recursive split beyond the documented two-hand limit");
        var snapshot=TableSnapshots.Blackjack(bj);
        Check(snapshot.dealer[1]==-1&&snapshot.seats[0].secondCards.SequenceEqual(seat.secondCards),"Split snapshots show both player hands while hiding dealer hole card");
        Check(bj.Double(1,18)&&seat.wager==30&&seat.handWager==20&&seat.activeHand==1&&bj.turn==1,"Double charges only the active split hand and advances to the same player's second hand");
        Check(bj.Double(1,19)&&seat.wager==40&&seat.secondWager==20&&seat.payout==80,"Double after split settles each hand against its own stake");
        Check(bj.TakePayments().Count()==1&&!bj.TakePayments().Any(),"Two split hands produce one exactly-once wallet payment");
        bj.Reset();Check(seat.secondCards.Count==0&&seat.secondWager==0&&seat.activeHand==0,"New round clears split state");
        bj=SplitHand(new[]{0,13},new[]{9,6},new[]{9,22});Check(bj.Split(1,17),"Aces may split");seat=bj.Find(1);
        Check(bj.phase==TablePhase.Results&&seat.payout==40&&!bj.CanDouble(1)&&seat.cards.Count==2&&seat.secondCards.Count==2,"Split aces take one card each; two 21s pay ordinary 1:1, not natural 3:2");
        bj=SplitHand(new[]{7,20},new[]{0,9},new[]{2,3});bj.Split(1,17);bj.Stand(1,18);bj.Stand(1,19);
        Check(bj.Find(1).payout==0,"Dealer natural beats split non-natural hands");
        bj=SplitHand(new[]{7,20},new[]{9,6},new[]{2,3});bj.Split(1,17);
        bj.Tick(bj.deadline,new Random(1));Check(bj.turn==1&&bj.Find(1).activeHand==1&&bj.phase==TablePhase.Playing,"AFK timeout stands the first split hand and gives the second its own timer");
        bj.Tick(bj.deadline,new Random(1));Check(bj.phase==TablePhase.Results,"Second split timeout settles without hanging the round");
        bj=SplitHand(new[]{7,20},new[]{9,6},new[]{2,3});bj.Split(1,17);
        Check(bj.Leave(1,18)==0&&bj.phase==TablePhase.Results,"Leaving stands both split hands without refunding committed stakes");
        bj=SplitHand(new[]{7,21},new[]{9,6},Array.Empty<int>());Check(!bj.Split(1,17),"Different ranks cannot split");
        int splits=0;var rng=new Random(15487);
        for(int run=0;run<3000;run++)
        {
            var shared=new SharedBlackjack();
            for(uint id=1;id<=8;id++){shared.Join(id,"P"+id);shared.Bet(id,10,0);}
            shared.Start(rng,16);double now=17;int actions=0;int[] reserved=Enumerable.Repeat(10,8).ToArray();
            while(shared.phase==TablePhase.Playing)
            {
                uint id=shared.turn;var active=shared.Find(id);int extra=shared.ActionStake(id);
                if(shared.CanSplit(id)){Check(shared.Split(id,now),"Random eligible split accepted");reserved[id-1]+=extra;splits++;}
                else if(shared.CanDouble(id)&&rng.Next(4)==0){Check(shared.Double(id,now),"Random two-card double accepted");reserved[id-1]+=extra;}
                else if(Games.Value(active.ActiveCards)<17)shared.Hit(id,now);
                else shared.Stand(id,now);
                Check(++actions<200,"Split rounds finish within a bounded number of actions");now++;
            }
            foreach(var participant in shared.seats)
            {
                Check(participant.wager==reserved[participant.id-1],"Reserved bankroll equals all base/split/double debits");
                Check(participant.payout>=0&&participant.payout<=3*participant.wager,"Split payouts cannot mint unbounded gold");
                Check(participant.secondCards.Count==0||participant.wager==participant.handWager+participant.secondWager,"Aggregate split stake equals the two independent hand stakes");
            }
            Check(shared.TakePayments().Count()==8&&!shared.TakePayments().Any(),"Mixed split rounds credit all eight wallets exactly once");
        }
        Check(splits>100,"Random integration exercises many split hands");
        Console.WriteLine($"Split bankroll regression: 3,000 eight-player rounds, {splits:N0} split hands, independent debit accounting and exactly-once settlement.");
        Console.WriteLine("Integrated tables: 49 betting regions including corners, automatic roulette countdown, two-hand split, split aces, double after split, dealer privacy and AFK/exit settlement.");
    }
    static void SharedTableTests()
    {
        var random=new Random(651);int settled=0;
        for(int run=0;run<2000;run++)
        {
            var table=new SharedBlackjack();
            for(uint i=1;i<=8;i++){Check(table.Join(i,"Player "+i),"Eight blackjack seats can join");table.Bet(i,10+i.GetHashCode(),0);}
            Check(!table.Join(99,"Overflow"),"Ninth seat rejected");
            table.Start(random,16);
            Check(table.dealer.Count==2,"One shared dealer deals two cards");
            Check(table.seats.All(s=>s.cards.Count==2),"Every funded seat has its own two cards");
            Check(table.deck.Count==294,"Eight player hands and one dealer share the same six-deck shoe");
            var publicState=TableSnapshots.Blackjack(table);
            if(table.phase==TablePhase.Playing)Check(publicState.dealer.Length==2&&publicState.dealer[1]==-1,"Dealer hole card never enters an active public network snapshot");
            double now=17;
            while(table.phase==TablePhase.Playing)
            {
                uint actor=table.turn;
                Check(!table.Hit(999,now)&&!table.Stand(999,now),"A spectator cannot act on someone's turn");
                var seat=table.Find(actor);
                if(seat.cards.Count==2&&run%3==0)Check(table.Double(actor,now),"Current two-card hand can double");
                else if(Games.Value(seat.cards)<17)Check(table.Hit(actor,now),"Hit accepted only for current player");
                else Check(table.Stand(actor,now),"Stand advances shared turn");
                now++;
            }
            Check(table.phase==TablePhase.Results&&table.turn==0,"Shared round reaches results without a dangling turn");
            Check(table.seats.All(s=>s.payout==0||s.payout==s.wager||s.payout==2*s.wager||s.payout==s.wager+s.wager*3/2),"Every seat has a legal payout against the shared dealer");
            Check(table.TakePayments().Count()==8 && !table.TakePayments().Any(),"Each seat is credited exactly once");
            table.Tick(now+100,random);
            Check(table.phase==TablePhase.Betting&&table.seats.All(s=>s.wager==0&&s.cards.Count==0),"Next shared round requires fresh wagers");settled++;
        }
        var bj=new SharedBlackjack();bj.Join(1,"A");bj.Join(2,"B");bj.Bet(1,10,0);bj.Bet(2,20,0);
        Check(bj.Leave(2,1)==20&&bj.Find(2)==null,"Leaving before deal refunds reserved stake");
        bj.Start(new Random(22),15);bj.Finish(20);Check(bj.phase==TablePhase.Results,"Closing Bazaar settles an active blackjack table");
        bj=new SharedBlackjack();bj.Join(1,"A");bj.Bet(1,10,0);bj.Tick(16,new Random(875));
        while(bj.phase==TablePhase.Playing)bj.Tick(bj.deadline+1,new Random(875));
        Check(bj.phase==TablePhase.Results,"Timeout automatically stands AFK hands");
        var roulette=new SharedRoulette();roulette.Join(1,"A");roulette.Join(2,"B");
        roulette.Bet(1,new[]{new RouletteBet{choice=0,amount=10}},10,0);
        roulette.Bet(2,new[]{new RouletteBet{choice=1,amount=20}},20,0);
        roulette.Start(12345,16);Check(roulette.number==-1&&roulette.phase==TablePhase.Playing,"Shared wheel does not reveal result before ball settles");
        Check(roulette.Leave(1)==0&&roulette.Find(1)!=null,"Leaving a spinning wheel does not refund or remove a committed bet");
        roulette.Tick(16+roulette.spin.duration+1,new Random(1));
        Check(roulette.number==roulette.spin.number,"All roulette seats settle on the same physical pocket");
        Check(roulette.Find(1).payout==10*Games.RouletteMultiplier(roulette.number,0,0)&&roulette.Find(2).payout==20*Games.RouletteMultiplier(roulette.number,1,0),"Independent wallets receive payouts for their own bets");
        Check(roulette.TakePayments().Count()==2&&!roulette.TakePayments().Any(),"Shared roulette payouts cannot be repeated");
        roulette.Reset();roulette.Bet(1,new[]{new RouletteBet{choice=6,number=0,amount=10}},10,0);
        Check(roulette.Leave(1)==10,"Unspun roulette chips refund on leave");
        var full=new SharedRoulette();
        for(uint id=1;id<=8;id++)
        {
            full.Join(id,new string('界',24));
            var bets=Enumerable.Range(0,37).Select(n=>new RouletteBet{choice=6,number=n,amount=10}).Concat(Enumerable.Range(0,13).Where(c=>c!=6).Select(c=>new RouletteBet{choice=c,amount=10})).ToArray();
            full.Bet(id,bets,490,0);
        }
        var fullSnapshot=TableSnapshots.Roulette(full);
        foreach(var seat in fullSnapshot.seats){seat.seated=true;seat.returnX=-135.239f;seat.returnY=-25.47001f;seat.returnZ=-45.92221f;}
        var json=System.Text.Json.JsonSerializer.Serialize(fullSnapshot,new System.Text.Json.JsonSerializerOptions{IncludeFields=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
        Check(System.Text.Encoding.UTF8.GetByteCount(json)<30000,"Maximum eight-player 49-spot table snapshot fits the UNet message limit");
        Console.WriteLine($"Shared tables: {settled:N0} eight-player blackjack rounds, turn ownership, AFK timeout, refunds, common roulette result, exactly-once payouts. Reel alignment checked.");
    }
}
