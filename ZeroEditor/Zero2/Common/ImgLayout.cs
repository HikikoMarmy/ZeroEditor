using static BinUtil;

namespace ZeroEditor.Zero2.Common
{
    public readonly record struct ImgLayout(int Count, long AddrDataTable);

    public interface IZero2ImgLayout
    {
        ImgLayout Img { get; }
    }

    public interface IZero2ElfLayout
    {
        Segment JEneDat { get; }
    }

    public interface IZero2Layout : IZero2ElfLayout, IZero2ImgLayout
    {
    }
}
