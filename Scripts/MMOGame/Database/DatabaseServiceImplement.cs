﻿using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Google.Protobuf;
using UnityEngine;

namespace MultiplayerARPG.MMO
{
    public class DatabaseServiceImplement : DatabaseService.DatabaseServiceBase
    {
        public delegate Task<CustomResp> CustomRequestDelegate(int type, ByteString data);
        public static CustomRequestDelegate onCustomRequest;
        public BaseDatabase Database { get; private set; }
        // TODO: I'm going to make in-memory database without Redis for now
        // In the future it may implements Redis
        // It's going to get some data from all tables but not every records
        // Just some records that players were requested
        private HashSet<string> cachedUsernames = new HashSet<string>();
        private HashSet<string> cachedCharacterNames = new HashSet<string>();
        private HashSet<string> cachedGuildNames = new HashSet<string>();
        private Dictionary<string, string> cachedUserAccessToken = new Dictionary<string, string>();
        private Dictionary<string, int> cachedUserGold = new Dictionary<string, int>();
        private Dictionary<string, int> cachedUserCash = new Dictionary<string, int>();
        private Dictionary<string, PlayerCharacterData> cachedUserCharacter = new Dictionary<string, PlayerCharacterData>();
        private Dictionary<string, SocialCharacterData> cachedSocialCharacter = new Dictionary<string, SocialCharacterData>();
        private Dictionary<string, Dictionary<string, BuildingSaveData>> cachedBuilding = new Dictionary<string, Dictionary<string, BuildingSaveData>>();
        private Dictionary<string, List<SocialCharacterData>> cachedFriend = new Dictionary<string, List<SocialCharacterData>>();
        private Dictionary<int, PartyData> cachedParty = new Dictionary<int, PartyData>();
        private Dictionary<int, GuildData> cachedGuild = new Dictionary<int, GuildData>();
        private Dictionary<StorageId, List<CharacterItem>> cachedStorageItems = new Dictionary<StorageId, List<CharacterItem>>();

        public DatabaseServiceImplement(BaseDatabase database)
        {
            Database = database;
        }

        public override async Task<ValidateUserLoginResp> ValidateUserLogin(ValidateUserLoginReq request, ServerCallContext context)
        {
            await Task.Yield();
            string userId = Database.ValidateUserLogin(request.Username, request.Password);
            return new ValidateUserLoginResp()
            {
                UserId = userId
            };
        }

        public override async Task<ValidateAccessTokenResp> ValidateAccessToken(ValidateAccessTokenReq request, ServerCallContext context)
        {
            await Task.Yield();
            bool isPass;
            if (cachedUserAccessToken.ContainsKey(request.UserId))
            {
                // Already cached access token, so validate access token from cache
                isPass = request.AccessToken.Equals(cachedUserAccessToken[request.UserId]);
            }
            else
            {
                // Doesn't cached yet, so try validate from database
                isPass = Database.ValidateAccessToken(request.UserId, request.AccessToken);
                // Pass, store access token to the dictionary
                if (isPass)
                    cachedUserAccessToken[request.UserId] = request.AccessToken;
            }
            return new ValidateAccessTokenResp()
            {
                IsPass = isPass
            };
        }

        public override async Task<GetUserLevelResp> GetUserLevel(GetUserLevelReq request, ServerCallContext context)
        {
            await Task.Yield();
            byte userLevel = Database.GetUserLevel(request.UserId);
            return new GetUserLevelResp()
            {
                UserLevel = userLevel
            };
        }

        public override async Task<GoldResp> GetGold(GetGoldReq request, ServerCallContext context)
        {
            return new GoldResp()
            {
                Gold = await ReadGold(request.UserId)
            };
        }

        public override async Task<GoldResp> ChangeGold(ChangeGoldReq request, ServerCallContext context)
        {
            int gold = await ReadGold(request.UserId);
            gold += request.ChangeAmount;
            // Cache the data, it will be used later
            cachedUserGold[request.UserId] = gold;
            // Update data to database
            Database.UpdateGold(request.UserId, gold);
            return new GoldResp()
            {
                Gold = gold
            };
        }

        public override async Task<CashResp> GetCash(GetCashReq request, ServerCallContext context)
        {
            return new CashResp()
            {
                Cash = await ReadCash(request.UserId)
            };
        }

        public override async Task<CashResp> ChangeCash(ChangeCashReq request, ServerCallContext context)
        {
            int cash = await ReadCash(request.UserId);
            cash += request.ChangeAmount;
            // Cache the data, it will be used later
            cachedUserCash[request.UserId] = cash;
            // Update data to database
            Database.UpdateCash(request.UserId, cash);
            return new CashResp()
            {
                Cash = cash
            };
        }

        public override async Task<VoidResp> UpdateAccessToken(UpdateAccessTokenReq request, ServerCallContext context)
        {
            await Task.Yield();
            // Store access token to the dictionary, it will be used to validate later
            cachedUserAccessToken[request.UserId] = request.AccessToken;
            // Update data to database
            Database.UpdateAccessToken(request.UserId, request.AccessToken);
            return new VoidResp();
        }

        public override async Task<VoidResp> CreateUserLogin(CreateUserLoginReq request, ServerCallContext context)
        {
            await Task.Yield();
            // Cache username, it will be used to validate later
            cachedUsernames.Add(request.Username);
            // Insert new user login to database
            Database.CreateUserLogin(request.Username, request.Password);
            return new VoidResp();
        }

