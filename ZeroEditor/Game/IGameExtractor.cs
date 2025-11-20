using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroEditor.Game
{
	public interface IGameExtractor
	{
		Task ExtractBinAsync(
		FileStream isoStream,
		GameContext ctx,
		string outputRoot,
		IProgress<string>? log,
		CancellationToken ct );
	}

	public interface IGameRebuilder
	{
		Task RebuildIsoAsync(
			string extractedFolder,
			string outputIsoPath,
			IProgress<string>? log,
			CancellationToken ct );
	}
}
