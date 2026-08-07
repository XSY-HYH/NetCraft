using NetCraft;

namespace NetCraft.Test.Modules;

//Config 测试覆盖 PropertiesConfig 通用 properties 读写 + GameConfig options.txt + ServerSettings server.properties
internal static class ConfigTests
{
    public const string Module = "config";

    public static IEnumerable<(string Name, Func<bool> Test)> All()
    {
        yield return ("PropertiesConfig load and read", TestPropertiesLoad);
        yield return ("PropertiesConfig save and reload round-trip", TestPropertiesSaveReload);
        yield return ("PropertiesConfig comments and separators", TestPropertiesCommentsAndSeparators);
        yield return ("PropertiesConfig GetInt invalid returns default", TestPropertiesGetIntInvalid);
        yield return ("PropertiesConfig GetSize K M G suffix", TestPropertiesGetSize);
        yield return ("GameConfig load round-trip", TestGameConfigLoad);
        yield return ("GameConfig out of range clamped", TestGameConfigClamped);
        yield return ("ServerSettings default values", TestServerSettingsDefaults);
        yield return ("ServerSettings load existing file", TestServerSettingsLoadExisting);
        yield return ("ServerSettings generate default file", TestServerSettingsGenerateDefault);
    }

    //WriteTempFile 写临时文件返回路径测试隔离
    private static string WriteTempFile(string content, string name = "tmp.properties")
    {
        var path = Path.Combine(Path.GetTempPath(), $"netcraft_{Guid.NewGuid():N}_{name}");
        File.WriteAllText(path, content);
        return path;
    }

    private static bool TestPropertiesLoad()
    {
        var content = """
            # 这是注释
            ! 这也是注释
            name=NetCraft
            port=25565
            enable=true
            gamma=0.5
            """;
        var path = WriteTempFile(content);
        try
        {
            var props = new PropertiesConfig();
            props.Load(path);
            return props["name"] == "NetCraft"
                && props.GetInt("port", 0) == 25565
                && props.GetBool("enable", false)
                && props.GetFloat("gamma", 0) == 0.5f;
        }
        finally { File.Delete(path); }
    }

    private static bool TestPropertiesSaveReload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"netcraft_{Guid.NewGuid():N}_save.properties");
        try
        {
            var props = new PropertiesConfig();
            props.SetInt("renderDistance", 16);
            props.SetBool("fullscreen", true);
            props.Set("lang", "zh_cn");
            props.Save(path);

            var reloaded = new PropertiesConfig();
            reloaded.Load(path);
            return reloaded.GetInt("renderDistance", 0) == 16
                && reloaded.GetBool("fullscreen", false)
                && reloaded["lang"] == "zh_cn";
        }
        finally { File.Delete(path); }
    }

    private static bool TestPropertiesCommentsAndSeparators()
    {
        //支持 = 与 : 分隔
        var content = """
            key1=value1
            key2:value2
            key3=  spaced
            """;
        var path = WriteTempFile(content);
        try
        {
            var props = new PropertiesConfig();
            props.Load(path);
            return props["key1"] == "value1"
                && props["key2"] == "value2"
                && props["key3"] == "spaced";
        }
        finally { File.Delete(path); }
    }

    private static bool TestPropertiesGetIntInvalid()
    {
        var content = "port=abc\nempty=\n";
        var path = WriteTempFile(content);
        try
        {
            var props = new PropertiesConfig();
            props.Load(path);
            return props.GetInt("port", 25565) == 25565
                && props.GetInt("empty", 100) == 100
                && props.GetInt("missing", 50) == 50;
        }
        finally { File.Delete(path); }
    }

    private static bool TestPropertiesGetSize()
    {
        var content = """
            small=512
            kb=2K
            mb=4M
            gb=1G
            invalid=abc
            """;
        var path = WriteTempFile(content);
        try
        {
            var props = new PropertiesConfig();
            props.Load(path);
            return props.GetSize("small", 0) == 512
                && props.GetSize("kb", 0) == 2 * 1024L
                && props.GetSize("mb", 0) == 4 * 1024L * 1024
                && props.GetSize("gb", 0) == 1L * 1024 * 1024 * 1024
                && props.GetSize("invalid", 999) == 999;
        }
        finally { File.Delete(path); }
    }

    private static bool TestGameConfigLoad()
    {
        var content = """
            renderDistance=24
            fov=90
            gamma=0.8
            fullscreen=true
            lang=zh_cn
            mainHand=left
            """;
        var path = WriteTempFile(content, "options.txt");
        try
        {
            var config = GameConfig.Load(path);
            return config.RenderDistance == 24
                && config.Fov == 90
                && config.Gamma == 0.8f
                && config.Fullscreen
                && config.Language == "zh_cn"
                && config.MainHand == "left";
        }
        finally { File.Delete(path); }
    }

    private static bool TestGameConfigClamped()
    {
        var content = """
            renderDistance=999
            fov=200
            gamma=-1
            """;
        var path = WriteTempFile(content, "options.txt");
        try
        {
            var config = GameConfig.Load(path);
            return config.RenderDistance == 12
                && config.Fov == 70
                && config.Gamma == 0.5f;
        }
        finally { File.Delete(path); }
    }

    private static bool TestServerSettingsDefaults()
    {
        //空文件所有字段返回默认
        var path = WriteTempFile("", "server.properties");
        try
        {
            var settings = new ServerSettings();
            settings.Load(path);
            return settings.ServerPort == 25565
                && settings.MaxPlayers == 20
                && settings.LevelName == "world"
                && settings.Gamemode == "survival"
                && settings.Difficulty == "easy"
                && settings.OnlineMode
                && settings.AllowPvp
                && settings.ViewDistance == 10
                && settings.Motd == "A Minecraft Server";
        }
        finally { File.Delete(path); }
    }

    private static bool TestServerSettingsLoadExisting()
    {
        var content = """
            server-port=19132
            max-players=100
            level-name=myworld
            gamemode=creative
            difficulty=hard
            online-mode=false
            pvp=false
            motd=My Server
            """;
        var path = WriteTempFile(content, "server.properties");
        try
        {
            var settings = new ServerSettings();
            settings.Load(path);
            return settings.ServerPort == 19132
                && settings.MaxPlayers == 100
                && settings.LevelName == "myworld"
                && settings.Gamemode == "creative"
                && settings.Difficulty == "hard"
                && !settings.OnlineMode
                && !settings.AllowPvp
                && settings.Motd == "My Server";
        }
        finally { File.Delete(path); }
    }

    private static bool TestServerSettingsGenerateDefault()
    {
        var path = Path.Combine(Path.GetTempPath(), $"netcraft_{Guid.NewGuid():N}_gen.properties");
        try
        {
            var settings = ServerSettings.LoadOrGenerate(path);
            var generated = File.Exists(path);
            var reloaded = new ServerSettings();
            reloaded.Load(path);
            return generated
                && settings.ServerPort == 25565
                && reloaded.ServerPort == 25565
                && reloaded.Motd == "A Minecraft Server";
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
