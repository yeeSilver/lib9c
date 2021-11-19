﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Bencodex.Types;
using Lib9c.DevExtensions.Model;
using Lib9c.Model.Order;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using static Lib9c.SerializeKeys;
using Libplanet.Crypto;
using Nekoyume;
using Nekoyume.Action;

namespace Lib9c.DevExtensions.Action
{
    [Serializable]
    [ActionType("create_testbed")]
    public class CreateTestbed : GameAction
    {
        private int _slotIndex = 0;
        private PrivateKey _privateKey = new PrivateKey();

        protected override IImmutableDictionary<string, IValue> PlainValueInternal =>
            new Dictionary<string, IValue>()
            {
            }.ToImmutableDictionary();

        protected override void LoadPlainValueInternal(
            IImmutableDictionary<string, IValue> plainValue)
        {
        }

        public override IAccountStateDelta Execute(IActionContext context)
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase);
            path = path.Replace(".Lib9c.Tests\\bin\\Debug\\netcoreapp3.1",
                            "Lib9c.DevExtensions\\Data\\TestbedSell.json");
            path = path.Replace("file:\\", "");
            var data = TestbedHelper.LoadJsonFile<TestbedSell>(path);
            var addedItemInfos = data.Items
                .Select(item => new TestbedHelper.AddedItemInfo(
                    context.Random.GenerateRandomGuid(),
                    context.Random.GenerateRandomGuid()))
                .ToList();

            var agentAddress = _privateKey.PublicKey.ToAddress();
            var states = context.PreviousStates;

            var avatarAddress = agentAddress.Derive(
                string.Format(
                    CultureInfo.InvariantCulture,
                    CreateAvatar.DeriveFormat,
                    _slotIndex
                )
            );
            var inventoryAddress = avatarAddress.Derive(LegacyInventoryKey);
            var worldInformationAddress = avatarAddress.Derive(LegacyWorldInformationKey);
            var questListAddress = avatarAddress.Derive(LegacyQuestListKey);
            var orderReceiptAddress = OrderDigestListState.DeriveAddress(avatarAddress);

            if (context.Rehearsal)
            {
                states = states.SetState(agentAddress, MarkChanged);
                for (var i = 0; i < AvatarState.CombinationSlotCapacity; i++)
                {
                    var slotAddress = avatarAddress.Derive(
                        string.Format(CultureInfo.InvariantCulture,
                            CombinationSlotState.DeriveFormat, i));
                    states = states.SetState(slotAddress, MarkChanged);
                }

                states = states.SetState(avatarAddress, MarkChanged)
                    .SetState(Addresses.Ranking, MarkChanged)
                    .SetState(worldInformationAddress, MarkChanged)
                    .SetState(questListAddress, MarkChanged)
                    .SetState(inventoryAddress, MarkChanged);

                for (var i = 0; i < data.Items.Length; i++)
                {
                    var itemAddress = Addresses.GetItemAddress(addedItemInfos[i].TradableId);
                    var orderAddress = Order.DeriveAddress(addedItemInfos[i].OrderId);
                    var shopAddress = ShardedShopStateV2.DeriveAddress(
                        data.Items[i].ItemSubType,
                        addedItemInfos[i].OrderId);

                    states = states.SetState(avatarAddress, MarkChanged)
                        .SetState(inventoryAddress, MarkChanged)
                        .MarkBalanceChanged(GoldCurrencyMock, agentAddress,
                            GoldCurrencyState.Address)
                        .SetState(orderReceiptAddress, MarkChanged)
                        .SetState(itemAddress, MarkChanged)
                        .SetState(orderAddress, MarkChanged)
                        .SetState(shopAddress, MarkChanged);
                }

                return states;
            }

            // Create Agent and avatar
            var existingAgentState = states.GetAgentState(agentAddress);
            var agentState = existingAgentState ?? new AgentState(agentAddress);
            var avatarState = states.GetAvatarState(avatarAddress);
            if (!(avatarState is null))
            {
                throw new InvalidAddressException(
                    $"Aborted as there is already an avatar at {avatarAddress}.");
            }

            if (agentState.avatarAddresses.ContainsKey(_slotIndex))
            {
                throw new AvatarIndexAlreadyUsedException(
                    $"borted as the signer already has an avatar at index #{_slotIndex}.");
            }

            agentState.avatarAddresses.Add(_slotIndex, avatarAddress);

