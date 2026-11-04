using NSubstitute;
using Sems.Api.Modules.Iam.Application;
using Sems.Api.Modules.Iam.Domain.Model;
using Sems.Api.Modules.Iam.Domain.Repositories;
using Sems.Api.Shared.Errors;
using Xunit;

namespace Sems.Api.Tests.Iam;

/// <summary>
/// Nombre y segmento del propio usuario.
/// </summary>
/// <remarks>
/// Son datos del perfil y no permisos: no deciden lo que se puede hacer, solo
/// con que cara se presenta la aplicacion. Antes vivian unicamente en el
/// navegador, asi que entrar desde otro equipo los perdia.
/// </remarks>
public class UserProfileServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly UserProfileService _service;
    private readonly User _user;

    public UserProfileServiceTests()
    {
        _user = User.Register(new EmailAddress("manager@energix.test"), "hash", RoleName.STAFF,
            requireVerification: false);
        _users.FindByIdAsync(_user.UserId, Arg.Any<CancellationToken>()).Returns(_user);
        _users.SaveAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<User>());
        _service = new UserProfileService(_users);
    }

    [Fact]
    public async Task UpdateProfileAsync_NameAndSegment_StoresThemTrimmed()
    {
        var actualizado = await _service.UpdateProfileAsync(_user.UserId, "  Alexis Quispe  ",
            "retail_chain");

        Assert.Equal("Alexis Quispe", actualizado.FullName);
        Assert.Equal(UserSegment.RETAIL_CHAIN, actualizado.Segment);
        await _users.Received(1).SaveAsync(actualizado, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("RETAIL_CHAIN", UserSegment.RETAIL_CHAIN)]
    [InlineData("retail-chain", UserSegment.RETAIL_CHAIN)]
    [InlineData(" Independent ", UserSegment.INDEPENDENT)]
    public async Task UpdateProfileAsync_SegmentHoweverItIsWritten_IsUnderstood(string escrito,
        UserSegment esperado)
    {
        Assert.Equal(esperado,
            (await _service.UpdateProfileAsync(_user.UserId, null, escrito)).Segment);
    }

    [Fact]
    public async Task UpdateProfileAsync_UnknownSegment_FailsSayingWhichAreValid()
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.UpdateProfileAsync(_user.UserId, null, "FABRICA"));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
        Assert.Contains("RETAIL_CHAIN", error.Message);
    }

    [Fact]
    public async Task UpdateProfileAsync_BlankName_LeavesItEmptyInsteadOfStoringSpaces()
    {
        await _service.UpdateProfileAsync(_user.UserId, "Alexis", null);

        Assert.Null((await _service.UpdateProfileAsync(_user.UserId, "   ", null)).FullName);
    }

    [Fact]
    public async Task UpdateProfileAsync_NameLongerThanTheColumn_Fails()
    {
        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.UpdateProfileAsync(_user.UserId, new string('a', 121), null));

        Assert.Equal(ErrorCode.VALIDATION_ERROR, error.Code);
    }

    [Fact]
    public async Task UpdateProfileAsync_NoSegment_ClearsItWithoutFailing()
    {
        // El segmento es opcional: quien no quiera declararlo puede dejarlo.
        await _service.UpdateProfileAsync(_user.UserId, "Alexis", "INDEPENDENT");

        Assert.Null((await _service.UpdateProfileAsync(_user.UserId, "Alexis", null)).Segment);
    }

    [Fact]
    public async Task UpdateProfileAsync_UnknownUser_Fails()
    {
        var ajeno = Guid.NewGuid();
        _users.FindByIdAsync(ajeno, Arg.Any<CancellationToken>()).Returns((User?)null);

        var error = await Assert.ThrowsAsync<AppException>(
            () => _service.UpdateProfileAsync(ajeno, "Alexis", null));

        Assert.Equal(ErrorCode.NOT_FOUND, error.Code);
    }
}
