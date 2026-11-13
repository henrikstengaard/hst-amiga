namespace Hst.Amiga.RigidDiskBlocks.Recovery
{
    public class RecoveryMessage
    {
        public RecoveryMessageLevel Level { get; }
        public string Text { get; }

        public RecoveryMessage(RecoveryMessageLevel level, string text)
        {
            Level = level;
            Text = text;
        }

        public override string ToString() => $"{Level}: {Text}";
    }
}
