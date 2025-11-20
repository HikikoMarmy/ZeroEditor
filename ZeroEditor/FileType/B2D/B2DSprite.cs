using System.Drawing;

namespace ZeroEditor.Zero3.Editors.B2D
{
	public sealed class B2dSprite
	{
		public int Material;
		public int OriginalTextureId;
		public int TextureId;
		public Rectangle SrcRect;
		public string Name = string.Empty;
		internal int U32StartIndex;
		internal uint Constant;
		public override string ToString() => string.IsNullOrEmpty( Name ) ? $"[{TextureId}] {SrcRect}" : Name;
	}
}
