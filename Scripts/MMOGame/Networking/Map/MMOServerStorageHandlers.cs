﻿using Cysharp.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace MultiplayerARPG.MMO
{
    public partial class MMOServerStorageHandlers : MonoBehaviour, IServerStorageHandlers
    {
#if UNITY_STANDALONE && !CLIENT_BUILD
        private readonly ConcurrentDictionary<StorageId, List<CharacterItem>> storageItems = new ConcurrentDictionary<StorageId, List<CharacterItem>>();
        private readonly ConcurrentDictionary<StorageId, HashSet<long>> usingStorageClients = new ConcurrentDictionary<StorageId, HashSet<long>>();
        private readonly ConcurrentDictionary<long, StorageId> usingStorageIds = new ConcurrentDictionary<long, StorageId>();
#endif

#if UNITY_STANDALONE && !CLIENT_BUILD
        public DatabaseNetworkManager DbServiceClient
        {
            get { return MMOServerInstance.Singleton.DatabaseNetworkManager; }
        }
#endif

        public async UniTaskVoid OpenStorage(long connectionId, IPlayerCharacterData playerCharacter, StorageId storageId)
        {
#if UNITY_STANDALONE && !CLIENT_BUILD
            if (!CanAccessStorage(playerCharacter, storageId))
            {
                GameInstance.ServerGameMessageHandlers.SendGameMessage(connectionId, UITextKeys.UI_ERROR_CANNOT_ACCESS_STORAGE);
                return;
            }
            // Store storage usage states
            if (!usingStorageClients.ContainsKey(storageId))
                usingStorageClients.TryAdd(storageId, new HashSet<long>());
            usingStorageClients[storageId].Add(connectionId);
            usingStorageIds.TryRemove(connectionId, out _);
            usingStorageIds.TryAdd(connectionId, storageId);
            // Load storage items from database
            ReadStorageItemsReq req = new ReadStorageItemsReq();
            req.StorageType = storageId.storageType;
            req.StorageOwnerId = storageId.storageOwnerId;
            ReadStorageItemsResp resp = await DbServiceClient.ReadStorageItemsAsync(req);
            List<CharacterItem> storageItems = resp.StorageCharacterItems;
            SetStorageItems(storageId, storageItems);
            // Notify storage items to client
            uint storageObjectId;
            Storage storage = GetStorage(storageId, out storageObjectId);
            GameInstance.ServerGameMessageHandlers.NotifyStorageOpened(connectionId, storageId.storageType, storageId.storageOwnerId, storageObjectId, storage.weightLimit, storage.slotLimit);
            storageItems.FillEmptySlots(storage.slotLimit > 0, storage.slotLimit);
            GameInstance.ServerGameMessageHandlers.NotifyStorageItems(connectionId, storageItems);
#endif
        }

        public async UniTaskVoid CloseStorage(long connectionId)
        {
#if UNITY_STANDALONE && !CLIENT_BUILD
            StorageId storageId;
            if (usingStorageIds.TryGetValue(connectionId, out storageId) && usingStorageClients.ContainsKey(storageId))
            {
                usingStorageClients[storageId].Remove(connectionId);
                usingStorageIds.TryRemove(connectionId, out _);
                GameInstance.ServerGameMessageHandlers.NotifyStorageClosed(connectionId);
            }
#endif
            await UniTask.Yield();
        }

        public bool TryGetOpenedStorageId(long connectionId, out StorageId storageId)
        {
#if UNITY_STANDALONE && !CLIENT_BUILD
            return usingStorageIds.TryGetValue(connectionId, out storageId);
#else
            storageId = default;
            return false;
#endif
        }

        public async UniTask<bool> IncreaseStorageItems(StorageId storageId, CharacterItem addingItem)
        {
#if UNITY_STANDALONE && !CLIENT_BUILD
            Storage storge = GetStorage(storageId, out _);
            IncreaseStorageItemsReq req = new IncreaseStorageItemsReq();
            req.StorageType = storageId.storageType;
            req.StorageOwnerId = storageId.storageOwnerId;
            req.WeightLimit = storge.weightLimit;
            req.SlotLimit = storge.slotLimit;
            req.Item = addingItem;
            IncreaseStorageItemsResp resp = await DbServiceClient.IncreaseStorageItemsAsync(req);
            if (UITextKeys.NONE != (UITextKeys)resp.Error)
            {
                // Error ocurring, storage may overwhelming let's it drop items to ground
                return false;
            }
            SetStorageItems(storageId, resp.StorageCharacterItems);
            NotifyStorageItemsUpdated(storageId.storageType, storageId.storageOwnerId);
            return true;
#else
            return false;
#endif
        }

        public async UniTask<DecreaseStorageItemsResult> DecreaseStorageItems(StorageId storageId, int dataId, short amount)
        {
#if UNITY_STANDALONE && !CLIENT_BUILD
            Storage storge = GetStorage(storageId, out _);
            DecreaseStorageItemsReq req = new DecreaseStorageItemsReq();
            req.StorageType = storageId.storageType;
            req.StorageOwnerId = storageId.storageOwnerId;
            req.WeightLimit = storge.weightLimit;
            req.SlotLimit = storge.slotLimit;
            req.DataId = dataId;
            req.Amount = amount;
            DecreaseStorageItemsResp resp = await DbServiceClient.DecreaseStorageItemsAsync(req);
            if (UITextKeys.NONE != (UITextKeys)resp.Error)
            {
                // Error ocurring, storage may overwhelming let's it drop items to ground
                return new DecreaseStorageItemsResult();
            }
            SetStorageItems(storageId, resp.StorageCharacterItems);
            NotifyStorageItemsUpdated(storageId.storageType, storageId.storageOwnerId);
            Dictionary<int, short> decreasedItems = new Dictionary<int, short>();
            foreach (ItemIndexAmountMap entry in resp.DecreasedItems)
            {
                decreasedItems.Add(entry.Index, (short)entry.Amount);
            }
            return new DecreaseStorageItemsResult()
            {
                IsSuccess = true,
                DecreasedItems = decreasedItems,
            };
#else
            return new DecreaseStorageItemsResult();
#endif
        }

        public List<CharacterItem> GetStorageItems(StorageId storageId)
        {
#if UNITY_STANDALONE && !CLIENT_BUILD
            if (!storageItems.ContainsKey(storageId))
                storageItems.TryAdd(storageId, new List<CharacterItem>());
            return storageItems[storageId];
#else
            return new List<CharacterItem>();
#endif
        }

        public void SetStorageItems(StorageId storageId, List<CharacterItem> items)
        {
#if UNITY_STANDALONE && !CLIENT_BUILD
            if (!storageItems.ContainsKey(storageId))
                storageItems.TryAdd(storageId, new List<CharacterItem>());
            storageItems[storageId] = items;
#endif
        }

        public Storage GetStorage(StorageId storageId, out uint objectId)
        {
            objectId = 0;
            Storage storage = default(Storage);
            switch (storageId.storageType)
            {
                case StorageType.Player:
                    storage = GameInstance.Singleton.playerStorage;
                    break;
                case StorageType.Guild:
                    storage = GameInstance.Singleton.guildStorage;
                    break;
                case StorageType.Building:
                    StorageEntity buildingEntity;
                    if (GameInstance.ServerBuildingHandlers.TryGetBuilding(storageId.storageOwnerId, out buildingEntity))
                    {
                        objectId = buildingEntity.ObjectId;
                        storage = buildingEntity.Storage;
                    }
                    break;
            }
            return storage;
        }

        public bool CanAccessStorage(IPlayerCharacterData playerCharacter, StorageId storageId)
        {
            switch (storageId.storageType)
            {
                case StorageType.Player:
                    if (!playerCharacter.UserId.Equals(storageId.storageOwnerId))
                        return false;
                    break;
                case StorageType.Guild:
                    if (!GameInstance.ServerGuildHandlers.ContainsGuild(playerCharacter.GuildId) ||
                        !playerCharacter.GuildId.ToString().Equals(storageId.storageOwnerId))
                        return false;
                    break;
                case StorageType.Building:
                    StorageEntity buildingEntity;
                    if (!GameInstance.ServerBuildingHandlers.TryGetBuilding(storageId.storageOwnerId, out buildingEntity) ||
                        !(buildingEntity.IsCreator(playerCharacter.Id) || buildingEntity.CanUseByEveryone))
                        return false;
                    break;
            }
            return true;
        }

        public bool IsStorageEntityOpen(StorageEntity storageEntity)
        {
#if UNITY_STANDALONE && !CLIENT_BUILD
            if (storageEntity == null)
                return false;
            StorageId id = new StorageId(StorageType.Building, storageEntity.Id);
            return usingStorageClients.ContainsKey(id) && usingStorageClients[id].Count > 0;
#else
            return false;
#endif
        }

        public List<CharacterItem> GetStorageEntityItems(StorageEntity storageEntity)
        {
#if UNITY_STANDALONE && !CLIENT_BUILD
            if (storageEntity == null)
                return new List<CharacterItem>();
            return GetStorageItems(new StorageId(StorageType.Building, storageEntity.Id));
#else
            return new List<CharacterItem>();
#endif
        }

        public void ClearStorage()
        {
#if UNITY_STANDALONE && !CLIENT_BUILD
            storageItems.Clear();
            usingStorageClients.Clear();
            usingStorageIds.Clear();
#endif
        }

        public void NotifyStorageItemsUpdated(StorageType storageType, string storageOwnerId)
        {
#if UNITY_STANDALONE && !CLIENT_BUILD
            StorageId storageId = new StorageId(storageType, storageOwnerId);
            GameInstance.ServerGameMessageHandlers.NotifyStorageItemsToClients(usingStorageClients[storageId], GetStorageItems(storageId));
#endif
        }

        public IDictionary<StorageId, List<CharacterItem>> GetAllStorageItems()
        {
#if UNITY_STANDALONE && !CLIENT_BUILD
            return storageItems;
#else
            return new ConcurrentDictionary<StorageId, List<CharacterItem>>();
#endif
        }
    }
}
