using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BinUtil;

namespace ZeroEditor.Zero1.Common
{
	public readonly record struct ImgLayout( int Count, long ImgHd, long ImgBd );

	public interface IZero1ImgLayout
	{
		ImgLayout Img { get; }
	}

	public interface IZero1ElfLayout
	{
		Segment[] JibakuByNight { get; }
		Segment[] FuyuByNight { get; }
		Segment[] AutoByNight { get; }
		Segment FogParam { get; }
		Segment FogRgb { get; }
		Segment FogParamFinder { get; }
		Segment FogRgbFinder { get; }
		Segment MapItemData { get; }
	}
}