        public override async Task<FindUsernameResp> FindUsername(FindUsernameReq request, ServerCallContext context)
        {
            await Task.Yield();
            long foundAmount;
            if (cachedUsernames.Contains(request.Username))
            {
                // Already cached username, so validate username from cache
                foundAmount = 1;
            }
            else
            {
                // Doesn't cached yet, so try validate from database
                foundAmount = Database.FindUsername(request.Username);
                // Cache username, it will be used to validate later
                cachedUsernames.Add(request.Username);
            }
            return new FindUsernameResp()
            {
                FoundAmount = foundAmount
            };
        }

        public override async Task<CharacterResp> CreateCharacter(CreateCharacterReq request, ServerCallContext context)
        {
            await Task.Yield();
            PlayerCharacterData character = DatabaseServiceUtils.FromByteString<PlayerCharacterData>(request.CharacterData);
            // Store character to the dictionary, it will be used later
            cachedUserCharacter[request.UserId] = character;
            cachedCharacterNames.Add(character.CharacterName);
            // Insert new character to database
            Database.CreateCharacter(request.UserId, character);
            return new CharacterResp()
            {
                CharacterData = DatabaseServiceUtils.ToByteString(character)
            };
        }

        public override async Task<CharacterResp> ReadCharacter(ReadCharacterReq request, ServerCallContext context)
        {
            return new CharacterResp()
            {
                CharacterData = DatabaseServiceUtils.ToByteString(await ReadCharacter(request.CharacterId))
            };
        }

        public override async Task<CharactersResp> ReadCharacters(ReadCharactersReq request, ServerCallContext context)
        {
            await Task.Yield();
            CharactersResp resp = new CharactersResp();
            DatabaseServiceUtils.CopyToRepeatedByteString(Database.ReadCharacters(request.UserId), resp.List);
            return resp;
        }

        public override async Task<CharacterResp> UpdateCharacter(UpdateCharacterReq request, ServerCallContext context)
        {
            await Task.Yield();
            PlayerCharacterData character = DatabaseServiceUtils.FromByteString<PlayerCharacterData>(request.CharacterData);
            // Cache the data, it will be used later
            cachedUserCharacter[character.Id] = character;
            // Update data to database
            // TODO: May update later to reduce amount of processes
            Database.UpdateCharacter(character);
            return new CharacterResp()
            {
                CharacterData = DatabaseServiceUtils.ToByteString(character)
            };
        }

        public override async Task<VoidResp> DeleteCharacter(DeleteCharacterReq request, ServerCallContext context)
        {
            await Task.Yield();
            // Remove data from cache
            if (cachedUserCharacter.ContainsKey(request.CharacterId))
            {
                string characterName = cachedUserCharacter[request.CharacterId].CharacterName;
                cachedCharacterNames.Remove(characterName);
                cachedUserCharacter.Remove(request.CharacterId);
            }
            // Delete data from database
            Database.DeleteCharacter(request.UserId, request.CharacterId);
            return new VoidResp();
        }

        public override async Task<FindCharacterNameResp> FindCharacterName(FindCharacterNameReq request, ServerCallContext context)
        {
            await Task.Yield();
            long foundAmount;
            if (cachedCharacterNames.Contains(request.CharacterName))
            {
                // Already cached character name, so validate character name from cache
                foundAmount = 1;
            }
            else
            {
                // Doesn't cached yet, so try validate from database
                foundAmount = Database.FindCharacterName(request.CharacterName);
                // Cache character name, it will be used to validate later
                cachedCharacterNames.Add(request.CharacterName);
            }
            return new FindCharacterNameResp()
            {
                FoundAmount = foundAmount
            };
        }

        public override async Task<FindCharactersResp> FindCharacters(FindCharactersReq request, ServerCallContext context)
        {
            await Task.Yield();
            FindCharactersResp resp = new FindCharactersResp();
            DatabaseServiceUtils.CopyToRepeatedByteString(Database.FindCharacters(request.CharacterName), resp.List);
            return resp;
        }

        public override async Task<ReadFriendsResp> CreateFriend(CreateFriendReq request, ServerCallContext context)
        {
            List<SocialCharacterData> friends = await ReadFriends(request.Character1Id);
            // Update to cache
            SocialCharacterData character = await ReadSocialCharacter(request.Character2Id);
            friends.Add(character);
            cachedFriend[request.Character1Id] = friends;
            // Update to database
            Database.CreateFriend(request.Character1Id, character.id);
            ReadFriendsResp resp = new ReadFriendsResp();
            DatabaseServiceUtils.CopyToRepeatedByteString(friends, resp.List);
            return resp;
        }

        public override async Task<ReadFriendsResp> DeleteFriend(DeleteFriendReq request, ServerCallContext context)
        {
            await Task.Yield();
            List<SocialCharacterData> friends = await ReadFriends(request.Character1Id);
            // Update to cache
            for (int i = 0; i < friends.Count; ++i)
            {
                if (friends[i].id.Equals(request.Character2Id))
                {
                    friends.RemoveAt(i);
                    break;
                }
            }
            // Update to database
            Database.DeleteFriend(request.Character1Id, request.Character2Id);
            ReadFriendsResp resp = new ReadFriendsResp();
            DatabaseServiceUtils.CopyToRepeatedByteString(friends, resp.List);
            return resp;
        }

        public override async Task<ReadFriendsResp> ReadFriends(ReadFriendsReq request, ServerCallContext context)
        {
            ReadFriendsResp resp = new ReadFriendsResp();
            DatabaseServiceUtils.CopyToRepeatedByteString(await ReadFriends(request.CharacterId), resp.List);
            return resp;
        }

