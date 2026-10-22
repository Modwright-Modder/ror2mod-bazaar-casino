using System;
using BazaarCasino;

static class CustomWagerChecks
{
    internal static void Run(Action<bool,string> check)
    {
        check(WagerRules.Valid(123,50,1000,500,false),"Arbitrary whole-number custom wager is valid");
        check(!WagerRules.Valid(49,50,1000,500,false),"Custom wager cannot bypass scaled minimum");
        check(WagerRules.Valid(7,50,1000,7,true),"All In can use a positive bankroll below minimum");
        check(!WagerRules.Valid(7,50,1000,8,true),"Forged All In flag cannot bypass minimum with a partial bankroll");
        check(!WagerRules.Valid(1001,50,1000,1001,true),"All In respects maximum table risk");
        check(!WagerRules.Valid(0,50,1000,0,true),"Zero-wallet All In cannot start a free game");
        check(WagerRules.Valid(0,50,1000,100,false,true),"Refund-all remains possible");
        check(!WagerRules.Valid(-1,50,1000,uint.MaxValue,true,true),"Negative wire amount is rejected");
        check(!WagerRules.Valid(1000,50,1000,999,false),"Gold affordability is authoritative");
        check(WagerRules.Valid(507,50,1000,507,true),"All In includes own refundable stake");
        foreach(string value in new[]{"0","-1","+50","1.5","1,000","1e3"," 50","50 ","99999999999",""})
            check(!WagerRules.TryAmount(value,out _),"Invalid custom numeric input: "+value);
        check(WagerRules.TryAmount("123",out int parsed)&&parsed==123,"Plain positive custom amount parses");
        for(int wallet=0;wallet<500;wallet++)for(int stake=0;stake<600;stake++)
        {
            bool all=WagerRules.Valid(stake,50,400,(ulong)wallet,true);
            check(all==(wallet>0&&wallet<=400&&stake==wallet),"Exact All In predicate across low-wallet and limit boundaries");
        }
        var small=new[]{new RouletteBet{choice=0,number=0,amount=7}};
        check(!RouletteBets.Validate(small,50,1000,7,out _),"Ordinary roulette chips require minimum");
        check(RouletteBets.Validate(small,50,1000,7,out int total,true)&&total==7,"Roulette All In below minimum works");
        check(!RouletteBets.Validate(small,50,1000,8,out _,true),"Roulette rejects partial-wallet All In");
        check(!RouletteBets.Validate(new[]{new RouletteBet{choice=0,amount=-1}},50,1000,7,out _,true),"Roulette negative chip rejected");
        var split=new[]{new RouletteBet{choice=0,amount=3},new RouletteBet{choice=1,amount=4}};
        check(RouletteBets.Validate(split,50,1000,7,out total,true)&&total==7,"All In validates complete aggregate rather than trusting a client flag");
        check(!RouletteBets.Validate(new[]{small[0],small[0]},50,1000,14,out _,true),"All In still rejects duplicate roulette spots");
        check(!RouletteBets.Validate(new[]{new RouletteBet{choice=13,amount=7}},50,1000,7,out _,true),"All In still rejects invalid roulette choice");
        var catalog=new TranslationCatalog();
        check(!catalog.Add("Value {99999999999999999999999}","Nilai {99999999999999999999999}"),"Oversized placeholder index rejected without aborting the pack");
        check(catalog.Add("Gold: {0}","Emas: {0}"),"Translation template accepted");
        check(catalog.Translate("Gold: 1,234")=="Emas: 1,234","Wallet formatting preserved during translation");
        check(catalog.Add("Sold 1 {0} for {1} gold.","Terjual {0}: {1} emas."),"Multiple placeholders supported");
        check(catalog.Translate("Sold 1 <color=#FFD700>Test</color> for 53 gold.")=="Terjual <color=#FFD700>Test</color>: 53 emas.","Native item name, rich text, numbers remain intact");
        check(!catalog.Add("Gold: {0}","Emas: {1}"),"Invalid replacement placeholders rejected");
        check(catalog.Translate("Gold: 1,234")=="Emas: 1,234","Invalid translation does not replace working entry");
        check(catalog.Translate("Untranslated error")=="Untranslated error","Missing translation falls back to original English");
        check(catalog.Add("Gold: {0}","Uang: {0}")&&catalog.Translate("Gold: 7")=="Uang: 7","Regional entry overrides base template");
        check(catalog.Add("Gold: {0}","Gold: {0}")&&catalog.Translate("Gold: 7")=="Gold: 7","English override restores fallback");
        check(catalog.Add("Outcome: {0}","Hasil: {0}")&&catalog.Add("You win","Menang"),"Nested outcome strings registered");
        check(catalog.Translate("Outcome: You win")=="Hasil: Menang","Captured result is translated without changing network message");
        check(catalog.Add("Same {0} / {0}","Sama {0}"),"Repeated source placeholder accepted");
        check(catalog.Translate("Same 2 / 3")=="Same 2 / 3"&&catalog.Translate("Same 2 / 2")=="Sama 2","Repeated placeholder uses same captured value");
        string directory=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory,"../../../../../language"));
        using(var json=System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(directory,"en.json"))))
        {
            var template=new TranslationCatalog();
            foreach(var entry in json.RootElement.GetProperty("entries").EnumerateArray())
                check(template.Add(entry.GetProperty("key").GetString(),entry.GetProperty("text").GetString()),"Pack template validates every key and placeholder");
        }
        Console.WriteLine("Custom wagers/All In: budget, minimum, reservation, maximum, malformed input and roulette validation; localization: fallback, rich text, nested messages and overrides.");
    }
}
