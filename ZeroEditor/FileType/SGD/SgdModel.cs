using System.Collections.Generic;
using System.Numerics;

public enum SgdKind
{
	Room,
	Character
}

struct PHead
{
	public uint HeaderSections;   
	public uint UniqHeaderSize;   
	public uint pUniqVertex;      
	public uint pUniqNormal;      
	public uint pUniqList;        
	public uint CommonHeaderSize; 
	public uint pCommonVertex;    
	public uint pCommonNormal;    
	public uint pCommonList;
	public uint WeightedHeaderSize;
	public uint pWeightedVertex;  
	public uint pWeightedNormal;  
	public uint pWeightedList;    
}

public sealed class SgdMaterial
{
	public string Name { get; set; } = string.Empty;
	public SgdPrimitive Primitive { get; set; } = SgdPrimitive.Triangle;
}

public enum SgdPrimitive : ushort
{
	Point = 0, Line = 1, LineStrip = 2, Triangle = 3, TriangleStrip = 4, TriangleFan = 5, Sprite = 6
}

public sealed class SgdMesh
{
	public string Name { get; set; } = "mesh";
	public int MaterialIndex { get; set; } = -1;
	public SgdPrimitive Primitive { get; set; } = SgdPrimitive.Triangle;
	public List<Vector3> Positions { get; } = new();
	public List<Vector3> Normals { get; } = new();
	public List<Vector2> UVs { get; } = new();
	public List<int> Indices { get; } = new();
}

public sealed class SgdModel
{
	public SgdKind Kind { get; set; } = SgdKind.Room;
	public List<SgdMaterial> Materials { get; } = new();
	public List<SgdMesh> Meshes { get; } = new();
}