        public override async Task<BuildingResp> CreateBuilding(CreateBuildingReq request, ServerCallContext context)
        {
            await Task.Yield();
            BuildingSaveData building = DatabaseServiceUtils.FromByteString<BuildingSaveData>(request.BuildingData);
            // Cache building data
            if (cachedBuilding.ContainsKey(request.MapName))
                cachedBuilding[request.MapName][building.Id] = building;
            // Insert data to database
            Database.CreateBuilding(request.MapName, building);
            return new BuildingResp()
            {
                BuildingData = DatabaseServiceUtils.ToByteString(building)
            };
        }

        public override async Task<BuildingResp> UpdateBuilding(UpdateBuildingReq request, ServerCallContext context)
        {
            await Task.Yield();
            BuildingSaveData building = DatabaseServiceUtils.FromByteString<BuildingSaveData>(request.BuildingData);
            // Cache building data
            if (cachedBuilding.ContainsKey(request.MapName))
                cachedBuilding[request.MapName][building.Id] = building;
            // Update data to database
            Database.UpdateBuilding(request.MapName, building);
            return new BuildingResp()
            {
                BuildingData = DatabaseServiceUtils.ToByteString(building)
            };
        }

        public override async Task<VoidResp> DeleteBuilding(DeleteBuildingReq request, ServerCallContext context)
        {
            await Task.Yield();
            // Remove from cache
            if (cachedBuilding.ContainsKey(request.MapName))
                cachedBuilding[request.MapName].Remove(request.BuildingId);
            // Remove from database
            Database.DeleteBuilding(request.MapName, request.BuildingId);
            return new VoidResp();
        }

        public override async Task<BuildingsResp> ReadBuildings(ReadBuildingsReq request, ServerCallContext context)
        {
            await Task.Yield();
            BuildingsResp resp = new BuildingsResp();
            List<BuildingSaveData> buildings;
            if (cachedBuilding.ContainsKey(request.MapName))
            {
                // Get buildings from cache
                buildings = new List<BuildingSaveData>(cachedBuilding[request.MapName].Values);
            }
            else
            {
                // Store buildings to cache
                cachedBuilding[request.MapName] = new Dictionary<string, BuildingSaveData>();
                buildings = Database.ReadBuildings(request.MapName);
                foreach (BuildingSaveData building in buildings)
                {
                    cachedBuilding[request.MapName][building.Id] = building;
                }
            }
            DatabaseServiceUtils.CopyToRepeatedByteString(buildings, resp.List);
            return resp;
        }

        public override async Task<PartyResp> CreateParty(CreatePartyReq request, ServerCallContext context)
        {
            await Task.Yield();
            // Insert to database
            int partyId = Database.CreateParty(request.ShareExp, request.ShareItem, request.LeaderCharacterId);
            // Cached the data
            PartyData party = new PartyData(partyId, request.ShareExp, request.ShareItem, request.LeaderCharacterId);
            cachedParty[partyId] = party;
            return new PartyResp()
            {
                PartyData = DatabaseServiceUtils.ToByteString(party)
            };
        }

        public override async Task<PartyResp> UpdateParty(UpdatePartyReq request, ServerCallContext context)
        {
            PartyData party = await ReadParty(request.PartyId);
            // Update to cache
            party.Setting(request.ShareExp, request.ShareItem);
            cachedParty[request.PartyId] = party;
            // Update to database
            Database.UpdateParty(request.PartyId, request.ShareExp, request.ShareItem);
            return new PartyResp()
            {
                PartyData = DatabaseServiceUtils.ToByteString(party)
            };
        }

        public override async Task<PartyResp> UpdatePartyLeader(UpdatePartyLeaderReq request, ServerCallContext context)
        {
            PartyData party = await ReadParty(request.PartyId);
            // Update to cache
            party.SetLeader(request.LeaderCharacterId);
            cachedParty[request.PartyId] = party;
            // Update to database
            Database.UpdatePartyLeader(request.PartyId, request.LeaderCharacterId);
            return new PartyResp()
            {
                PartyData = DatabaseServiceUtils.ToByteString(party)
            };
        }

        public override async Task<VoidResp> DeleteParty(DeletePartyReq request, ServerCallContext context)
        {
            await Task.Yield();
            Database.DeleteParty(request.PartyId);
            return new VoidResp();
        }

        public override async Task<PartyResp> UpdateCharacterParty(UpdateCharacterPartyReq request, ServerCallContext context)
        {
            PartyData party = await ReadParty(request.PartyId);
            // Update to cache
            SocialCharacterData character = DatabaseServiceUtils.FromByteString<SocialCharacterData>(request.SocialCharacterData);
            party.AddMember(character);
            cachedParty[request.PartyId] = party;
            // Update to cached character
            if (cachedUserCharacter.ContainsKey(character.id))
                cachedUserCharacter[character.id].PartyId = request.PartyId;
            // Update to database
            Database.UpdateCharacterParty(character.id, request.PartyId);
            return new PartyResp()
            {
                PartyData = DatabaseServiceUtils.ToByteString(party)
            };
        }

        public override async Task<VoidResp> ClearCharacterParty(ClearCharacterPartyReq request, ServerCallContext context)
        {
            PlayerCharacterData character = await ReadCharacter(request.CharacterId);
            PartyData party = await ReadParty(character.PartyId);
            // Update to cache
            party.RemoveMember(request.CharacterId);
            cachedParty[character.PartyId] = party;
            // Update to cached character
            if (cachedUserCharacter.ContainsKey(request.CharacterId))
                cachedUserCharacter[request.CharacterId].PartyId = 0;
            // Update to database
            Database.UpdateCharacterParty(request.CharacterId, 0);
            return new VoidResp();
        }

