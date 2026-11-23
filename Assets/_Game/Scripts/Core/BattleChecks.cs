using System;
using System.Collections.Generic;
using System.Linq;

namespace ShenYi.Prototype
{
    // Deterministic checks executable in Unity and in a standalone C# runner.
    public static class BattleChecks
    {
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("RULE CHECK FAILED: " + message); }
        public static string Run(PrototypeData data)
        {
            data.Validate();
            var checks = new List<string>();
            var battle = new BattleModel(data, 42);
            Require(battle.Players.All(p => p.Deck.Count == 25 && p.Hand.Count == 5), "30-card decks and 5-card opening");
            Require(battle.Players.All(p => p.Hand.Count(c => c.IsEgg) == 3), "teaching hand has 3 eggs");
            Require(!battle.PlayEgg(1, battle.Players[1].Hand[0].Id, 0), "inactive player cannot play");
            var eggCard = battle.Players[0].Hand.First(c => c.IsEgg);
            Require(!battle.PlayEgg(0, eggCard.Id, 4), "locked slot rejected");
            Require(battle.PlayEgg(0, eggCard.Id, 0), "egg placement");
            Require(!battle.Hatch(0, 0, 0), "unready hatch rejected");
            battle.EndTurn(0); battle.EndTurn(1);
            Require(battle.Players[0].Eggs[0].Remaining == 0, "only owner's next turn advances egg");
            Require(battle.Hatch(0, 0, 0), "ready egg hatches");
            var first = battle.Players[0].Monsters[0];
            Require(first.Tier == 1 && first.Attacks == 1, "new monster can attack after opening turn");
            checks.Add("PASS: opening hand, legal commands, slot locking, hatch timing, summon attacks");

            // Establish three alternating monsters through the same hatch path, then inspect pair output.
            var breeding = new BattleModel(data, 123); var d = breeding.Players[0];
            for (int i = 0; i < 3; i++)
            {
                var c = d.Hand.First(x => x.IsEgg); breeding.PlayEgg(0, c.Id, i); d.Eggs[i].Remaining = 0;
                breeding.Hatch(0, i, i); d.Monsters[i].Male = i != 1; d.Monsters[i].Definition = data.Monster("mitten");
                d.Monsters[i].Spirit = 0;
            }
            Require(!breeding.Attack(0, 0, -1), "first player cannot attack turn one");
            breeding.EndTurn(0);
            Require(d.Eggs.Count(x => x != null) == 2, "male-female-male creates two eggs");
            Require(d.Eggs.Where(x => x != null).All(x => x.Tier == 2 && x.Generation == 1 && x.Weights.Sum() == 100), "tier and generation independent");
            Require(d.Eggs[0].Pool.Length == Math.Max(2, d.Monsters[0].LineagePool.Length - 1), "same-generation parents remove one non-core candidate");
            checks.Add("PASS: first-turn attack restriction, adjacent pairs, offspring tier and normalized pool");

            var cap = new BattleModel(data, 5); var capD = cap.Players[0];
            for (int i = 0; i < 2; i++)
            {
                var c = capD.Hand.First(x => x.IsEgg); cap.PlayEgg(0, c.Id, i); capD.Eggs[i].Remaining = 0; cap.Hatch(0, i, i);
                capD.Monsters[i].Tier = 3; capD.Monsters[i].Male = i == 0; capD.Monsters[i].Definition = data.Monster("mitten");
            }
            cap.EndTurn(0); Require(capD.Eggs.First(x => x != null).Tier == 3, "ordinary breeding capped at tier three");
            checks.Add("PASS: tier-three cap");

            var a = new BattleModel(data, 999); var b = new BattleModel(data, 999);
            for (int step = 0; step < 12000 && !a.Over; step++) { Require(a.StepAI(a.Active), "AI must perform legal action"); Require(b.StepAI(b.Active), "replay action"); }
            Require(a.Over && b.Over && a.Winner == b.Winner && a.History.SequenceEqual(b.History), "seeded deterministic replay and completion");
            checks.Add("PASS: deterministic full battle replay");
            int totalActions = 0, breedEvents = 0, quests = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                var game = new BattleModel(data, seed); int actions = 0;
                while (!game.Over && actions++ < 14000)
                {
                    Require(game.StepAI(game.Active), "AI stalled seed " + seed);
                    foreach (var side in game.Players)
                    {
                        Require(side.Hand.Count <= data.maxHand, "hand cap");
                        Require(side.Monsters.All(x => x == null || x.Health > 0), "dead units cleaned up");
                        Require(side.Eggs.All(x => x == null || x.Remaining >= 0 && x.Remaining <= x.Initial && x.Tier >= 1 && x.Tier <= 3
                            && x.Pool.Length == x.Weights.Length && x.Weights.Sum() == 100), "egg invariant");
                    }
                }
                Require(game.Over, "battle did not terminate seed " + seed);
                Require(!game.EndTurn(game.Active), "finished battle is immutable to commands");
                totalActions += actions;
                breedEvents += game.History.Count(x => x.Contains("→")); quests += game.Players.Count(x => x.QuestClaimed);
            }
            checks.Add("PASS: 40 complete AI battles, " + totalActions + " actions, " + breedEvents + " breeding/sex events, " + quests + " boss quests");
            Require(quests > 0 && breedEvents > 0, "full simulations exercise breeding and bosses");
            return string.Join("\n", checks.ToArray());
        }
    }
}
