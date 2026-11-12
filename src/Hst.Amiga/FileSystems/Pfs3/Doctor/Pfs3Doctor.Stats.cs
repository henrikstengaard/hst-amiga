namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    /// <summary>
    /// Port of stats.c. Functions to call when error found or fixed and new block started.
    /// </summary>
    public partial class Pfs3Doctor
    {
        private bool statsError; // error in stats.c
        private bool statsFixed; // fixed in stats.c

        private void ClearStats()
        {
            stats.NumFiles =
                stats.NumDirs =
                    stats.NumSoftLink =
                        stats.NumHardLink =
                            stats.NumRollover =
                                stats.FragmentedFiles =
                                    stats.AnodesUsed = 0;

            volume.UpdateStats();
        }

        private void AddError(string message)
        {
            if (!statsError)
            {
                if (stats.BlockNr != 0)
                    statsError = true;
                stats.NumErrors++;
            }

            if (message != null)
            {
                volume.ShowMsg($"ERROR:{message}\n");
            }

            volume.UpdateStats();
        }

        private void FixedError(string message)
        {
            if (mode == Mode.Check)
            {
                AddError(message);
                return;
            }

            AddError(null);

            if (!statsFixed)
            {
                if (stats.BlockNr != 0)
                    statsFixed = true;
                stats.ErrorsFixed++;
            }

            volume.ShowMsg(message != null ? $"FIXED:{message}\n" : "FIXED\n");

            volume.UpdateStats();
        }

        private void EnterBlock(uint blknr)
        {
            if (blknr != stats.BlockNr)
            {
                if (stats.BlockNr != 0)
                    stats.PrevBlkNr = stats.BlockNr;
                stats.BlockNr = blknr;
                statsError = statsFixed = false;
                stats.BlocksChecked++;
                volume.UpdateStats(); /* optimise !! */
            }
        }

        private void ExitBlock()
        {
            stats.BlockNr = stats.PrevBlkNr;
            stats.PrevBlkNr = 0;
        }
    }
}
