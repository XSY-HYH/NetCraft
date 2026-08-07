namespace NetCraft.Game.Network.Protocol.Game;

//GamePacketTypes Play 阶段所有包类型注册对应原版 GamePacketTypes
//整合 game 阶段所有 Clientbound+Serverbound 包类型共 188 个
//按原版 GameProtocols.CLIENTBOUND_TEMPLATE/SERVERBOUND_TEMPLATE 顺序分配 id
//common 包和 cookie 包复用 Configuration 注册暂时不再注册到 Play 协议避免重复
//显式指定 Create THandler 类型参数避免 C# 类型推断失败
public static class GamePacketTypes
{
    //Clientbound (Play, Clientbound) 0-109 按原版 GameProtocols.CLIENTBOUND_TEMPLATE 顺序

    public static readonly PacketType<ClientGamePacketListener> ClientboundBundle =
        Create<ClientGamePacketListener>(0, "bundle");
    public static readonly PacketType<ClientGamePacketListener> ClientboundBundleDelimiter =
        Create<ClientGamePacketListener>(1, "bundle_delimiter");
    public static readonly PacketType<ClientGamePacketListener> ClientboundAddEntity =
        Create<ClientGamePacketListener>(2, "add_entity");
    public static readonly PacketType<ClientGamePacketListener> ClientboundAnimate =
        Create<ClientGamePacketListener>(3, "animate");
    public static readonly PacketType<ClientGamePacketListener> ClientboundAwardStats =
        Create<ClientGamePacketListener>(4, "award_stats");
    public static readonly PacketType<ClientGamePacketListener> ClientboundBlockChangedAck =
        Create<ClientGamePacketListener>(5, "block_changed_ack");
    public static readonly PacketType<ClientGamePacketListener> ClientboundBlockDestruction =
        Create<ClientGamePacketListener>(6, "block_destruction");
    public static readonly PacketType<ClientGamePacketListener> ClientboundBlockEntityData =
        Create<ClientGamePacketListener>(7, "block_entity_data");
    public static readonly PacketType<ClientGamePacketListener> ClientboundBlockEvent =
        Create<ClientGamePacketListener>(8, "block_event");
    public static readonly PacketType<ClientGamePacketListener> ClientboundBlockUpdate =
        Create<ClientGamePacketListener>(9, "block_update");
    public static readonly PacketType<ClientGamePacketListener> ClientboundBossEvent =
        Create<ClientGamePacketListener>(10, "boss_event");
    public static readonly PacketType<ClientGamePacketListener> ClientboundChangeDifficulty =
        Create<ClientGamePacketListener>(11, "change_difficulty");
    public static readonly PacketType<ClientGamePacketListener> ClientboundChunkBatchFinished =
        Create<ClientGamePacketListener>(12, "chunk_batch_finished");
    public static readonly PacketType<ClientGamePacketListener> ClientboundChunkBatchStart =
        Create<ClientGamePacketListener>(13, "chunk_batch_start");
    public static readonly PacketType<ClientGamePacketListener> ClientboundChunksBiomes =
        Create<ClientGamePacketListener>(14, "chunks_biomes");
    public static readonly PacketType<ClientGamePacketListener> ClientboundClearTitles =
        Create<ClientGamePacketListener>(15, "clear_titles");
    public static readonly PacketType<ClientGamePacketListener> ClientboundCommandSuggestions =
        Create<ClientGamePacketListener>(16, "command_suggestions");
    public static readonly PacketType<ClientGamePacketListener> ClientboundCommands =
        Create<ClientGamePacketListener>(17, "commands");
    public static readonly PacketType<ClientGamePacketListener> ClientboundContainerClose =
        Create<ClientGamePacketListener>(18, "container_close");
    public static readonly PacketType<ClientGamePacketListener> ClientboundContainerSetContent =
        Create<ClientGamePacketListener>(19, "container_set_content");
    public static readonly PacketType<ClientGamePacketListener> ClientboundContainerSetData =
        Create<ClientGamePacketListener>(20, "container_set_data");
    public static readonly PacketType<ClientGamePacketListener> ClientboundContainerSetSlot =
        Create<ClientGamePacketListener>(21, "container_set_slot");
    public static readonly PacketType<ClientGamePacketListener> ClientboundCooldown =
        Create<ClientGamePacketListener>(22, "cooldown");
    public static readonly PacketType<ClientGamePacketListener> ClientboundCustomChatCompletions =
        Create<ClientGamePacketListener>(23, "custom_chat_completions");
    public static readonly PacketType<ClientGamePacketListener> ClientboundDamageEvent =
        Create<ClientGamePacketListener>(24, "damage_event");
    public static readonly PacketType<ClientGamePacketListener> ClientboundDebugBlockValue =
        Create<ClientGamePacketListener>(25, "debug/block_value");
    public static readonly PacketType<ClientGamePacketListener> ClientboundDebugChunkValue =
        Create<ClientGamePacketListener>(26, "debug/chunk_value");
    public static readonly PacketType<ClientGamePacketListener> ClientboundDebugEntityValue =
        Create<ClientGamePacketListener>(27, "debug/entity_value");
    public static readonly PacketType<ClientGamePacketListener> ClientboundDebugEvent =
        Create<ClientGamePacketListener>(28, "debug/event");
    public static readonly PacketType<ClientGamePacketListener> ClientboundDebugSample =
        Create<ClientGamePacketListener>(29, "debug_sample");
    public static readonly PacketType<ClientGamePacketListener> ClientboundDeleteChat =
        Create<ClientGamePacketListener>(30, "delete_chat");
    public static readonly PacketType<ClientGamePacketListener> ClientboundDisguisedChat =
        Create<ClientGamePacketListener>(31, "disguised_chat");
    public static readonly PacketType<ClientGamePacketListener> ClientboundEntityEvent =
        Create<ClientGamePacketListener>(32, "entity_event");
    public static readonly PacketType<ClientGamePacketListener> ClientboundEntityPositionSync =
        Create<ClientGamePacketListener>(33, "entity_position_sync");
    public static readonly PacketType<ClientGamePacketListener> ClientboundExplode =
        Create<ClientGamePacketListener>(34, "explode");
    public static readonly PacketType<ClientGamePacketListener> ClientboundForgetLevelChunk =
        Create<ClientGamePacketListener>(35, "forget_level_chunk");
    public static readonly PacketType<ClientGamePacketListener> ClientboundGameEvent =
        Create<ClientGamePacketListener>(36, "game_event");
    public static readonly PacketType<ClientGamePacketListener> ClientboundGameTestHighlightPos =
        Create<ClientGamePacketListener>(37, "game_test_highlight_pos");
    public static readonly PacketType<ClientGamePacketListener> ClientboundMountScreenOpen =
        Create<ClientGamePacketListener>(38, "mount_screen_open");
    public static readonly PacketType<ClientGamePacketListener> ClientboundHurtAnimation =
        Create<ClientGamePacketListener>(39, "hurt_animation");
    public static readonly PacketType<ClientGamePacketListener> ClientboundInitializeBorder =
        Create<ClientGamePacketListener>(40, "initialize_border");
    public static readonly PacketType<ClientGamePacketListener> ClientboundLevelChunkWithLight =
        Create<ClientGamePacketListener>(41, "level_chunk_with_light");
    public static readonly PacketType<ClientGamePacketListener> ClientboundLevelEvent =
        Create<ClientGamePacketListener>(42, "level_event");
    public static readonly PacketType<ClientGamePacketListener> ClientboundLevelParticles =
        Create<ClientGamePacketListener>(43, "level_particles");
    public static readonly PacketType<ClientGamePacketListener> ClientboundLightUpdate =
        Create<ClientGamePacketListener>(44, "light_update");
    public static readonly PacketType<ClientGamePacketListener> ClientboundLogin =
        Create<ClientGamePacketListener>(45, "login");
    public static readonly PacketType<ClientGamePacketListener> ClientboundLowDiskSpaceWarning =
        Create<ClientGamePacketListener>(46, "low_disk_space_warning");
    public static readonly PacketType<ClientGamePacketListener> ClientboundMapItemData =
        Create<ClientGamePacketListener>(47, "map_item_data");
    public static readonly PacketType<ClientGamePacketListener> ClientboundMerchantOffers =
        Create<ClientGamePacketListener>(48, "merchant_offers");
    public static readonly PacketType<ClientGamePacketListener> ClientboundMoveEntityPos =
        Create<ClientGamePacketListener>(49, "move_entity_pos");
    public static readonly PacketType<ClientGamePacketListener> ClientboundMoveEntityPosRot =
        Create<ClientGamePacketListener>(50, "move_entity_pos_rot");
    public static readonly PacketType<ClientGamePacketListener> ClientboundMoveMinecartAlongTrack =
        Create<ClientGamePacketListener>(51, "move_minecart_along_track");
    public static readonly PacketType<ClientGamePacketListener> ClientboundMoveEntityRot =
        Create<ClientGamePacketListener>(52, "move_entity_rot");
    public static readonly PacketType<ClientGamePacketListener> ClientboundMoveVehicle =
        Create<ClientGamePacketListener>(53, "move_vehicle");
    public static readonly PacketType<ClientGamePacketListener> ClientboundOpenBook =
        Create<ClientGamePacketListener>(54, "open_book");
    public static readonly PacketType<ClientGamePacketListener> ClientboundOpenScreen =
        Create<ClientGamePacketListener>(55, "open_screen");
    public static readonly PacketType<ClientGamePacketListener> ClientboundOpenSignEditor =
        Create<ClientGamePacketListener>(56, "open_sign_editor");
    public static readonly PacketType<ClientGamePacketListener> ClientboundPlaceGhostRecipe =
        Create<ClientGamePacketListener>(57, "place_ghost_recipe");
    public static readonly PacketType<ClientGamePacketListener> ClientboundPlayerAbilities =
        Create<ClientGamePacketListener>(58, "player_abilities");
    public static readonly PacketType<ClientGamePacketListener> ClientboundGameRuleValues =
        Create<ClientGamePacketListener>(59, "game_rule_values");
    public static readonly PacketType<ClientGamePacketListener> ClientboundPlayerChat =
        Create<ClientGamePacketListener>(60, "player_chat");
    public static readonly PacketType<ClientGamePacketListener> ClientboundPlayerCombatEnd =
        Create<ClientGamePacketListener>(61, "player_combat_end");
    public static readonly PacketType<ClientGamePacketListener> ClientboundPlayerCombatEnter =
        Create<ClientGamePacketListener>(62, "player_combat_enter");
    public static readonly PacketType<ClientGamePacketListener> ClientboundPlayerCombatKill =
        Create<ClientGamePacketListener>(63, "player_combat_kill");
    public static readonly PacketType<ClientGamePacketListener> ClientboundPlayerInfoRemove =
        Create<ClientGamePacketListener>(64, "player_info_remove");
    public static readonly PacketType<ClientGamePacketListener> ClientboundPlayerInfoUpdate =
        Create<ClientGamePacketListener>(65, "player_info_update");
    public static readonly PacketType<ClientGamePacketListener> ClientboundPlayerLookAt =
        Create<ClientGamePacketListener>(66, "player_look_at");
    public static readonly PacketType<ClientGamePacketListener> ClientboundPlayerPosition =
        Create<ClientGamePacketListener>(67, "player_position");
    public static readonly PacketType<ClientGamePacketListener> ClientboundPlayerRotation =
        Create<ClientGamePacketListener>(68, "player_rotation");
    public static readonly PacketType<ClientGamePacketListener> ClientboundRecipeBookAdd =
        Create<ClientGamePacketListener>(69, "recipe_book_add");
    public static readonly PacketType<ClientGamePacketListener> ClientboundRecipeBookRemove =
        Create<ClientGamePacketListener>(70, "recipe_book_remove");
    public static readonly PacketType<ClientGamePacketListener> ClientboundRecipeBookSettings =
        Create<ClientGamePacketListener>(71, "recipe_book_settings");
    public static readonly PacketType<ClientGamePacketListener> ClientboundRemoveEntities =
        Create<ClientGamePacketListener>(72, "remove_entities");
    public static readonly PacketType<ClientGamePacketListener> ClientboundRemoveMobEffect =
        Create<ClientGamePacketListener>(73, "remove_mob_effect");
    public static readonly PacketType<ClientGamePacketListener> ClientboundRespawn =
        Create<ClientGamePacketListener>(74, "respawn");
    public static readonly PacketType<ClientGamePacketListener> ClientboundRotateHead =
        Create<ClientGamePacketListener>(75, "rotate_head");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSectionBlocksUpdate =
        Create<ClientGamePacketListener>(76, "section_blocks_update");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSelectAdvancementsTab =
        Create<ClientGamePacketListener>(77, "select_advancements_tab");
    public static readonly PacketType<ClientGamePacketListener> ClientboundServerData =
        Create<ClientGamePacketListener>(78, "server_data");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetActionBarText =
        Create<ClientGamePacketListener>(79, "set_action_bar_text");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetBorderCenter =
        Create<ClientGamePacketListener>(80, "set_border_center");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetBorderLerpSize =
        Create<ClientGamePacketListener>(81, "set_border_lerp_size");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetBorderSize =
        Create<ClientGamePacketListener>(82, "set_border_size");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetBorderWarningDelay =
        Create<ClientGamePacketListener>(83, "set_border_warning_delay");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetBorderWarningDistance =
        Create<ClientGamePacketListener>(84, "set_border_warning_distance");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetCamera =
        Create<ClientGamePacketListener>(85, "set_camera");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetChunkCacheCenter =
        Create<ClientGamePacketListener>(86, "set_chunk_cache_center");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetChunkCacheRadius =
        Create<ClientGamePacketListener>(87, "set_chunk_cache_radius");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetDefaultSpawnPosition =
        Create<ClientGamePacketListener>(88, "set_default_spawn_position");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetDisplayObjective =
        Create<ClientGamePacketListener>(89, "set_display_objective");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetEntityData =
        Create<ClientGamePacketListener>(90, "set_entity_data");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetEntityLink =
        Create<ClientGamePacketListener>(91, "set_entity_link");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetEntityMotion =
        Create<ClientGamePacketListener>(92, "set_entity_motion");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetEquipment =
        Create<ClientGamePacketListener>(93, "set_equipment");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetExperience =
        Create<ClientGamePacketListener>(94, "set_experience");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetHealth =
        Create<ClientGamePacketListener>(95, "set_health");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetHeldSlot =
        Create<ClientGamePacketListener>(96, "set_held_slot");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetObjective =
        Create<ClientGamePacketListener>(97, "set_objective");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetPassengers =
        Create<ClientGamePacketListener>(98, "set_passengers");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetPlayerTeam =
        Create<ClientGamePacketListener>(99, "set_player_team");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetScore =
        Create<ClientGamePacketListener>(100, "set_score");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetSimulationDistance =
        Create<ClientGamePacketListener>(101, "set_simulation_distance");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetSubtitleText =
        Create<ClientGamePacketListener>(102, "set_subtitle_text");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetTime =
        Create<ClientGamePacketListener>(103, "set_time");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetTitleText =
        Create<ClientGamePacketListener>(104, "set_title_text");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetTitlesAnimation =
        Create<ClientGamePacketListener>(105, "set_titles_animation");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSoundEntity =
        Create<ClientGamePacketListener>(106, "sound_entity");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSound =
        Create<ClientGamePacketListener>(107, "sound");
    public static readonly PacketType<ClientGamePacketListener> ClientboundStartConfiguration =
        Create<ClientGamePacketListener>(108, "start_configuration");
    public static readonly PacketType<ClientGamePacketListener> ClientboundStopSound =
        Create<ClientGamePacketListener>(109, "stop_sound");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSystemChat =
        Create<ClientGamePacketListener>(110, "system_chat");
    public static readonly PacketType<ClientGamePacketListener> ClientboundTabList =
        Create<ClientGamePacketListener>(111, "tab_list");
    public static readonly PacketType<ClientGamePacketListener> ClientboundTagQuery =
        Create<ClientGamePacketListener>(112, "tag_query");
    public static readonly PacketType<ClientGamePacketListener> ClientboundTakeItemEntity =
        Create<ClientGamePacketListener>(113, "take_item_entity");
    public static readonly PacketType<ClientGamePacketListener> ClientboundTeleportEntity =
        Create<ClientGamePacketListener>(114, "teleport_entity");
    public static readonly PacketType<ClientGamePacketListener> ClientboundTestInstanceBlockStatus =
        Create<ClientGamePacketListener>(115, "test_instance_block_status");
    public static readonly PacketType<ClientGamePacketListener> ClientboundUpdateAdvancements =
        Create<ClientGamePacketListener>(116, "update_advancements");
    public static readonly PacketType<ClientGamePacketListener> ClientboundUpdateAttributes =
        Create<ClientGamePacketListener>(117, "update_attributes");
    public static readonly PacketType<ClientGamePacketListener> ClientboundUpdateMobEffect =
        Create<ClientGamePacketListener>(118, "update_mob_effect");
    public static readonly PacketType<ClientGamePacketListener> ClientboundUpdateRecipes =
        Create<ClientGamePacketListener>(119, "update_recipes");
    public static readonly PacketType<ClientGamePacketListener> ClientboundProjectilePower =
        Create<ClientGamePacketListener>(120, "projectile_power");
    public static readonly PacketType<ClientGamePacketListener> ClientboundWaypoint =
        Create<ClientGamePacketListener>(121, "waypoint");
    public static readonly PacketType<ClientGamePacketListener> ClientboundResetScore =
        Create<ClientGamePacketListener>(122, "reset_score");
    public static readonly PacketType<ClientGamePacketListener> ClientboundTickingState =
        Create<ClientGamePacketListener>(123, "ticking_state");
    public static readonly PacketType<ClientGamePacketListener> ClientboundTickingStep =
        Create<ClientGamePacketListener>(124, "ticking_step");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetCursorItem =
        Create<ClientGamePacketListener>(125, "set_cursor_item");
    public static readonly PacketType<ClientGamePacketListener> ClientboundSetPlayerInventory =
        Create<ClientGamePacketListener>(126, "set_player_inventory");

