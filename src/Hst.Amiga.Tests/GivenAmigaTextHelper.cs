namespace Hst.Amiga.Tests
{
    using System;
    using System.Text;
    using Xunit;

    public class GivenAmigaTextHelper
    {
        [Fact]
        public void WhenGetStringFromBytesThenStringMatch()
        {
            // arrange - bytes with a string
            var stringBytes = Encoding.Latin1.GetBytes("Test String");
            
            // act - get string from bytes
            var actualText = AmigaTextHelper.GetString(stringBytes);
            
            // assert - text matches expected string
            Assert.Equal("Test String", actualText);
        }
        
        [Fact]
        public void WhenGetStringFromBytesWithIndexThenStringMatch()
        {
            // arrange - bytes with a string
            var stringBytes = Encoding.Latin1.GetBytes("Test String");
            
            // act - get string from bytes
            var text = AmigaTextHelper.GetString(stringBytes, 3, 20);
            
            // assert - text matches expected string
            Assert.Equal("t String", text);
        }

        [Fact]
        public void WhenGetStringFromBytesWithIndexOfLastByteThenStringMatch()
        {
            // arrange - bytes with a string
            var stringBytes = Encoding.Latin1.GetBytes("Test String");
            
            // act - get string from bytes
            var text = AmigaTextHelper.GetString(stringBytes, 10, 3);
            
            // assert - text matches expected string
            Assert.Equal("g", text);
        }
        
        [Fact]
        public void WhenGetStringFromBytesWithCountThenStringMatch()
        {
            // arrange - bytes with a string
            var stringBytes = Encoding.Latin1.GetBytes("Test String");
            
            // act - get string from bytes
            var text = AmigaTextHelper.GetString(stringBytes, 0, 3);
            
            // assert - text matches expected string
            Assert.Equal("Tes", text);
        }
        
        [Fact]
        public void WhenGetStringFromBytesWithCountLargerThanBytesLengthThenStringMatch()
        {
            // arrange - bytes with a string
            var stringBytes = Encoding.Latin1.GetBytes("Test String");
            
            // act - get string from bytes
            var text = AmigaTextHelper.GetString(stringBytes, 0, 30);
            
            // assert - text matches expected string
            Assert.Equal("Test String", text);
        }
        
        [Fact]
        public void WhenGetStringFromBytesWithIndexLargerThanBytesLengthThenExceptionIsThrown()
        {
            // arrange - bytes with a string
            var stringBytes = Encoding.Latin1.GetBytes("Test String");
            
            // act & assert - get string from bytes throws out of range exception
            Assert.Throws<ArgumentOutOfRangeException>(() => AmigaTextHelper.GetString(stringBytes, 11, 1));
        }
    }
}