        public override async Task<PartyResp> ReadParty(ReadPartyReq request, ServerCallContext context)
        {
            return new PartyResp()
            {
                PartyData = DatabaseServiceUtils.ToByteString(await ReadParty(request.PartyId))
            };
        }

        public override async Task<GuildResp> CreateGuild(CreateGuildReq request, ServerCallContext context)
        {
            await Task.Yield();
            // Insert to database
            int guildId = Database.CreateGuild(request.GuildName, request.LeaderCharacterId);
            // Cached the data
            GuildData guild = new GuildData(guildId, request.GuildName, request.LeaderCharacterId);
            cachedGuild[guildId] = guild;
            return new GuildResp()
            {
                GuildData = DatabaseServiceUtils.ToByteString(guild)
            };
        }

        public override async Task<GuildResp> UpdateGuildLeader(UpdateGuildLeaderReq request, ServerCallContext context)
        {
            GuildData guild = await ReadGuild(request.GuildId);
            // Update to cache
            guild.SetLeader(request.LeaderCharacterId);
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildLeader(request.GuildId, request.LeaderCharacterId);
            return new GuildResp()
            {
                GuildData = DatabaseServiceUtils.ToByteString(guild)
            };
        }

        public override async Task<GuildResp> UpdateGuildMessage(UpdateGuildMessageReq request, ServerCallContext context)
        {
            GuildData guild = await ReadGuild(request.GuildId);
            // Update to cache
            guild.guildMessage = request.GuildMessage;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildMessage(request.GuildId, request.GuildMessage);
            return new GuildResp()
            {
                GuildData = DatabaseServiceUtils.ToByteString(guild)
            };
        }

        public override async Task<GuildResp> UpdateGuildRole(UpdateGuildRoleReq request, ServerCallContext context)
        {
            GuildData guild = await ReadGuild(request.GuildId);
            // Update to cache
            guild.SetRole((byte)request.GuildRole, request.RoleName, request.CanInvite, request.CanKick, (byte)request.ShareExpPercentage);
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildRole(request.GuildId, (byte)request.GuildRole, request.RoleName, request.CanInvite, request.CanKick, (byte)request.ShareExpPercentage);
            return new GuildResp()
            {
                GuildData = DatabaseServiceUtils.ToByteString(guild)
            };
        }

        public override async Task<GuildResp> UpdateGuildMemberRole(UpdateGuildMemberRoleReq request, ServerCallContext context)
        {
            GuildData guild = await ReadGuild(request.GuildId);
            // Update to cache
            guild.SetMemberRole(request.MemberCharacterId, (byte)request.GuildRole);
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildMemberRole(request.MemberCharacterId, (byte)request.GuildRole);
            return new GuildResp()
            {
                GuildData = DatabaseServiceUtils.ToByteString(guild)
            };
        }

        public override async Task<VoidResp> DeleteGuild(DeleteGuildReq request, ServerCallContext context)
        {
            await Task.Yield();
            // Remove data from cache
            if (cachedGuild.ContainsKey(request.GuildId))
            {
                string characterName = cachedGuild[request.GuildId].guildName;
                cachedGuildNames.Remove(characterName);
                cachedGuild.Remove(request.GuildId);
            }
            // Remove data from database
            Database.DeleteGuild(request.GuildId);
            return new VoidResp();
        }

        public override async Task<GuildResp> UpdateCharacterGuild(UpdateCharacterGuildReq request, ServerCallContext context)
        {
            GuildData guild = await ReadGuild(request.GuildId);
            // Update to cache
            SocialCharacterData character = DatabaseServiceUtils.FromByteString<SocialCharacterData>(request.SocialCharacterData);
            guild.AddMember(character, (byte)request.GuildRole);
            cachedGuild[request.GuildId] = guild;
            // Update to cached character
            if (cachedUserCharacter.ContainsKey(character.id))
                cachedUserCharacter[character.id].GuildId = request.GuildId;
            // Update to database
            Database.UpdateCharacterGuild(character.id, request.GuildId, (byte)request.GuildRole);
            return new GuildResp()
            {
                GuildData = DatabaseServiceUtils.ToByteString(guild)
            };
        }

        public override async Task<VoidResp> ClearCharacterGuild(ClearCharacterGuildReq request, ServerCallContext context)
        {
            PlayerCharacterData character = await ReadCharacter(request.CharacterId);
            GuildData guild = await ReadGuild(character.GuildId);
            // Update to cache
            guild.RemoveMember(request.CharacterId);
            cachedGuild[character.GuildId] = guild;
            // Update to cached character
            if (cachedUserCharacter.ContainsKey(request.CharacterId))
            {
                cachedUserCharacter[request.CharacterId].GuildId = 0;
                cachedUserCharacter[request.CharacterId].GuildRole = 0;
            }
            // Update to database
            Database.UpdateCharacterGuild(request.CharacterId, 0, 0);
            return new VoidResp();
        }

        public override async Task<FindGuildNameResp> FindGuildName(FindGuildNameReq request, ServerCallContext context)
        {
            await Task.Yield();
            long foundAmount;
            if (cachedGuildNames.Contains(request.GuildName))
            {
                // Already cached username, so validate username from cache
                foundAmount = 1;
            }
            else
            {
                // Doesn't cached yet, so try validate from database
                foundAmount = Database.FindGuildName(request.GuildName);
                // Cache username, it will be used to validate later
                cachedGuildNames.Add(request.GuildName);
            }
            return new FindGuildNameResp()
            {
                FoundAmount = foundAmount
            };
        }

