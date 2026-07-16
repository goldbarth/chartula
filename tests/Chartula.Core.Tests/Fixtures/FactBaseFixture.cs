using Chartula.Core.Facts;
using Chartula.Core.Serialization;

namespace Chartula.Core.Tests.Fixtures;

/// <summary>
/// The stored fact bases the tests replay. They are ordinary <c>changelog.json</c>
/// files, so a real release can be frozen into a fixture by copying the file a run
/// wrote - there is no second format to keep in step.
/// </summary>
public static class FactBaseFixture
{
    /// <summary>A release with a spread of categories, links, issues and descriptions.</summary>
    public const string Typical = "typical-release";

    /// <summary>A release carrying breaking changes among ordinary ones.</summary>
    public const string Breaking = "breaking-release";

    /// <summary>A release built from commits: no pull request numbers, links or descriptions.</summary>
    public const string CommitsOnly = "commits-only-release";

    /// <summary>A release where nothing is user-visible.</summary>
    public const string InternalOnly = "internal-only-release";

    /// <summary>A release with no changes at all.</summary>
    public const string Empty = "empty-release";

    /// <summary>Every fixture, for tests that must hold for all of them.</summary>
    public static IReadOnlyList<string> All => [Typical, Breaking, CommitsOnly, InternalOnly, Empty];

    /// <summary>Every fixture as xUnit theory data.</summary>
    public static TheoryData<string> AllAsTheoryData
    {
        get
        {
            TheoryData<string> data = [];
            foreach (string name in All)
            {
                data.Add(name);
            }

            return data;
        }
    }

    /// <summary>Loads a fixture by name.</summary>
    public static FactBase Load(string name)
        => ChangelogJsonSerializer.DeserializeFactBase(ReadText(name));

    /// <summary>The raw file contents of a fixture.</summary>
    public static string ReadText(string name)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Fixture '{name}' was not found at '{path}'. Fixtures are copied to the output "
                + "directory by the test project; check the file name and the Content item.",
                path);
        }

        return File.ReadAllText(path);
    }
}
