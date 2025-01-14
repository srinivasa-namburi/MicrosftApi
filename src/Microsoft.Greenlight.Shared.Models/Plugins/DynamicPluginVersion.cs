namespace Microsoft.Greenlight.Shared.Models.Plugins;

/// <summary>
/// This class is a version of a dynamic plugin with major, minor, and patch components.
/// Implements IEquatable and IComparable for version comparison and equality checks.
/// </summary>
public class DynamicPluginVersion : IEquatable<DynamicPluginVersion>, IComparable<DynamicPluginVersion>
{
    /// <summary>
    /// Major version component.
    /// </summary>
    public int Major { get; set; }

    /// <summary>
    /// Minor version component.
    /// </summary>
    public int Minor { get; set; }

    /// <summary>
    /// Patch version component.
    /// </summary>
    public int Patch { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicPluginVersion"/> class with specified
    /// major, minor, and patch components.
    /// </summary>
    /// <param name="major">The major version component.</param>
    /// <param name="minor">The minor version component.</param>
    /// <param name="patch">The patch version component.</param>
    public DynamicPluginVersion(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <summary>
    /// Parses a version string into a <see cref="DynamicPluginVersion"/> instance.
    /// </summary>
    /// <param name="version">The version string to parse.</param>
    /// <returns>A <see cref="DynamicPluginVersion"/> instance.</returns>
    public static DynamicPluginVersion Parse(string version)
    {
        var parts = version.Split('.').Select(int.Parse).ToArray();
        return new DynamicPluginVersion(parts[0], parts[1], parts[2]);
    }

    /// <summary>
    /// Tries to parse a version string into a <see cref="DynamicPluginVersion"/> instance.
    /// </summary>
    /// <param name="version">The version string to parse.</param>
    /// <param name="pluginVersion">When this method returns, contains the <see cref="DynamicPluginVersion"/>
    /// instance, if the parse succeeded, or null if the parse failed.</param>
    /// <returns><c>true</c> if the version string was parsed successfully; otherwise, <c>false</c>.</returns>
    public static bool TryParse(string version, out DynamicPluginVersion pluginVersion)
    {
        pluginVersion = null;
        var parts = version.Split('.');
        if (parts.Length != 3)
            return false;

        if (int.TryParse(parts[0], out var major) &&
            int.TryParse(parts[1], out var minor) &&
            int.TryParse(parts[2], out var patch))
        {
            pluginVersion = new DynamicPluginVersion(major, minor, patch);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    /// <summary>
    /// Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance precedes, follows, or occurs in the same position in the sort order as the other object.
    /// </summary>
    /// <param name="other">An object to compare with this instance.</param>
    /// <returns>A value that indicates the relative order of the objects being compared.</returns>
    public int CompareTo(DynamicPluginVersion other)
    {
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        return Patch.CompareTo(other.Patch);
    }

    /// <summary>
    /// Indicates whether the current object is equal to another object of the same type.
    /// </summary>
    /// <param name="other">An object to compare with this object.</param>
    /// <returns><c>true</c> if the current object is equal to the <paramref name="other"/> parameter; otherwise, <c>false</c>.</returns>
    public bool Equals(DynamicPluginVersion other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Major == other.Major && Minor == other.Minor && Patch == other.Patch;
    }

    /// <inheritdoc/>
    public override bool Equals(object obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((DynamicPluginVersion)obj);
    }

    /// <summary>
    /// Serves as the default hash function.
    /// </summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Major, Minor, Patch);
    }

    /// <summary>
    /// Indicates whether two <see cref="DynamicPluginVersion"/> instances are equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><c>true</c> if the instances are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(DynamicPluginVersion left, DynamicPluginVersion right)
    {
        return Equals(left, right);
    }

    /// <summary>
    /// Indicates whether two <see cref="DynamicPluginVersion"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><c>true</c> if the instances are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(DynamicPluginVersion left, DynamicPluginVersion right)
    {
        return !Equals(left, right);
    }
}