        public override async Task<GuildResp> ReadGuild(ReadGuildReq request, ServerCallContext context)
        {
            return new GuildResp()
            {
                GuildData = DatabaseServiceUtils.ToByteString(await ReadGuild(request.GuildId))
            };
        }

        public override async Task<GuildResp> IncreaseGuildExp(IncreaseGuildExpReq request, ServerCallContext context)
        {
            // TODO: May validate guild by character
            GuildData guild = await ReadGuild(request.GuildId);
            // Update to cache
            guild = GameInstance.Singleton.SocialSystemSetting.IncreaseGuildExp(guild, request.Exp);
            cachedGuild[guild.id] = guild;
            // Update to database
            Database.UpdateGuildLevel(request.GuildId, guild.level, guild.exp, guild.skillPoint);
            return new GuildResp()
            {
                GuildData = DatabaseServiceUtils.ToByteString(guild)
            };
        }

        public override async Task<GuildResp> AddGuildSkill(AddGuildSkillReq request, ServerCallContext context)
        {
            // TODO: May validate guild by character
            GuildData guild = await ReadGuild(request.GuildId);
            // Update to cache
            guild.AddSkillLevel(request.SkillId);
            cachedGuild[guild.id] = guild;
            // Update to database
            Database.UpdateGuildSkillLevel(request.GuildId, request.SkillId, guild.GetSkillLevel(request.SkillId), guild.skillPoint);
            return new GuildResp()
            {
                GuildData = DatabaseServiceUtils.ToByteString(guild)
            };
        }

        public override async Task<GuildGoldResp> GetGuildGold(GetGuildGoldReq request, ServerCallContext context)
        {
            GuildData guild = await ReadGuild(request.GuildId);
            return new GuildGoldResp()
            {
                GuildGold = guild.gold
            };
        }

        public override async Task<GuildGoldResp> ChangeGuildGold(ChangeGuildGoldReq request, ServerCallContext context)
        {
            GuildData guild = await ReadGuild(request.GuildId);
            // Update to cache
            guild.gold += request.ChangeAmount;
            cachedGuild[request.GuildId] = guild;
            // Update to database
            Database.UpdateGuildGold(request.GuildId, guild.gold);
            return new GuildGoldResp()
            {
                GuildGold = guild.gold
            };
        }

        public override async Task<ReadStorageItemsResp> ReadStorageItems(ReadStorageItemsReq request, ServerCallContext context)
        {
            await Task.Yield();
            ReadStorageItemsResp resp = new ReadStorageItemsResp();
            // Prepare storage data
            StorageId storageId = new StorageId((StorageType)request.StorageType, request.StorageOwnerId);
            List<CharacterItem> storageItems;
            if (cachedStorageItems.ContainsKey(storageId))
            {
                // Already cached data, so get data from cache
                storageItems = cachedStorageItems[storageId];
            }
            else
            {
                // Doesn't cached yet, so get data from database
                storageItems = Database.ReadStorageItems(storageId.storageType, storageId.storageOwnerId);
                // Cache data, it will be used to validate later
                if (storageItems != null)
                    cachedStorageItems[storageId] = storageItems;
            }
            DatabaseServiceUtils.CopyToRepeatedByteString(storageItems, resp.StorageCharacterItems);
            return resp;
        }

        public override async Task<MoveItemToStorageResp> MoveItemToStorage(MoveItemToStorageReq request, ServerCallContext context)
        {
            await Task.Yield();
            MoveItemToStorageResp resp = new MoveItemToStorageResp();
            // Prepare storage data
            StorageId storageId = new StorageId((StorageType)request.StorageType, request.StorageOwnerId);
            Storage storage;
            List<CharacterItem> storageItemList;
            if (!GetStorage(storageId, request.MapName, out storage) ||
                !cachedStorageItems.TryGetValue(storageId, out storageItemList))
            {
                // Cannot find storage
                resp.Error = EStorageError.StorageErrorInvalidStorage;
                return resp;
            }
            PlayerCharacterData character;
            if (!cachedUserCharacter.TryGetValue(request.CharacterId, out character))
            {
                // Cannot find character
                resp.Error = EStorageError.StorageErrorInvalidCharacter;
                return resp;
            }
            if (request.InventoryItemIndex <= 0 || 
                request.InventoryItemIndex > character.NonEquipItems.Count)
            {
                // Invalid inventory index
                resp.Error = EStorageError.StorageErrorInvalidInventoryIndex;
                return resp;
            }
            bool isLimitWeight = storage.weightLimit > 0;
            bool isLimitSlot = storage.slotLimit > 0;
            short weightLimit = storage.weightLimit;
            short slotLimit = storage.slotLimit;
            // Prepare character and item data
            CharacterItem movingItem = character.NonEquipItems[request.InventoryItemIndex].Clone();
            movingItem.id = GenericUtils.GetUniqueId();
            movingItem.amount = (short)request.InventoryItemAmount;
            if (request.StorageItemIndex < 0 ||
                request.StorageItemIndex >= storageItemList.Count ||
                storageItemList[request.StorageItemIndex].dataId == movingItem.dataId)
            {
                // Add to storage or merge
                bool isOverwhelming = storageItemList.IncreasingItemsWillOverwhelming(
                    movingItem.dataId, movingItem.amount, isLimitWeight, weightLimit,
                    storageItemList.GetTotalItemWeight(), isLimitSlot, slotLimit);
                if (isOverwhelming || !storageItemList.IncreaseItems(movingItem))
                {
                    // Storage will overwhelming
                    resp.Error = EStorageError.StorageErrorStorageWillOverwhelming;
                    return resp;
                }
                // Remove from inventory
                character.DecreaseItemsByIndex(request.InventoryItemIndex, (short)request.InventoryItemAmount);
                character.FillEmptySlots();
            }
            else
            {
                // Swapping
                CharacterItem storageItem = storageItemList[request.StorageItemIndex];
                CharacterItem nonEquipItem = character.NonEquipItems[request.InventoryItemIndex];
                storageItem.id = GenericUtils.GetUniqueId();
                nonEquipItem.id = GenericUtils.GetUniqueId();
                storageItemList[request.StorageItemIndex] = nonEquipItem;
                character.NonEquipItems[request.InventoryItemIndex] = storageItem;
            }
            storageItemList.FillEmptySlots(isLimitSlot, slotLimit);
            // Update storage list
            // TODO: May update later to reduce amount of processes
            Database.UpdateStorageItems((StorageType)request.StorageType, request.StorageOwnerId, storageItemList);
            resp.Error = EStorageError.StorageErrorNone;
            DatabaseServiceUtils.CopyToRepeatedByteString(character.NonEquipItems, resp.InventoryItemItems);
            DatabaseServiceUtils.CopyToRepeatedByteString(storageItemList, resp.StorageCharacterItems);
            return resp;
        }

