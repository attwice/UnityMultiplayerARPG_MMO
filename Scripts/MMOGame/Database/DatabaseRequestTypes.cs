﻿namespace MultiplayerARPG.MMO
{
    public static partial class DatabaseRequestTypes
    {
        public const ushort RequestValidateUserLogin = 1;
        public const ushort RequestValidateAccessToken = 2;
        public const ushort RequestGetUserLevel = 3;
        public const ushort RequestGetGold = 4;
        public const ushort RequestChangeGold = 5;
        public const ushort RequestGetCash = 6;
        public const ushort RequestChangeCash = 7;
        public const ushort RequestUpdateAccessToken = 8;
        public const ushort RequestCreateUserLogin = 9;
        public const ushort RequestFindUsername = 10;
        public const ushort RequestCreateCharacter = 11;
        public const ushort RequestReadCharacter = 12;
        public const ushort RequestReadCharacters = 13;
        public const ushort RequestUpdateCharacter = 14;
        public const ushort RequestDeleteCharacter = 15;
        public const ushort RequestFindCharacterName = 16;
        public const ushort RequestFindCharacters = 17;
        public const ushort RequestCreateFriend = 18;
        public const ushort RequestDeleteFriend = 19;
        public const ushort RequestReadFriends = 20;
        public const ushort RequestCreateBuilding = 21;
        public const ushort RequestUpdateBuilding = 22;
        public const ushort RequestDeleteBuilding = 23;
        public const ushort RequestReadBuildings = 24;
        public const ushort RequestCreateParty = 25;
        public const ushort RequestUpdateParty = 26;
        public const ushort RequestUpdatePartyLeader = 27;
        public const ushort RequestDeleteParty = 28;
        public const ushort RequestUpdateCharacterParty = 29;
        public const ushort RequestClearCharacterParty = 30;
        public const ushort RequestReadParty = 31;
        public const ushort RequestCreateGuild = 32;
        public const ushort RequestUpdateGuildLeader = 33;
        public const ushort RequestUpdateGuildMessage = 34;
        public const ushort RequestUpdateGuildRole = 35;
        public const ushort RequestUpdateGuildMemberRole = 36;
        public const ushort RequestDeleteGuild = 37;
        public const ushort RequestUpdateCharacterGuild = 38;
        public const ushort RequestClearCharacterGuild = 39;
        public const ushort RequestFindGuildName = 40;
        public const ushort RequestReadGuild = 41;
        public const ushort RequestIncreaseGuildExp = 42;
        public const ushort RequestAddGuildSkill = 43;
        public const ushort RequestGetGuildGold = 44;
        public const ushort RequestChangeGuildGold = 45;
        public const ushort RequestReadStorageItems = 46;
        public const ushort RequestMoveItemToStorage = 47;
        public const ushort RequestMoveItemFromStorage = 48;
        public const ushort RequestSwapOrMergeStorageItem = 49;
        public const ushort RequestIncreaseStorageItems = 50;
        public const ushort RequestDecreaseStorageItems = 51;
        public const ushort RequestMailList = 52;
        public const ushort RequestUpdateReadMailState = 53;
        public const ushort RequestUpdateClaimMailItemsState = 54;
        public const ushort RequestUpdateDeleteMailState = 55;
        public const ushort RequestSendMail = 56;
        public const ushort RequestGetMail = 57;
        public const ushort RequestGetIdByCharacterName = 58;
        public const ushort RequestGetUserIdByCharacterName = 59;
        public const ushort RequestUpdateGuildMessage2 = 60;
        public const ushort RequestUpdateGuildScore = 61;
        public const ushort RequestUpdateGuildOptions = 62;
        public const ushort RequestUpdateGuildAutoAcceptRequests = 67;
        public const ushort RequestUpdateGuildRank = 68;
        public const ushort RequestGetMailNotificationCount = 69;
        public const ushort RequestGetUserUnbanTime = 70;
        public const ushort RequestSetUserUnbanTimeByCharacterName = 71;
        public const ushort RequestSetCharacterUnmuteTimeByName = 72;
        public const ushort RequestGetSummonBuffs = 73;
        public const ushort RequestSetSummonBuffs = 74;
        public const ushort RequestValidateEmailVerification = 75;
        public const ushort RequestFindEmail = 76;
    }
}