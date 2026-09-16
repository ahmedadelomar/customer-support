using System.Text.RegularExpressions;
using CustomerSupport.Application.Common.Security;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.UnitTests.Security;

/// <summary>
/// The permission registry exists twice — <c>Permissions.cs</c> on the server and
/// <c>permissions.ts</c> on the client — because both need it and neither can import the other.
/// Drift between them surfaces as a route guard that hides a page the API would happily serve, or
/// worse, a button that 403s. This test turns that into a build failure instead.
/// </summary>
/// <remarks>
/// Deliberately a test rather than code generation: both files stay readable and hand-editable, and
/// the failure message points at exactly which key is missing on which side.
/// </remarks>
public class PermissionRegistryTests
{
    /// <summary>Matches a dotted permission key such as <c>tickets.view.all</c>.</summary>
    private static readonly Regex KeyPattern = new(@"'([a-z]+(?:\.[a-z]+)+)'", RegexOptions.Compiled);

    [Fact]
    public void ClientRegistryMatchesServerRegistry()
    {
        var clientKeys = ReadClientKeys();
        var serverKeys = Permissions.All.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

        var missingOnClient = serverKeys.Except(clientKeys).OrderBy(k => k).ToList();
        var missingOnServer = clientKeys.Except(serverKeys).OrderBy(k => k).ToList();

        missingOnClient.Should().BeEmpty(
            "every key in Permissions.cs must also exist in frontend/src/app/core/permissions.ts");

        missingOnServer.Should().BeEmpty(
            "every key in permissions.ts must also exist in Permissions.cs");
    }

    [Fact]
    public void ServerRegistryHasNoDuplicateKeys()
    {
        var duplicates = Permissions.All
            .GroupBy(p => p.Key, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        duplicates.Should().BeEmpty("a permission key granted through two constants can be revoked from only one");
    }

    private static HashSet<string> ReadClientKeys()
    {
        var path = FindClientRegistry();
        var source = File.ReadAllText(path);

        return KeyPattern.Matches(source)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Walks up from the test binary to the repository root. The test must not depend on the working
    /// directory, which differs between `dotnet test`, an IDE run and CI.
    /// </summary>
    private static string FindClientRegistry()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "frontend", "src", "app", "core", "permissions.ts");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Could not locate frontend/src/app/core/permissions.ts by walking up from the test output directory.");
    }
}