        public override async Task<MoveItemFromStorageResp> MoveItemFromStorage(MoveItemFromStorageReq request, ServerCallContext context)
        {
            await Task.Yield();
            MoveItemFromStorageResp resp = new MoveItemFromStorageResp();
            StorageId storageId = new StorageId((StorageType)request.StorageType, request.StorageOwnerId);
            Storage storage;
            List<CharacterItem> storageItemList;
            if (!GetStorage(storageId, request.MapName, out storage) ||
                !cachedStorageItems.TryGetValue(storageId, out storageItemList))
            {
                // Cannot find storage
                resp.Error = EStorageError.StorageErrorInvalidStorage;
                return resp;
            }
            PlayerCharacterData character;
            if (!cachedUserCharacter.TryGetValue(request.CharacterId, out character))
            {
                // Cannot find character
                resp.Error = EStorageError.StorageErrorInvalidCharacter;
                return resp;
            }
            if (request.StorageItemIndex <= 0 ||
                request.StorageItemIndex > storageItemList.Count)
            {
                // Invalid storage index
                resp.Error = EStorageError.StorageErrorInvalidStorageIndex;
                return resp;
            }
            bool isLimitSlot = storage.slotLimit > 0;
            short slotLimit = storage.slotLimit;
            // Prepare item data
            CharacterItem movingItem = storageItemList[request.StorageItemIndex].Clone();
            movingItem.id = GenericUtils.GetUniqueId();
            movingItem.amount = (short)request.StorageItemAmount;
            if (request.InventoryItemIndex < 0 ||
                request.InventoryItemIndex >= character.NonEquipItems.Count ||
                character.NonEquipItems[request.InventoryItemIndex].dataId == movingItem.dataId)
            {
                // Add to inventory or merge
                bool isOverwhelming = character.IncreasingItemsWillOverwhelming(movingItem.dataId, movingItem.amount);
                if (isOverwhelming || !character.IncreaseItems(movingItem))
                {
                    // inventory will overwhelming
                    resp.Error = EStorageError.StorageErrorInventoryWillOverwhelming;
                    return resp;
                }
                // Remove from storage
                storageItemList.DecreaseItemsByIndex(request.StorageItemIndex, (short)request.StorageItemAmount, isLimitSlot);
            }
            else
            {
                // Swapping
                CharacterItem storageItem = storageItemList[request.StorageItemIndex];
                CharacterItem nonEquipItem = character.NonEquipItems[request.InventoryItemIndex];
                storageItem.id = GenericUtils.GetUniqueId();
                nonEquipItem.id = GenericUtils.GetUniqueId();
                storageItemList[request.StorageItemIndex] = nonEquipItem;
                character.NonEquipItems[request.InventoryItemIndex] = storageItem;
            }
            storageItemList.FillEmptySlots(isLimitSlot, slotLimit);
            character.FillEmptySlots();
            // Update storage list
            // TODO: May update later to reduce amount of processes
            Database.UpdateStorageItems((StorageType)request.StorageType, request.StorageOwnerId, storageItemList);
            resp.Error = EStorageError.StorageErrorNone;
            DatabaseServiceUtils.CopyToRepeatedByteString(character.NonEquipItems, resp.InventoryItemItems);
            DatabaseServiceUtils.CopyToRepeatedByteString(storageItemList, resp.StorageCharacterItems);
            return resp;
        }

