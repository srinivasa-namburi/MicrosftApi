using System.Text.Json;
using Microsoft.Greenlight.Shared.Models.SourceReferences;
using StackExchange.Redis;
using Microsoft.Greenlight.Shared.Plugins;
using Xunit;
using Assert = Xunit.Assert;
using Moq;

namespace Microsoft.Greenlight.Shared.Tests.Plugins
{
    public class PluginSourceReferenceCollectorTests
    {
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly PluginSourceReferenceCollector _collector;
        private readonly Guid _executionId;
        private readonly PluginSourceReferenceItem _item;

        public PluginSourceReferenceCollectorTests()
        {
            var mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            mockConnectionMultiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_mockDatabase.Object);
            _collector = new PluginSourceReferenceCollector(mockConnectionMultiplexer.Object);
            _executionId = Guid.NewGuid();
            _item = new PluginSourceReferenceItem
            {
                PluginIdentifier = "TestPlugin",
                SourceOutput = "TestOutput",
                SourceInputJson = "{\"key\":\"value\"}"
            };
        }

        [Fact]
        public void Add_ShouldAddItemToCache()
        {
            // Arrange
            var serializedItem = JsonSerializer.Serialize(_item);
            var redisKey = $"PluginSourceReferenceItems:{_executionId}";

            // Act
            _collector.Add(_executionId, _item);

            // Assert
            _mockDatabase.Verify(db => db.ListRightPush(redisKey, serializedItem, It.IsAny<When>(), It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public void GetAll_ShouldReturnAllItemsFromCache()
        {
            // Arrange
            var serializedItem = JsonSerializer.Serialize(_item);
            var redisKey = $"PluginSourceReferenceItems:{_executionId}";
            _mockDatabase.Setup(db => db.ListRange(redisKey, It.IsAny<long>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                         .Returns([serializedItem]);

            // Act
            var result = _collector.GetAll(_executionId);

            // Assert
            Assert.Single(result);
            Assert.Equal(_item.PluginIdentifier, result[0].PluginIdentifier);
            Assert.Equal(_item.SourceOutput, result[0].SourceOutput);
            Assert.Equal(_item.SourceInputJson, result[0].SourceInputJson);
        }

        [Fact]
        public void Clear_ShouldRemoveAllItemsFromCache()
        {
            // Arrange
            var redisKey = $"PluginSourceReferenceItems:{_executionId}";

            // Act
            _collector.Clear(_executionId);

            // Assert
            _mockDatabase.Verify(db => db.KeyDelete(redisKey, It.IsAny<CommandFlags>()), Times.Once);
        }
    }
}
