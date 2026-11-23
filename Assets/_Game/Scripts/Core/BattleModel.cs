using System;
using System.Collections.Generic;
using System.Linq;

namespace ShenYi.Prototype
{
    [Serializable] public sealed class PrototypeData
    {
        public int startingHealth = 60, slots = 5, earlySlots = 3;
        public int expandOnPersonalTurn = 4, questGoal = 5, maxHand = 10;
        public MonsterDefinition[] monsters;
        public EggDefinition[] eggs;
        public MonsterDefinition Monster(string id) { return monsters.First(x => x.id == id); }
        public EggDefinition Egg(string id) { return eggs.First(x => x.id == id); }
        public void Validate()
        {
            if (slots != 5 || earlySlots < 2 || earlySlots > slots || startingHealth <= 0 || questGoal <= 0)
                throw new InvalidOperationException("原型需要 5 格棋盘、有效生命和任务目标。");
            if (monsters == null || eggs == null || monsters.Select(x => x.id).Distinct().Count() != monsters.Length
                || eggs.Select(x => x.id).Distinct().Count() != eggs.Length)
                throw new InvalidOperationException("卡牌定义缺失或 ID 重复。");
            foreach (var m in monsters)
                if (m.attack.Length != 3 || m.health.Length != 3 || m.elements.Length < 1 || m.elements.Length > 2)
                    throw new InvalidOperationException("怪兽三阶数据或属性不合法：" + m.id);
            foreach (var e in eggs)
            {
                if (e.pool.Length < 2 || e.pool.Length > 5 || e.pool.Length != e.weights.Length
                    || e.weights.Sum() != 100 || e.weights.Any(x => x <= 0)
                    || e.weights.Last() != 5 || !e.pool.Contains(e.core) || e.turns < 0)
                    throw new InvalidOperationException("蛋池概率不合法：" + e.id);
                foreach (var id in e.pool)
                    if (Monster(id).race != Monster(e.core).race || Monster(id).elements.Any(x => !e.elements.Contains(x)))
                        throw new InvalidOperationException("蛋池种族或属性不符：" + e.id);
            }
        }
    }
    [Serializable] public sealed class MonsterDefinition
    {
        public string id, name, race, ability, description;
        public string[] elements;
        public int[] attack, health;
    }
    [Serializable] public sealed class EggDefinition
    {
        public string id, name, core;
        public string[] pool, elements;
        public int[] weights;
        public int turns;
    }
    public sealed class Card
    {
        public int Id;
        public string Key;
        public bool IsEgg;
    }
    public sealed class EggUnit
    {
        public int Id, Remaining, Initial, Tier = 1, Generation, Spirit;
        public bool BlockUsed;
        public string Name, Core;
        public string[] Pool, Elements;
        public int[] Weights;
    }
    public sealed class MonsterUnit
    {
        public int Id, Tier, Generation, Attack, Health, MaxHealth, Spirit, Attacks;
        public bool Male, SkillUsed, IsBoss;
        public MonsterDefinition Definition;
        public string[] Elements, LineagePool;
        public int[] LineageWeights;
    }
    public sealed class Duelist
    {
        public string Name;
        public int Health, PersonalTurn, Hatched, Fatigue;
        public bool QuestClaimed;
        public readonly List<Card> Deck = new List<Card>(), Hand = new List<Card>();
        public EggUnit[] Eggs;
        public MonsterUnit[] Monsters;
        public MonsterUnit LastDead;
    }

