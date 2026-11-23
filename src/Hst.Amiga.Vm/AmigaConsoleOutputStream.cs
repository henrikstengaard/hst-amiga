namespace Hst.Amiga.Vm;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

/// <summary>
/// Write only stream translating Amiga console.device control sequences to ANSI terminal sequences, used to show
/// Amiga console output in a host terminal.
/// <list type="bullet">
/// <item>Single byte control sequence introducer 0x9B and C1 control codes are translated to escape sequences.</item>
/// <item>Form feed, which clears the window on Amiga, is translated to clear screen and cursor home.</item>
/// <item>Vertical tab, which moves cursor up on Amiga, is translated to cursor up.</item>
/// <item>Foreground and background colors selecting Workbench pens are translated to terminal colors.</item>
/// <item>Cursor on and off are translated to show and hide cursor.</item>
/// <item>Amiga specific sequences like window status request, raw events and window bounds are removed.</item>
/// </list>
/// Bytes in UTF-8 multi byte sequences are not translated, so UTF-8 text isn't changed.
/// </summary>
public class AmigaConsoleOutputStream : Stream
{
    private const byte Esc = 0x1B;
    private const byte Csi = 0x9B;
    private const int MaxSequenceLength = 32;

    /// <summary>
    /// Terminal colors for Workbench 2.0+ pens: 0 grey background, 1 black text, 2 white highlight and 3 blue.
    /// Pen 0 as background and pen 1 as foreground use terminal default colors, so normal text looks like
    /// normal text in the terminal. Pens 4-7 use same color numbers.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, int> PenColors = new Dictionary<int, int>
    {
        { 30, 37 }, { 31, 39 }, { 32, 97 }, { 33, 94 },
        { 40, 49 }, { 41, 40 }, { 42, 107 }, { 43, 44 }
    };

    private static readonly byte[] ClearScreen = Encoding.ASCII.GetBytes("\x1b[H\x1b[2J");
    private static readonly byte[] CursorUp = Encoding.ASCII.GetBytes("\x1b[A");

    private readonly Stream stream;
    private readonly bool leaveOpen;
    private readonly List<byte> output = new();
    private readonly StringBuilder sequence = new();
    private State state = State.Text;
    private int utf8Remaining;

    private enum State
    {
        Text,
        Escape,
        ControlSequence
    }

    /// <param name="stream">Stream to write translated output to.</param>
    /// <param name="leaveOpen">Leave stream open, when this stream is disposed.</param>
    public AmigaConsoleOutputStream(Stream stream, bool leaveOpen = false)
    {
        this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
        this.leaveOpen = leaveOpen;
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        for (var i = 0; i < count; i++)
        {
            Translate(buffer[offset + i]);
        }

        WriteOutput();
    }

    private void Translate(byte value)
    {
        switch (state)
        {
            case State.Escape:
                if (value == '[')
                {
                    BeginSequence();
                    return;
                }

                state = State.Text;
                output.Add(Esc);
                if (value == Esc)
                {
                    state = State.Escape;
                    return;
                }

                output.Add(value);
                return;
            case State.ControlSequence:
                TranslateSequence(value);
                return;
        }

        if (utf8Remaining > 0 && value is >= 0x80 and <= 0xBF)
        {
            utf8Remaining--;
            output.Add(value);
            return;
        }

        utf8Remaining = value switch
        {
            >= 0xC2 and <= 0xDF => 1,
            >= 0xE0 and <= 0xEF => 2,
            >= 0xF0 and <= 0xF4 => 3,
            _ => 0
        };

        switch (value)
        {
            case Esc:
                state = State.Escape;
                break;
            case Csi:
                BeginSequence();
                break;
            case 0x0B:
                output.AddRange(CursorUp);
                break;
            case 0x0C:
                output.AddRange(ClearScreen);
                break;
            case 0x84: // IND index
            case 0x85: // NEL next line
            case 0x88: // HTS horizontal tab set
            case 0x8D: // RI reverse index
                output.Add(Esc);
                output.Add((byte)(value - 0x40));
                break;
            default:
                output.Add(value);
                break;
        }
    }

    private void BeginSequence()
    {
        state = State.ControlSequence;
        sequence.Clear();
    }

    private void TranslateSequence(byte value)
    {
        // parameter and intermediate bytes
        if (value is >= 0x20 and <= 0x3F)
        {
            sequence.Append((char)value);
            if (sequence.Length > MaxSequenceLength)
            {
                AbortSequence();
            }

            return;
        }

        // control characters and invalid bytes abort sequence and are handled as text
        if (value is < 0x40 or > 0x7E)
        {
            AbortSequence();
            Translate(value);
            return;
        }

        state = State.Text;
        var translated = TranslateSequence(sequence.ToString(), (char)value);
        if (translated != null)
        {
            output.Add(Esc);
            output.Add((byte)'[');
            output.AddRange(Encoding.ASCII.GetBytes(translated));
        }
    }

    private void AbortSequence()
    {
        state = State.Text;
        output.Add(Esc);
        output.Add((byte)'[');
        output.AddRange(Encoding.ASCII.GetBytes(sequence.ToString()));
        sequence.Clear();
    }

    /// <summary>
    /// Translate control sequence with parameters and final character.
    /// </summary>
    /// <returns>Translated sequence without escape and bracket or null to remove sequence.</returns>
    private static string TranslateSequence(string parameters, char final)
    {
        switch (final)
        {
            case 'm':
                // set graphics rendition with "ESC[>Nm" setting window background color
                return parameters.StartsWith(">") ? null : TranslateGraphicsRendition(parameters);
            case 'p':
                // cursor on "ESC[ p" or "ESC[1 p" and cursor off "ESC[0 p"
                return parameters switch
                {
                    "0 " => "?25l",
                    " " or "1 " => "?25h",
                    _ => null
                };
            case 'h':
            case 'l':
                // linefeed mode "ESC[20h" and auto scroll "ESC[>1h" are Amiga console modes
                return parameters == "20" || parameters.StartsWith(">") ? null : parameters + final;
            case 'q': // window status request
            case 't': // page length
            case 'u': // line length
            case 'x': // left offset
            case 'y': // top offset
            case '{': // set raw events
            case '}': // reset raw events
                return null;
            default:
                return parameters + final;
        }
    }

    private static string TranslateGraphicsRendition(string parameters)
    {
        if (parameters.Length == 0)
        {
            return "m";
        }

        var values = parameters.Split(';');
        for (var i = 0; i < values.Length; i++)
        {
            if (int.TryParse(values[i], out var value) && PenColors.TryGetValue(value, out var color))
            {
                values[i] = color.ToString();
            }
        }

        return string.Join(";", values) + "m";
    }

    private void WriteOutput()
    {
        if (output.Count == 0)
        {
            return;
        }

        var bytes = output.ToArray();
        output.Clear();
        stream.Write(bytes, 0, bytes.Length);
    }

    public override void Flush()
    {
        WriteOutput();
        stream.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // write incomplete sequence untranslated
            if (state == State.Escape)
            {
                output.Add(Esc);
            }
            else if (state == State.ControlSequence)
            {
                AbortSequence();
            }

            state = State.Text;
            WriteOutput();
            stream.Flush();
            if (!leaveOpen)
            {
                stream.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
