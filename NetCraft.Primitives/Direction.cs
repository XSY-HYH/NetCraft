namespace NetCraft.Primitives;

//方向枚举对应原版net.minecraft.core.Direction
//原版有16个方向这里只取6个基础方向加DOWN/UP等枚举值
//持StepX/StepY/StepZ偏移量与Axis轴标记
public readonly struct Direction : IEquatable<Direction>
{
    public enum Axis
    {
        X,
        Y,
        Z
    }

    public enum AxisDirection
    {
        Positive,
        Negative
    }

    public const int DownId = 0;
    public const int UpId = 1;
    public const int NorthId = 2;
    public const int SouthId = 3;
    public const int WestId = 4;
    public const int EastId = 5;

    public static readonly Direction Down = new(DownId, UpId, AxisDirection.Negative, Axis.Y, 0, -1, 0);
    public static readonly Direction Up = new(UpId, DownId, AxisDirection.Positive, Axis.Y, 0, 1, 0);
    public static readonly Direction North = new(NorthId, SouthId, AxisDirection.Negative, Axis.Z, 0, 0, -1);
    public static readonly Direction South = new(SouthId, NorthId, AxisDirection.Positive, Axis.Z, 0, 0, 1);
    public static readonly Direction West = new(WestId, EastId, AxisDirection.Negative, Axis.X, -1, 0, 0);
    public static readonly Direction East = new(EastId, WestId, AxisDirection.Positive, Axis.X, 1, 0, 0);

    public static readonly Direction[] Values = { Down, Up, North, South, West, East };
    public static readonly Direction[] AllShuffledOrder = { West, East, North, South, Down, Up };

    public int Id3D { get; }
    public int OppositeId { get; }
    public AxisDirection AxisDir { get; }
    public Axis AxisValue { get; }
    public int StepX { get; }
    public int StepY { get; }
    public int StepZ { get; }

    private Direction(int id3d, int oppositeId, AxisDirection axisDir, Axis axis, int stepX, int stepY, int stepZ)
    {
        Id3D = id3d;
        OppositeId = oppositeId;
        AxisDir = axisDir;
        AxisValue = axis;
        StepX = stepX;
        StepY = stepY;
        StepZ = stepZ;
    }

    //ById按id取方向
    public static Direction ById(int id)
    {
        return Values[((id % Values.Length) + Values.Length) % Values.Length];
    }

    //byAxisDirection按轴方向取该轴正负方向
    public static Direction ByAxisDirection(Axis axis, AxisDirection dir)
    {
        foreach (var d in Values)
        {
            if (d.AxisValue == axis && d.AxisDir == dir) return d;
        }
        return Down;
    }

    //opposite取反方向
    public Direction Opposite => ById(OppositeId);

    //counterClockWise逆时针旋转一次
    public Direction CounterClockWise
    {
        get
        {
            if (AxisValue == Axis.Y) return ById(Id3D + (IsHorizontal ? -1 : -2));
            return this;
        }
    }

    //clockWise顺时针旋转一次
    public Direction ClockWise
    {
        get
        {
            if (AxisValue == Axis.Y) return ById(Id3D + (IsHorizontal ? 1 : 2));
            return this;
        }
    }

    public bool IsHorizontal => AxisValue == Axis.X || AxisValue == Axis.Z;

    //getAxis返回Axis
    public Axis GetAxis() => AxisValue;

    //getStep按轴取步长
    public int GetStep(Axis axis)
    {
        if (axis == Axis.X) return StepX;
        if (axis == Axis.Y) return StepY;
        return StepZ;
    }

    public override int GetHashCode() => Id3D;

    public bool Equals(Direction other) => Id3D == other.Id3D;

    public override bool Equals(object? obj) => obj is Direction d && Equals(d);

    public static bool operator ==(Direction left, Direction right) => left.Equals(right);
    public static bool operator !=(Direction left, Direction right) => !left.Equals(right);

    public override string ToString()
    {
        return AxisValue switch
        {
            Axis.X => AxisDir == AxisDirection.Positive ? "+X" : "-X",
            Axis.Y => AxisDir == AxisDirection.Positive ? "+Y" : "-Y",
            _ => AxisDir == AxisDirection.Positive ? "+Z" : "-Z"
        };
    }
}
