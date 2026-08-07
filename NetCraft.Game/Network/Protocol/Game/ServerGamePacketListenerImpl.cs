using NetCraft.Game.Network.Protocol.Common;
using NetCraft.Game.Network.Protocol.Cookie;
using NetCraft.Game.Network.Protocol.Login;
using NetCraft.Game.Network.Protocol.Ping;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Network.Protocol;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerGamePacketListenerImpl 服务端 play 阶段监听器实现
//对应原版 ServerGamePacketListenerImpl
//核心 HandleChat/HandleMovePlayer/HandleClientCommand/HandleAcceptTeleportation 等注册包 Log.Debug
//其余继承自 ServerCommonPacketListener/ServerCookiePacketListener/ServerPingPacketListener 的方法暂空实现
//Protocol 显式返回 Play 因继承链默认是 Configuration
public sealed class ServerGamePacketListenerImpl : ServerGamePacketListener
{
    private readonly Connection _connection;
    private readonly GameProfile _profile;

    public ServerGamePacketListenerImpl(Connection connection, GameProfile profile)
    {
        _connection = connection;
        _profile = profile;
    }

    //显式返回 Play 因 ServerCookiePacketListener 默认 Protocol 是 Configuration
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Play;

    //HandleChat 玩家聊天包本轮 Log.Debug 记录未来接入 PlayerList.BroadcastAll
    public void HandleChat(ServerboundChatPacket packet)
    {
        Log.Debug($"HandleChat profile={_profile.Name} message={packet.Message}");
    }

    //HandleMovePlayer 玩家移动包 Log.Debug 记录坐标
    public void HandleMovePlayer(ServerboundMovePlayerPacket packet)
    {
        Log.Debug($"HandleMovePlayer profile={_profile.Name}");
    }

    //HandleClientCommand 客户端命令包如请求 respawn Log.Debug
    public void HandleClientCommand(ServerboundClientCommandPacket packet)
    {
        Log.Debug($"HandleClientCommand profile={_profile.Name}");
    }

    //HandleAcceptTeleportation 接受传送空实现
    public void HandleAcceptTeleportPacket(ServerboundAcceptTeleportationPacket packet)
    {
        Log.Debug($"HandleAcceptTeleportation profile={_profile.Name}");
    }

