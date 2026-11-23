namespace Hst.Amiga.Tests.VmTests;

using System.IO;
using System.Text;
using System.Threading.Tasks;
using HunkTests;
using Vm;
using Xunit;
using static M68kAssembler;

public class GivenAmigaConsoleOutputStream
{
    // single byte control sequence introducer
    private const string Csi = "\u009b";
    private const string Esc = "\u001b";

    private static string Translate(params byte[][] writes)
    {
        var output = new MemoryStream();
        using (var stream = new AmigaConsoleOutputStream(output, true))
        {
            foreach (var bytes in writes)
            {
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        return Encoding.Latin1.GetString(output.ToArray());
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);

    [Theory]
    [InlineData(Csi + "1mbold" + Csi + "0m", Esc + "[1mbold" + Esc + "[0m")]
    [InlineData(Esc + "[1mbold", Esc + "[1mbold")]
    [InlineData(Csi + "32mwhite" + Csi + "31m", Esc + "[97mwhite" + Esc + "[39m")]
    [InlineData(Esc + "[0;33;40m", Esc + "[0;94;49m")]
    [InlineData(Esc + "[35;41m", Esc + "[35;40m")]
    [InlineData("\f", Esc + "[H" + Esc + "[2J")]
    [InlineData("\v", Esc + "[A")]
    [InlineData(Csi + "0 p", Esc + "[?25l")]
    [InlineData(Csi + " p", Esc + "[?25h")]
    [InlineData("a" + Csi + "0 qb", "ab")]
    [InlineData("a" + Esc + "[>1mb", "ab")]
    [InlineData("a" + Csi + "20hb", "ab")]
    [InlineData(Csi + "1{", "")]
    [InlineData(Esc + "c", Esc + "c")]
    [InlineData(Csi + "2;5H" + Csi + "K", Esc + "[2;5H" + Esc + "[K")]
    [InlineData("\u0085", Esc + "E")]
    [InlineData("café", "café")]
    public void When_WritingAmigaConsoleOutput_Then_ControlSequencesAreTranslated(string input, string expected)
    {
        Assert.Equal(expected, Translate(Bytes(input)));
    }

    [Fact]
    public void When_ControlSequenceIsSplitAcrossWrites_Then_SequenceIsTranslated()
    {
        Assert.Equal(Esc + "[94mblue", Translate(Bytes(Csi), Bytes("3"), Bytes("3mblue")));
    }

    [Fact]
    public void When_WritingUtf8Text_Then_ContinuationBytesAreNotTranslated()
    {
        // e with caron is c4 9b in UTF-8 and a with ring is c3 a5
        var utf8 = Encoding.UTF8.GetBytes("ěå");

        Assert.Equal(Encoding.Latin1.GetString(utf8), Translate(utf8));
    }

    [Fact]
    public void When_ControlSequenceIsIncomplete_Then_SequenceIsWrittenUntranslatedOnDispose()
    {
        Assert.Equal(Esc + "[12", Translate(Bytes(Csi + "12")));
    }

    [Fact]
    public async Task When_TranslatingConsoleOutputInVm_Then_ProgramOutputIsTranslated()
    {
        var program = new M68kAssembler()
            .Move(Size.Long, AbsW(4), A(6))
            .Lea(Pc("dosname"), 1)
            .Moveq(36, 0)
            .CallLib(-552) // OpenLibrary
            .Move(Size.Long, D(0), A(6))
            .Lea(Pc("message"), 0)
            .Move(Size.Long, A(0), D(1))
            .CallLib(-948) // PutStr
            .Moveq(0, 0)
            .Rts()
            .Label("message").Bytes(Bytes("\f" + Csi + "1mbold" + Csi + "0m\n\0"))
            .Label("dosname").String("dos.library");
        var output = new MemoryStream();
        var options = new VmOptions
        {
            Input = new MemoryStream(),
            Output = output,
            Error = new MemoryStream(),
            Interactive = false,
            TranslateConsoleOutput = true,
            MaxInstructions = 1_000_000,
            Log = _ => { }
        };
        options.Volumes["SYS"] = Path.GetTempPath();

        var result = await new AmigaVm(options)
            .Run(new HunkFileBuilder().AddCode(program.ToArray()).Build(), "test", string.Empty);

        Assert.Null(result.Error);
        Assert.Equal(Esc + "[H" + Esc + "[2J" + Esc + "[1mbold" + Esc + "[0m\n",
            Encoding.Latin1.GetString(output.ToArray()));
        Assert.True(output.CanWrite, "Output stream from options is not disposed");
    }
}
