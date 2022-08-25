using System;
using System.Collections.Generic;
using System.Linq;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Nekoyume.Action;
using Nekoyume.TableData;

namespace Nekoyume.Extensions
{
    public static class SheetsExtensions
    {
        public static Address GetAddress<T>(this Dictionary<Type, (Address address, ISheet sheet)> sheets)
            where T : ISheet
        {
            if (!sheets.TryGetAddress<T>(out var address))
            {
                throw new FailedLoadSheetException(typeof(T));
            }

            return address;
        }

        public static Address GetAddress(this Dictionary<Type, (Address address, ISheet sheet)> sheets, Type type)
        {
            if (!sheets.TryGetAddress(type, out var address))
            {
                throw new FailedLoadSheetException(type);
            }

            return address;
        }

        public static T GetSheet<T>(this Dictionary<Type, (Address address, ISheet sheet)> sheets)
            where T : ISheet
        {
            if (!sheets.TryGetSheet<T>(out var sheet))
            {
                throw new FailedLoadSheetException(typeof(T));
            }

            return sheet;
        }

        public static ISheet GetSheet(this Dictionary<Type, (Address address, ISheet sheet)> sheets, Type sheetType)
        {
            if (!sheets.TryGetSheet(sheetType, out var sheet))
            {
                throw new FailedLoadSheetException(sheetType);
            }

            return sheet;
        }

        public static bool TryGetAddress<T>(
            this Dictionary<Type, (Address address, ISheet sheet)> sheets,
            out Address address)
            where T : ISheet
        {
            var type = typeof(T);
            if (!sheets.TryGetValue(type, out var tuple))
            {
                address = default;
                return false;
            }

            address = tuple.address;
            return true;
        }

        public static bool TryGetAddress(
            this Dictionary<Type, (Address address, ISheet sheet)> sheets,
            Type type,
            out Address address)
        {
            if (!sheets.TryGetValue(type, out var tuple))
            {
                address = default;
                return false;
            }

            address = tuple.address;
            return true;
        }

        public static bool TryGetSheet<T>(
            this Dictionary<Type, (Address address, ISheet sheet)> sheets,
            out T sheet)
            where T : ISheet
        {
            var type = typeof(T);
            if (!sheets.TryGetValue(type, out var tuple))
            {
                sheet = default;
                return false;
            }

            try
            {
                sheet = (T)tuple.sheet;
                return true;
            }
            catch
            {
                sheet = default;
                return false;
            }
        }

        public static bool TryGetSheet(
            this Dictionary<Type, (Address address, ISheet sheet)> sheets,
            Type type,
            out ISheet sheet)
        {
            if (!sheets.TryGetValue(type, out var tuple))
            {
                sheet = default;
                return false;
            }

            sheet = tuple.sheet;
            return true;
        }

        public static AvatarSheets GetAvatarSheets(this Dictionary<Type, (Address address, ISheet sheet)> sheets)
        {
            return new AvatarSheets(
                sheets.GetSheet<WorldSheet>(),
                sheets.GetQuestSheet(),
                sheets.GetSheet<QuestRewardSheet>(),
                sheets.GetSheet<QuestItemRewardSheet>(),
                sheets.GetSheet<EquipmentItemRecipeSheet>(),
                sheets.GetSheet<EquipmentItemSubRecipeSheet>()
            );
        }

        public static ItemSheet GetItemSheet(this Dictionary<Type, (Address address, ISheet sheet)> sheets)
        {
            var sheet = new ItemSheet();
            sheet.Set(sheets.GetSheet<ConsumableItemSheet>(), false);
            sheet.Set(sheets.GetSheet<CostumeItemSheet>(), false);
            sheet.Set(sheets.GetSheet<EquipmentItemSheet>(), false);
            sheet.Set(sheets.GetSheet<MaterialItemSheet>());
            return sheet;
        }

        public static QuestSheet GetQuestSheet(this Dictionary<Type, (Address address, ISheet sheet)> sheets)
        {
            var questSheet = new QuestSheet();
            questSheet.Set(sheets.GetSheet<WorldQuestSheet>(), false);
            questSheet.Set(sheets.GetSheet<CollectQuestSheet>(), false);
            questSheet.Set(sheets.GetSheet<CombinationQuestSheet>(), false);
            questSheet.Set(sheets.GetSheet<TradeQuestSheet>(), false);
            questSheet.Set(sheets.GetSheet<MonsterQuestSheet>(), false);
            questSheet.Set(sheets.GetSheet<ItemEnhancementQuestSheet>(), false);
            questSheet.Set(sheets.GetSheet<GeneralQuestSheet>(), false);
            questSheet.Set(sheets.GetSheet<ItemGradeQuestSheet>(), false);
            questSheet.Set(sheets.GetSheet<ItemTypeCollectQuestSheet>(), false);
            questSheet.Set(sheets.GetSheet<GoldQuestSheet>(), false);
            questSheet.Set(sheets.GetSheet<CombinationEquipmentQuestSheet>());
            return questSheet;
        }

        public static SimulatorSheets GetSimulatorSheets(
            this Dictionary<Type, (Address address, ISheet sheet)> sheets)
        {
            return new SimulatorSheets(
                sheets.GetSheet<MaterialItemSheet>(),
                sheets.GetSheet<SkillSheet>(),
                sheets.GetSheet<SkillBuffSheet>(),
                sheets.GetSheet<StatBuffSheet>(),
                sheets.GetSheet<SkillActionBuffSheet>(),
                sheets.GetSheet<ActionBuffSheet>(),
                sheets.GetSheet<CharacterSheet>(),
                sheets.GetSheet<CharacterLevelSheet>(),
                sheets.GetSheet<EquipmentItemSetEffectSheet>()
            );
        }