        public override async Task<SwapOrMergeStorageItemResp> SwapOrMergeStorageItem(SwapOrMergeStorageItemReq request, ServerCallContext context)
        {
            await Task.Yield();
            SwapOrMergeStorageItemResp resp = new SwapOrMergeStorageItemResp();
            // Prepare storage data
            StorageId storageId = new StorageId((StorageType)request.StorageType, request.StorageOwnerId);
            Storage storage;
            List<CharacterItem> storageItemList;
            if (!GetStorage(storageId, request.MapName, out storage) ||
                !cachedStorageItems.TryGetValue(storageId, out storageItemList))
            {
                // Cannot find storage
                resp.Error = EStorageError.StorageErrorInvalidStorage;
                return resp;
            }
            bool isLimitSlot = storage.slotLimit > 0;
            short slotLimit = storage.slotLimit;
            // Prepare item data
            CharacterItem fromItem = storageItemList[request.FromIndex];
            CharacterItem toItem = storageItemList[request.ToIndex];
            fromItem.id = GenericUtils.GetUniqueId();
            toItem.id = GenericUtils.GetUniqueId();
            if (fromItem.dataId.Equals(toItem.dataId) && !fromItem.IsFull() && !toItem.IsFull())
            {
                // Merge if same id and not full
                short maxStack = toItem.GetMaxStack();
                if (toItem.amount + fromItem.amount <= maxStack)
                {
                    toItem.amount += fromItem.amount;
                    storageItemList[request.FromIndex] = CharacterItem.Empty;
                    storageItemList[request.ToIndex] = toItem;
                }
                else
                {
                    short remains = (short)(toItem.amount + fromItem.amount - maxStack);
                    toItem.amount = maxStack;
                    fromItem.amount = remains;
                    storageItemList[request.FromIndex] = fromItem;
                    storageItemList[request.ToIndex] = toItem;
                }
            }
            else
            {
                // Swap
                storageItemList[request.FromIndex] = toItem;
                storageItemList[request.ToIndex] = fromItem;
            }
            storageItemList.FillEmptySlots(isLimitSlot, slotLimit);
            // Update storage list
            // TODO: May update later to reduce amount of processes
            Database.UpdateStorageItems((StorageType)request.StorageType, request.StorageOwnerId, storageItemList);
            resp.Error = EStorageError.StorageErrorNone;
            DatabaseServiceUtils.CopyToRepeatedByteString(storageItemList, resp.StorageCharacterItems);
            return resp;
        }

        public override async Task<IncreaseStorageItemsResp> IncreaseStorageItems(IncreaseStorageItemsReq request, ServerCallContext context)
        {
            await Task.Yield();
            IncreaseStorageItemsResp resp = new IncreaseStorageItemsResp();
            // Prepare storage data
            StorageId storageId = new StorageId((StorageType)request.StorageType, request.StorageOwnerId);
            Storage storage;
            List<CharacterItem> storageItemList;
            if (!GetStorage(storageId, request.MapName, out storage) ||
                !cachedStorageItems.TryGetValue(storageId, out storageItemList))
            {
                // Cannot find storage
                resp.Error = EStorageError.StorageErrorInvalidStorage;
                return resp;
            }
            bool isLimitWeight = storage.weightLimit > 0;
            bool isLimitSlot = storage.slotLimit > 0;
            short weightLimit = storage.weightLimit;
            short slotLimit = storage.slotLimit;
            CharacterItem addingItem = DatabaseServiceUtils.FromByteString<CharacterItem>(request.Item);
            // Increase item to storage
            bool isOverwhelming = storageItemList.IncreasingItemsWillOverwhelming(
                addingItem.dataId, addingItem.amount, isLimitWeight, weightLimit,
                storageItemList.GetTotalItemWeight(), isLimitSlot, slotLimit);
            if (isOverwhelming || !storageItemList.IncreaseItems(addingItem))
            {
                // Storage will overwhelming
                resp.Error = EStorageError.StorageErrorStorageWillOverwhelming;
                return resp;
            }
            storageItemList.FillEmptySlots(isLimitSlot, slotLimit);
            // Update storage list
            // TODO: May update later to reduce amount of processes
            Database.UpdateStorageItems((StorageType)request.StorageType, request.StorageOwnerId, storageItemList);
            resp.Error = EStorageError.StorageErrorNone;
            DatabaseServiceUtils.CopyToRepeatedByteString(storageItemList, resp.StorageCharacterItems);
            return resp;
        }

        public override async Task<DecreaseStorageItemsResp> DecreaseStorageItems(DecreaseStorageItemsReq request, ServerCallContext context)
        {
            await Task.Yield();
            DecreaseStorageItemsResp resp = new DecreaseStorageItemsResp();
            // Prepare storage data
            StorageId storageId = new StorageId((StorageType)request.StorageType, request.StorageOwnerId);
            Storage storage;
            List<CharacterItem> storageItemList;
            if (!GetStorage(storageId, request.MapName, out storage) ||
                !cachedStorageItems.TryGetValue(storageId, out storageItemList))
            {
                // Cannot find storage
                resp.Error = EStorageError.StorageErrorInvalidStorage;
                return resp;
            }
            bool isLimitSlot = storage.slotLimit > 0;
            short slotLimit = storage.slotLimit;
            // Increase item to storage
            Dictionary<int, short> decreaseItems;
            if (!storageItemList.DecreaseItems(request.DataId, (short)request.Amount, isLimitSlot, out decreaseItems))
            {
                resp.Error = EStorageError.StorageErrorDecreaseItemNotEnough;
                return resp;
            }
            storageItemList.FillEmptySlots(isLimitSlot, slotLimit);
            // Update storage list
            // TODO: May update later to reduce amount of processes
            Database.UpdateStorageItems((StorageType)request.StorageType, request.StorageOwnerId, storageItemList);
            resp.Error = EStorageError.StorageErrorNone;
            DatabaseServiceUtils.CopyToRepeatedByteString(storageItemList, resp.StorageCharacterItems);
            foreach (int itemIndex in decreaseItems.Keys)
            {
                resp.DecreasedItems.Add(new ItemIndexAmountMap()
                {
                    Index = itemIndex,
                    Amount = decreaseItems[itemIndex]
                });
            }
            return resp;
        }

