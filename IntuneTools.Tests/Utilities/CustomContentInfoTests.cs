using IntuneTools.Utilities;

namespace IntuneTools.Tests.Utilities
{
    public class CustomContentInfoTests
    {
        [Fact]
        public void Properties_SetAndGet_ReturnCorrectValues()
        {
            var info = new CustomContentInfo
            {
                ContentName = "Test Policy",
                ContentPlatform = "Windows",
                ContentType = "DeviceConfiguration",
                ContentId = "abc-123",
                ContentDescription = "A test policy"
            };

            Assert.Equal("Test Policy", info.ContentName);
            Assert.Equal("Windows", info.ContentPlatform);
            Assert.Equal("DeviceConfiguration", info.ContentType);
            Assert.Equal("abc-123", info.ContentId);
            Assert.Equal("A test policy", info.ContentDescription);
        }

        [Fact]
        public void Properties_DefaultValues_AreNull()
        {
            var info = new CustomContentInfo();

            Assert.Null(info.ContentName);
            Assert.Null(info.ContentPlatform);
            Assert.Null(info.ContentType);
            Assert.Null(info.ContentId);
            Assert.Null(info.ContentDescription);
        }

        [Fact]
        public void Properties_CanBeSetToNull()
        {
            var info = new CustomContentInfo
            {
                ContentName = "Initial",
                ContentPlatform = "Windows"
            };

            info.ContentName = null;
            info.ContentPlatform = null;

            Assert.Null(info.ContentName);
            Assert.Null(info.ContentPlatform);
        }

        [Fact]
        public void Properties_CanBeUpdated()
        {
            var info = new CustomContentInfo
            {
                ContentName = "Original Name"
            };

            info.ContentName = "Updated Name";
            Assert.Equal("Updated Name", info.ContentName);
        }
    }
}
