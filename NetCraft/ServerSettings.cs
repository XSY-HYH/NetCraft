using NetCraft.Logging;

namespace NetCraft;

//ServerSettings 服务端 server.properties 配置
//对应原版 net.minecraft.server.dedicated.Settings 简化版
//继承 Settings<ServerSettings> 用泛型自引用对齐原版 self type 模式
//仅保留核心字段端口/世界名/难度/max-players 等
public sealed class ServerSettings : Settings<ServerSettings>
{
    //服务器监听端口默认 25565
    public int ServerPort => GetInt("server-port", 25565);

    //最大玩家数默认 20
    public int MaxPlayers => GetInt("max-players", 20);

    //世界名称默认 world
    public string LevelName => GetOrDefault("level-name", "world");

    //游戏模式 survival/creative/adventure/spectator
    public string Gamemode => GetOrDefault("gamemode", "survival");

    //难度 peaceful/easy/normal/hard
    public string Difficulty => GetOrDefault("difficulty", "easy");

    //是否开启正版验证默认 true
    public bool OnlineMode => GetBool("online-mode", true);

    //是否允许 PVP 默认 true
    public bool AllowPvp => GetBool("pvp", true);

    //视野距离单位 chunk 默认 10
    public int ViewDistance => GetInt("view-distance", 10);

    //是否允许飞行默认 false
    public bool AllowFlight => GetBool("allow-flight", false);

    //是否生成动物默认 true
    public bool SpawnAnimals => GetBool("spawn-animals", true);

    //是否生成怪物默认 true
    public bool SpawnMonsters => GetBool("spawn-monsters", true);

    //是否生成 NPC 默认 true
    public bool SpawnNpcs => GetBool("spawn-npcs", true);

    //是否启用白名单默认 false
    public bool WhiteList => GetBool("white-list", false);

    //是否生成结构默认 true
    public bool GenerateStructures => GetBool("generate-structures", true);

    //是否允许下界默认 true
    public bool AllowNether => GetBool("allow-nether", true);

    //世界种子空字符串表示随机生成
    public string LevelSeed => GetOrDefault("level-seed", string.Empty);

    //level-type default/flat/large_biomes/amplified
    public string LevelType => GetOrDefault("level-type", "default");

    //最大世界大小单位 chunk 默认 29999984
    public int MaxWorldSize => GetInt("max-world-size", 29999984);

    //服务器描述 motd 默认 A Minecraft Server
    public string Motd => GetOrDefault("motd", "A Minecraft Server");

    //是否启用 RCON 远程管理默认 false
    public bool EnableRcon => GetBool("enable-rcon", false);

    //是否启用 Query 协议默认 false
    public bool EnableQuery => GetBool("enable-query", false);

    //加载并应用默认值若文件不存在生成默认 server.properties
    public static ServerSettings LoadOrGenerate(string path)
    {
        var settings = new ServerSettings();
        if (File.Exists(path))
        {
            settings.Load(path);
            Log.Info($"已加载服务端配置 {path}");
        }
        else
        {
            Log.Info($"server.properties 不存在生成默认配置到 {path}");
            settings.SaveDefault(path);
        }
        return settings;
    }

    //生成默认 server.properties 写入路径
    public void SaveDefault(string path)
    {
        Set("server-port", ServerPort.ToString());
        Set("max-players", MaxPlayers.ToString());
        Set("level-name", LevelName);
        Set("gamemode", Gamemode);
        Set("difficulty", Difficulty);
        Set("online-mode", OnlineMode ? "true" : "false");
        Set("pvp", AllowPvp ? "true" : "false");
        Set("view-distance", ViewDistance.ToString());
        Set("allow-flight", AllowFlight ? "true" : "false");
        Set("spawn-animals", SpawnAnimals ? "true" : "false");
        Set("spawn-monsters", SpawnMonsters ? "true" : "false");
        Set("spawn-npcs", SpawnNpcs ? "true" : "false");
        Set("white-list", WhiteList ? "true" : "false");
        Set("generate-structures", GenerateStructures ? "true" : "false");
        Set("allow-nether", AllowNether ? "true" : "false");
        Set("level-seed", LevelSeed);
        Set("level-type", LevelType);
        Set("max-world-size", MaxWorldSize.ToString());
        Set("motd", Motd);
        Set("enable-rcon", EnableRcon ? "true" : "false");
        Set("enable-query", EnableQuery ? "true" : "false");
        Save(path);
    }
}
