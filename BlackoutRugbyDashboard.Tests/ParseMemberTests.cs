using BlackoutRugbyDashboard.Services;
using Xunit;

namespace BlackoutRugbyDashboard.Tests;

/// <summary>
/// Member-record parsing for the Club Link probe (D3 §5): the one r=m read that
/// validates a Member ID + Member Key pair and carries the member's teamid.
/// Regression shape: docs/research/artifacts/r2-m-memberid.xml.
/// </summary>
public class ParseMemberTests
{
    [Fact]
    public void ParseMember_ReadsIdUsernameAndTeamFromTheVerifiedArtifact()
    {
        var member = TestXml.CreateAdapter().ParseMember(TestXml.Load("r2-m-memberid.xml"));

        Assert.NotNull(member);
        Assert.Equal(202665, member!.Id);
        Assert.Equal("Doyleski", member.Username);
        Assert.Equal(45047, member.TeamId);
    }

    [Fact]
    public void ParseMember_MissingOptionalFields_YieldDefaults()
    {
        const string xml = "<blackoutrugby_api_response><member id=\"7\"><id>7</id></member></blackoutrugby_api_response>";

        var member = TestXml.CreateAdapter().ParseMember(xml);

        Assert.NotNull(member);
        Assert.Equal(7, member!.Id);
        Assert.Equal(string.Empty, member.Username);
        Assert.Equal(0, member.TeamId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseMember_BlankInput_YieldsNoMember(string xml)
    {
        Assert.Null(TestXml.CreateAdapter().ParseMember(xml));
    }

    [Fact]
    public void ParseMember_ResponseWithoutMemberElement_YieldsNoMember()
    {
        Assert.Null(TestXml.CreateAdapter().ParseMember("<teams><team><id>1</id></team></teams>"));
    }

    [Fact]
    public void ParseMember_MalformedXml_YieldsNoMember()
    {
        Assert.Null(TestXml.CreateAdapter().ParseMember("<member><id>2</member>"));
    }
}