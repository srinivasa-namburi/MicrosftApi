namespace Microsoft.Greenlight.Shared.Models.Plugins;

/// <summary>
/// This class is 
/// </summary>
public class DynamicPluginVersion : IEquatable<DynamicPluginVersion>, IComparable<DynamicPluginVersion>
{
    public int Major { get; set; }
    public int Minor { get; set; }
    public int Patch { get; set; }

    public DynamicPluginVersion(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    public static DynamicPluginVersion Parse(string version)
    {
        var parts = version.Split('.').Select(int.Parse).ToArray();
        return new DynamicPluginVersion(parts[0], parts[1], parts[2]);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public int CompareTo(DynamicPluginVersion other)
    {
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        return Patch.CompareTo(other.Patch);
    }

    public bool Equals(DynamicPluginVersion other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Major == other.Major && Minor == other.Minor && Patch == other.Patch;
    }

    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((DynamicPluginVersion)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Major, Minor, Patch);
    }

    public static bool operator ==(DynamicPluginVersion left, DynamicPluginVersion right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(DynamicPluginVersion left, DynamicPluginVersion right)
    {
        return !Equals(left, right);
    }
}