    //Serverbound (Play, Serverbound) 0-64 按原版 GameProtocols.SERVERBOUND_TEMPLATE 顺序
    public static readonly PacketType<ServerGamePacketListener> ServerboundAcceptTeleportation =
        Create<ServerGamePacketListener>(0, "accept_teleportation");
    public static readonly PacketType<ServerGamePacketListener> ServerboundAttack =
        Create<ServerGamePacketListener>(1, "attack");
    public static readonly PacketType<ServerGamePacketListener> ServerboundBlockEntityTagQuery =
        Create<ServerGamePacketListener>(2, "block_entity_tag_query");
    public static readonly PacketType<ServerGamePacketListener> ServerboundBundleItemSelected =
        Create<ServerGamePacketListener>(3, "bundle_item_selected");
    public static readonly PacketType<ServerGamePacketListener> ServerboundChangeDifficulty =
        Create<ServerGamePacketListener>(4, "change_difficulty");
    public static readonly PacketType<ServerGamePacketListener> ServerboundChangeGameMode =
        Create<ServerGamePacketListener>(5, "change_game_mode");
    public static readonly PacketType<ServerGamePacketListener> ServerboundChatAck =
        Create<ServerGamePacketListener>(6, "chat_ack");
    public static readonly PacketType<ServerGamePacketListener> ServerboundChatCommand =
        Create<ServerGamePacketListener>(7, "chat_command");
    public static readonly PacketType<ServerGamePacketListener> ServerboundChatCommandSigned =
        Create<ServerGamePacketListener>(8, "chat_command_signed");
    public static readonly PacketType<ServerGamePacketListener> ServerboundChat =
        Create<ServerGamePacketListener>(9, "chat");
    public static readonly PacketType<ServerGamePacketListener> ServerboundChatSessionUpdate =
        Create<ServerGamePacketListener>(10, "chat_session_update");
    public static readonly PacketType<ServerGamePacketListener> ServerboundChunkBatchReceived =
        Create<ServerGamePacketListener>(11, "chunk_batch_received");
    public static readonly PacketType<ServerGamePacketListener> ServerboundClientCommand =
        Create<ServerGamePacketListener>(12, "client_command");
    public static readonly PacketType<ServerGamePacketListener> ServerboundClientTickEnd =
        Create<ServerGamePacketListener>(13, "client_tick_end");
    public static readonly PacketType<ServerGamePacketListener> ServerboundCommandSuggestion =
        Create<ServerGamePacketListener>(14, "command_suggestion");
    public static readonly PacketType<ServerGamePacketListener> ServerboundConfigurationAcknowledged =
        Create<ServerGamePacketListener>(15, "configuration_acknowledged");
    public static readonly PacketType<ServerGamePacketListener> ServerboundContainerButtonClick =
        Create<ServerGamePacketListener>(16, "container_button_click");
    public static readonly PacketType<ServerGamePacketListener> ServerboundContainerClick =
        Create<ServerGamePacketListener>(17, "container_click");
    public static readonly PacketType<ServerGamePacketListener> ServerboundContainerClose =
        Create<ServerGamePacketListener>(18, "container_close");
    public static readonly PacketType<ServerGamePacketListener> ServerboundContainerSlotStateChanged =
        Create<ServerGamePacketListener>(19, "container_slot_state_changed");
    public static readonly PacketType<ServerGamePacketListener> ServerboundDebugSubscriptionRequest =
        Create<ServerGamePacketListener>(20, "debug_subscription_request");
    public static readonly PacketType<ServerGamePacketListener> ServerboundEditBook =
        Create<ServerGamePacketListener>(21, "edit_book");
    public static readonly PacketType<ServerGamePacketListener> ServerboundEntityTagQuery =
        Create<ServerGamePacketListener>(22, "entity_tag_query");
    public static readonly PacketType<ServerGamePacketListener> ServerboundInteract =
        Create<ServerGamePacketListener>(23, "interact");
    public static readonly PacketType<ServerGamePacketListener> ServerboundJigsawGenerate =
        Create<ServerGamePacketListener>(24, "jigsaw_generate");
    public static readonly PacketType<ServerGamePacketListener> ServerboundLockDifficulty =
        Create<ServerGamePacketListener>(25, "lock_difficulty");
    public static readonly PacketType<ServerGamePacketListener> ServerboundMovePlayerPos =
        Create<ServerGamePacketListener>(26, "move_player_pos");
    public static readonly PacketType<ServerGamePacketListener> ServerboundMovePlayerPosRot =
        Create<ServerGamePacketListener>(27, "move_player_pos_rot");
    public static readonly PacketType<ServerGamePacketListener> ServerboundMovePlayerRot =
        Create<ServerGamePacketListener>(28, "move_player_rot");
    public static readonly PacketType<ServerGamePacketListener> ServerboundMovePlayerStatusOnly =
        Create<ServerGamePacketListener>(29, "move_player_status_only");
    public static readonly PacketType<ServerGamePacketListener> ServerboundMoveVehicle =
        Create<ServerGamePacketListener>(30, "move_vehicle");
    public static readonly PacketType<ServerGamePacketListener> ServerboundPaddleBoat =
        Create<ServerGamePacketListener>(31, "paddle_boat");
    public static readonly PacketType<ServerGamePacketListener> ServerboundPickItemFromBlock =
        Create<ServerGamePacketListener>(32, "pick_item_from_block");
    public static readonly PacketType<ServerGamePacketListener> ServerboundPickItemFromEntity =
        Create<ServerGamePacketListener>(33, "pick_item_from_entity");
    public static readonly PacketType<ServerGamePacketListener> ServerboundPlaceRecipe =
        Create<ServerGamePacketListener>(34, "place_recipe");
    public static readonly PacketType<ServerGamePacketListener> ServerboundPlayerAbilities =
        Create<ServerGamePacketListener>(35, "player_abilities");
    public static readonly PacketType<ServerGamePacketListener> ServerboundPlayerAction =
        Create<ServerGamePacketListener>(36, "player_action");
    public static readonly PacketType<ServerGamePacketListener> ServerboundPlayerCommand =
        Create<ServerGamePacketListener>(37, "player_command");
    public static readonly PacketType<ServerGamePacketListener> ServerboundPlayerInput =
        Create<ServerGamePacketListener>(38, "player_input");
    public static readonly PacketType<ServerGamePacketListener> ServerboundPlayerLoaded =
        Create<ServerGamePacketListener>(39, "player_loaded");
    public static readonly PacketType<ServerGamePacketListener> ServerboundRecipeBookChangeSettings =
        Create<ServerGamePacketListener>(40, "recipe_book_change_settings");
    public static readonly PacketType<ServerGamePacketListener> ServerboundRecipeBookSeenRecipe =
        Create<ServerGamePacketListener>(41, "recipe_book_seen_recipe");
    public static readonly PacketType<ServerGamePacketListener> ServerboundRenameItem =
        Create<ServerGamePacketListener>(42, "rename_item");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSeenAdvancements =
        Create<ServerGamePacketListener>(43, "seen_advancements");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSelectTrade =
        Create<ServerGamePacketListener>(44, "select_trade");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSetBeacon =
        Create<ServerGamePacketListener>(45, "set_beacon");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSetCarriedItem =
        Create<ServerGamePacketListener>(46, "set_carried_item");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSetCommandBlock =
        Create<ServerGamePacketListener>(47, "set_command_block");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSetCommandMinecart =
        Create<ServerGamePacketListener>(48, "set_command_minecart");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSetCreativeModeSlot =
        Create<ServerGamePacketListener>(49, "set_creative_mode_slot");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSetGameRule =
        Create<ServerGamePacketListener>(50, "set_game_rule");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSetJigsawBlock =
        Create<ServerGamePacketListener>(51, "set_jigsaw_block");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSetStructureBlock =
        Create<ServerGamePacketListener>(52, "set_structure_block");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSetTestBlock =
        Create<ServerGamePacketListener>(53, "set_test_block");
    public static readonly PacketType<ServerGamePacketListener> ServerboundTestInstanceBlockAction =
        Create<ServerGamePacketListener>(54, "test_instance_block_action");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSignUpdate =
        Create<ServerGamePacketListener>(55, "sign_update");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSpectatorAction =
        Create<ServerGamePacketListener>(56, "spectator_action");
    public static readonly PacketType<ServerGamePacketListener> ServerboundSwing =
        Create<ServerGamePacketListener>(57, "swing");
    public static readonly PacketType<ServerGamePacketListener> ServerboundTeleportToEntity =
        Create<ServerGamePacketListener>(58, "teleport_to_entity");
    public static readonly PacketType<ServerGamePacketListener> ServerboundUseItemOn =
        Create<ServerGamePacketListener>(59, "use_item_on");
    public static readonly PacketType<ServerGamePacketListener> ServerboundUseItem =
        Create<ServerGamePacketListener>(60, "use_item");

    private static PacketType<THandler> Create<THandler>(int id, string identifier)
        where THandler : class
    {
        var direction = typeof(THandler) == typeof(ServerGamePacketListener)
            ? FlowDirection.Serverbound
            : FlowDirection.Clientbound;
        return PacketTypeRegistry.Register<THandler>(id, ConnectionProtocol.Play, direction)
            .WithIdentifier(Identifier.WithDefaultNamespace(identifier));
    }
}