    //以下 54 个 ServerGamePacketListener 自有方法本轮未注册对应 PacketType 不会解码到空实现
    public void HandleAnimate(ServerboundSwingPacket packet) { }
    public void HandleChatCommand(ServerboundChatCommandPacket packet) { }
    public void HandleSignedChatCommand(ServerboundChatCommandSignedPacket packet) { }
    public void HandleChatAck(ServerboundChatAckPacket packet) { }
    public void HandleContainerButtonClick(ServerboundContainerButtonClickPacket packet) { }
    public void HandleContainerClick(ServerboundContainerClickPacket packet) { }
    public void HandlePlaceRecipe(ServerboundPlaceRecipePacket packet) { }
    public void HandleContainerClose(ServerboundContainerClosePacket packet) { }
    public void HandleAttack(ServerboundAttackPacket packet) { }
    public void HandleInteract(ServerboundInteractPacket packet) { }
    public void HandleSpectatorAction(ServerboundSpectatorActionPacket packet) { }
    public void HandlePlayerAbilities(ServerboundPlayerAbilitiesPacket packet) { }
    public void HandlePlayerAction(ServerboundPlayerActionPacket packet) { }
    public void HandlePlayerCommand(ServerboundPlayerCommandPacket packet) { }
    public void HandlePlayerInput(ServerboundPlayerInputPacket packet) { }
    public void HandleSetCarriedItem(ServerboundSetCarriedItemPacket packet) { }
    public void HandleSetCreativeModeSlot(ServerboundSetCreativeModeSlotPacket packet) { }
    public void HandleSignUpdate(ServerboundSignUpdatePacket packet) { }
    public void HandleUseItemOn(ServerboundUseItemOnPacket packet) { }
    public void HandleUseItem(ServerboundUseItemPacket packet) { }
    public void HandleTeleportToEntityPacket(ServerboundTeleportToEntityPacket packet) { }
    public void HandlePaddleBoat(ServerboundPaddleBoatPacket packet) { }
    public void HandleMoveVehicle(ServerboundMoveVehiclePacket packet) { }
    public void HandleAcceptPlayerLoad(ServerboundPlayerLoadedPacket packet) { }
    public void HandleRecipeBookSeenRecipePacket(ServerboundRecipeBookSeenRecipePacket packet) { }
    public void HandleBundleItemSelectedPacket(ServerboundSelectBundleItemPacket packet) { }
    public void HandleRecipeBookChangeSettingsPacket(ServerboundRecipeBookChangeSettingsPacket packet) { }
    public void HandleSeenAdvancements(ServerboundSeenAdvancementsPacket packet) { }
    public void HandleCustomCommandSuggestions(ServerboundCommandSuggestionPacket packet) { }
    public void HandleSetCommandBlock(ServerboundSetCommandBlockPacket packet) { }
    public void HandleSetCommandMinecart(ServerboundSetCommandMinecartPacket packet) { }
    public void HandlePickItemFromBlock(ServerboundPickItemFromBlockPacket packet) { }
    public void HandlePickItemFromEntity(ServerboundPickItemFromEntityPacket packet) { }
    public void HandleRenameItem(ServerboundRenameItemPacket packet) { }
    public void HandleSetBeaconPacket(ServerboundSetBeaconPacket packet) { }
    public void HandleSetGameRule(ServerboundSetGameRulePacket packet) { }
    public void HandleSetStructureBlock(ServerboundSetStructureBlockPacket packet) { }
    public void HandleSetTestBlock(ServerboundSetTestBlockPacket packet) { }
    public void HandleTestInstanceBlockAction(ServerboundTestInstanceBlockActionPacket packet) { }
    public void HandleSelectTrade(ServerboundSelectTradePacket packet) { }
    public void HandleEditBook(ServerboundEditBookPacket packet) { }
    public void HandleEntityTagQuery(ServerboundEntityTagQueryPacket packet) { }
    public void HandleContainerSlotStateChanged(ServerboundContainerSlotStateChangedPacket packet) { }
    public void HandleBlockEntityTagQuery(ServerboundBlockEntityTagQueryPacket packet) { }
    public void HandleSetJigsawBlock(ServerboundSetJigsawBlockPacket packet) { }
    public void HandleJigsawGenerate(ServerboundJigsawGeneratePacket packet) { }
    public void HandleChangeDifficulty(ServerboundChangeDifficultyPacket packet) { }
    public void HandleChangeGameMode(ServerboundChangeGameModePacket packet) { }
    public void HandleLockDifficulty(ServerboundLockDifficultyPacket packet) { }
    public void HandleChatSessionUpdate(ServerboundChatSessionUpdatePacket packet) { }
    public void HandleConfigurationAcknowledged(ServerboundConfigurationAcknowledgedPacket packet) { }
    public void HandleChunkBatchReceived(ServerboundChunkBatchReceivedPacket packet) { }
    public void HandleDebugSubscriptionRequest(ServerboundDebugSubscriptionRequestPacket packet) { }
    public void HandleClientTickEnd(ServerboundClientTickEndPacket packet) { }

    //以下继承自 ServerCommonPacketListener 的 6 个方法本轮空实现
    public void HandleClientInformation(ServerboundClientInformationPacket packet) { }
    public void HandleCustomPayload(ServerboundCustomPayloadPacket packet) { }
    public void HandleKeepAlive(ServerboundKeepAlivePacket packet) { }
    public void HandlePong(ServerboundPongPacket packet) { }
    public void HandleResourcePack(ServerboundResourcePackPacket packet) { }
    public void HandleCustomClickAction(ServerboundCustomClickActionPacket packet) { }

    //继承自 ServerCookiePacketListener
    public void HandleCookieResponse(ServerboundCookieResponsePacket packet) { }

    //继承自 ServerPingPacketListener
    public void HandlePingRequest(ServerboundPingRequestPacket packet) { }

    public void OnDisconnect(string reason)
    {
        Log.Info($"play 阶段断连 reason={reason} profile={_profile.Name}");
    }
}
