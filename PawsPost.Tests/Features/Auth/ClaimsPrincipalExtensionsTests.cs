using System.Security.Claims;
using PawsPost.Common;

namespace PawsPost.Tests.Features.Auth;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_NameIdentifierClaimIsGuid_ReturnsGuid()
    {
        // Arrange
        var id = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));

        // Act
        var result = principal.GetUserId();

        // Assert
        Assert.Equal(id, result);
    }

    [Fact]
    public void GetUserId_ClaimMissing_Throws()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => principal.GetUserId());
    }

    [Fact]
    public void GetUserId_ClaimNotAGuid_Throws()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "not-a-guid")], "test"));

        // Act and Assert
        Assert.Throws<InvalidOperationException>(() => principal.GetUserId());
    }
}
