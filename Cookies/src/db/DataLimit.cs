using System.Text;

namespace Cookies.Database;

/// <summary>The most the Data column holds, measured the way the database measures it.</summary>
internal readonly record struct DataLimit(long Max, bool InBytes)
{
    public string Unit => InBytes ? "bytes" : "characters";

    public bool Fits(string json) => (InBytes ? Encoding.UTF8.GetByteCount(json) : json.Length) <= Max;
}
