using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

public class ExtractResponseErrorTests
{
    [Fact]
    public void ExtractResponseError_ReadsErrorTextFromRegressionFixture()
    {
        var error = TestXml.CreateAdapter().ExtractResponseError(TestXml.Load("error-response.xml"));

        Assert.Equal("No fixtures found", error);
    }

    [Fact]
    public void ExtractResponseError_ValidErrorResponse_YieldsErrorText()
    {
        const string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                           "<response brt=\"52880\"><error>Invalid credentials</error></response>";

        Assert.Equal("Invalid credentials", TestXml.CreateAdapter().ExtractResponseError(xml));
    }

    [Fact]
    public void ExtractResponseError_NonErrorResponse_YieldsNoError()
    {
        Assert.Null(TestXml.CreateAdapter().ExtractResponseError("<fixtures><fixture><id>1</id></fixture></fixtures>"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractResponseError_BlankContent_YieldsNoError(string xml)
    {
        Assert.Null(TestXml.CreateAdapter().ExtractResponseError(xml));
    }

    [Fact]
    public void ExtractResponseError_MalformedXml_YieldsNoError()
    {
        Assert.Null(TestXml.CreateAdapter().ExtractResponseError("<error>No fixtures found</fixtures>"));
    }
}
