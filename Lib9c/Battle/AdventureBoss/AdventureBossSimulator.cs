// #define TEST_LOG

using System;
using System.Collections.Generic;
using System.Linq;
using Libplanet.Action;
using Nekoyume.Helper;
using Nekoyume.Model;
using Nekoyume.Model.BattleStatus;
using Nekoyume.Model.Item;
using Nekoyume.Model.Stat;
using Nekoyume.Model.State;
using Nekoyume.Model.Buff;
using Nekoyume.TableData;
using Nekoyume.TableData.AdventureBoss;
using Priority_Queue;
using NormalAttack = Nekoyume.Model.BattleStatus.NormalAttack;
using Skill = Nekoyume.Model.Skill.Skill;

namespace Nekoyume.Battle.AdventureBoss
{
    public class AdventureBossSimulator : Simulator, IStageSimulator
    {
        private readonly List<Wave> _waves;
        private readonly List<ItemBase> _waveRewards;

        public CollectionMap ItemMap { get; private set; } = new ();
        public EnemySkillSheet EnemySkillSheet { get; }

        public int BossId { get; }
        public int StageId => FloorId;
        public int FloorId { get; }
        private int TurnLimit { get; }
        public override IEnumerable<ItemBase> Reward => _waveRewards;

        public AdventureBossSimulator(
            int bossId,
            int floorId,
            IRandom random,
            AvatarState avatarState,
            List<Guid> foods,
            AllRuneState runeStates,
            RuneSlotState runeSlotState,
            FloorSheet.Row floorRow,
            FloorWaveSheet.Row floorWaveRow,
            SimulatorSheets simulatorSheets,
            EnemySkillSheet enemySkillSheet,
            CostumeStatSheet costumeStatSheet,
            List<ItemBase> waveRewards,
            List<StatModifier> collectionModifiers,
            DeBuffLimitSheet deBuffLimitSheet,
            bool logEvent = true,
            long shatterStrikeMaxDamage = 400_000
        )
            : base(
                random,
                avatarState,
                foods,
                simulatorSheets,
                logEvent,
                shatterStrikeMaxDamage
            )
        {
            DeBuffLimitSheet = deBuffLimitSheet;
            var runeOptionSheet = simulatorSheets.RuneOptionSheet;
            var skillSheet = simulatorSheets.SkillSheet;
            var runeLevelBonus = RuneHelper.CalculateRuneLevelBonus(
                runeStates, simulatorSheets.RuneListSheet, simulatorSheets.RuneLevelBonusSheet
            );
            var equippedRune = new List<RuneState>();
            foreach (var runeInfo in runeSlotState.GetEquippedRuneSlotInfos())
            {
                if (runeStates.TryGetRuneState(runeInfo.RuneId, out var runeState))
                {
                    equippedRune.Add(runeState);
                }
            }

            Player.ConfigureStats(costumeStatSheet, equippedRune, runeOptionSheet, runeLevelBonus,
                skillSheet, collectionModifiers);

            // call SetRuneSkills last. because rune skills affect from total calculated stats
            Player.SetRuneSkills(equippedRune, runeOptionSheet, skillSheet);

            _waves = new List<Wave>();
            _waveRewards = waveRewards;
            BossId = bossId;
            FloorId = floorId;
            EnemySkillSheet = enemySkillSheet;
            TurnLimit = floorRow.TurnLimit;

            SetWave(floorRow, floorWaveRow);
        }

        public static List<ItemBase> GetWaveRewards(
            IRandom random,
            FloorSheet.Row floorRow,
            MaterialItemSheet materialItemSheet,
            int playCount = 1)
        {
            var maxCountForItemDrop = random.Next(
                floorRow.DropItemMin,
                floorRow.DropItemMax + 1);
            var waveRewards = new List<ItemBase>();
            for (var i = 0; i < playCount; i++)
            {
                var itemSelector = SetItemSelector(floorRow, random);
                var rewards = SetReward(
                    itemSelector,
                    maxCountForItemDrop,
                    random,
                    materialItemSheet
                );

                waveRewards.AddRange(rewards);
            }

            return waveRewards;
        }