    // All changes to the battle happen here; UI and AI use the same validated commands.
    public sealed class BattleModel
    {
        public readonly PrototypeData Data;
        public readonly Duelist[] Players = new Duelist[2];
        public readonly List<string> History = new List<string>();
        public readonly int Seed;
        public int Active { get; private set; }
        public int Turn { get; private set; }
        public bool Over { get; private set; }
        public int Winner { get; private set; } = -1;
        public string Error { get; private set; }
        readonly Random random;
        int nextId;
        public BattleModel(PrototypeData data, int seed)
        {
            Data = data;
            Data.Validate();
            Seed = seed;
            random = new Random(seed);
            for (int p = 0; p < 2; p++)
            {
                Players[p] = new Duelist { Name = p == 0 ? "你 · 童趣蕴灵" : "司欧斯 · 怒霆凶灵", Health = data.startingHealth,
                    Eggs = new EggUnit[data.slots], Monsters = new MonsterUnit[data.slots] };
                var eggKeys = p == 0 ? new[] { "water_croc", "water_bear", "fire_triangle", "fire_mitten", "electric" }
                    : new[] { "fire_mitten", "fire_triangle", "electric", "water_croc", "water_bear" };
                foreach (var key in eggKeys) for (int n = 0; n < 3; n++)
                    Players[p].Deck.Add(new Card { Id = ++nextId, Key = key, IsEgg = true });
                foreach (var key in ToolKeys) for (int n = 0; n < 3; n++)
                    Players[p].Deck.Add(new Card { Id = ++nextId, Key = key });
                Shuffle(Players[p].Deck);
                // Teaching preset: a playable mix of three eggs and two tools, not a hidden mulligan.
                for (int n = 0; n < 3; n++) TakeOpeningCard(p, true);
                for (int n = 0; n < 2; n++) TakeOpeningCard(p, false);
            }
            Log("教学对局：你先手，起手固定为 3 枚蛋 + 2 张道具。双方生命 " + data.startingHealth + "。");
            BeginTurn(0);
        }
        public static readonly string[] ToolKeys = { "watergun", "craft", "draw", "fold", "revive" };
        public static string ToolName(string key)
        {
            switch (key) { case "watergun": return "玩具水枪"; case "craft": return "特殊工艺";
                case "draw": return "绝望抽卡"; case "fold": return "折纸工艺"; case "revive": return "复活币"; default: return key; }
        }
        public static string ToolDescription(string key)
        {
            switch (key) { case "watergun": return "选择己方一枚蛋：孵化 -1；已就绪则蕴灵 +2。";
                case "craft": return "选择己方一只怪兽：攻击 +3，当前与最大生命 +3。";
                case "draw": return "立即抽 1 张牌。（原型简化效果）";
                case "fold": return "选择己方一枚蛋：立即就绪，下次孵化的怪兽生命为 1。";
                case "revive": return "将上一只阵亡怪兽复活至选择的空位，恢复基础生命，攻击次数为 0。"; default: return ""; }
        }
        readonly HashSet<int> fragileEggs = new HashSet<int>();
        void Shuffle<T>(IList<T> list) { for (int n = list.Count - 1; n > 0; n--) { int j = random.Next(n + 1); T t = list[n]; list[n] = list[j]; list[j] = t; } }
        void TakeOpeningCard(int p, bool egg)
        {
            int i = Players[p].Deck.FindIndex(c => c.IsEgg == egg);
            Players[p].Hand.Add(Players[p].Deck[i]); Players[p].Deck.RemoveAt(i);
        }
        public int OpenSlots(int p) { return Players[p].PersonalTurn >= Data.expandOnPersonalTurn ? Data.slots : Data.earlySlots; }
        public int FirstEmptyEgg(int p) { return Array.FindIndex(Players[p].Eggs, 0, OpenSlots(p), x => x == null); }
        public int FirstEmptyMonster(int p) { return Array.FindIndex(Players[p].Monsters, 0, OpenSlots(p), x => x == null); }
        public int TotalSpirit(int p, MonsterUnit m)
        {
            return m.Spirit + Players[p].Monsters.Count(x => x != null && x.IsBoss && x != m) * 3;
        }
        bool Fail(string error) { Error = error; return false; }
        bool CanAct(int p) { Error = null; return !Over && p == Active || Fail("请等待自己的行动回合。"); }
        bool SlotValid(int p, int slot) { return slot >= 0 && slot < OpenSlots(p); }
        public void Log(string message) { History.Add(message); }
        void Draw(int p)
        {
            var d = Players[p];
            if (d.Deck.Count == 0) { d.Health -= ++d.Fatigue; Log(d.Name + " 牌库耗尽，疲劳伤害 " + d.Fatigue + "。"); CheckWinner(); return; }
            var c = d.Deck[0]; d.Deck.RemoveAt(0);
            if (d.Hand.Count >= Data.maxHand) Log(d.Name + " 手牌已满，新抽牌弃置。"); else d.Hand.Add(c);
        }
        void BeginTurn(int p)
        {
            Active = p; Turn++; var d = Players[p]; d.PersonalTurn++;
            Log("—— " + d.Name + " 第 " + d.PersonalTurn + " 回合 ——");
            foreach (var m in d.Monsters.Where(x => x != null)) { m.Attacks = Turn == 1 ? 0 : 1; m.SkillUsed = false; }
            foreach (var e in d.Eggs.Where(x => x != null))
            {
                if (e.Remaining > 0) e.Remaining--; else { e.Spirit++; Log(e.Name + " 等待蕴灵 +1。"); }
            }
            if (Turn > 1) Draw(p);
        }
        public bool PlayEgg(int p, int cardId, int slot)
        {
            if (!CanAct(p)) return false;
            var c = Players[p].Hand.Find(x => x.Id == cardId && x.IsEgg);
            if (c == null || !SlotValid(p, slot) || Players[p].Eggs[slot] != null) return Fail("请选择开放的空蛋位。");
            var def = Data.Egg(c.Key);
            Players[p].Eggs[slot] = new EggUnit { Id = ++nextId, Initial = def.turns, Remaining = def.turns,
                Name = def.name, Core = def.core, Pool = (string[])def.pool.Clone(), Weights = (int[])def.weights.Clone(), Elements = (string[])def.elements.Clone() };
            Players[p].Hand.Remove(c); Log(Players[p].Name + " 放置「" + def.name + "」，孵化需 " + def.turns + " 个己方回合。"); return true;
        }
        public bool Hatch(int p, int eggSlot, int monsterSlot)
        {
            if (!CanAct(p)) return false;
            if (!SlotValid(p, eggSlot) || !SlotValid(p, monsterSlot)) return Fail("该位置尚未开放。");
            var e = Players[p].Eggs[eggSlot];
            if (e == null || e.Remaining > 0 || Players[p].Monsters[monsterSlot] != null) return Fail("需要一枚就绪的蛋和一个空怪兽位。");
            int roll = random.Next(100), cumulative = 0, chosen = 0;
            for (int i = 0; i < e.Pool.Length; i++) { cumulative += e.Weights[i]; if (roll < cumulative) { chosen = i; break; } }
            var def = Data.Monster(e.Pool[chosen]);
            var m = NewMonster(def, e.Tier, e.Generation, e.Spirit, e.Elements, e.Pool, e.Weights);
            if (fragileEggs.Remove(e.Id)) m.Health = 1;
            m.Attacks = Turn == 1 ? 0 : 1;
            Players[p].Eggs[eggSlot] = null; Players[p].Monsters[monsterSlot] = m; Players[p].Hatched++;
            Log(Players[p].Name + " 孵化「" + def.name + "」" + (chosen == e.Pool.Length - 1 ? " ★ 隐藏结果" : "")
                + "，" + m.Tier + " 阶 / 第 " + (m.Generation + 1) + " 代 / " + (m.Male ? "公" : "母") + "（抽取 " + (roll + 1) + "/100）。");
            if (def.ability == "copy" && TotalSpirit(p, m) >= 2)
            {
                int empty = FirstEmptyMonster(p);
                if (empty >= 0) { var copy = Copy(m); copy.Attacks = m.Attacks; Players[p].Monsters[empty] = copy; Log("忍者熊桑召唤复制。"); }
            }
            return true;
        }
        MonsterUnit NewMonster(MonsterDefinition def, int tier, int generation, int spirit, string[] elements, string[] pool, int[] weights)
        {
            return new MonsterUnit { Id = ++nextId, Definition = def, Tier = tier, Generation = generation,
                Attack = def.attack[tier - 1], Health = def.health[tier - 1], MaxHealth = def.health[tier - 1], Spirit = spirit,
                Male = random.Next(2) == 0, Elements = (string[])def.elements.Clone(),
                LineagePool = (string[])pool.Clone(), LineageWeights = (int[])weights.Clone() };
        }
        MonsterUnit Copy(MonsterUnit source)
        {
            return new MonsterUnit { Id = ++nextId, Definition = source.Definition, Tier = source.Tier, Generation = source.Generation,
                Attack = source.Attack, Health = source.Health, MaxHealth = source.MaxHealth, Spirit = source.Spirit,
                Male = source.Male, Elements = (string[])source.Elements.Clone(), LineagePool = (string[])source.LineagePool.Clone(),
                LineageWeights = (int[])source.LineageWeights.Clone(), IsBoss = source.IsBoss };
        }
        public bool PlayTool(int p, int cardId, int targetSlot)
        {
            if (!CanAct(p)) return false;
            var d = Players[p]; var c = d.Hand.Find(x => x.Id == cardId && !x.IsEgg);
            if (c == null) return Fail("道具已不在手牌中。");
            if (c.Key != "draw" && !SlotValid(p, targetSlot)) return Fail("请选择合法目标。");
            if (c.Key == "watergun" || c.Key == "fold")
            {
                var e = d.Eggs[targetSlot]; if (e == null) return Fail("请选择己方的蛋。");
                if (c.Key == "watergun") { if (e.Remaining > 0) e.Remaining--; else e.Spirit += 2; }
                else { e.Remaining = 0; fragileEggs.Add(e.Id); }
            }
            else if (c.Key == "craft")
            {
                var m = d.Monsters[targetSlot]; if (m == null) return Fail("请选择己方怪兽。");
                m.Attack += 3; m.Health += 3; m.MaxHealth += 3;
            }
            else if (c.Key == "revive")
            {
                if (d.LastDead == null || d.Monsters[targetSlot] != null) return Fail("需要阵亡记录和一个空怪兽位。");
                var revived = Copy(d.LastDead); revived.Health = revived.MaxHealth; revived.Attacks = 0;
                d.Monsters[targetSlot] = revived; d.LastDead = null;
            }
            else if (c.Key != "draw") return Fail("原型尚未支持该道具。");
            d.Hand.Remove(c); Log(d.Name + " 使用「" + ToolName(c.Key) + "」。");
            if (c.Key == "draw") Draw(p);
            return true;
        }
        public bool UseSkill(int p, int slot)
        {
            if (!CanAct(p) || !SlotValid(p, slot)) return false;
            var m = Players[p].Monsters[slot];
            if (m == null || m.SkillUsed || m.Spirit <= 0) return Fail("没有可用主动技能或可消耗蕴灵。");
            if (m.Definition.ability == "burst") { m.Attack += new[] { 5, 8, 12 }[m.Tier - 1] * m.Spirit; m.Spirit = 0; }
            else if (m.Definition.ability == "draw")
            {
                m.Spirit = 0; m.Health -= 5 * m.Tier;
                for (int i = 0; i < m.Tier && !Over; i++) Draw(p);
            }
            else return Fail("这只怪兽没有主动技能。");
            m.SkillUsed = true; Log(m.Definition.name + " 发动主动技能。"); Cleanup(); CheckWinner(); return true;
        }
        public bool Attack(int p, int sourceSlot, int targetSlot)
        {
            if (!CanAct(p) || !SlotValid(p, sourceSlot)) return false;
            var source = Players[p].Monsters[sourceSlot]; var enemy = Players[1 - p];
            if (source == null || source.Attacks <= 0) return Fail("这只怪兽本回合不能攻击。");
            if (targetSlot < -1 || targetSlot >= Data.slots) return Fail("攻击目标无效。");
            var target = targetSlot >= 0 ? enemy.Monsters[targetSlot] : null;
            if (targetSlot >= 0 && target == null) return Fail("该位置没有敌方怪兽。");
            if (targetSlot < 0 && enemy.Monsters.Any(x => x != null)) return Fail("必须先击败敌方场上的怪兽。");
            source.Attacks--;
            if (target != null)
            {
                int retaliation = target.Attack;
                target.Health -= source.Attack; source.Health -= retaliation;
                Log(source.Definition.name + " 攻击 " + target.Definition.name + "，造成 " + source.Attack + " / 反击 " + retaliation + "。");
                if (target.Definition.ability == "guard" && random.Next(100) < Math.Min(100, TotalSpirit(1 - p, target) * 20))
                { source.Health = 0; Log("咬咬咬人鳄的蕴灵摧毁攻击者。"); }
                foreach (var thorn in enemy.Monsters.Where(x => x != null && x != target && x.Definition.ability == "thorns"))
                    source.Health -= thorn.Tier * TotalSpirit(1 - p, thorn);
            }
            else
            {
                var block = enemy.Eggs.FirstOrDefault(x => x != null && !x.BlockUsed && x.Remaining < x.Initial);
                if (block != null)
                {
                    block.BlockUsed = true; block.Remaining = Math.Min(block.Initial, block.Remaining + 1);
                    Log(enemy.Name + " 的蛋抵挡一次攻击，孵化 +1（每蛋一次）。");
                }
                else { int damage = source.Definition.ability == "multi" ? source.Attack / 2 : source.Attack;
                    enemy.Health -= damage; Log(source.Definition.name + " 对 " + enemy.Name + " 造成 " + damage + " 伤害。"); }
            }
            if (source.Definition.ability == "share")
            {
                int spirit = TotalSpirit(p, source);
                foreach (var m in Players[p].Monsters.Where(x => x != null && x != source)) m.Spirit += spirit;
            }
            if (source.Definition.ability == "multi" && source.Spirit > 0) { source.Spirit--; source.Attacks++; }
            Cleanup(); CheckWinner(); return true;
        }
        void Cleanup()
        {
            for (int p = 0; p < 2; p++) for (int i = 0; i < Data.slots; i++)
            {
                var m = Players[p].Monsters[i];
                if (m == null || m.Health > 0) continue;
                Players[p].LastDead = Copy(m); Players[p].Monsters[i] = null; Log(m.Definition.name + " 阵亡。");
            }
        }
        void CheckWinner()
        {
            if (Over) return;
            if (Players[0].Health <= 0 || Players[1].Health <= 0)
            {
                Over = true; Winner = Players[0].Health <= 0 && Players[1].Health <= 0 ? -1 : Players[0].Health > 0 ? 0 : 1;
                Log(Winner < 0 ? "本场平局。" : Players[Winner].Name + " 获胜！");
            }
        }
        public bool ClaimQuest(int p, int slot)
        {
            if (!CanAct(p)) return false;
            var d = Players[p];
            if (d.QuestClaimed || d.Hatched < Data.questGoal || !SlotValid(p, slot) || d.Monsters[slot] != null)
                return Fail("任务需要孵化 " + Data.questGoal + " 只怪兽，并选择一个空怪兽位。");
            var boss = NewMonster(Data.Monster("boss"), 3, 0, 0, new[] { "电" }, new[] { "boss" }, new[] { 100 });
            boss.IsBoss = true; boss.Attacks = Turn == 1 ? 0 : 1; d.Monsters[slot] = boss; d.QuestClaimed = true;
            var enemy = Players[1 - p]; for (int n = 0; n < 2 && enemy.Hand.Count > 0; n++) enemy.Hand.RemoveAt(random.Next(enemy.Hand.Count));
            Log(d.Name + " 完成任务：最强毁灭机器人登场！敌方最多 2 张手牌被破坏。"); return true;
        }
        public bool EndTurn(int p)
        {
            if (!CanAct(p)) return false;
            var d = Players[p];
            // Snapshot pairs before placing any offspring; newly generated eggs cannot affect this phase.
            var pairs = new List<MonsterUnit[]>();
            for (int i = 0; i < OpenSlots(p) - 1; i++)
            {
                var a = d.Monsters[i]; var b = d.Monsters[i + 1];
                if (a != null && b != null && !a.IsBoss && !b.IsBoss && a.Male != b.Male) pairs.Add(new[] { a, b });
            }
            foreach (var pair in pairs)
            {
                int slot = FirstEmptyEgg(p); if (slot < 0) { Log("孵化区已满，本次后代放弃。"); continue; }
                var father = pair[0].Male ? pair[0] : pair[1]; var mother = pair[0].Male ? pair[1] : pair[0];
                var e = Breed(father, mother); d.Eggs[slot] = e;
                Log(father.Definition.name + " × " + mother.Definition.name + " → " + e.Tier + " 阶 / 第 " + (e.Generation + 1) + " 代蛋。");
            }
            var ordinary = d.Monsters.Where(x => x != null && !x.IsBoss).ToArray();
            if (ordinary.Length >= 2 && ordinary.All(x => x.Male == ordinary[0].Male))
            {
                var selected = ordinary[random.Next(ordinary.Length)]; selected.Male = random.Next(2) == 0;
                Log("同性交互：自动为 " + selected.Definition.name + " 重抽一次性别 → " + (selected.Male ? "公" : "母") + "。");
            }
            foreach (var m in d.Monsters.Where(x => x != null).ToArray())
            {
                int spirit = TotalSpirit(p, m); if (spirit <= 0) continue;
                if (m.Definition.ability == "accelerate")
                {
                    var eggs = d.Eggs.Where(x => x != null).ToArray();
                    if (eggs.Length > 0) { var egg = eggs[random.Next(eggs.Length)]; if (egg.Remaining > 0) egg.Remaining--; else egg.Spirit++; Log("司南鳌使一枚蛋加速。"); }
                }
                else if (m.Definition.ability == "burn")
                {
                    var targets = Players[1 - p].Monsters.Where(x => x != null && x.Health > 0).ToArray();
                    if (targets.Length > 0) { targets[random.Next(targets.Length)].Health -= 3 * m.Tier; if (m.Spirit > 0) m.Spirit--; Log("三角积木蛇触发回合结束伤害。"); }
                }
            }
            Cleanup(); CheckWinner(); if (!Over) BeginTurn(1 - p); return true;
        }
        EggUnit Breed(MonsterUnit father, MonsterUnit mother)
        {
            int tier = father.Tier == mother.Tier ? Math.Min(3, father.Tier + 1)
                : Math.Abs(father.Tier - mother.Tier) == 1 ? Math.Max(father.Tier, mother.Tier) : 2;
            int generation = father.Generation == mother.Generation ? father.Generation + 1 : Math.Max(father.Generation, mother.Generation);
            generation = Math.Min(3, generation);
            // MVP supports one race; keep offspring in the father's known pool. Cross-attribute effect inheritance is deferred.
            var pool = father.LineagePool.ToList();
            string core = pool.Contains(father.Definition.id) ? father.Definition.id : pool[0];
            pool.Remove(core); pool.Insert(0, core);
            int desired = Math.Max(2, father.LineagePool.Length - (generation > father.Generation ? 1 : 0));
            while (pool.Count > desired) pool.RemoveAt(pool.Count - 2);
            int[] weights = pool.Count == 5 ? new[] { 40, 20, 20, 15, 5 } : pool.Count == 4 ? new[] { 50, 25, 20, 5 }
                : pool.Count == 3 ? new[] { 65, 30, 5 } : new[] { 95, 5 };
            var elements = pool.SelectMany(id => Data.Monster(id).elements).Distinct().Take(2).ToArray();
            int turns = tier == 1 ? 1 : tier == 2 ? 3 : 5;
            return new EggUnit { Id = ++nextId, Tier = tier, Generation = generation, Initial = turns, Remaining = turns,
                Name = "进化灵蛋", Core = core, Pool = pool.ToArray(), Weights = weights, Elements = elements };
        }
        // One atomic legal action per call lets the presentation show the opponent's decisions.
        public bool StepAI(int p)
        {
            if (Over || Active != p) return false;
            var d = Players[p]; int empty;
            if (!d.QuestClaimed && d.Hatched >= Data.questGoal && (empty = FirstEmptyMonster(p)) >= 0) return ClaimQuest(p, empty);
            for (int i = 0; i < OpenSlots(p); i++)
                if (d.Eggs[i] != null && d.Eggs[i].Remaining == 0 && (empty = FirstEmptyMonster(p)) >= 0) return Hatch(p, i, empty);
            var card = d.Hand.FirstOrDefault(c => c.IsEgg);
            if (card != null && (empty = FirstEmptyEgg(p)) >= 0) return PlayEgg(p, card.Id, empty);
            foreach (var c in d.Hand.Where(x => !x.IsEgg).ToArray())
            {
                int slot = -1;
                if (c.Key == "draw") return PlayTool(p, c.Id, 0);
                if (c.Key == "watergun" || c.Key == "fold") slot = Array.FindIndex(d.Eggs, x => x != null && x.Remaining > 0);
                if (c.Key == "craft") slot = Array.FindIndex(d.Monsters, x => x != null);
                if (c.Key == "revive" && d.LastDead != null) slot = FirstEmptyMonster(p);
                if (slot >= 0) return PlayTool(p, c.Id, slot);
            }
            for (int i = 0; i < OpenSlots(p); i++)
            {
                var m = d.Monsters[i]; if (m == null) continue;
                if (!m.SkillUsed && m.Spirit > 0 && m.Definition.ability == "burst") return UseSkill(p, i);
                if (m.Attacks > 0)
                {
                    var enemy = Players[1 - p]; int target = -1, score = int.MinValue;
                    for (int j = 0; j < Data.slots; j++)
                    {
                        var t = enemy.Monsters[j]; if (t == null) continue;
                        int s = (t.Health <= m.Attack ? 100 : 0) - t.Health - (t.Attack >= m.Health ? 20 : 0);
                        if (s > score) { score = s; target = j; }
                    }
                    return Attack(p, i, target);
                }
            }
            return EndTurn(p);
        }
    }
}
