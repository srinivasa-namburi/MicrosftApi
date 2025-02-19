using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Greenlight.API.Main.Controllers;
using Microsoft.Greenlight.Shared.Contracts.DTO;
using Microsoft.Greenlight.Shared.Data.Sql;
using Microsoft.Greenlight.Shared.Enums;
using Microsoft.Greenlight.Shared.Mappings;
using Microsoft.Greenlight.Shared.Models;
using Microsoft.Greenlight.Shared.Testing.SQLite;

namespace Microsoft.Greenlight.API.Main.Tests.Controllers
{
    public sealed class AuthorizationControllerFixture : IDisposable
    {
        private readonly ConnectionFactory _connectionFactory = new();
        public DocGenerationDbContext DocGenerationDbContext { get; }
        public AuthorizationControllerFixture()
        {
            DocGenerationDbContext = _connectionFactory.CreateContext();
        }
        public void Dispose()
        {
            _connectionFactory.Dispose();
        }
    }
    public class AuthorizationControllerTests : IClassFixture<AuthorizationControllerFixture>
    {
        private readonly IMapper _mapper;
        private readonly DocGenerationDbContext _docGenerationDbContext;

        public AuthorizationControllerTests(AuthorizationControllerFixture fixture)
        {
            _mapper = new MapperConfiguration(cfg => cfg.AddProfile<UserInfoProfile>()).CreateMapper();
            _docGenerationDbContext = fixture.DocGenerationDbContext;
        }

        [Fact]
        public async Task StoreOrUpdateUserDetails_WhenUserIsNew_AddsUserDetails()
        {
            // Arrange
            var providerSubjectId = Guid.NewGuid().ToString();
            var userId = Guid.NewGuid();
            var userInfoDto = new UserInfoDTO(providerSubjectId, "New User") //create DTO instance
            {
                Id = userId,
                Email = "newuser@example.com"
            };
            var _controller = new AuthorizationController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.StoreOrUpdateUserDetails(userInfoDto);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedUserInfoDto = Assert.IsType<UserInfoDTO>(okResult.Value);
            Assert.Equal(userId, returnedUserInfoDto.Id);

            Assert.Contains(_docGenerationDbContext.UserInformations, user => user.Id == userId && 
                            user.ProviderSubjectId == providerSubjectId);
        }

        [Fact]
        public async Task StoreOrUpdateUserDetails_WhenUserExists_UpdatesUserDetails()
        {
            // Arrange
            var providerSubjectId = Guid.NewGuid().ToString();
            var existingUser = new UserInformation
            {
                ProviderSubjectId = providerSubjectId,
                FullName = "Existing User",
                Email = "existinguser@example.com"
            };

            var updatedUserInfoDto = new UserInfoDTO(providerSubjectId, "Updated User")
            {
                Email = "updateduser@example.com"
            };

            _docGenerationDbContext.UserInformations.Add(existingUser);
            _docGenerationDbContext.SaveChanges();

            var _controller = new AuthorizationController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.StoreOrUpdateUserDetails(updatedUserInfoDto);

            // Assert
            var actionResult = Assert.IsType<OkObjectResult>(result.Result);
            var returnedUserInfoDto = Assert.IsType<UserInfoDTO>(actionResult.Value);
            Assert.Equal(existingUser.Id, returnedUserInfoDto.Id);
            Assert.Equal(updatedUserInfoDto.FullName, returnedUserInfoDto.FullName);
            Assert.Equal(updatedUserInfoDto.Email, returnedUserInfoDto.Email);

            Assert.Contains(_docGenerationDbContext.UserInformations, user => user.Id == existingUser.Id &&
                            user.FullName == updatedUserInfoDto.FullName &&
                            user.Email == updatedUserInfoDto.Email);


            // Cleanup
            _docGenerationDbContext.UserInformations.Remove(existingUser);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task StoreOrUpdateUserDetails_WhenUserInfoDtoIsNull_ReturnsBadRequest()
        {
            // Arrange
            UserInfoDTO? userInfoDto = null;
            var _controller = new AuthorizationController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.StoreOrUpdateUserDetails(userInfoDto);

            // Assert
            Assert.IsType<BadRequestResult>(result.Result);
        }

        [Fact]
        public async Task GetUserInfo_WhenProviderSubjectIdIsNull_ReturnsNotFoundResult()
        {
            // Arrange
            string? providerSubjectId = null;
            var _controller = new AuthorizationController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.GetUserInfo(providerSubjectId!);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task GetUserInfo_WhenUserExists_ReturnsExpectedUserInfoDto()
        {
            // Arrange
            var providerSubjectId = Guid.NewGuid().ToString();
            var existingUser = new UserInformation
            {
                ProviderSubjectId = providerSubjectId,
                FullName = "Existing User",
                Email = "existinguser@example.com"
            };
            _docGenerationDbContext.UserInformations.Add(existingUser);
            _docGenerationDbContext.SaveChanges();
            var _controller = new AuthorizationController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.GetUserInfo(providerSubjectId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var userInfoDto = Assert.IsType<UserInfoDTO>(okResult.Value);
            Assert.Equal(existingUser.Id, userInfoDto.Id);
            Assert.Equal(providerSubjectId, userInfoDto.ProviderSubjectId);
            Assert.Equal(existingUser.FullName, userInfoDto.FullName);
            Assert.Equal(existingUser.Email, userInfoDto.Email);

            // Cleanup
            _docGenerationDbContext.UserInformations.Remove(existingUser);
            _docGenerationDbContext.SaveChanges();
        }

        [Fact]
        public async Task GetThemePreference_WhenUserDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            string? providerSubjectId = Guid.NewGuid().ToString();
            var _controller = new AuthorizationController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.GetThemePreference(providerSubjectId);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task SetThemePreference_WhenUserDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            var themePreferenceDto = new ThemePreferenceDTO
            {
                ProviderSubjectId = Guid.NewGuid().ToString(),
                ThemePreference = ThemePreference.Dark
            };
            var _controller = new AuthorizationController(_docGenerationDbContext, _mapper);

            // Act
            var result = await _controller.SetThemePreference(themePreferenceDto);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
