using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZeroEditor.Zero2.Common
{
	public readonly record struct ImgLayout( int Count, long AddrDataTable );

	public interface IZero2ImgLayout
	{
		ImgLayout Img { get; }
	}
}
