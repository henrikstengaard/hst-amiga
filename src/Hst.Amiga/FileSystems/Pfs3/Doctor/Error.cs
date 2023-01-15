namespace Hst.Amiga.FileSystems.Pfs3.Doctor
{
    public enum Error
    {
        e_none = 0,		/* no error */
        e_aborted,
        e_dirty,
        e_remove,
        e_repartition,
        e_empty,
        e_not_found,
        e_out_of_memory,
        e_fatal_error,
        e_rbl_not_found,
        e_max_pass_exceeded,
        e_res_bitmap_fail,
        e_main_bitmap_fail,
        e_anode_bitmap_fail,
        e_block_outside_partition,
        e_block_outside_reserved,
        e_options_error,
        e_direntry_error,
        e_invalid_softlink,
        e_anode_error,
        e_reserved_area_error,
        e_outside_bitmap_error,
        e_double_allocation,
        e_number_error,
        e_syntax_error,
        e_read_error,
        e_write_error,
        e_alloc_fail
    }
}