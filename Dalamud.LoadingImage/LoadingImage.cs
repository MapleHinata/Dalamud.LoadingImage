using Lumina.Excel;
using Lumina.Text.ReadOnly;

namespace Dalamud.LoadingImage
{
   [Sheet( "LoadingImage" )]
   public readonly struct LoadingImage(ExcelPage page, uint offset, uint row) : IExcelRow<LoadingImage>
    {
        public uint RowId => row;

        public ReadOnlySeString Name => page.ReadString( offset, offset );

        public static LoadingImage Create(ExcelPage page, uint offset, uint row) => new(page, offset, row);
    }
}