        public static StageSimulatorSheets GetStageSimulatorSheets(
            this Dictionary<Type, (Address address, ISheet sheet)> sheets)
        {
            return new StageSimulatorSheets(
                sheets.GetSheet<MaterialItemSheet>(),
                sheets.GetSheet<SkillSheet>(),
                sheets.GetSheet<SkillBuffSheet>(),
                sheets.GetSheet<StatBuffSheet>(),
                sheets.GetSheet<SkillActionBuffSheet>(),
                sheets.GetSheet<ActionBuffSheet>(),
                sheets.GetSheet<CharacterSheet>(),
                sheets.GetSheet<CharacterLevelSheet>(),
                sheets.GetSheet<EquipmentItemSetEffectSheet>(),
                sheets.GetSheet<StageSheet>(),
                sheets.GetSheet<StageWaveSheet>(),
                sheets.GetSheet<EnemySkillSheet>()
            );
        }

        public static RankingSimulatorSheets GetRankingSimulatorSheets(
            this Dictionary<Type, (Address address, ISheet sheet)> sheets)
        {
            return new RankingSimulatorSheets(
                sheets.GetSheet<MaterialItemSheet>(),
                sheets.GetSheet<SkillSheet>(),
                sheets.GetSheet<SkillBuffSheet>(),
                sheets.GetSheet<StatBuffSheet>(),
                sheets.GetSheet<SkillActionBuffSheet>(),
                sheets.GetSheet<ActionBuffSheet>(),
                sheets.GetSheet<CharacterSheet>(),
                sheets.GetSheet<CharacterLevelSheet>(),
                sheets.GetSheet<EquipmentItemSetEffectSheet>(),
                sheets.GetSheet<WeeklyArenaRewardSheet>()
            );
        }

        public static ArenaSimulatorSheets GetArenaSimulatorSheets(
            this Dictionary<Type, (Address address, ISheet sheet)> sheets)
        {
            return new ArenaSimulatorSheets(
                sheets.GetSheet<MaterialItemSheet>(),
                sheets.GetSheet<SkillSheet>(),
                sheets.GetSheet<SkillBuffSheet>(),
                sheets.GetSheet<StatBuffSheet>(),
                sheets.GetSheet<SkillActionBuffSheet>(),
                sheets.GetSheet<ActionBuffSheet>(),
                sheets.GetSheet<CharacterSheet>(),
                sheets.GetSheet<CharacterLevelSheet>(),
                sheets.GetSheet<EquipmentItemSetEffectSheet>(),
                sheets.GetSheet<CostumeStatSheet>(),
                sheets.GetSheet<WeeklyArenaRewardSheet>()
            );
        }

        public static RaidSimulatorSheets GetRaidSimulatorSheets(
            this Dictionary<Type, (Address address, ISheet sheet)> sheets)
        {
            return new RaidSimulatorSheets(
                sheets.GetSheet<MaterialItemSheet>(),
                sheets.GetSheet<SkillSheet>(),
                sheets.GetSheet<SkillBuffSheet>(),
                sheets.GetSheet<StatBuffSheet>(),
                sheets.GetSheet<SkillActionBuffSheet>(),
                sheets.GetSheet<ActionBuffSheet>(),
                sheets.GetSheet<CharacterSheet>(),
                sheets.GetSheet<CharacterLevelSheet>(),
                sheets.GetSheet<EquipmentItemSetEffectSheet>(),
                sheets.GetSheet<WorldBossCharacterSheet>(),
                sheets.GetSheet<WorldBossActionPatternSheet>()
            );
        }

        public static int FindLevelByStakedAmount(this IStakeRewardSheet sheet, Address agentAddress,
            FungibleAssetValue balance)
        {
            List<IStakeRewardRow> orderedRows =
                sheet.OrderedRows.OrderBy(row => row.RequiredGold).ToList();
            // throw Exception when balance < minimum RequiredGold
            var minimumRequired = orderedRows.First().RequiredGold * balance.Currency;
            if (balance < minimumRequired)
            {
                var msg = $"The account {agentAddress}'s balance of {balance.Currency} is insufficient to " +
                          $"staked minimum amount: {balance} < {minimumRequired}.";
                throw new InsufficientBalanceException(agentAddress, balance, msg);
            }
            for (int i = 0; i < orderedRows.Count - 1; ++i)
            {
                if (balance.Currency * orderedRows[i].RequiredGold <= balance &&
                    balance < balance.Currency * orderedRows[i + 1].RequiredGold)
                {
                    return orderedRows[i].Level;
                }
            }

            // Return maximum level when balance > maximum RequiredGold
            return orderedRows.Last().Level;
        }

        public static WorldBossListSheet.Row FindRowByBlockIndex(this WorldBossListSheet sheet,
            long blockIndex)
        {
            return sheet.OrderedList
                .First(r =>
                    r.StartedBlockIndex <= blockIndex &&
                    blockIndex <= r.EndedBlockIndex
                );
        }

        public static WorldBossListSheet.Row FindPreviousRowByBlockIndex(
            this WorldBossListSheet sheet, long blockIndex)
        {
            return sheet.OrderedList.Last(
                r => r.EndedBlockIndex < blockIndex
            );
        }

        public static int FindRaidIdByBlockIndex(this WorldBossListSheet sheet, long blockIndex)
        {
            WorldBossListSheet.Row row = sheet.FindRowByBlockIndex(blockIndex);
            return row.Id;
        }

        public static int FindPreviousRaidIdByBlockIndex(this WorldBossListSheet sheet,
            long blockIndex)
        {
            WorldBossListSheet.Row row = sheet.FindPreviousRowByBlockIndex(blockIndex);
            return row.Id;
        }
    }
}