        public Player Simulate()
        {
            Log.stageId = FloorId;
            Log.waveCount = _waves.Count;
            Log.clearedWaveNumber = 0;
            Log.newlyCleared = false;
            Player.Spawn();
            TurnNumber = 0;
            for (var wv = 0; wv < _waves.Count; wv++)
            {
                Characters = new SimplePriorityQueue<CharacterBase, decimal>();
                Characters.Enqueue(Player, TurnPriority / Player.SPD);

                WaveNumber = wv + 1;
                WaveTurn = 1;
                _waves[wv].Spawn(this);

                while (true)
                {
                    // 제한 턴을 넘어서는 경우 break.
                    if (TurnNumber > TurnLimit)
                    {
                        Result = wv == 0 ? BattleLog.Result.Lose : BattleLog.Result.TimeOver;
                        break;
                    }

                    // 캐릭터 큐가 비어 있는 경우 break.
                    if (!Characters.TryDequeue(out var character))
                    {
                        break;
                    }

                    character.Tick();

                    // 플레이어가 죽은 경우 break;
                    if (Player.IsDead)
                    {
                        Result = wv == 0 ? BattleLog.Result.Lose : BattleLog.Result.Win;
                        break;
                    }

                    // 플레이어의 타겟(적)이 없는 경우 break.
                    if (!Player.Targets.Any())
                    {
                        Result = BattleLog.Result.Win;
                        Log.clearedWaveNumber = WaveNumber;

                        // Adventure boss has only one wave. Drop item box and clear.
                        ItemMap = Player.GetRewards(_waveRewards);
                        if (LogEvent)
                        {
                            var dropBox = new DropBox(null, _waveRewards);
                            Log.Add(dropBox);
                            var getReward = new GetReward(null, _waveRewards);
                            Log.Add(getReward);
                        }

                        Log.newlyCleared = true;
                        break;
                    }

                    foreach (var other in Characters)
                    {
                        var spdMultiplier = 0.6m;
                        var current = Characters.GetPriority(other);
                        if (other == Player && other.usedSkill is not null &&
                            other.usedSkill is not NormalAttack)
                        {
                            spdMultiplier = 0.9m;
                        }

                        var speed = current * spdMultiplier;
                        Characters.UpdatePriority(other, speed);
                    }

                    Characters.Enqueue(character, TurnPriority / character.SPD);
                }

                // 제한 턴을 넘거나 플레이어가 죽은 경우 break;
                if (TurnNumber > TurnLimit || Player.IsDead)
                {
                    break;
                }
            }

            Log.result = Result;
            return Player;
        }

        private void SetWave(FloorSheet.Row floorRow, FloorWaveSheet.Row floorWaveRow)
        {
            var enemyStatModifiers = floorRow.EnemyInitialStatModifiers;
            var waves = floorWaveRow.Waves;
            foreach (var wave in
                     waves.Select(e => SpawnWave(e, enemyStatModifiers))
                    )
            {
                _waves.Add(wave);
            }
        }

        private Wave SpawnWave(
            FloorWaveSheet.WaveData waveData,
            IReadOnlyList<StatModifier> initialStatModifiers)
        {
            var wave = new Wave();
            foreach (var monsterData in waveData.Monsters)
            {
                for (var i = 0; i < monsterData.Count; i++)
                {
                    CharacterSheet.TryGetValue(
                        monsterData.CharacterId,
                        out var row,
                        true);

                    var stat = new CharacterStats(row, monsterData.Level, initialStatModifiers);
                    var enemyModel = new Enemy(Player, stat, row, row.ElementalType);
                    wave.Add(enemyModel);
                    wave.HasBoss = waveData.HasBoss;
                }
            }

            return wave;
        }

        public static WeightedSelector<FloorSheet.RewardData> SetItemSelector(
            FloorSheet.Row floorRow, IRandom random
        )
        {
            var itemSelector = new WeightedSelector<FloorSheet.RewardData>(random);
            foreach (var r in floorRow.Rewards)
            {
                itemSelector.Add(r, r.Ratio);
            }

            return itemSelector;
        }

        public static List<ItemBase> SetReward(
            WeightedSelector<FloorSheet.RewardData> itemSelector,
            int maxCount,
            IRandom random,
            MaterialItemSheet materialItemSheet
        )
        {
            var reward = new List<ItemBase>();

            while (reward.Count < maxCount)
            {
                try
                {
                    var data = itemSelector.Select(1).First();
                    if (materialItemSheet.TryGetValue(data.ItemId, out var itemData))
                    {
                        var count = random.Next(data.Min, data.Max + 1);
                        for (var i = 0; i < count; i++)
                        {
                            var item = ItemFactory.CreateMaterial(itemData);
                            if (reward.Count < maxCount)
                            {
                                reward.Add(item);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
                catch (ListEmptyException)
                {
                    break;
                }
            }

            reward = reward.OrderBy(r => r.Id).ToList();
            return reward;
        }
    }
}