        public override async Task<CustomResp> Custom(CustomReq request, ServerCallContext context)
        {
            return await onCustomRequest.Invoke(request.Type, request.Data);
        }

        public async Task<int> ReadGold(string userId)
        {
            await Task.Yield();
            int gold;
            if (cachedUserGold.ContainsKey(userId))
            {
                // Already cached data, so get data from cache
                gold = cachedUserGold[userId];
            }
            else
            {
                // Doesn't cached yet, so get data from database and cache it
                gold = Database.GetGold(userId);
                cachedUserGold[userId] = gold;
            }
            return gold;
        }

        public async Task<int> ReadCash(string userId)
        {
            await Task.Yield();
            int cash;
            if (cachedUserCash.ContainsKey(userId))
            {
                // Already cached data, so get data from cache
                cash = cachedUserCash[userId];
            }
            else
            {
                // Doesn't cached yet, so get data from database and cache it
                cash = Database.GetCash(userId);
                cachedUserCash[userId] = cash;
            }
            return cash;
        }

        public async Task<PlayerCharacterData> ReadCharacter(string id)
        {
            await Task.Yield();
            PlayerCharacterData character;
            if (cachedUserCharacter.ContainsKey(id))
            {
                // Already cached data, so get data from cache
                character = cachedUserCharacter[id];
            }
            else
            {
                // Doesn't cached yet, so get data from database
                character = Database.ReadCharacter(id);
                // Cache character, it will be used to validate later
                if (character != null)
                {
                    cachedUserCharacter[id] = character;
                    cachedCharacterNames.Add(character.CharacterName);
                }
            }
            return character;
        }

        public async Task<SocialCharacterData> ReadSocialCharacter(string id)
        {
            await Task.Yield();
            //cachedSocialCharacter
            SocialCharacterData character;
            if (cachedSocialCharacter.ContainsKey(id))
            {
                // Already cached data, so get data from cache
                character = cachedSocialCharacter[id];
            }
            else
            {
                // Doesn't cached yet, so get data from database
                character = SocialCharacterData.Create(Database.ReadCharacter(id, false, false, false, false, false, false, false, false, false, false));
                // Cache the data
                cachedSocialCharacter[id] = character;
            }
            return character;
        }

        public async Task<List<SocialCharacterData>> ReadFriends(string id)
        {
            await Task.Yield();
            List<SocialCharacterData> friends;
            if (cachedFriend.ContainsKey(id))
            {
                // Already cached data, so get data from cache
                friends = cachedFriend[id];
            }
            else
            {
                // Doesn't cached yet, so get data from database
                friends = Database.ReadFriends(id);
                // Cache the data
                if (friends != null)
                {
                    cachedFriend[id] = friends;
                    CacheSocialCharacters(friends);
                }
            }
            return friends;
        }

        public async Task<PartyData> ReadParty(int id)
        {
            await Task.Yield();
            PartyData party;
            if (cachedParty.ContainsKey(id))
            {
                // Already cached data, so get data from cache
                party = cachedParty[id];
            }
            else
            {
                // Doesn't cached yet, so get data from database
                party = Database.ReadParty(id);
                // Cache the data
                if (party != null)
                {
                    cachedParty[id] = party;
                    CacheSocialCharacters(party.GetMembers());
                }
            }
            return party;
        }

        public async Task<GuildData> ReadGuild(int id)
        {
            await Task.Yield();
            GuildData guild;
            if (cachedGuild.ContainsKey(id))
            {
                // Already cached data, so get data from cache
                guild = cachedGuild[id];
            }
            else
            {
                // Doesn't cached yet, so get data from database
                guild = Database.ReadGuild(id, GameInstance.Singleton.SocialSystemSetting.GuildMemberRoles);
                // Cache the data
                if (guild != null)
                {
                    cachedGuild[id] = guild;
                    cachedGuildNames.Add(guild.guildName);
                    CacheSocialCharacters(guild.GetMembers());
                }
            }
            return guild;
        }

        public void CacheSocialCharacters(IEnumerable<SocialCharacterData> socialCharacters)
        {
            foreach (SocialCharacterData socialCharacter in socialCharacters)
            {
                cachedSocialCharacter[socialCharacter.id] = socialCharacter;
            }
        }

        public bool GetStorage(StorageId storageId, string mapName, out Storage storage)
        {
            storage = default(Storage);
            switch (storageId.storageType)
            {
                case StorageType.Player:
                    // Get storage setting from game instance
                    storage = GameInstance.Singleton.playerStorage;
                    break;
                case StorageType.Guild:
                    // Get storage setting from game instance
                    storage = GameInstance.Singleton.guildStorage;
                    break;
                case StorageType.Building:
                    // Get building from cache, then get building entity from game instance
                    // And get storage setting from building entity
                    BuildingSaveData building;
                    BuildingEntity buildingEntity;
                    if (cachedBuilding.ContainsKey(mapName) &&
                        cachedBuilding[mapName].TryGetValue(storageId.storageOwnerId, out building) &&
                        GameInstance.BuildingEntities.TryGetValue(building.EntityId, out buildingEntity) &&
                        buildingEntity is StorageEntity)
                        storage = (buildingEntity as StorageEntity).storage;
                    else
                        return false;
                    break;
            }
            return true;
        }
    }
}