            var rankingState = context.PreviousStates.GetRankingState();
            var rankingMapAddress = rankingState.UpdateRankingMap(avatarAddress);
            avatarState = TestbedHelper.CreateAvatarState(data.avatar.Name,
                agentAddress,
                avatarAddress,
                context.BlockIndex,
                context.PreviousStates.GetAvatarSheets(),
                context.PreviousStates.GetSheet<WorldSheet>(),
                context.PreviousStates.GetGameConfigState(),
                rankingMapAddress);

            // Add item
            var costumeItemSheet =  context.PreviousStates.GetSheet<CostumeItemSheet>();
            var equipmentItemSheet = context.PreviousStates.GetSheet<EquipmentItemSheet>();
            var optionSheet = context.PreviousStates.GetSheet<EquipmentItemOptionSheet>();
            var skillSheet = context.PreviousStates.GetSheet<SkillSheet>();
            var materialItemSheet = context.PreviousStates.GetSheet<MaterialItemSheet>();
            var consumableItemSheet = context.PreviousStates.GetSheet<ConsumableItemSheet>();
            for (var i = 0; i < data.Items.Length; i++)
            {
                TestbedHelper.AddItem(costumeItemSheet,
                    equipmentItemSheet,
                    optionSheet,
                    skillSheet,
                    materialItemSheet,
                    consumableItemSheet,
                    context.Random,
                    data.Items[i], addedItemInfos[i], avatarState);
            }

            avatarState.Customize(0, 0, 0, 0);

            foreach (var address in avatarState.combinationSlotAddresses)
            {
                var slotState =
                    new CombinationSlotState(address,
                        GameConfig.RequireClearedStageLevel.CombinationEquipmentAction);
                states = states.SetState(address, slotState.Serialize());
            }

            avatarState.UpdateQuestRewards(materialItemSheet);
            states = states.SetState(agentAddress, agentState.Serialize())
                .SetState(Addresses.Ranking, rankingState.Serialize())
                .SetState(inventoryAddress, avatarState.inventory.Serialize())
                .SetState(worldInformationAddress, avatarState.worldInformation.Serialize())
                .SetState(questListAddress, avatarState.questList.Serialize())
                .SetState(avatarAddress, avatarState.SerializeV2());

            // for sell
            for (var i = 0; i < data.Items.Length; i++)
            {
                var itemAddress = Addresses.GetItemAddress(addedItemInfos[i].TradableId);
                var orderAddress = Order.DeriveAddress(addedItemInfos[i].OrderId);
                var shopAddress = ShardedShopStateV2.DeriveAddress(
                    data.Items[i].ItemSubType,
                    addedItemInfos[i].OrderId);

                var balance =
                    context.PreviousStates.GetBalance(agentAddress, states.GetGoldCurrency());
                var price = new FungibleAssetValue(balance.Currency, data.Items[i].Price, 0);
                var order = OrderFactory.Create(agentAddress, avatarAddress,
                    addedItemInfos[i].OrderId,
                    price,
                    addedItemInfos[i].TradableId,
                    context.BlockIndex,
                    data.Items[i].ItemSubType,
                    data.Items[i].Count);

                order.Validate(avatarState, data.Items[i].Count);
                var tradableItem = order.Sell(avatarState);

                var shardedShopState =
                    states.TryGetState(shopAddress, out Dictionary serializedState)
                        ? new ShardedShopStateV2(serializedState)
                        : new ShardedShopStateV2(shopAddress);
                var costumeStatSheet = states.GetSheet<CostumeStatSheet>();
                var orderDigest = order.Digest(avatarState, costumeStatSheet);
                shardedShopState.Add(orderDigest, context.BlockIndex);
                var orderReceiptList =
                    states.TryGetState(orderReceiptAddress, out Dictionary receiptDict)
                        ? new OrderDigestListState(receiptDict)
                        : new OrderDigestListState(orderReceiptAddress);
                orderReceiptList.Add(orderDigest);

                states = states.SetState(orderReceiptAddress, orderReceiptList.Serialize())
                    .SetState(inventoryAddress, avatarState.inventory.Serialize())
                    .SetState(avatarAddress, avatarState.SerializeV2())
                    .SetState(itemAddress, tradableItem.Serialize())
                    .SetState(orderAddress, order.Serialize())
                    .SetState(shopAddress, shardedShopState.Serialize());
            }

            return states;
        }
    }
}
